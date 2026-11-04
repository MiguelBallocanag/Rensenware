using System;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Audio;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.UI
{
    // Centred modal windows - alerts, confirmations, prompts - with the era's
    // icon on the left and buttons on the right. A dialog is a window: it
    // animates through a RetroPopup, drags by its caption, and dismisses itself.
    public static class RetroDialog
    {
        public static void Prompt(Transform overlay, string title, string message,
            Action<string> onOk, bool password = false)
        {
            var (blocker, body) = Frame(overlay, title, 360, 180, BuiltinIcon.Question);

            // Flexible, not fixed: the field and buttons have their own heights.
            var prompt = RetroUi.Label(body, message, 13);
            RetroUi.Flexible(prompt);

            var field = RetroUi.NewRect("Field", body);
            RetroUi.Size(field, h: 26f);
            RetroUi.HLayout(field.gameObject, 4, 0);

            var input = RetroUi.TextBox(field, "", password);
            RetroUi.Flexible(input);

            // A password box you cannot read back earns you a wrong-password message.
            if (password) AddReveal(field, input);

            Buttons(body, blocker,
                ("OK", () => onOk?.Invoke(input.Value)),
                ("Cancel", null));

            // Enter confirms. onSubmit, not onEndEdit: the latter also fires on Cancel.
            input.Field.onSubmit.AddListener(value =>
            {
                onOk?.Invoke(value);
                Dismiss(blocker);
            });
            input.Focus();
        }

        public static void Alert(Transform overlay, string title, string message)
        {
            RetroSfx.Error();
            var (blocker, body) = Frame(overlay, title, 380, 170, BuiltinIcon.Warning);
            var label = RetroUi.Label(body, message, 13);
            RetroUi.Flexible(label);
            Buttons(body, blocker, ("OK", null));
        }

        public static void Info(Transform overlay, string title, string message)
        {
            var (blocker, body) = Frame(overlay, title, 380, 170, BuiltinIcon.Info);
            var label = RetroUi.Label(body, message, 13);
            RetroUi.Flexible(label);
            Buttons(body, blocker, ("OK", null));
        }

        // Yes/No question. The callback gets true only when the player agrees.
        public static void Confirm(Transform overlay, string title, string message,
            Action<bool> onAnswer)
        {
            var (blocker, body) = Frame(overlay, title, 380, 170, BuiltinIcon.Question);
            var label = RetroUi.Label(body, message, 13);
            RetroUi.Flexible(label);
            Buttons(body, blocker,
                ("Yes", () => onAnswer?.Invoke(true)),
                ("No", () => onAnswer?.Invoke(false)));
        }

        // A dialog with a body the caller fills itself (settings, wizards).
        public static (GameObject blocker, Transform body) Custom(Transform overlay, string title,
            float width, float height, BuiltinIcon icon = BuiltinIcon.Computer) =>
            Frame(overlay, title, width, height, icon);

        static (GameObject blocker, Transform body) Frame(Transform overlay, string title,
            float w, float h, BuiltinIcon icon)
        {
            var blocker = RetroUi.Panel(overlay, new Color(0, 0, 0, 0.35f), "Modal");
            RetroUi.Stretch(blocker.rectTransform);

            var dialog = RetroUi.Panel(blocker.transform, PaletteSlot.Face, "Dialog");
            RetroUi.Bevel(dialog.rectTransform, true);
            var rt = dialog.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);

            var bar = RetroUi.Panel(dialog.transform, PaletteSlot.TitleActive, "TitleBar");
            var barRt = bar.rectTransform;
            barRt.anchorMin = new Vector2(0, 1);
            barRt.anchorMax = new Vector2(1, 1);
            barRt.pivot = new Vector2(0.5f, 1);
            barRt.sizeDelta = new Vector2(-6, 22);
            barRt.anchoredPosition = new Vector2(0, -3);
            RetroPalette.PaintGradient(bar, PaletteSlot.TitleActive, PaletteSlot.TitleActiveEnd);

            // Clipped, not wrapped: a long caption would drop a line into the body.
            var caption = RetroUi.Clipped(bar.transform, 4);

            var titleLabel = RetroUi.NoWrap(
                RetroUi.Label(caption, title, 13, TextAnchor.MiddleLeft));
            RetroPalette.Paint(titleLabel, PaletteSlot.TitleText);
            titleLabel.fontStyle = FontStyle.Bold;
            RetroUi.Stretch(titleLabel.rectTransform);

            // Drag a dialog by its title bar, like every other window.
            var drag = bar.gameObject.AddComponent<Windowing.WindowDragger>();
            drag.dragObject = rt;
            drag.dragArea = blocker.rectTransform;

            var row = RetroUi.NewRect("Row", dialog.transform);
            RetroUi.Stretch(row);
            row.offsetMin = new Vector2(10, 8);
            row.offsetMax = new Vector2(-10, -28);
            RetroUi.HLayout(row.gameObject, 10, 2);

            // Own column, top-anchored by the spacer: the row controls child height.
            var glyphColumn = RetroUi.NewRect("Icon", row);
            RetroUi.Size(glyphColumn, w: 34f);
            RetroUi.VLayout(glyphColumn.gameObject, 0, 0);

            RetroUi.Size(RetroUi.Icon(glyphColumn, RetroIcons.Get(icon), 32), 32f, 32f);
            RetroUi.Spacer(glyphColumn);

            var body = RetroUi.NewRect("Body", row);
            RetroUi.Flexible(body);
            RetroUi.VLayout(body.gameObject, 6, 2);

#if UNITY_EDITOR
            var fit = dialog.gameObject.AddComponent<FitCheck>();
            fit.Body = body;
            fit.Title = title;
#endif

            // The whole modal fades in, so the dialog never appears before its dim sheet.
            var popup = blocker.gameObject.AddComponent<RetroPopup>();
            popup.mode = PopupAnimation.Fade;
            popup.speed = 14f;
            popup.deactivateOnHide = false;
            popup.PlayIn();

            // The dialog itself zooms, which is what reads as "a window opened".
            RetroPopup.Play(dialog.gameObject, PopupAnimation.Scale, 13f);

            return (blocker.gameObject, body);
        }

        // The eye beside a password field.
        static void AddReveal(Transform row, Components.RetroTextBox box)
        {
            var shown = false;
            var button = RetroUi.Button(row, "", null, RetroIcons.Get(BuiltinIcon.Eye));
            RetroUi.Size(button, w: 26f);

            button.onClick.AddListener(() =>
            {
                shown = !shown;
                box.Field.contentType = shown
                    ? UnityEngine.UI.InputField.ContentType.Standard
                    : UnityEngine.UI.InputField.ContentType.Password;

                // The field re-renders its mask only on the next edit, so tell it.
                box.Field.ForceLabelUpdate();
            });
        }

        /// <summary>Takes a dialog down the way it came up. Public so a Custom dialog can close itself.</summary>
        public static void Dismiss(GameObject blocker)
        {
            if (!blocker) return;
            var popup = blocker.GetComponent<RetroPopup>();
            if (popup) popup.Dismiss();
            else UnityEngine.Object.Destroy(blocker);
        }

        static void Buttons(Transform body, GameObject blocker, params (string text, Action action)[] items)
        {
            var row = RetroUi.NewRect("Buttons", body);
            RetroUi.Size(row, h: 26);
            RetroUi.HLayout(row.gameObject, 6, 0);
            RetroUi.Spacer(row);

            foreach (var (text, action) in items)
            {
                var button = RetroPushButton.Create(row, text, () =>
                {
                    action?.Invoke();
                    Dismiss(blocker);
                });
                RetroUi.Size(button, w: 76f);
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: warns when a dialog is shorter than the content built into it (<see cref="Custom"/> is sized before its body exists). Removes itself after checking.</summary>
        class FitCheck : MonoBehaviour
        {
            public RectTransform Body;
            public string Title;

            int _frames;

            void LateUpdate()
            {
                // The body is filled after Custom returns and two layout groups settle;
                // three frames is past that. Scale is uniform, so measuring in dialog space cancels it.
                if (++_frames < 3) return;

                var dialog = (RectTransform)transform;
                if (Body)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(dialog);

                    // Body vs frame: too much content pushes the body's bottom
                    // (and the button row on it) out through the bottom of the window.
                    var corners = new Vector3[4];
                    Body.GetWorldCorners(corners);
                    var over = dialog.rect.yMin - dialog.InverseTransformPoint(corners[0]).y;

                    if (over > 1f)
                        Debug.LogWarning(
                            $"RetroOS: the '{Title}' dialog is {over:0} pixels shorter than what "
                                + "was built into it, so its bottom row is drawn outside the "
                                + "frame. Give RetroDialog.Custom a taller height.",
                            this);
                }

                Destroy(this);
            }
        }
#endif
    }
}
