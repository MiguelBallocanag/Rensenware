using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Deepwave.RetroOS.Apps;
using Deepwave.RetroOS.Audio;
using Deepwave.RetroOS.Comms;
using Deepwave.RetroOS.Core.Auth;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Core.Network;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.Desktop;
using Deepwave.RetroOS.Display;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.Login;
using Deepwave.RetroOS.Screens;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.Windowing;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Deepwave.RetroOS
{
    /// <summary>The one component that turns a GameObject into a computer: it builds the canvas and the OS services, installs the program components found under it, then boots to the logon screen or straight to the desktop.</summary>
    [AddComponentMenu("Deepwave/RetroOS/RetroOS Bootstrap")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RetroMachine))]
    [RequireComponent(typeof(RetroMonitor))]
    public class RetroOsBootstrap : MonoBehaviour
    {
        [Tooltip(
            "Name of the operating system itself - what the Start menu banner "
                + "reads and what Control Panel calls this system. The computer's "
                + "own name is on its RetroMachine, and the two are different "
                + "things: WORKSTATION-04 running RetroOS 95."
        )]
        [SerializeField]
        string osName = "RetroOS 95";

        [Tooltip("Skin for the whole OS. Empty = the built-in Windows 95 palette.")]
        [SerializeField]
        RetroTheme theme;

        [Tooltip(
            "Picture the desktop comes up with. The player can change it from "
                + "the Control Panel, and their choice is what comes back the "
                + "next time this machine boots."
        )]
        [SerializeField]
        Sprite wallpaper;

        [SerializeField]
        SoundSettings sounds = new();

        [Tooltip(
            "No-code reactions: when an OS event happens, run actions - reveal "
                + "a file, deliver mail, set a flag, crash the machine."
        )]
        [SerializeField]
        List<OsRule> rules = new();

        [Tooltip(
            "Start with the player sitting at the machine. Off means the OS "
                + "is a picture on its screen until SetFocused(true)."
        )]
        [SerializeField]
        bool startFocused = true;

        [Tooltip(
            "Start with the machine switched on. Off leaves a dark monitor "
                + "until something calls PowerOn() - a switch on the case, a "
                + "player walking up to it - and nothing boots until it does."
        )]
        [SerializeField]
        bool startPoweredOn = true;

        // The RetroMachine on this same object (RequireComponent) - a computer and its OS are one thing, nothing to wire.
        RetroMachine machine;

        // The RetroMonitor on this same object - the glass and the machine are one piece of hardware, nothing to wire.
        RetroMonitor monitor;

        /// <summary>The computer this OS runs on: its folder of drives and its address on the network.</summary>
        public RetroMachine Machine => machine ? machine : machine = GetComponent<RetroMachine>();

        /// <summary>Name of the operating system, for the Start banner and the About box. Not the computer name - that is <see cref="RetroMachine"/>.</summary>
        public string OsName => string.IsNullOrWhiteSpace(osName) ? "RetroOS" : osName.Trim();

        /// <summary>The desktop picture this machine ships with.</summary>
        public Sprite Wallpaper => wallpaper;

        /// <summary>The story rules this machine runs, for tooling that wants to check them without running the game.</summary>
        public IReadOnlyList<OsRule> Rules => rules;

        RectTransform _screen;
        RectTransform _background,
            _windowLayer,
            _taskbarLayer,
            _overlay,
            _systemLayer;
        RetroOsContext _context;
        DesktopScreen _desktop;
        OsRuleRunner _rules;
        RetroScreenHost _host;
        RetroScreenPointer _pointer;
        RetroVirtualCursor _cursor;
        RetroScreenInput _screenInput;
        InputSystemUIInputModule _inputModule;
        MachinePrefs _prefs;
        MachineSkin _skin;
        RetroWorld _world;
        Action _neighborhoodChanged;
        bool _startupPlayed;
        bool _booted;

        // How long the please-wait screens hold and the glass fades - short enough not to wait, long enough to read as a machine going down, not a power cut.
        const float LogoffPause = 1.1f;
        const float ShutdownPause = 1.4f;
        const float ShutdownFade = 0.7f;

        // The exit currently playing. Both tear the session down before waiting, so a second request during the pause must be caught, and a mid-fade power-on must cut it short.
        Coroutine _sessionExit;

        /// <summary>The running OS. <c>null</c> before <c>Start</c>.</summary>
        public RetroOsContext Context => _context;

        /// <summary>Whether the OS is currently accepting clicks.</summary>
        public bool Focused { get; private set; }

        /// <summary>Whether the machine is switched on. A machine that starts off is built but not booted - its context, drives and programs all exist, and <see cref="PowerOn"/> is what brings the OS up.</summary>
        public bool PoweredOn { get; private set; }

        /// <summary>The screen this computer draws on: the RetroMonitor beside this component.</summary>
        public RetroMonitor Monitor => monitor ? monitor : monitor = GetComponent<RetroMonitor>();

        /// <summary>Whether the OS is driven by a cursor drawn on its own screen rather than by flipping to a fullscreen overlay when focused.</summary>
        public bool UsesOnScreenCursor => Monitor && Monitor.UsesOnScreenCursor;

        void Start()
        {
            // The machine and the world, resolved before anything is drawn - everything below reads one or the other.
            EnsureMachine();
            EnsureMonitor();
            _world = RetroWorld.Require();

            // This machine's look, not the process's. Claimed before the first rect and again whenever the player turns to it.
            _skin = new MachineSkin(theme, () => _screen);
            _skin.Claim();

            BuildCanvas();
            EnsureEventSystem();
            EnsureAudioListener();
            BuildContext();

            RestoreSettings(SettingsMoment.Booting);

            RetroOs.SetCurrent(_context);

            // A machine that starts off is dark glass; everything is built, and PowerOn only has to boot it.
            if (!startPoweredOn)
            {
                SetPower(false);
                if (_host)
                    _host.SetVisible(false);
                return;
            }

            SetPower(true);
            Boot();
            SetFocused(startFocused);
        }

        // The OS coming up. os.boot fires the first time only - on a machine that starts off, not until it is switched on, so a boot greeting fires when the player is there.
        void Boot()
        {
            if (!_booted)
            {
                _booted = true;
                _context.Events.Raise(OsEvents.Boot);
            }

            StartUp();
        }

        // The power switch. The network reads the same flag, so an off machine drops out of Network Neighborhood.
        void SetPower(bool on)
        {
            PoweredOn = on;
            if (machine)
                machine.PoweredOn = on;
        }

        /// <summary>Asks every app that remembers something to put it back.</summary>
        void RestoreSettings(SettingsMoment moment)
        {
            if (_context?.Apps == null) return;

            foreach (var app in _context.Apps.All)
                if (app is IRemembersSettings remembers)
                    remembers.Restore(_context, moment);
        }

        // The start-up sequence, then the logon prompt. The chime waits for the memory count to finish.
        void StartUp()
        {
            var settings = machine ? machine.Boot : new BootSettings();
            if (!settings.enabled)
            {
                ShowLogin();
                PlayStartupChime();
                return;
            }

            var screen = BootScreen.Play(
                _context,
                _systemLayer,
                settings,
                () =>
                {
                    ShowLogin();
                    PlayStartupChime();
                }
            );

            // Only the player actually sitting at this machine can skip it.
            if (screen)
                screen.CanSkip = () => Focused;
        }

        /// <summary>Sits the player down at the machine, or gets them up again. The screen stays lit either way - it is a monitor, and a monitor left alone does not go dark - but only a focused machine takes clicks, and only a focused machine owns the display.</summary>
        public void SetFocused(bool focused)
        {
            Focused = focused;
            if (_host)
                _host.SetFocused(focused);

            if (focused)
            {
                TakeSharedResources();
                WarnIfDeaf();
                TakeClickSpeed();
            }

            RefreshOnScreenCursor();
            TakeHardwareCursor(focused);
        }

        // The double-click window is one static for the whole scene - two machines
        // cannot each have their own while one EventSystem counts for both - so the
        // machine the player has actually turned to is the one that sets it.
        void TakeClickSpeed()
        {
            if (_context?.Prefs is not { Ready: true }) return;

            UI.RetroClicks.Seconds = _context.Prefs.GetFloat(
                Apps.SettingsView.DoubleClickKey, UI.RetroClicks.DefaultSeconds);
        }

        bool _warnedDeaf;

        // A fullscreen-overlay machine relies on the module's own point action; with none, the OS takes the display and answers nothing (the on-screen-cursor shim supplies its own, so this only bites one mode).
        void WarnIfDeaf()
        {
            if (_warnedDeaf || UsesOnScreenCursor || !_inputModule || _inputModule.point)
                return;

            _warnedDeaf = true;
            Debug.LogWarning(
                $"RetroOS on '{name}': the EventSystem's InputSystemUIInputModule has no Point "
                    + "action, so this machine is drawn over the display and cannot be clicked. "
                    + "Assign the module's UI actions (its Actions Asset, or Default Input "
                    + "Actions), or switch this machine to the On Screen Cursor interaction "
                    + "mode, which brings its own.",
                _inputModule
            );
        }

        // The hardware cursor's pre-take state, restored on the way out so the game gets back what it had.
        CursorLockMode _cursorLock;
        bool _cursorVisible;
        bool _cursorHeld;

        /// <summary>
        /// The hardware cursor while the player is at this machine, and the game's
        /// own back again when they stand up. On-screen cursor: hidden and Confined
        /// - not Locked, which forces mouse pointers to (-1,-1) in the UI module and
        /// drops drags. Overlay: visible and unlocked, or a game that locked it for
        /// mouse-look leaves the OS drawn but dead.
        /// </summary>
        void TakeHardwareCursor(bool focused)
        {
            if (focused)
            {
                if (!_cursorHeld)
                {
                    _cursorLock = Cursor.lockState;
                    _cursorVisible = Cursor.visible;
                    _cursorHeld = true;
                }

                if (UsesOnScreenCursor)
                {
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Confined;
                    return;
                }

                Cursor.visible = true;
                if (Cursor.lockState == CursorLockMode.Locked)
                    Cursor.lockState = CursorLockMode.None;
                return;
            }

            if (!_cursorHeld)
                return;

            _cursorHeld = false;
            Cursor.lockState = _cursorLock;
            Cursor.visible = _cursorVisible;
        }

        // The drawn cursor and its pointer stay live whenever the machine is on and lit, so an idle monitor still shows a resting pointer. Focus only decides whether a click goes through and who owns the hardware cursor.
        void RefreshOnScreenCursor()
        {
            if (!UsesOnScreenCursor)
                return;

            var live = PoweredOn && _host && _host.ScreenVisible;

            // The pointer walks with real input only while the player is here; unfocused, the drawn arrow holds its last spot.
            if (_pointer)
                _pointer.enabled = live && Focused;
            if (_cursor)
            {
                _cursor.SetVisible(live);
                _cursor.SetTracking(Focused);
            }

            // The shim is shared across every machine in the scene; hand it our
            // pointer so a stale call from another desk cannot take it back.
            if (_screenInput)
                _screenInput.SetActive(live && Focused, _pointer);
        }

        // Once-per-game things (the widget palette, the sound module, the settings store, the one EventSystem) belong to whichever machine the player is at - only sitting down decides that.
        void TakeSharedResources()
        {
            _skin?.Claim();

            if (_context?.Audio)
                RetroSfx.Bind(_context.Audio);

            if (machine)
                RetroPrefs.Bind(machine.Folder);

            if (_screenInput && _pointer)
                _screenInput.Bind(_pointer, _inputModule);
        }

        /// <summary>Switches the screen off, picture and all.</summary>
        public void SetVisible(bool visible)
        {
            if (_host)
                _host.SetVisible(visible);

            // A dark screen shows no cursor; a screen coming back does, even
            // before the player walks over to it.
            RefreshOnScreenCursor();

            if (visible)
                PlayStartupChime();
        }

        /// <summary>Enables or disables clicking without turning the screen off. The same thing as focus; kept because a scene event may call it.</summary>
        public void SetInteractable(bool interactable) => SetFocused(interactable);

        /// <summary>The screen the OS is drawn on: its canvas, its projector camera and the picture they produce.</summary>
        public RetroScreenHost ScreenHost => _host;

        void PlayStartupChime()
        {
            if (_startupPlayed || _context == null)
                return;
            _startupPlayed = true;
            _context.Audio.PlayUi(UiSfx.Startup);
        }

        // One canvas, drawn once, projected onto whatever surface. Layers are sibling rects, so OS-wide depth is sibling order.
        void BuildCanvas()
        {
            _host = RetroScreenHost.Create(
                transform,
                monitor.Resolution,
                monitor.DynamicResolution,
                monitor.SortingOrder
            );
            _host.PixelPerfect = monitor.PixelPerfect;
            _host.UnfocusedFps = monitor.UnfocusedFps;

            // The picture only. Where it shows (a screen material, a RawImage, six of them) is the game's to wire from this RenderTexture.
            if (monitor.TextureAsset)
                _host.UseExternalTexture(monitor.TextureAsset);

            // The OS never becomes an overlay in this mode; a drawn cursor,
            // walked by relative input, drives it on its own texture.
            if (UsesOnScreenCursor)
                _host.LockToTexture = true;

            _screen = BuildWorkArea(_host.Root);

            _background = Layer("Background", _screen);
            _windowLayer = Layer("Windows", _screen);
            _taskbarLayer = Layer("Taskbar", _screen);
            _overlay = Layer("Overlay", _screen);
            _systemLayer = Layer("System", _screen);

            if (UsesOnScreenCursor)
            {
                _pointer = _host.gameObject.AddComponent<RetroScreenPointer>();
                _pointer.Configure(_host, monitor.CursorSpeed, monitor.StickSpeed);
                _pointer.enabled = false;

                // Last child of the canvas: drawn over every OS layer.
                _cursor = RetroVirtualCursor.Create(_host, _pointer);
            }
        }

        // The part of the picture the OS fills. Which part is the screen's call - a bezel is a fact about the monitor.
        RectTransform BuildWorkArea(Transform canvas)
        {
            var screen = RetroUi.NewRect("Screen", canvas);
            RetroUi.Stretch(screen);

            var area = monitor.WorkArea;
            screen.anchorMin = area.min;
            screen.anchorMax = area.max;
            screen.offsetMin = screen.offsetMax = Vector2.zero;
            return screen;
        }

        static RectTransform Layer(string name, Transform parent) => RetroUi.Stretch(RetroUi.NewRect(name, parent));

        /// <summary>Finds the scene's EventSystem, and makes one only if the scene has none at all.</summary>
        void EnsureEventSystem()
        {
            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (!eventSystem)
            {
                // Nothing to disturb: no EventSystem means no uGUI yet, and one must exist for any of this to be clickable.
                eventSystem = new GameObject("EventSystem", typeof(EventSystem))
                    .GetComponent<EventSystem>();
                var fresh = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                fresh.AssignDefaultActions();
                _inputModule = fresh;
            }
            else
            {
                _inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();

                if (!_inputModule && !eventSystem.GetComponent<BaseInputModule>())
                {
                    // An EventSystem with no module is an oversight, not a config - nothing is clickable until it has one.
                    var added = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                    added.AssignDefaultActions();
                    _inputModule = added;
                }
            }

            if (!_inputModule && UsesOnScreenCursor)
                Debug.LogError(
                    $"RetroOS on '{name}': the on-screen cursor needs an " +
                    "InputSystemUIInputModule on the scene's EventSystem, and this scene has a " +
                    "different input module on it. Add one, or switch this machine off the " +
                    "on-screen cursor. Nothing has been changed for you - the module on that " +
                    "EventSystem belongs to your game.", eventSystem);

            // In on-screen-cursor mode the module's pointer is retargeted at a private device; the real mouse stays the game's.
            if (!UsesOnScreenCursor || !_pointer)
                return;

            // Found, not bound: one EventSystem per scene, so a second computer would take its pointer. Binding waits for someone to sit down (TakeSharedResources).
            _screenInput =
                eventSystem.gameObject.GetComponent<RetroScreenInput>()
                ?? eventSystem.gameObject.AddComponent<RetroScreenInput>();
        }

        /// <summary>Warns when the scene has nowhere to hear from.</summary>
        void EnsureAudioListener()
        {
            // Include inactive: a game whose listener sits on a camera that is
            // switched off at boot still has one.
            if (FindFirstObjectByType<AudioListener>(FindObjectsInactive.Include)) return;

            Debug.LogWarning(
                $"RetroOS on '{name}': this scene has no AudioListener, so nothing it plays " +
                "will be heard. Put one on the player's camera - where the listener belongs is " +
                "your game's decision, and a computer on a desk is a bad guess at it.", this);
        }

        void BuildContext()
        {
            // Accounts are the machine's - the same person at the next desk is a different user with a different Documents.
            var users = machine
                .Users.Where(u => u != null && !string.IsNullOrWhiteSpace(u.username))
                .Select(u => new UserAccount(u.username, u.password, u.displayName))
                .ToList();

            var events = new OsEventBus();
            var flags = new GameFlags();
            flags.Changed += (flag, on) => events.Raise(on ? OsEvents.FlagSet : OsEvents.FlagCleared, flag);

            var drives = MountMachine(events);
            var network = new NetworkService(events.Raise, machine);
            var fs = new VirtualFileSystem(drives.Root);

            // Path resolution answers as the signed-in user, so another user's file resolves to nothing. Set here because the context does not exist yet.
            fs.Viewer = () => _context?.CurrentUser;

            var windows = new WindowManager(_windowLayer) { Skin = _skin };
            var audio = AudioManager.Create(transform, sounds);
            var apps = new AppManager();

            var installed = CollectApps();

            // Mail lives on the Inbox app; the mailbox stays a shared service so rules and notifications work before the app opens.
            var mailSource = installed.OfType<IMailSource>().FirstOrDefault();

            _context = new RetroOsContext
            {
                Fs = fs,
                Auth = new AuthService(users),
                OsName = OsName,
                Wallpaper = wallpaper,
                Windows = windows,
                Overlay = _overlay,
                Root = _screen,
                SystemLayer = _systemLayer,
                Drives = drives,
                Prefs = _prefs ?? RetroPrefs.For(machine ? machine.Folder : null),
                Skin = _skin,
                Machine = machine,
                Apps = apps,
                Audio = audio,
                Events = events,
                Flags = flags,
                Clock = new OsClock(_world ? _world.SystemDate : null),
                Mail = new MailboxService(mailSource?.Messages, events.Raise),
                Net = network,
                Runner = this,
                Screen = _host,
                Pointer = _pointer,
                LogoutRequested = Logout,
                ShutdownRequested = Shutdown,
                IsFocused = () => Focused,
            };

            // The bay exists before the OS it reports to, so it is told where
            // to report once there is somewhere.
            drives.ReportTo(_context.NotifyFsChanged);

            apps.Init(_context, installed);
            _rules = new OsRuleRunner(_context, rules);

            // What this machine's file system is made of, said where the file
            // system lives rather than in the middle of the boot sequence.
            if (!_world || _world.ShowNetworkNeighborhood)
                _neighborhoodChanged = MachineMounts.Neighborhood(drives, network, events,
                    () => _context,
                    _world ? _world.NeighborhoodLabel : "Network Neighborhood");

            MachineMounts.UserFolders(drives, events, () => _context);

            // Interaction sounds driven by OS activity.
            windows.Opened += _ => audio.PlayUi(UiSfx.WindowOpen);
            windows.Closed += _ => audio.PlayUi(UiSfx.WindowClose);
            events.Subscribe(OsEvents.DiscInserted, _ => audio.PlayUi(UiSfx.DiscInsert));
            events.Subscribe(OsEvents.DiscEjected, _ => audio.PlayUi(UiSfx.DiscEject));

            AnnounceEvents(events);
        }

        // Everything the OS mentions on its own goes to the tray, not a dialog - a message that steals the pointer interrupts worse than the news.
        void AnnounceEvents(OsEventBus events)
        {
            events.Subscribe(
                OsEvents.MailReceived,
                id =>
                {
                    var message = _context.Mail.Inbox.LastOrDefault();
                    _context.Notify(
                        "New mail",
                        message != null ? $"{message.From}: {message.Subject}" : id,
                        RetroIcons.Mail,
                        () => _context.Apps.Launch("mail")
                    );
                }
            );

            events.Subscribe(
                OsEvents.DiscInserted,
                label => _context.Notify("Disc inserted", label, RetroIcons.Disc, () => _context.Apps.Launch("files"))
            );
        }

        // The RetroMachine on this object (RequireComponent adds one; this covers one removed by hand).
        void EnsureMachine()
        {
            if (Machine)
                return;

            machine = gameObject.AddComponent<RetroMachine>();
            Debug.Log($"RetroOS: no RetroMachine on '{name}', so one was added.");
        }

        // The RetroMonitor on this object. With none, the OS runs on a default screen (game view, 1024x768) and says so, rather than drawing on some other desk's glass.
        void EnsureMonitor()
        {
            if (!Monitor)
            {
                monitor = gameObject.AddComponent<RetroMonitor>();
                Debug.LogWarning(
                    $"RetroOS: no Monitor on '{name}', so a default one was added and the "
                        + "picture goes to the game view. Add Component > Deepwave > RetroOS > Monitor "
                        + "on this object to say how big its picture is, which RenderTexture "
                        + "it renders into and how the player points at it.",
                    this
                );
            }

            // The screen is where a world prop and a computer meet - a desk asks the monitor which machine booted on it.
            monitor.PlugIn(this);
        }

        /// <summary>Who can sign in at this machine, by name.</summary>
        List<string> AccountNames() => machine == null
            ? new List<string>()
            : machine.Users
                .Where(u => u != null && !string.IsNullOrWhiteSpace(u.username))
                .Select(u => u.username.Trim())
                .ToList();

        // The machine's drives are the folders inside its own folder on disk.
        DriveBay MountMachine(OsEventBus events)
        {
            var bay = new DriveBay(events.Raise);
            var folder = machine.Folder;

            if (!System.IO.Directory.Exists(folder))
                Debug.LogWarning(
                    $"RetroOS: machine folder '{folder}' does not exist. "
                        + "Run Deepwave > RetroOS > Setup, or create it by hand."
                );

            // Settings belong to the machine: the file sits beside the drive folders, so wiping the machine wipes them. One store at a time; a second bootstrap takes it and says so.
            _prefs = RetroPrefs.Bind(folder);

            bay.Mount(DiskMount.MountDrives(folder, AccountNames()));
            events.Raise(OsEvents.ZoneMounted, machine.FolderName);
            return bay;
        }

        // Every RetroOS app component under this object, in hierarchy order - software is on the computer it is installed on.
        List<IRetroApp> CollectApps()
        {
            var list = new List<IRetroApp>();
            foreach (var app in GetComponentsInChildren<RetroAppBehaviour>(true))
                list.Add(app);
            return list;
        }

        void ShowLogin()
        {
            // The logon screen is built frames after Start, by which time the other computer may hold the palette - so every deferred build claims it back first.
            _skin?.Claim();

            var settings = machine ? machine.Login : new LoginSettings();
            var skip =
                settings.mode == LoginMode.SkipAlways
                || (settings.mode == LoginMode.Automatic && _context.Accounts.Count == 0);

            // A shortcut past the dialog, never a way onto the desktop with nobody signed in: VfsNode.IsVisibleTo
            // reads an empty viewer as single-user and exposes every account's private folders. If the shortcut fails, ask.
            if (skip && (AutoLogin(settings) || _context.Accounts.Count == 0))
            {
                ShowDesktop();
                return;
            }

            LoginScreen.Show(_context, ShowDesktop);
        }

        /// <summary>
        /// Signs in without a dialog so os.login still fires. Every non-sign-in exit
        /// is reported: reaching the desktop with nobody signed in makes
        /// VfsNode.IsVisibleTo read an empty viewer as single-user and expose every
        /// account's private folders, so one mistyped inspector name turns privacy off.
        /// </summary>
        /// <returns><c>false</c> sends the machine to the logon screen instead of the desktop, and says why in the console.</returns>
        bool AutoLogin(LoginSettings settings)
        {
            var asked = settings.autoLoginUser?.Trim();
            var username = string.IsNullOrEmpty(asked)
                ? _context.Accounts.FirstOrDefault()?.username
                : asked;

            if (string.IsNullOrEmpty(username))
            {
                if (_context.Accounts.Count > 0)
                    Debug.LogError($"RetroOS on '{name}': set to sign in without asking, but no " +
                                   "account was named and the account list could not supply one. " +
                                   "Showing the logon screen instead. Name an account in the " +
                                   "machine's Login settings.", this);
                return false;
            }

            var entry = _context.Accounts.FirstOrDefault(u =>
                string.Equals(u.username, username, System.StringComparison.OrdinalIgnoreCase)
            );
            if (entry == null)
            {
                Debug.LogError($"RetroOS on '{name}': no account named '{username}' to sign in " +
                               "automatically. Showing the logon screen instead. Check the " +
                               "spelling against the machine's account list.", this);
                return false;
            }

            if (_context.Auth.Login(entry.username, entry.password) == LoginResult.Success)
            {
                _context.Events.Raise(OsEvents.Login, entry.username);
                return true;
            }

            Debug.LogError($"RetroOS on '{name}': '{entry.username}' was refused its own stored " +
                           "password. Showing the logon screen instead.", this);
            return false;
        }

        void ShowDesktop()
        {
            _skin?.Claim();

            _desktop = DesktopScreen.Create(_context, _background, _taskbarLayer);
            RestoreSettings(SettingsMoment.Desktop);
            _context.Apps.LaunchStartupApps();
        }

        // Signs the user out: the desktop goes, the please-wait screen holds
        // for a moment, and the logon screen comes back up.
        void Logout()
        {
            if (!Closable("log off")) return;

            _sessionExit = StartCoroutine(LogoutSequence());
        }

        IEnumerator LogoutSequence()
        {
            TearDownSession();
            _skin?.Claim();
            var screen = SystemScreen.LoggingOff(_systemLayer);

            yield return new WaitForSecondsRealtime(LogoffPause);

            if (screen) screen.Dismiss();
            else ClearChildren(_systemLayer);

            _sessionExit = null;
            ShowLogin();
        }

        // Turns the machine off: the desktop goes, the please-wait screen holds, the
        // glass fades to a dead monitor. The game reacts via os.shutdown; PowerOn() boots it again.
        void Shutdown()
        {
            if (!Closable("shut down")) return;

            _sessionExit = StartCoroutine(ShutdownSequence());
        }

        // Whether there is a session to end: both exits come from rules and code too, so both can hit a machine with nothing to close, and a replayed shutdown over a dead monitor is a bug nobody catches.
        bool Closable(string what)
        {
            if (_sessionExit != null)
            {
                Debug.LogWarning($"RetroOS on '{name}': ignored a {what} - this machine is " +
                                 "already signing out or shutting down.", this);
                return false;
            }

            if (!PoweredOn)
            {
                Debug.LogWarning($"RetroOS on '{name}': ignored a {what} - this machine is " +
                                 "already off. Call PowerOn() to bring it back up.", this);
                return false;
            }

            return true;
        }

        IEnumerator ShutdownSequence()
        {
            TearDownSession();
            _skin?.Claim();
            var screen = SystemScreen.ShuttingDown(_systemLayer);

            yield return new WaitForSecondsRealtime(ShutdownPause);

            var faded = false;
            if (screen) screen.FadeToBlack(ShutdownFade, () => faded = true);
            else faded = true;

            while (!faded)
                yield return null;

            // Off to the outside world. Powered down only now, so a file server stays up until its screen actually goes dark.
            SetPower(false);
            SystemScreen.Blackout(_systemLayer);

            // A machine that is off draws no cursor on its glass.
            RefreshOnScreenCursor();
            _sessionExit = null;
        }

        /// <summary>Flips the switch: boots a machine that was shut down, or one that started off. Safe to call from a scene event, a rule or your own code - a power switch on the CRT prop, a player pressing the interact key at the desk. Raises <see cref="OsEvents.PowerOn"/>, and <see cref="OsEvents.Boot"/> with it the first time this machine comes up. Does nothing on a machine that is already on.</summary>
        public void PowerOn()
        {
            if (_context == null || PoweredOn)
                return;

            // A machine switched back on while its shutdown is still fading
            // must not have that fade finish over the desktop it just booted.
            if (_sessionExit != null)
            {
                StopCoroutine(_sessionExit);
                _sessionExit = null;
            }

            ClearChildren(_systemLayer);
            _skin?.Claim();
            SetPower(true);

            // Straight to the screen, not SetVisible: the chime belongs to the OS coming up, and Boot plays it after the memory count.
            if (_host)
                _host.SetVisible(true);
            _startupPlayed = false;

            // The screen is lit again, so its cursor tracks the mouse once
            // more - the player does not have to sit down first to see it.
            RefreshOnScreenCursor();

            _context.Events.Raise(OsEvents.PowerOn);
            Boot();
        }

        // Everything the signed-in session owns: windows, dialogs, the desktop.
        void TearDownSession()
        {
            ClearChildren(_windowLayer);
            ClearChildren(_overlay);
            if (_desktop)
                Destroy(_desktop.gameObject);
            _desktop = null;
        }

        static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        void OnDestroy()
        {
            // Anything changed but unwritten (a scheme picked before quitting). This machine's own store; another keeps writing to its.
            _prefs?.Save();

            // The screen lives at scene root, so tell it to go. Its pointer/cursor go with it; the input shim on the shared EventSystem is released now and destroyed only when the last machine leaves.
            if (_screenInput)
            {
                _screenInput.SetActive(false, _pointer);
                if (RetroOs.All.Count <= 1)
                    Destroy(_screenInput);
            }

            // Torn down with the player still seated (a mid-session scene unload). The cursor is the game's - a hidden or penned one outlives the machine.
            TakeHardwareCursor(false);

            if (_host)
                _host.Dispose();
            if (monitor)
                monitor.Unplug(this);

            if (_neighborhoodChanged != null && _context?.Net != null)
                _context.Net.Changed -= _neighborhoodChanged;
            _neighborhoodChanged = null;

            _context?.Net?.Dispose();

            // What the machines share is freed by whatever knows how many are
            // left, which is RetroOs and not this.
            RetroOs.Forget(_context);
        }
    }
}
