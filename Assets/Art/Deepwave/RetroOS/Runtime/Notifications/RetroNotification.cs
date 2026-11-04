using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Notifications
{
    /// <summary>The balloons that rise out of the tray: new mail, a reminder going off, a disc going in.</summary>
    public class RetroNotification : MonoBehaviour
    {
        const float Width = 260f;
        const float Height = 62f;
        const float Gap = 4f;
        const int MaxOnScreen = 4;

        RetroPopup _popup;
        Coroutine _timer;
        Action _onClick;
        Transform _layer;

        // Dismissed but still animating out; no longer counted, so the rest close the gap now.
        bool _leaving;

        /// <summary>The balloons up on one machine, oldest first.</summary>
        public static List<RetroNotification> On(Transform layer)
        {
            var showing = new List<RetroNotification>();
            if (!layer) return showing;

            // Sibling order is the order they were added, which is the order
            // they are stacked in. The scene keeps it; nothing else has to.
            foreach (Transform child in layer)
            {
                var notice = child.GetComponent<RetroNotification>();
                if (notice && !notice._leaving) showing.Add(notice);
            }
            return showing;
        }

        /// <summary>Raises a balloon above the tray. Clicking it runs <paramref name="onClick"/> - usually opening whatever it is about.</summary>
        /// <param name="os">The running OS. Nothing is shown without one.</param>
        /// <param name="title">The bold first line.</param>
        /// <param name="body">The line under it. Kept to one line.</param>
        /// <param name="icon">Optional glyph on the left.</param>
        /// <param name="onClick">What clicking it does.</param>
        /// <param name="seconds">How long it stays up. 0 keeps it until clicked.</param>
        public static RetroNotification Show(RetroOsContext os, string title, string body,
            Sprite icon = null, Action onClick = null, float seconds = 6f)
        {
            var layer = os?.Overlay;
            if (!layer) return null;

            // Cap per machine: a beat that delivers five mails should not bury the desktop.
            var showing = On(layer);
            for (var i = 0; showing.Count - i >= MaxOnScreen && i < showing.Count; i++)
                showing[i].Dismiss();

            var panel = RetroUi.Panel(layer, PaletteSlot.Face, "Notification");
            RetroUi.Bevel(panel.rectTransform, true);

            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(Width, Height);

            var notice = panel.gameObject.AddComponent<RetroNotification>();
            notice._onClick = onClick;
            notice._layer = layer;
            notice.Build(panel.transform, title, body, icon);

            Restack(layer);

            notice._popup = RetroPopup.Play(panel.gameObject, PopupAnimation.Horizontal, 10f);
            os.Audio?.PlayUi(Audio.UiSfx.Notify);

            if (seconds > 0f) notice._timer = notice.StartCoroutine(notice.Expire(seconds));
            return notice;
        }

        void Build(Transform panel, string title, string body, Sprite icon)
        {
            var row = RetroUi.Stretch(RetroUi.NewRect("Row", panel), 6f);
            RetroUi.HLayout(row.gameObject, 6, 2);

            if (icon)
            {
                var glyph = RetroUi.Icon(row, icon, 32f);
                RetroUi.Size(glyph, w: 34f);
            }

            var column = RetroUi.NewRect("Text", row);
            RetroUi.Flexible(column);
            RetroUi.VLayout(column.gameObject, 1, 0);

            var heading = RetroUi.NoWrap(RetroUi.Label(column, title, 13, TextAnchor.LowerLeft));
            heading.fontStyle = FontStyle.Bold;
            RetroUi.Size(heading, h: 18f);

            var text = RetroUi.NoWrap(RetroUi.Label(column, body, 12, TextAnchor.UpperLeft));
            RetroPalette.Paint(text, PaletteSlot.TextDisabled);
            RetroUi.Flexible(text);

            // Anywhere on the balloon acts; the close box only dismisses it.
            var relay = panel.gameObject.AddComponent<RetroPointerRelay>();
            relay.Clicked = Activate;

            var close = RetroPushButton.Create(panel, "x", Dismiss);
            var crt = close.Rect;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 1f);
            crt.sizeDelta = new Vector2(16f, 14f);
            crt.anchoredPosition = new Vector2(-4f, -4f);
            if (close.Label) close.Label.fontSize = 10;
        }

        void Activate()
        {
            var action = _onClick;
            _onClick = null;
            Dismiss();
            action?.Invoke();
        }

        /// <summary>Takes the balloon down early.</summary>
        public void Dismiss()
        {
            if (_timer != null) { StopCoroutine(_timer); _timer = null; }

            // Marked leaving before the restack, so the departing balloon is not counted into the staying ones' positions.
            _leaving = true;
            Restack(_layer);

            if (_popup) _popup.Dismiss();
            else Destroy(gameObject);
        }

        IEnumerator Expire(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            _timer = null;
            Dismiss();
        }

        // Newest at the bottom, just above the taskbar, older ones stepping up.
        static void Restack(Transform layer)
        {
            var showing = On(layer);
            for (var i = 0; i < showing.Count; i++)
            {
                var rt = (RectTransform)showing[i].transform;
                var fromBottom = showing.Count - 1 - i;
                rt.anchoredPosition = new Vector2(
                    -8f, RetroSkin.TaskbarHeight + 8f + fromBottom * (Height + Gap));
            }
        }

        void OnDestroy()
        {
            if (_leaving) return;      // Dismiss already restacked.
            Restack(_layer);
        }
    }
}
