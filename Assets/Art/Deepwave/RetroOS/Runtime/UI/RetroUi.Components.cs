using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.UI
{
    // Component-backed half of the widget factory: these spawn real RetroWidget
    // components (restylable, disable-able, upgradable). Prefer these in new app code.
    public static partial class RetroUi
    {
        // Captions/menu titles/list cells never wrap - a Win95 control clips, not reflows.
        public static Text NoWrap(Text label)
        {
            if (!label) return null;
            // Vertical overflow too, so a line a pixel taller than its row is not hidden.
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        // Rough caption width at a font size, for sizing controls to their text.
        public static float TextWidth(string text, float fontSize = 13f) =>
            (text?.Length ?? 0) * fontSize * 0.62f;

        public static RetroPushButton PushButton(Transform parent, string caption, Action onClick,
            Sprite icon = null) => RetroPushButton.Create(parent, caption, onClick, icon);

        public static RetroPushButton ToggleButton(Transform parent, string caption, bool on,
            Action<bool> onToggled, Sprite icon = null)
        {
            var button = RetroPushButton.Create(parent, caption, null, icon);
            button.IsToggle = true;
            button.On = on;
            if (onToggled != null) button.Toggled += onToggled;
            return button;
        }

        public static RetroCheckBox CheckBox(Transform parent, string caption, bool value,
            Action<bool> onChanged) => RetroCheckBox.Create(parent, caption, value, onChanged);

        public static RetroTrackBar TrackBar(Transform parent, float value, Action<float> onChanged,
            int ticks = 0) => RetroTrackBar.Create(parent, value, onChanged, ticks);

        public static RetroProgressBar ProgressBar(Transform parent, float value = 0f) =>
            RetroProgressBar.Create(parent, value);

        public static RetroComboBox ComboBox(Transform parent, IEnumerable<string> items, int selected,
            Action<int> onChanged) => RetroComboBox.Create(parent, items, selected, onChanged);

        public static RetroTextBox TextBox(Transform parent, string text = "", bool password = false,
            bool multiline = false, Font font = null) =>
            RetroTextBox.Create(parent, text, password, multiline, font);

        public static RetroListView ListView(Transform parent, ListViewMode mode = ListViewMode.List) =>
            RetroListView.Create(parent, mode);

        public static RetroTreeView TreeView(Transform parent) => RetroTreeView.Create(parent);

        public static RetroTabControl Tabs(Transform parent) => RetroTabControl.Create(parent);

        public static RetroGroupBox GroupBox(Transform parent, string caption) =>
            RetroGroupBox.Create(parent, caption);

        public static RetroToolStrip ToolStrip(Transform parent) => RetroToolStrip.Create(parent);

        public static RetroStatusStrip StatusStrip(Transform parent, params float[] panes) =>
            RetroStatusStrip.Create(parent, panes);

        // A working menu bar; fill it with AddMenu("File"). The string overload
        // in RetroUi.cs is the decorative one.
        public static RetroMenuStrip Menu(Transform parent) => RetroMenuStrip.Create(parent);

        public static RetroToolTip ToolTip(Component target, string text) =>
            RetroToolTip.Attach(target.gameObject, text);

        public static RetroContextMenu ContextMenu(Component target, Func<RetroMenu> build) =>
            RetroContextMenu.Attach(target.gameObject, build);

        // Two panes with a draggable divider; the split is kept by flexible weights.
        public static (RectTransform first, RectTransform second) Split(Transform parent, bool vertical,
            float fraction = 0.4f)
        {
            var container = NewRect("Split", parent);
            if (vertical) VLayout(container.gameObject, 0, 0);
            else HLayout(container.gameObject, 0, 0);

            var first = NewRect("PaneA", container);
            var splitter = RetroSplitter.Create(container, container, vertical, fraction, null);
            var second = NewRect("PaneB", container);

            var a = Flexible(first);
            var b = Flexible(second);

            void Apply(float f)
            {
                if (vertical) { a.flexibleHeight = f; b.flexibleHeight = 1f - f; }
                else { a.flexibleWidth = f; b.flexibleWidth = 1f - f; }
            }

            Apply(fraction);
            splitter.Moved += Apply;
            return (first, second);
        }

        // Titled window body used by dialogs, wizards and settings pages.
        public static RectTransform Card(Transform parent, string caption)
        {
            var box = GroupBox(parent, caption);
            Flexible(box);
            return box.Body;
        }
    }
}
