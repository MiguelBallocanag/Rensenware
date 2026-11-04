using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Deepwave.RetroOS.Apps;
using Deepwave.RetroOS.Audio;
using Deepwave.RetroOS.Comms;
using Deepwave.RetroOS.Core.Auth;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Core.Network;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.Screens;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.Windowing;

namespace Deepwave.RetroOS
{
    /// <summary>The running OS: every service an app or your game code can reach. Wired once by <see cref="RetroOsBootstrap"/> and available from <see cref="RetroOs.Current"/>.</summary>
    public class RetroOsContext
    {
        public VirtualFileSystem Fs { get; internal set; }
        public AuthService Auth { get; internal set; }

        /// <summary>Name of the operating system, for the Start menu banner and the About box. The computer's own name belongs to <see cref="Machine"/> and is a different thing: WORKSTATION-04 running RetroOS 95.</summary>
        public string OsName { get; internal set; } = "RetroOS";

        /// <summary>The desktop picture this machine came up with.</summary>
        public Sprite Wallpaper { get; internal set; }

        public WindowManager Windows { get; internal set; }
        public Transform Overlay { get; internal set; }

        // The work area - everything the OS draws lives here. Use it to touch the whole screen at once (a repaint, the CRT warp).
        public Transform Root { get; internal set; }

        // Above everything, including dialogs: where the blue screen and the
        // shutdown screens are drawn.
        public Transform SystemLayer { get; internal set; }

        // The machine's drives, and the root of everything it can browse.
        public DriveBay Drives { get; internal set; }

        /// <summary>What this machine remembers between sessions - the colour scheme, the clock format, where the desktop icons were dragged.</summary>
        public MachinePrefs Prefs { get; internal set; }

        /// <summary>How this machine looks: its theme, the colour scheme its player picked, whether its widgets click.</summary>
        public MachineSkin Skin { get; internal set; }

        // This machine's identity on the network.
        public RetroMachine Machine { get; internal set; }

        static readonly List<UserEntry> NoAccounts = new();
        static readonly LoginSettings NoLogin = new();

        /// <summary>Who can sign in at this machine. Accounts belong to the computer, the way they did on a real 95 box, so the machine on the next desk has its own and a file in one account's home is not the other's to read.</summary>
        public List<UserEntry> Accounts => Machine ? Machine.Users : NoAccounts;

        /// <summary>Whether this machine asks who you are before the desktop.</summary>
        public LoginSettings Login => Machine ? Machine.Login : NoLogin;

        // The upgrade modules.
        public AppManager Apps { get; internal set; }
        public AudioManager Audio { get; internal set; }
        public OsEventBus Events { get; internal set; }
        public GameFlags Flags { get; internal set; }
        public OsClock Clock { get; internal set; }
        public MailboxService Mail { get; internal set; }

        // The LAN and "the internet", served from real folders on disk.
        public NetworkService Net { get; internal set; }

        // The bootstrap behaviour, for coroutines and scene lookups.
        public MonoBehaviour Runner { get; internal set; }

        /// <summary>The screen this machine draws on. Handed over rather than found, so a second computer in the scene cannot answer for this one.</summary>
        public Display.RetroScreenHost Screen { get; internal set; }

        /// <summary>The drawn pointer walking this machine's screen, or <c>null</c> when the screen is driven by the player's own mouse. What the Control Panel's pointer speed is a speed of.</summary>
        public Display.RetroScreenPointer Pointer { get; internal set; }

        public Action LogoutRequested;
        public Action ShutdownRequested;

        /// <summary>Whether the player is at this machine. Set by the bootstrap; a machine with nobody in front of it should not react to their keys.</summary>
        public Func<bool> IsFocused = () => true;

        /// <summary>Whoever is signed in, or <c>null</c> on a machine with no accounts.</summary>
        public string CurrentUser => Auth?.Current?.Username;

        /// <summary>Raised when something on this machine's file system changes, saying what and where.</summary>
        public event Action<FsChangedArgs> FsChanged;

        /// <summary>Says that <paramref name="path"/> changed, to everything that listens - the rule engine through the bus, and the OS's own views through <see cref="FsChanged"/>.</summary>
        public void NotifyFsChanged(string path, FsChange kind)
        {
            path = string.IsNullOrEmpty(path) ? "/" : path;
            Events?.Raise(OsEvents.FsChanged, path);
            FsChanged?.Invoke(new FsChangedArgs(path, kind));
        }

        /// <summary>Whether the signed-in user is allowed to see a node. Another user's home folders are not theirs to read.</summary>
        public bool CanAccess(VfsNode node) => node == null || node.IsVisibleTo(CurrentUser);

        /// <summary>The children of a folder as this user should see them: no hidden entries unless asked for, and nothing belonging to somebody else. Everything that lists a folder should go through here.</summary>
        public IEnumerable<VfsNode> List(VfsDirectory dir, bool showHidden = false) =>
            dir == null ? Enumerable.Empty<VfsNode>() : dir.List(showHidden, CurrentUser);

        /// <summary>Opens a file the way double-clicking it does: refuses another user's file, asks for the password on an encrypted one, then hands it to whichever program claims it.</summary>
        public void OpenFile(VfsFile file)
        {
            if (file == null) return;
            Skin?.Claim();

            if (!CanAccess(file))
            {
                RetroDialog.Alert(Overlay, "Access Denied",
                    $"'{file.Name}' belongs to another user.\n\n" +
                    $"Log on as {file.Owner} to open it.");
                Events.Raise(OsEvents.AccessDenied, file.Path);
                return;
            }

            if (file.Locked) { PromptUnlock(file, () => OpenFile(file)); return; }

            if (!Apps.OpenFile(file))
            {
                RetroDialog.Alert(Overlay, "Open", "No program can open this file.");
                return;
            }
            Events.Raise(OsEvents.FileOpened, file.Path);
        }

        /// <summary>Shows the password box for an encrypted file or folder and runs <paramref name="onSuccess"/> once it opens.</summary>
        public void PromptUnlock(VfsNode node, Action onSuccess)
        {
            Skin?.Claim();
            RetroDialog.Prompt(Overlay, "Encrypted", $"Password for '{node.Name}':", pw =>
            {
                if (node.Unlock(pw))
                {
                    Events.Raise(OsEvents.NodeUnlocked, node.Path);
                    NotifyFsChanged(node.Path, FsChange.Unlocked);
                    onSuccess?.Invoke();
                }
                else
                {
                    RetroDialog.Alert(Overlay, "Encrypted", "Wrong password.");
                }
            }, password: true);
        }

        /// <summary>Asks the player to confirm, then signs them out. This is what the Start menu and the Control Panel call; a story rule wanting the machine to sign out on its own calls <see cref="Logout"/>.</summary>
        public void RequestLogout() => Confirm(
            "Log Off " + OsName, "Are you sure you want to log off?", Logout);

        /// <summary>Asks the player to confirm, then takes the machine down. This is what the Start menu calls; a story rule ending the scene by killing the computer calls <see cref="Shutdown"/>.</summary>
        public void RequestShutdown() => Confirm(
            "Shut Down " + OsName, "Are you sure you want to shut down the computer?", Shutdown);

        // With no overlay to ask on, do the thing and warn - a Start menu shutting down without asking is a bug upstream.
        void Confirm(string title, string question, Action onYes)
        {
            if (!Overlay)
            {
                Debug.LogWarning($"RetroOS: '{title}' had no overlay to ask on, so it went " +
                                 "ahead without asking. The OS was probably torn down mid-click.");
                onYes();
                return;
            }

            RetroDialog.Confirm(Overlay, title, question, yes => { if (yes) onYes(); });
        }

        /// <summary>Signs the current user out and returns to the logon screen. Raises <see cref="OsEvents.Logout"/>.</summary>
        public void Logout()
        {
            var user = Auth.Current?.Username;
            Auth.Logout();
            Events.Raise(OsEvents.Logout, user);
            LogoutRequested?.Invoke();
        }

        /// <summary>Turns the in-fiction machine off: the desktop goes, the please-wait screen holds, and the glass fades to a dead monitor. The player's game keeps running, and the machine can be started again with <see cref="RetroOsBootstrap.PowerOn"/>. Raises <see cref="OsEvents.Shutdown"/>.</summary>
        public void Shutdown()
        {
            Events.Raise(OsEvents.Shutdown);
            ShutdownRequested?.Invoke();
        }

        /// <summary>Raises a balloon above the tray - new mail, a reminder, a disc going in. Use it instead of a dialog for anything the player has not asked to be interrupted by.</summary>
        /// <param name="title">The bold first line.</param>
        /// <param name="body">The line under it.</param>
        /// <param name="icon">Optional glyph. Defaults to the OS logo.</param>
        /// <param name="onClick">What clicking the balloon does.</param>
        public void Notify(string title, string body, Sprite icon = null, Action onClick = null)
        {
            // A balloon is often the one thing a machine draws while nobody is
            // at it, and it is built from the palette like everything else.
            Skin?.Claim();
            Notifications.RetroNotification.Show(this, title, body,
                icon ? icon : RetroIcons.Get(BuiltinIcon.Info), onClick);
        }

        /// <summary>Crashes the machine to a blue screen. A story tool: call it from a rule, a scene event or an app when the fiction needs the computer to fall over.</summary>
        /// <param name="title">Text in the light bar at the top.</param>
        /// <param name="message">The body of the crash.</param>
        /// <param name="canContinue">
        /// <c>true</c> lets the player clear it with a key or a click and drops
        /// them back on the desktop; <c>false</c> leaves it up until your game
        /// dismisses the returned screen.
        /// </param>
        /// <returns>The screen, so it can be dismissed or watched.</returns>
        public SystemScreen Bsod(string title, string message, bool canContinue = true)
        {
            var layer = SystemLayer ? SystemLayer : Overlay;
            if (!layer)
            {
                Debug.LogWarning($"RetroOS: cannot show the blue screen '{title}' - this machine " +
                                 "has nothing on its glass to draw it over. It is either not " +
                                 "booted yet or already torn down.");
                return null;
            }

            Skin?.Claim();
            Events.Raise(OsEvents.Bsod, title);
            Audio?.PlayUi(UiSfx.Error);

            var screen = SystemScreen.Bsod(layer, title, message, canContinue);
            // Only whoever is actually at this machine can clear it.
            if (screen) screen.CanDismiss = IsFocused;
            return screen;
        }
    }
}
