using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Deepwave.RetroOS.Apps.Components;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.Windowing;

namespace Deepwave.RetroOS.Apps
{
    /// <summary>The installed-program registry. Programs are <see cref="RetroAppBehaviour"/> components; launching one builds the window, hands over the content and wires lifecycle events.</summary>
    public class AppManager
    {
        readonly List<IRetroApp> _apps = new();
        readonly Dictionary<IRetroApp, Window> _running = new();
        readonly Dictionary<FileKind, RetroAppBehaviour> _fallbackViewers = new();
        RetroOsContext _os;
        GameObject _viewerHost;

        public IReadOnlyList<IRetroApp> All => _apps;

        /// <summary>Raised when a program is installed while the OS is running.</summary>
        public event Action Changed;

        public void Init(RetroOsContext os, IEnumerable<IRetroApp> apps)
        {
            _os = os;
            _apps.Clear();
            foreach (var app in apps ?? Enumerable.Empty<IRetroApp>()) Register(app, quiet: true);

            Sort();
            if (_apps.Count == 0)
                Debug.LogWarning("RetroOS: no programs installed. Add RetroOS app components " +
                                 "to a child of the bootstrap (Add Component, then search by name), " +
                                 "or run Deepwave > RetroOS > Setup.");
        }

        /// <summary>Installs a program while the OS is running; the desktop and Start menu pick it up on their next rebuild.</summary>
        /// <returns><c>false</c> when the id is already taken.</returns>
        public bool Install(IRetroApp app)
        {
            if (!Register(app, quiet: false)) return false;
            Sort();
            Changed?.Invoke();
            return true;
        }

        bool Register(IRetroApp app, bool quiet)
        {
            if (app == null || app.Equals(null)) return false;
            if (_apps.Any(a => string.Equals(a.Id, app.Id, StringComparison.OrdinalIgnoreCase)))
            {
                Debug.LogWarning($"RetroOS: duplicate app id '{app.Id}' ignored.");
                return false;
            }
            _apps.Add(app);
            return true;
        }

        // Stable sort: same sort order keeps install (hierarchy) order, so the
        // desktop and Start menu do not reshuffle between runs.
        void Sort()
        {
            var ordered = _apps.OrderBy(a => a.SortOrder).ToList();
            _apps.Clear();
            _apps.AddRange(ordered);
        }

        public IRetroApp Find(string appId) =>
            _apps.FirstOrDefault(a => string.Equals(a.Id, appId?.Trim(), StringComparison.OrdinalIgnoreCase));

        public bool IsRunning(string appId)
        {
            var app = Find(appId);
            return app != null && _running.TryGetValue(app, out var window) && window;
        }

        public Window Launch(string appId)
        {
            var app = Find(appId);
            if (app == null)
            {
                Debug.LogWarning($"RetroOS: no app with id '{appId}'.");
                return null;
            }
            return Launch(app);
        }

        public Window Launch(IRetroApp app, VfsFile file = null)
        {
            if (app == null)
            {
                Debug.LogWarning("RetroOS: asked to launch a program that is null.");
                return null;
            }

            if (_os == null)
            {
                Debug.LogWarning($"RetroOS: cannot launch '{app.Id}' - the OS is not running on " +
                                 "this machine yet. Wait for os.boot, or switch the machine on.");
                return null;
            }

            if (app.SingleInstance && _running.TryGetValue(app, out var open) && open)
            {
                open.Restore();
                _os.Windows.Focus(open);

                // Brought forward, not rebuilt, so tell the window about the new file.
                if (file != null && open.Content)
                {
                    open.SetTitle(app.WindowTitle(file));
                    foreach (var view in open.Content.GetComponentsInChildren<AppView>(true))
                        view.OnFileOpened(file);
                }

                return open;
            }

            var window = _os.Windows.Open(app.WindowTitle(file), app.WindowSize, app.IconSprite,
                new WindowOptions
                {
                    Resizable = app.Resizable,
                    MinSize = app.MinWindowSize,
                    StartMaximized = app.StartMaximized
                });

            var ctx = new AppContext { Os = _os, Window = window, File = file, App = app };
            try
            {
                app.BuildContent(ctx);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                var label = RetroUi.Label(window.Content,
                    $"'{app.Title}' failed to start.\n{e.GetType().Name}: {e.Message}", 13, TextAnchor.MiddleCenter);
                RetroUi.Stretch(label.rectTransform, 8);
            }

            if (app.SingleInstance) _running[app] = window;
            WireLifecycle(app, ctx, window);
            return window;
        }

        // Every window moment an app reacts to, on the bus and its own UnityEvents.
        void WireLifecycle(IRetroApp app, AppContext ctx, Window window)
        {
            app.OnAppEvent(AppLifecycle.Opened, ctx, app.Id);
            _os.Events.Raise(OsEvents.AppOpened, app.Id);

            window.Focused += () =>
            {
                app.OnAppEvent(AppLifecycle.Focused, ctx, app.Id);
                _os.Events.Raise(OsEvents.AppFocused, app.Id);
            };

            window.MinimizedChanged += minimized =>
            {
                if (!minimized) return;
                app.OnAppEvent(AppLifecycle.Minimized, ctx, app.Id);
                _os.Events.Raise(OsEvents.AppMinimized, app.Id);
            };

            window.MaximizedChanged += maximized =>
            {
                app.OnAppEvent(maximized ? AppLifecycle.Maximized : AppLifecycle.Restored, ctx, app.Id);
                if (maximized) _os.Events.Raise(OsEvents.AppMaximized, app.Id);
            };

            window.Closed += () =>
            {
                if (app.SingleInstance) _running.Remove(app);
                app.OnAppEvent(AppLifecycle.Closed, ctx, app.Id);
                _os.Events.Raise(OsEvents.AppClosed, app.Id);
            };
        }

        /// <summary>Opens every "launch on boot" program, in placement order. Called once the desktop appears.</summary>
        public void LaunchStartupApps()
        {
            foreach (var app in _apps.Where(a => a.LaunchOnBoot)) Launch(app);
        }

        /// <summary>Routes a file to its viewer: installed programs first, then the built-in system viewers.</summary>
        /// <returns><c>false</c> when nothing can open the file.</returns>
        public bool OpenFile(VfsFile file)
        {
            if (file == null) return false;

            var app = _apps.FirstOrDefault(a => a != null && a.CanOpen(file))
                      ?? (IRetroApp)FallbackViewer(file.Kind);
            if (app == null) return false;

            Launch(app, file);
            return true;
        }

        // Built-in viewers so a machine with no Notepad can still open a text file.
        // Created on demand on a hidden object; a real program for the kind wins.
        RetroAppBehaviour FallbackViewer(FileKind kind)
        {
            if (_fallbackViewers.TryGetValue(kind, out var cached) && cached) return cached;

            if (!_viewerHost)
            {
                _viewerHost = new GameObject("RetroOS System Viewers");
                if (_os?.Runner) _viewerHost.transform.SetParent(_os.Runner.transform, false);
                _viewerHost.hideFlags = HideFlags.HideInHierarchy;
            }

            RetroAppBehaviour viewer = kind switch
            {
                FileKind.Text => _viewerHost.AddComponent<RetroNotepadApp>(),
                FileKind.Image => _viewerHost.AddComponent<RetroImageViewerApp>(),
                FileKind.Audio => _viewerHost.AddComponent<RetroCdPlayerApp>(),
                FileKind.Video => _viewerHost.AddComponent<RetroMediaPlayerApp>(),
                FileKind.Web => _viewerHost.AddComponent<RetroBrowserApp>(),
                _ => null
            };
            if (viewer == null) return null;

            viewer.ApplyDefaults();
            viewer.Settings.showOnDesktop = false;
            viewer.Settings.showInStartMenu = false;
            _fallbackViewers[kind] = viewer;
            return viewer;
        }
    }
}
