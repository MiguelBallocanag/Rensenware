using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps
{
    /// <summary>Everything the OS needs to install and launch a program. Implemented by <see cref="RetroAppBehaviour"/>; the desktop, Start menu, taskbar and file associations all go through it.</summary>
    public interface IRetroApp
    {
        string Id { get; }
        string Title { get; }
        Sprite IconSprite { get; }

        Vector2 WindowSize { get; }
        Vector2 MinWindowSize { get; }
        bool Resizable { get; }
        bool StartMaximized { get; }
        bool SingleInstance { get; }

        bool ShowOnDesktop { get; }
        bool ShowInStartMenu { get; }
        // Submenu under Start > Programs. Empty means directly under it.
        string Category { get; }
        bool LaunchOnBoot { get; }
        int SortOrder { get; }

        string WindowTitle(VfsFile file);
        bool CanOpen(VfsFile file);
        void BuildContent(AppContext ctx);

        /// <summary>Called at each window moment so a program can react - raise story events, fire UnityEvents, save state.</summary>
        void OnAppEvent(AppLifecycle stage, AppContext ctx, string argument);
    }

    /// <summary>The window moments a program is told about.</summary>
    public enum AppLifecycle
    {
        Opened,
        Closed,
        Focused,
        Minimized,
        Maximized,
        Restored,
        Action        // an app-specific interaction; argument names it
    }

    /// <summary>The shared identity, window and placement block serialized on every program.</summary>
    [Serializable]
    public class AppSettings
    {
        [Header("Identity")]
        [Tooltip("Stable id used in events and rules. Empty = the object's name.")]
        public string appId;
        [Tooltip("Name under the desktop icon and in the Start menu. Empty = the object's name.")]
        public string title;

        [Header("Icon")]
        [Tooltip("Custom sprite. Leave empty to use a built-in pixel icon.")]
        public Sprite icon;
        public BuiltinIcon builtinIcon = BuiltinIcon.File;

        [Header("Window")]
        public Vector2 windowSize = new(560, 420);
        public Vector2 minWindowSize = new(320, 220);
        public bool resizable = true;
        public bool startMaximized;
        [Tooltip("Re-focus the running window instead of opening another one.")]
        public bool singleInstance = true;

        [Header("Placement")]
        [Tooltip("Show a shortcut on the desktop.")]
        public bool showOnDesktop = true;
        [Tooltip("List the program under Start > Programs.")]
        public bool showInStartMenu = true;
        [Tooltip("Group it into a submenu under Programs - 'Accessories', " +
                 "'Games'. Empty lists it directly under Programs.")]
        public string category = "";
        [Tooltip("Open this program automatically when the OS finishes booting.")]
        public bool launchOnBoot;
        [Tooltip("Lower numbers come first on the desktop and in the Start menu.")]
        public int sortOrder;

        public string ResolveId(string fallback) =>
            string.IsNullOrWhiteSpace(appId) ? fallback : appId.Trim();

        public string ResolveTitle(string fallback) =>
            string.IsNullOrWhiteSpace(title) ? fallback : title;

        public Sprite ResolveIcon() => icon ? icon : RetroIcons.Get(builtinIcon);
    }

    /// <summary>Inspector-wired reactions for one program - fire a game event on open, on a button click, etc. without scripting.</summary>
    [Serializable]
    public class RetroAppEvents
    {
        [Tooltip("Raised when a window of this app opens.")]
        public UnityEvent onOpened = new();
        [Tooltip("Raised when the last window of this app closes.")]
        public UnityEvent onClosed = new();
        [Tooltip("Raised when the window is brought to the front.")]
        public UnityEvent onFocused = new();
        [Tooltip("Raised when the window is minimized to the taskbar.")]
        public UnityEvent onMinimized = new();
        [Tooltip("Raised when the window is maximized.")]
        public UnityEvent onMaximized = new();

        [Tooltip("Raised for every app-specific interaction, with its name " +
                 "(a button id, a game result, a page url...).")]
        public UnityEvent<string> onAction = new();

        [Serializable]
        public class ActionHook
        {
            [Tooltip("Action name to listen for, e.g. 'won', 'print', 'page'. " +
                     "Trailing * matches a prefix.")]
            public string action;
            [Tooltip("Invoked with the action argument.")]
            public UnityEvent<string> response = new();
        }

        [Tooltip("Reactions to individual named actions of this app.")]
        public List<ActionHook> hooks = new();

        public void Fire(AppLifecycle stage, string argument)
        {
            switch (stage)
            {
                case AppLifecycle.Opened: onOpened.Invoke(); break;
                case AppLifecycle.Closed: onClosed.Invoke(); break;
                case AppLifecycle.Focused: onFocused.Invoke(); break;
                case AppLifecycle.Minimized: onMinimized.Invoke(); break;
                case AppLifecycle.Maximized: onMaximized.Invoke(); break;
                case AppLifecycle.Action:
                    onAction.Invoke(argument);
                    foreach (var hook in hooks)
                    {
                        if (hook?.response == null) continue;
                        if (!Matches(hook.action, argument)) continue;
                        hook.response.Invoke(argument);
                    }
                    break;
            }
        }

        static bool Matches(string pattern, string argument)
        {
            pattern = pattern?.Trim();
            if (string.IsNullOrEmpty(pattern)) return true;
            argument ??= "";

            // Match the action name of "action" or "action:payload"; trailing * = prefix.
            var name = argument.Split(':')[0];
            return pattern.EndsWith("*")
                ? name.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)
                : string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Per-app content contracts: a view asks its context for what it needs and
    // never learns which program class configured it.

    /// <summary>Supplies the channels a CCTV monitor shows.</summary>
    public interface ICameraSource { List<CameraEntry> Cameras { get; } }

    /// <summary>Supplies the messages a mail program shows.</summary>
    public interface IMailSource { List<MailEntry> Messages { get; } }

    // One button on the browser's directory strip - What's New!, Net Search.
    [Serializable]
    public class DirectoryLink
    {
        [Tooltip("Text on the button.")]
        public string caption = "What's New!";
        [Tooltip("Address it opens. Empty = the caption.")]
        public string url = "";
    }

    /// <summary>Supplies the pages and home address a browser serves.</summary>
    public interface IWebSource
    {
        string HomeUrl { get; }
        // Page load time. 0 is instant; a fraction of a second is a modem.
        float LoadSeconds { get; }
        // The strip of buttons under the address bar.
        List<DirectoryLink> DirectoryLinks { get; }

        /// <summary>The bookmarks menu. Inspector-authored and player-added; on the program so closing the browser keeps it.</summary>
        List<WebBookmark> Bookmarks { get; }

        /// <summary>Pages saved onto the machine's disk this session.</summary>
        List<string> Downloads { get; }

        /// <summary>Addresses this account has opened, so a visited link draws purple. On the program, like <see cref="Downloads"/>.</summary>
        HashSet<string> Visited { get; }

        /// <summary>Sets whose session this is; empties <see cref="Downloads"/> on an account change. Bookmarks are the machine's and are kept.</summary>
        void KeepFor(string username);
    }

    // Where a picture browser looks. Empty means the whole machine.
    public interface IGallerySource { string FolderPath { get; } }

    /// <summary>Supplies the list a reminder program keeps, and its clock.</summary>
    public interface IReminderSource
    {
        List<ReminderEntry> Reminders { get; }

        /// <summary>Lets a reminder that already fired today fire again (after a reschedule, re-enable or re-add).</summary>
        void Rearm(string reminderId);
    }

    /// <summary>Supplies the people a messenger can talk to.</summary>
    public interface IChatSource
    {
        List<ChatContact> Contacts { get; }

        /// <summary>Conversation progress by contact id. On the program, so closing the window mid-conversation keeps it.</summary>
        Dictionary<string, ChatThread> Threads { get; }

        /// <summary>Sets whose conversations <see cref="Threads"/> holds; drops them on an account change (a transcript is as private as a document).</summary>
        void KeepFor(string username);
    }

    /// <summary>Supplies a terminal its banner, its feel and its own commands.</summary>
    public interface ITerminalSource
    {
        string Banner { get; }
        bool Typewriter { get; }
        float TypewriterDelay { get; }
        List<TerminalCommandEntry> Commands { get; }
    }
}
