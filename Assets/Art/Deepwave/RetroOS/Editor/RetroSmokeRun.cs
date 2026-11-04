using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Deepwave.RetroOS.Core.Network;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.Display;
using Deepwave.RetroOS.Apps;
using Deepwave.RetroOS.Apps.Components;

namespace Deepwave.RetroOS.EditorTools
{
    // Boots the OS for real, opens programs and writes what it rendered to PNG - catches
    // layout collapses, empty windows, apps that throw on open. Renders through a camera into
    // a RenderTexture, so it is headless. CI: -executeMethod Deepwave.RetroOS.EditorTools.RetroSmokeRun.Run
    public static class RetroSmokeRun
    {
        const int Width = 1024;
        const int Height = 768;
        const string OutputFolder = "SmokeShots";

        // The machine the run builds. It reads its drives from the sample
        // content, so these have to be the names RetroSampleContent writes.
        const string SmokeUser = "operator";
        const string SmokePassword = "1997";

        static bool s_savedFastEnter;
        static EnterPlayModeOptions s_savedEnterOptions;
        static int s_frame;
        static int s_shot;
        static bool s_running;

        // Shots that were asked for and never reached disk, and whether the run
        // gave up before it got to the end. A run that could not photograph what
        // it was told to is a failed run, not a quiet one.
        static readonly List<string> Missed = new();
        static bool s_aborted;

        // Sign-in is retried until it takes; the numbered schedule starts from
        // the frame after it does.
        static bool s_signedIn;
        static int s_signInFrames;
        const int SignInDeadline = 600;

        /// <summary>Boots the OS, opens every program and writes what it rendered to <c>SmokeShots/</c>. Reached from the RetroOS Setup window, or from a command line.</summary>
        public static void Run()
        {
            Directory.CreateDirectory(OutputFolder);
            ClearPreviousShots();

            // Static state survives the play-mode transition so this runs in one batch invocation; Finish() restores the project's play-mode settings.
            s_savedFastEnter = EditorSettings.enterPlayModeOptionsEnabled;
            s_savedEnterOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

            BuildScene();

            s_frame = 0;
            s_shot = 0;
            s_pendingShot = null;
            s_settleFrames = 0;
            s_signedIn = false;
            s_signInFrames = 0;
            s_aborted = false;
            Missed.Clear();
            s_running = true;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        // Last run's pictures. Without this a shot this run fails to take hides
        // behind the old file of the same name, and the folder reads as complete
        // when it is not. Only the run's own PNGs: the icon contact sheets live
        // in subfolders and are not ours to throw away.
        static void ClearPreviousShots()
        {
            foreach (var file in Directory.GetFiles(OutputFolder, "*.png", SearchOption.TopDirectoryOnly))
            {
                try { File.Delete(file); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"RetroOS smoke run: could not clear '{file}', so what " +
                                     $"is in it is left over from an earlier run. {e.Message}");
                }
            }
        }

        static void BuildScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The world says where this game's files are; the machine says
            // which folder of drives is this computer's, and who may sign in.
            new GameObject("Retro OS World").AddComponent<RetroWorld>();

            var osGo = new GameObject("RetroOS");
            var machine = osGo.AddComponent<RetroMachine>();
            var machineSo = new SerializedObject(machine);
            machineSo.FindProperty("machineName").stringValue =
                RetroSampleContent.Workstation.ToUpperInvariant();
            machineSo.FindProperty("folderName").stringValue = RetroSampleContent.Workstation;

            // One account, so the run goes through the logon dialog the way a
            // player does rather than routing around it.
            var users = machineSo.FindProperty("users");
            users.arraySize = 1;
            var user = users.GetArrayElementAtIndex(0);
            user.FindPropertyRelative("username").stringValue = SmokeUser;
            user.FindPropertyRelative("password").stringValue = SmokePassword;
            user.FindPropertyRelative("displayName").stringValue = "OPERATOR";
            user.FindPropertyRelative("avatar").objectReferenceValue = null;
            machineSo.ApplyModifiedPropertiesWithoutUndo();

            var bootstrap = osGo.AddComponent<RetroOsBootstrap>();

            // Unfocused, the OS draws into its projector texture - what a headless run reads. Focused it becomes an overlay and renders nowhere.
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("startFocused").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // No Renderer on this object, so no glass, so the OS renders at the size asked here, not a display's.
            var monitor = osGo.GetComponent<RetroMonitor>();
            var monitorSo = new SerializedObject(monitor);
            // Every frame, so a shot is never of a stale picture.
            monitorSo.FindProperty("unfocusedFps").floatValue = 0f;
            monitorSo.FindProperty("dynamicResolution").boolValue = false;
            monitorSo.FindProperty("resolution").vector2Value = new Vector2(Width, Height);
            monitorSo.ApplyModifiedPropertiesWithoutUndo();

            InstallPrograms(osGo.transform);
        }

        // The programs this run opens - components the scene must carry. An empty scene means twenty screenshots of an empty desktop.
        static void InstallPrograms(Transform computer)
        {
            var host = new GameObject("Programs").transform;
            host.SetParent(computer, false);

            var types = new[]
            {
                typeof(RetroFileExplorerApp),
                typeof(RetroBrowserApp),
                typeof(RetroMailApp),
                typeof(RetroNotepadApp),
                typeof(RetroCalculatorApp),
                typeof(RetroChatApp),
                typeof(RetroPhotoGalleryApp),
                typeof(RetroMinesApp),
                typeof(RetroSolitaireApp),
                typeof(RetroControlPanelApp),
                typeof(RetroCdPlayerApp),
                typeof(RetroTerminalApp),
                typeof(RetroImageViewerApp),
                typeof(RetroMediaPlayerApp),
            };

            var order = 0;
            foreach (var type in types)
            {
                var go = new GameObject(type.Name);
                go.transform.SetParent(host, false);

                var app = (RetroAppBehaviour)go.AddComponent(type);
                app.ApplyDefaults();
                app.Settings.sortOrder = ++order;

                // The browser's load delay is a race in a screenshot run (this counts editor updates, which lag when busy), so it is zeroed here.
                if (app is RetroBrowserApp browser) Instant(browser);
            }
        }

        // Takes the load delay off the browser for the duration of the run.
        static void Instant(RetroBrowserApp browser)
        {
            var serialized = new SerializedObject(browser);
            var delay = serialized.FindProperty("loadSeconds");
            if (delay == null)
            {
                Debug.LogWarning("RetroOS smoke run: the browser has no loadSeconds field any " +
                                 "more, so its pages may still be arriving when they are " +
                                 "photographed.");
                return;
            }

            delay.floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Tick()
        {
            if (!s_running) return;
            if (!EditorApplication.isPlaying) return;

            // The editor throttles a background window; without runInBackground the OS stops redrawing and every later shot repeats the last.
            Application.runInBackground = true;

            // Sign-in comes first; the schedule waits for it, so a slow logon does not shift the whole run one dialog left.
            if (!s_signedIn)
            {
                s_signedIn = SignIn();
                if (!s_signedIn)
                {
                    if (++s_signInFrames < SignInDeadline) return;

                    Debug.LogError(
                        "RetroOS smoke run: could not get past the logon screen in "
                            + $"{SignInDeadline} frames; the run would only photograph it.");
                    s_aborted = true;
                    Finish();
                    return;
                }
            }

            // A shot waiting on a transition is taken the moment it ends.
            TrySettledCapture();

            // Until it has been, the schedule stands still. The next step would
            // change the screen out from under a shot that has not been taken yet
            // - the un-maximize at 600 arriving before "16-maximized" was written,
            // say - and the run would either lose the picture or keep a picture of
            // the wrong thing under the right name. Waiting only ever adds frames
            // between steps, never removes them, and SettleDeadline bounds it, so
            // this cannot stall.
            if (s_pendingShot != null) return;

            s_frame++;

            // Give the OS time to boot and lay out before the first shot.
            switch (s_frame)
            {
                case 40: Capture("01-desktop"); break;

                case 45: LaunchApp("files"); break;
                case 70: Capture("02-explorer"); break;
                case 75: CloseTop(); break;

                case 85: LaunchApp("web"); break;
                case 130: Capture("03-browser"); break;
                case 135: CloseTop(); break;

                case 145: LaunchApp("mail"); break;
                case 170: Capture("04-inbox"); break;
                case 175: CloseTop(); break;

                case 185: LaunchApp("notepad"); break;
                case 205: Capture("05-notepad"); break;
                case 210: CloseTop(); break;

                case 220: LaunchApp("calculator"); break;
                case 240: Capture("06-calculator"); break;
                case 245: CloseTop(); break;

                case 255: LaunchApp("chat"); break;
                case 275: Capture("07-messenger"); break;
                case 280: CloseTop(); break;

                case 290: LaunchApp("gallery"); break;
                case 310: Capture("08-gallery"); break;
                case 315: CloseTop(); break;

                case 325: LaunchApp("mines"); break;
                case 345: Capture("09-mines"); break;
                case 350: CloseTop(); break;

                case 360: LaunchApp("solitaire"); break;
                case 380: Capture("10-solitaire"); break;
                case 385: CloseTop(); break;

                case 395: LaunchApp("settings"); break;
                case 420: Capture("11-control-panel"); break;

                // The riskiest drawing code - repainting interface already up. Two windows open, so the shot shows whether both followed.
                case 425: LaunchApp("files"); break;
                case 440: ApplyScheme("Plum"); break;
                case 455: Capture("12-scheme-plum"); break;
                case 460: ApplyScheme("High Contrast Black"); break;
                case 475: Capture("13-scheme-high-contrast"); break;
                case 480: ApplyScheme(null); break;
                case 495: Capture("14-scheme-back-to-default"); break;
                case 500: CloseTop(); break;
                case 505: CloseTop(); break;

                // What only a running window proves: the panel host, the maximize/minimize transitions, a folded side pane behind its mask.
                case 515: LaunchApp("cdplayer"); break;
                case 535: Capture("15-cd-player"); break;
                case 540: CloseTop(); break;

                case 550: LaunchApp("web"); break;
                case 580: MaximizeTop(); break;
                case 595: Capture("16-maximized"); break;
                case 600: MaximizeTop(); break;
                case 615: Capture("17-restored-from-maximized"); break;
                case 620: MinimizeTop(); break;
                case 635: Capture("18-minimized-to-taskbar"); break;
                case 640: RestoreFirst(); break;
                case 655: Capture("19-restored-from-taskbar"); break;

                case 665: LaunchApp("mail"); break;
                case 690: FoldPane(); break;
                case 705: Capture("20-inbox-pane-folded"); break;
                case 710: FoldPane(); break;
                case 725: Capture("21-inbox-pane-open"); break;
                case 730: CloseTop(); break;
                case 735: CloseTop(); break;

                // The sample web: tables with a pinned sidebar column, a live scroll bar, a site painting its own dark-ground link colours, a followed (purple) link.
                case 745: LaunchApp("web"); break;
                case 775: Browse("riverbend-books.com"); break;
                case 800: Capture("22-site-bookshop"); break;
                case 805: ScrollTo(0.35f); break;
                case 815: Capture("23-site-scrolled"); break;

                case 825: Browse("hometown-pages.net/member.html"); break;
                case 850: Capture("24-site-dark-links"); break;

                case 860: Browse("kingfisher.com"); break;
                case 880: Browse("kingfisher.com/computers.html"); break;
                case 900: Browse("kingfisher.com"); break;
                case 925: Capture("25-site-visited-link"); break;
                case 930: CloseTop(); break;

                // The Control Panel's other pages. The first shot only ever saw
                // Appearance, which is how a row of test buttons sat in a heap in
                // the corner of the Sounds page through every earlier run.
                case 940: LaunchApp("settings"); break;
                case 965: SelectTab(1); break;
                case 975: Capture("26-control-panel-sounds"); break;
                case 980: SelectTab(2); break;
                case 990: Capture("27-control-panel-mouse"); break;

                case 1000: Finish(); break;
            }
        }

        // Closes the front window, so the next shot is of one program rather
        // than a pile of them.
        static void CloseTop()
        {
            var window = RetroOs.Current?.Windows?.Active;
            if (window) window.Close();
        }

        // The page of a tabbed window - a Control Panel page is only drawn once
        // something has asked for it.
        static void SelectTab(int index)
        {
            var window = RetroOs.Current?.Windows?.Active;
            var tabs = window ? window.GetComponentInChildren<UI.Components.RetroTabControl>(true) : null;

            if (!tabs)
            {
                Debug.LogWarning($"RetroOS smoke run: the front window has no tabs, so page " +
                                 $"{index} was not photographed.");
                return;
            }

            tabs.Select(index);
        }

        static void MaximizeTop()
        {
            var window = RetroOs.Current?.Windows?.Active;
            if (window) window.ToggleMaximize();
        }

        static void MinimizeTop()
        {
            var window = RetroOs.Current?.Windows?.Active;
            if (window) window.Minimize();
        }

        // A minimized window is no longer the active one, so bringing it back
        // means asking the manager for it the way the taskbar button does.
        static void RestoreFirst()
        {
            var windows = RetroOs.Current?.Windows;
            if (windows == null) return;

            foreach (var window in windows.Windows)
                if (window && window.Minimized) { window.Restore(); return; }
        }

        // Folds whichever side pane the front window has, if it has one.
        static void FoldPane()
        {
            var window = RetroOs.Current?.Windows?.Active;
            if (!window || !window.Content) return;

            var drawer = window.Content.GetComponentInChildren<UI.Components.RetroNavDrawer>(true);
            if (drawer) drawer.Toggle();
        }

        // Sends the front browser somewhere. Navigate is what a link click
        // calls, so this is the same path the player takes.
        static void Browse(string url)
        {
            var browser = Object.FindFirstObjectByType<Apps.WebBrowserView>();
            if (browser) browser.Navigate(url);
            else Debug.LogWarning("RetroOS smoke run: no browser window open for " + url);
        }

        // Scrolls the front window's first scroll area, so a shot can show the
        // bar with a thumb somewhere other than the top.
        static void ScrollTo(float fromTop)
        {
            var window = RetroOs.Current?.Windows?.Active;
            if (!window || !window.Content) return;

            var scroll = window.Content.GetComponentInChildren<ScrollRect>(true);
            if (scroll) scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - fromTop);
        }

        static void ApplyScheme(string name)
        {
            var os = RetroOs.Current;
            if (os == null) return;

            UI.RetroColorScheme scheme = null;
            if (!string.IsNullOrEmpty(name))
                foreach (var candidate in UI.RetroColorScheme.All)
                    if (candidate.Name == name) { scheme = candidate; break; }

            // Through the machine's own skin - the only thing that knows which screen is repainting; the old direct palette call did nothing useful.
            os.Skin?.Repaint(scheme);
            Debug.Log("RetroOS smoke run: scheme -> " + (name ?? "theme default"));
        }

        // Drives the logon dialog like a player, proving the login flow. Retried every frame until it
        // takes (the OS builds the logon screen a few frames in). Returns true when nothing is left to do.
        static bool SignIn()
        {
            // Read the accounts back off the running machine: a static
            // reference does not reliably survive the play-mode transition.
            var bootstrap = Object.FindFirstObjectByType<RetroOsBootstrap>();
            var machine = bootstrap ? bootstrap.Machine : null;
            if (!machine)
                return false;

            if (machine.Users.Count == 0)
            {
                Debug.Log("RetroOS smoke run: no users configured, already on the desktop.");
                return true;
            }

            var login = GameObject.Find("Login");
            if (!login)
            {
                // No dialog and no OS yet is a machine still coming up; no
                // dialog once it is running means it never asked.
                if (RetroOs.Current == null)
                    return false;

                Debug.Log("RetroOS smoke run: no logon screen, already on the desktop.");
                return true;
            }

            var fields = login.GetComponentsInChildren<InputField>(true);
            if (fields.Length < 2)
                return false;

            var user = machine.Users[0];
            fields[0].text = user.username;
            fields[1].text = user.password;

            // The OK button, not every button on the dialog: the password row
            // carries a reveal toggle, and firing the lot pressed that too.
            var ok = System.Array.Find(
                login.GetComponentsInChildren<Button>(true),
                b => b.GetComponentInChildren<Text>(true) is { } label
                     && label.text.Trim().Equals("OK", System.StringComparison.OrdinalIgnoreCase));

            if (ok)
                ok.onClick.Invoke();
            else
                foreach (var button in login.GetComponentsInChildren<Button>(true))
                    button.onClick.Invoke();

            // Not "did it work": Unity destroys at end of frame, so the screen is still findable here. The caller asks again next frame.
            return false;
        }

        static void LaunchApp(string appId)
        {
            var os = RetroOs.Current;
            if (os == null) { Debug.LogWarning("RetroOS smoke run: OS is not running."); return; }
            if (os.Apps.Find(appId) == null)
            {
                Debug.LogWarning($"RetroOS smoke run: no app '{appId}' installed, skipping.");
                return;
            }
            os.Apps.Launch(appId);
        }

        // A shot asked for, and how many frames it has been waiting for the
        // screen to stop moving.
        static string s_pendingShot;
        static int s_settleFrames;

        // Long enough for any transition in the package several times over.
        const int SettleDeadline = 120;

        /// <summary>Asks for a screenshot, taken as soon as nothing is animating.</summary>
        static void Capture(string name)
        {
            // The schedule holds while a shot is pending, so this cannot happen
            // from Tick. It is worth saying loudly if it ever does: displacing an
            // unwritten shot is exactly the silent loss this run used to have.
            if (s_pendingShot != null)
            {
                Debug.LogError($"RetroOS smoke run: '{name}' was asked for while " +
                               $"'{s_pendingShot}' was still waiting to be taken, so that " +
                               "one is lost. Something is driving Capture from outside " +
                               "the schedule.");
                Missed.Add(s_pendingShot);
            }

            s_pendingShot = name;
            s_settleFrames = 0;
            TrySettledCapture();
        }

        // Drained every frame until the screen is still.
        static void TrySettledCapture()
        {
            if (s_pendingShot == null) return;

            // Popups as well as windows: a tab page cross-fades, and the page on
            // its way out is drawn over the one coming in for the whole fade -
            // which is a photograph of two pages at once, not of either.
            var moving =
                Object.FindObjectsByType<Windowing.WindowTransition>(FindObjectsSortMode.None)
                    .Any(t => t && t.Playing.HasValue) ||
                Object.FindObjectsByType<UI.RetroPopup>(FindObjectsSortMode.None)
                    .Any(p => p && p.Animating);

            if (moving && ++s_settleFrames < SettleDeadline) return;

            if (moving)
                Debug.LogWarning($"RetroOS smoke run: '{s_pendingShot}' was taken with a window " +
                                 $"still moving after {SettleDeadline} frames. A transition that " +
                                 "does not finish is worth looking at.");

            var name = s_pendingShot;
            s_pendingShot = null;
            Write(name);
        }

        static void Write(string name)
        {
            var host = Object.FindFirstObjectByType<RetroOsBootstrap>()?.ScreenHost;
            var target = host ? host.Texture : null;
            if (!target)
            {
                Debug.LogError($"RetroOS smoke run: the screen has not been built, so " +
                               $"'{name}' could not be taken.");
                Missed.Add(name);
                return;
            }

            // The projector already drew this frame - just read the texture. A manual Camera.Render() is unsupported under an SRP.
            var previous = RenderTexture.active;
            RenderTexture.active = target;

            var shot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;

            var path = Path.Combine(OutputFolder, name + ".png");
            try
            {
                File.WriteAllBytes(path, shot.EncodeToPNG());
            }
            catch (System.Exception e)
            {
                // A disk that will not take the picture must not throw out of the
                // editor update loop and leave the run wedged in play mode.
                Debug.LogError($"RetroOS smoke run: could not write '{path}'. {e.Message}");
                Missed.Add(name);
                Object.DestroyImmediate(shot);
                return;
            }

            Object.DestroyImmediate(shot);

            s_shot++;
            Debug.Log("RetroOS smoke run: wrote " + path);
        }

        static void Finish()
        {
            s_running = false;
            EditorApplication.update -= Tick;

            EditorSettings.enterPlayModeOptionsEnabled = s_savedFastEnter;
            EditorSettings.enterPlayModeOptions = s_savedEnterOptions;

            // A shot still waiting when the run ends is one nobody will ever look
            // for, because no file appears under its name.
            if (s_pendingShot != null)
            {
                Missed.Add(s_pendingShot);
                s_pendingShot = null;
            }

            Debug.Log($"RetroOS smoke run: finished, {s_shot} screenshot(s) written to " +
                      Path.GetFullPath(OutputFolder));

            if (Missed.Count > 0)
                Debug.LogError($"RetroOS smoke run: {Missed.Count} shot(s) asked for and never " +
                               $"written: {string.Join(", ", Missed)}. The folder is short of " +
                               "those pictures, not showing older ones in their place.");

            var failed = s_aborted || Missed.Count > 0;
            EditorApplication.ExitPlaymode();

            // Quitting belongs to the CI invocation, which needs an exit code. From the menu it would close the project you are checking.
            if (Application.isBatchMode)
                EditorApplication.delayCall += () => EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}
