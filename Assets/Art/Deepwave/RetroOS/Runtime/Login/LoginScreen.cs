using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Audio;
using Deepwave.RetroOS.Core.Auth;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Login
{
    // Win95-style logon dialog shown before the desktop.
    public static class LoginScreen
    {
        public static void Show(RetroOsContext os, Action onSuccess)
        {
            var screen = RetroUi.Panel(os.Overlay, PaletteSlot.Desktop, "Login");
            RetroUi.Stretch(screen.rectTransform);

            var box = RetroUi.Panel(screen.transform, PaletteSlot.Face, "LogonBox");
            RetroUi.Bevel(box.rectTransform, true);
            var rt = box.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 210);

            var bar = RetroUi.Panel(box.transform, PaletteSlot.TitleActive, "TitleBar");
            var barRt = bar.rectTransform;
            barRt.anchorMin = new Vector2(0, 1);
            barRt.anchorMax = new Vector2(1, 1);
            barRt.pivot = new Vector2(0.5f, 1);
            barRt.sizeDelta = new Vector2(-6, 18);
            barRt.anchoredPosition = new Vector2(0, -3);
            RetroPalette.PaintGradient(bar, PaletteSlot.TitleActive, PaletteSlot.TitleActiveEnd);

            var title = RetroUi.Label(bar.transform, "Log On to RetroOS", 13, TextAnchor.MiddleLeft);
            RetroPalette.Paint(title, PaletteSlot.TitleText);
            title.fontStyle = FontStyle.Bold;
            RetroUi.Stretch(title.rectTransform, 4);

            var row = RetroUi.NewRect("Row", box.transform);
            RetroUi.Stretch(row);
            row.offsetMin = new Vector2(12, 10);
            row.offsetMax = new Vector2(-12, -26);
            RetroUi.HLayout(row.gameObject, 10, 0);

            // The account's picture, if any. It updates as the name is typed - the only cue that the machine knows who you say you are.
            var portrait = RetroUi.Panel(row, PaletteSlot.Face, "Portrait");
            RetroUi.Size(portrait, w: 60f);
            RetroUi.Bevel(portrait.rectTransform, false, 1f);

            // Most accounts have no picture, so the key stands in - 95 always had a graphic in this well.
            var stamp = RetroUi.Icon(portrait.transform, RetroIcons.Get(BuiltinIcon.Key), 32f);

            var face = RetroUi.Picture(portrait.transform, null);
            RetroUi.Stretch(face.rectTransform, 3f);
            face.enabled = false;

            var body = RetroUi.NewRect("Body", row);
            RetroUi.Flexible(body);
            RetroUi.VLayout(body.gameObject, 8, 0);

            var banner = os.Login?.banner;
            RetroUi.Size(RetroUi.Label(body, string.IsNullOrWhiteSpace(banner)
                ? "Type a user name and password to log on to RetroOS." : banner, 13), h: 34);

            var user = FieldRow(body, "User name:");
            var pass = FieldRow(body, "Password:", password: true);

            var message = RetroUi.Label(body, "", 12, TextAnchor.MiddleLeft);
            RetroPalette.Paint(message, PaletteSlot.Alert);
            RetroUi.Size(message, h: 16);

            void ShowPortrait(string username)
            {
                var entry = os.Accounts.FirstOrDefault(u =>
                    u != null && string.Equals(u.username, username?.Trim(),
                        StringComparison.OrdinalIgnoreCase));

                face.sprite = entry?.avatar;
                face.enabled = entry?.avatar;
                stamp.enabled = !face.enabled;
            }

            user.onValueChanged.AddListener(ShowPortrait);

            void Attempt()
            {
                var result = os.Auth.Login(user.text, pass.text);
                if (result == LoginResult.Success)
                {
                    os.Audio.PlayUi(UiSfx.Login);

                    // The machine remembers who used it, the way one that only
                    // ever has one user should.
                    os.Prefs.Set(LastUserKey, os.Auth.Current.Username);
                    os.Prefs.Save();

                    os.Events.Raise(OsEvents.Login, os.Auth.Current.Username);
                    UnityEngine.Object.Destroy(screen.gameObject);
                    onSuccess?.Invoke();
                }
                else
                {
                    os.Audio.PlayUi(UiSfx.Error);
                    message.text = result == LoginResult.UnknownUser
                        ? "The user name could not be found."
                        : "The password is incorrect.";
                }
            }

            var buttons = RetroUi.NewRect("Buttons", body);
            RetroUi.Size(buttons, h: 28);
            RetroUi.HLayout(buttons.gameObject, 8, 0);
            RetroUi.Spacer(buttons);
            RetroUi.Size(RetroUi.Button(buttons, "OK", Attempt), w: 80);

            // Enter in the password field logs on. Not onEndEdit - that also fires on focus loss.
            pass.onSubmit.AddListener(_ => Attempt());

            // Whoever used the machine last is already typed in.
            var last = os.Prefs.Get(LastUserKey);
            if (!string.IsNullOrEmpty(last))
            {
                user.text = last;
                ShowPortrait(last);
            }
        }

        /// <summary>Where the last account to sign in is remembered.</summary>
        public const string LastUserKey = "login.lastUser";

        static InputField FieldRow(RectTransform body, string caption, bool password = false)
        {
            var row = RetroUi.NewRect("Row", body);
            RetroUi.Size(row, h: 26);
            RetroUi.HLayout(row.gameObject, 6, 0);
            RetroUi.Size(RetroUi.NoWrap(RetroUi.Label(row, caption, 13, TextAnchor.MiddleLeft)), w: 84);
            var input = RetroUi.Input(row, "", password);
            RetroUi.Flexible(input);

            // The eye: read back what you typed. An unreadable password box earns you a wrong-password box.
            if (password) AddReveal(row, input);
            return input;
        }

        static void AddReveal(Transform row, InputField input)
        {
            var shown = false;
            var button = RetroUi.Button(row, "", null, RetroIcons.Get(BuiltinIcon.Eye));
            RetroUi.Size(button, w: 26f);

            button.onClick.AddListener(() =>
            {
                shown = !shown;
                input.contentType = shown
                    ? InputField.ContentType.Standard
                    : InputField.ContentType.Password;

                // The field only re-renders its masking on the next edit, so
                // it has to be told.
                input.ForceLabelUpdate();
            });

            RetroUi.ToolTip(button, "Show the password");
        }
    }
}
