using System;
using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Desktop;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    // Appearance, volumes, the network and where the files live.
    [AddComponentMenu("")]
    public class RetroControlPanelApp : RetroAppBehaviour, IRemembersSettings
    {
        public override void BuildContent(AppContext ctx) => AppView.Attach<SettingsView>(ctx);

        /// <summary>
        /// Puts back what the player last chose here (scheme, clock format, widget
        /// sounds, wallpaper). Belongs to whoever wrote the settings - here, not the boot path.
        /// </summary>
        public void Restore(RetroOsContext os, SettingsMoment moment)
        {
            var prefs = os?.Prefs;
            if (prefs is not { Ready: true }) return;

            if (moment == SettingsMoment.Booting) RestoreLook(os, prefs);
            else RestoreWallpaper(os, prefs);
        }

        // Before the first window: a scheme applied any later repaints the
        // screen under the player a moment after they have seen it.
        static void RestoreLook(RetroOsContext os, Core.Storage.MachinePrefs prefs)
        {
            var scheme = prefs.Get(SettingsView.SchemeKey);
            if (!string.IsNullOrEmpty(scheme))
                foreach (var candidate in RetroColorScheme.All)
                    if (string.Equals(candidate.Name, scheme, StringComparison.OrdinalIgnoreCase))
                    {
                        // Onto this machine's skin - writing the shared palette here repainted the machine at the next desk.
                        os.Skin?.Restore(candidate);
                        break;
                    }

            if (prefs.Has(SettingsView.ClockKey))
                os.Clock.Use24Hour = prefs.GetBool(SettingsView.ClockKey, true);

            // Onto the skin, not the shared palette: this is one machine's setting.
            if (prefs.Has(SettingsView.SoundsKey))
                os.Skin?.SetSounds(prefs.GetBool(SettingsView.SoundsKey, true));

            if (os.Audio)
            {
                if (prefs.Has(SettingsView.MasterVolumeKey))
                    os.Audio.MasterVolume = prefs.GetFloat(SettingsView.MasterVolumeKey, 1f);
                if (prefs.Has(SettingsView.UiVolumeKey))
                    os.Audio.UiVolume = prefs.GetFloat(SettingsView.UiVolumeKey, 1f);
                if (prefs.Has(SettingsView.MusicVolumeKey))
                    os.Audio.MusicVolume = prefs.GetFloat(SettingsView.MusicVolumeKey, 1f);
            }

            // The drawn pointer belongs to this machine's screen, so its speed
            // travels with the machine and not with the process.
            if (os.Pointer)
            {
                if (prefs.Has(SettingsView.PointerSpeedKey))
                    os.Pointer.CursorSpeed = prefs.GetFloat(SettingsView.PointerSpeedKey, 1f);
                if (prefs.Has(SettingsView.StickSpeedKey))
                    os.Pointer.StickSpeed = prefs.GetFloat(SettingsView.StickSpeedKey, 1600f);
            }

            if (os.Screen && prefs.Has(SettingsView.PixelPerfectKey))
                os.Screen.PixelPerfect = prefs.GetBool(SettingsView.PixelPerfectKey, true);

            // Not the double-click window: one static counts for the whole scene,
            // so the machine being clicked sets it as the player turns to it
            // (RetroOsBootstrap.SetFocused), not whichever booted last.
        }

        // Once there is a desktop. The remembered value is the path, read back through the
        // file system, so another account's picture never comes up behind this desktop.
        static void RestoreWallpaper(RetroOsContext os, Core.Storage.MachinePrefs prefs)
        {
            var path = prefs.Get(SettingsView.WallpaperKey);
            if (string.IsNullOrWhiteSpace(path)) return;

            if (os.Fs.Resolve(path.Trim()) is not VfsFile file) return;
            if (!file.TryLoad(os.CurrentUser, out var asset)) return;

            if (asset is Sprite sprite) DesktopScreen.SetWallpaper(os, sprite);
        }

        public override void ApplyDefaults()
        {
            app.appId = "settings";
            app.title = "Control Panel";
            app.builtinIcon = BuiltinIcon.Settings;
            // Seven tabs across the top want the room.
            app.windowSize = new Vector2(660, 470);
            app.minWindowSize = new Vector2(460, 340);
        }
    }
}
