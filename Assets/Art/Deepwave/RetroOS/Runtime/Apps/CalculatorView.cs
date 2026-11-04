using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Audio;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps
{
    // Calculator, standard view: the keypad and readout over CalculatorEngine's arithmetic.
    public class CalculatorView : AppView
    {
        readonly CalculatorEngine _engine = new();

        Text _display;
        Text _memoryFlag;
        RetroStatusStrip _status;

        protected override void Build()
        {
            RetroUi.VLayout(Root.gameObject, 4, 4);
            BuildMenu();
            BuildDisplay();
            BuildKeys();

            _status = RetroUi.StatusStrip(Root, -1f, 60f);
            Show();
        }

        void BuildMenu()
        {
            var menu = RetroUi.Menu(Root);

            menu.AddMenu("Edit")
                .Command("Copy", CopyToClipboard, "Ctrl+C")
                .Command("Paste", PasteFromClipboard, "Ctrl+V");

            menu.AddMenu("View")
                .Check("Standard", true, null)
                .Command("Scientific", null, null, null, false);

            menu.AddMenu("Help").Command("About", () => RetroDialog.Info(Os.Overlay, "Calculator",
                "Standard calculator.\n\nOperators finish the sum in progress, " +
                "the way a desk calculator does: 2 + 3 * 4 is 20."));
        }

        void BuildDisplay()
        {
            var box = RetroUi.Panel(Root, PaletteSlot.Field, "Display");
            RetroUi.Size(box, h: 40f);
            RetroUi.Bevel(box.rectTransform, false);

            _display = RetroUi.NoWrap(
                RetroUi.Label(box.transform, "0", 24, TextAnchor.MiddleRight, RetroSkin.Mono));
            RetroUi.Stretch(_display.rectTransform, 8f);

            var row = RetroUi.NewRect("Memory", Root);
            RetroUi.Size(row, h: 22f);
            RetroUi.HLayout(row.gameObject, 4, 0);

            var flagBox = RetroUi.Panel(row, PaletteSlot.Face, "MemFlag");
            RetroUi.Size(flagBox, w: 34f);
            RetroUi.Bevel(flagBox.rectTransform, false, 1f);
            _memoryFlag = RetroUi.Label(flagBox.transform, "", 12, TextAnchor.MiddleCenter);
            RetroUi.Stretch(_memoryFlag.rectTransform);

            Key(row, "Backspace", _engine.Backspace, 74f);
            Key(row, "CE", _engine.ClearEntry, 44f);
            Key(row, "C", _engine.Clear, 44f);
            RetroUi.Spacer(row);
        }

        void BuildKeys()
        {
            // Memory column on the left, digits in the middle, operators on the
            // right - the layout every calculator of the era used.
            var grid = RetroUi.NewRect("Keys", Root);
            RetroUi.Flexible(grid);

            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 6;
            layout.spacing = new Vector2(3f, 3f);
            layout.cellSize = new Vector2(44f, 30f);
            layout.childAlignment = TextAnchor.UpperCenter;

            Key(grid, "MC", _engine.MemoryClear);
            Key(grid, "7", () => _engine.Digit('7'));
            Key(grid, "8", () => _engine.Digit('8'));
            Key(grid, "9", () => _engine.Digit('9'));
            Key(grid, "/", () => _engine.Operator('/'));
            Key(grid, "sqrt", _engine.SquareRoot);

            Key(grid, "MR", _engine.MemoryRecall);
            Key(grid, "4", () => _engine.Digit('4'));
            Key(grid, "5", () => _engine.Digit('5'));
            Key(grid, "6", () => _engine.Digit('6'));
            Key(grid, "*", () => _engine.Operator('*'));
            Key(grid, "%", _engine.Percent);

            Key(grid, "MS", _engine.MemoryStore);
            Key(grid, "1", () => _engine.Digit('1'));
            Key(grid, "2", () => _engine.Digit('2'));
            Key(grid, "3", () => _engine.Digit('3'));
            Key(grid, "-", () => _engine.Operator('-'));
            Key(grid, "1/x", _engine.Reciprocal);

            Key(grid, "M+", _engine.MemoryAdd);
            Key(grid, "0", () => _engine.Digit('0'));
            Key(grid, "+/-", _engine.Negate);
            Key(grid, ".", _engine.Point);
            Key(grid, "+", () => _engine.Operator('+'));
            Key(grid, "=", Equals);
        }

        // Every key runs its engine call and then repaints, so the display can
        // never disagree with the sum behind it.
        RetroPushButton Key(Transform parent, string caption, Action action, float width = -1f)
        {
            var button = RetroUi.PushButton(parent, caption, () =>
            {
                action?.Invoke();
                Show();
            });
            if (button.Label) button.Label.fontSize = 13;
            if (width > 0f) RetroUi.Size(button, w: width);
            return button;
        }

        void Equals()
        {
            _engine.Equals();
            if (_engine.Error == null) Raise("result", _engine.Display);
        }

        void CopyToClipboard()
        {
            GUIUtility.systemCopyBuffer = _engine.Display;
            _status?.Set(0, "Copied " + _engine.Display);
        }

        void PasteFromClipboard()
        {
            var text = GUIUtility.systemCopyBuffer;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                _status?.Set(0, "Nothing numeric on the clipboard");
                return;
            }
            _engine.SetValue(value);
            Show();
        }

        void Show()
        {
            var error = _engine.Error;
            _display.text = error ?? _engine.Display;
            _memoryFlag.text = Math.Abs(_engine.Memory) > double.Epsilon ? "M" : "";

            if (_status == null) return;
            _status.Set(0, error ?? "");
            _status.Set(1, _engine.Pending == '\0' ? "" : _engine.Pending.ToString());

            if (error != null && error != _shownError) RetroSfx.Error();
            _shownError = error;
        }

        string _shownError;
    }
}
