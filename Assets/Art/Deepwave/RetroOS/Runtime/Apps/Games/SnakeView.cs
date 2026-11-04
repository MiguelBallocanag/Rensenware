using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Deepwave.RetroOS.Audio;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps.Games
{
    // Snake. Arrow keys or WASD steer; the on-screen pad works on a world-space CRT.
    // Eating grows the tail and speeds up; a wall or yourself ends it. snake.score carries the score.
    public class SnakeView : AppView
    {
        const int Width = 24;
        const int Height = 18;
        const float CellSize = 14f;

        readonly List<Vector2Int> _body = new();
        Vector2Int _direction = Vector2Int.right;
        Vector2Int _queued = Vector2Int.right;
        Vector2Int _apple;

        RectTransform _field;
        RetroStatusStrip _status;
        Image[,] _cells;
        float _step = 0.16f;
        float _nextStep;
        int _score, _best;
        bool _running, _dead;

        protected override void Build()
        {
            RetroUi.VLayout(Root.gameObject, 2, 2);

            var menu = RetroUi.Menu(Root);
            menu.AddMenu("Game")
                .Command("New", NewGame, "F2")
                .Command("Pause", () => _running = !_running, "P")
                .Separator()
                .Command("Exit", () => Window.Close());
            menu.AddMenu("Help").Command("How to play", () => RetroDialog.Alert(Os.Overlay, "Snake",
                "Steer with the arrow keys or WASD.\n\n" +
                "Eat the apples to grow. Do not hit the wall or your own tail."));

            var frame = RetroUi.Panel(Root, PaletteSlot.Screen, "Field");
            RetroUi.Flexible(frame);
            RetroUi.Bevel(frame.rectTransform, false, 3f);

            _field = RetroUi.NewRect("Grid", frame.transform);
            _field.anchorMin = _field.anchorMax = _field.pivot = new Vector2(0.5f, 0.5f);
            _field.sizeDelta = new Vector2(Width * CellSize, Height * CellSize);

            BuildCells();
            BuildPad();
            _status = RetroUi.StatusStrip(Root, -1f, 110f);

            NewGame();
        }

        void BuildCells()
        {
            _cells = new Image[Width, Height];
            for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                {
                    var cell = RetroUi.Panel(_field, Color.clear, "c");
                    cell.raycastTarget = false;
                    var rt = cell.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.sizeDelta = new Vector2(CellSize - 1f, CellSize - 1f);
                    rt.anchoredPosition = new Vector2(x * CellSize, -y * CellSize);
                    _cells[x, y] = cell;
                }
        }

        // A D-pad for machines with no keyboard (a CRT in a 3D scene).
        void BuildPad()
        {
            var pad = RetroUi.NewRect("Pad", Root);
            RetroUi.Size(pad, h: 26f);
            RetroUi.HLayout(pad.gameObject, 4, 0);

            RetroUi.Spacer(pad);
            RetroUi.Size(RetroUi.PushButton(pad, "←", () => Steer(Vector2Int.left)), w: 34f);
            RetroUi.Size(RetroUi.PushButton(pad, "↑", () => Steer(Vector2Int.up)), w: 34f);
            RetroUi.Size(RetroUi.PushButton(pad, "↓", () => Steer(Vector2Int.down)), w: 34f);
            RetroUi.Size(RetroUi.PushButton(pad, "→", () => Steer(Vector2Int.right)), w: 34f);
            RetroUi.Spacer(pad);
        }

        void NewGame()
        {
            _body.Clear();
            for (var i = 3; i >= 0; i--) _body.Add(new Vector2Int(4 + i, Height / 2));
            _direction = _queued = Vector2Int.right;
            _score = 0;
            _best = Os.Prefs.GetInt(BestKey);
            _step = 0.16f;
            _dead = false;
            _running = true;
            _nextStep = Time.unscaledTime + _step;

            DropApple();
            Raise("new");
            Redraw();
        }

        void DropApple()
        {
            for (var attempt = 0; attempt < 400; attempt++)
            {
                var spot = new Vector2Int(Random.Range(0, Width), Random.Range(0, Height));
                if (_body.Contains(spot)) continue;
                _apple = spot;
                return;
            }
        }

        void Steer(Vector2Int direction)
        {
            // No instant reversals - that would be an instant loss.
            if (direction + _direction == Vector2Int.zero) return;
            _queued = direction;
        }

        void Update()
        {
            ReadKeys();
            if (!_running || _dead) return;
            if (Time.unscaledTime < _nextStep) return;
            _nextStep = Time.unscaledTime + _step;
            Step();
        }

        void ReadKeys()
        {
            // Only the top window steers, only while the player is here, never while a text field has the keyboard.
            if (!HasKeyboard) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                Steer(Vector2Int.left);
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                Steer(Vector2Int.right);
            else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                Steer(Vector2Int.up);
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                Steer(Vector2Int.down);
        }

        void Step()
        {
            _direction = _queued;
            // Screen rows run downward, so "up" is one row less.
            var head = _body[0] + new Vector2Int(_direction.x, -_direction.y);

            if (head.x < 0 || head.y < 0 || head.x >= Width || head.y >= Height || _body.Contains(head))
            {
                Die();
                return;
            }

            _body.Insert(0, head);

            if (head == _apple)
            {
                _score += 10;
                _step = Mathf.Max(0.055f, _step * 0.96f);
                Os.Audio.PlayUi(UiSfx.Click);
                Raise("score", _score.ToString());
                DropApple();
            }
            else
            {
                _body.RemoveAt(_body.Count - 1);
            }

            Redraw();
        }

        void Redraw()
        {
            for (var y = 0; y < Height; y++)
                for (var x = 0; x < Width; x++)
                    _cells[x, y].color = Color.clear;

            _cells[_apple.x, _apple.y].color = new Color(1f, 0.25f, 0.2f);

            for (var i = 0; i < _body.Count; i++)
            {
                var part = _body[i];
                if (part.x < 0 || part.y < 0 || part.x >= Width || part.y >= Height) continue;
                _cells[part.x, part.y].color = i == 0
                    ? RetroSkin.ScreenText
                    : Color.Lerp(RetroSkin.ScreenText, RetroSkin.ScreenDim,
                        Mathf.Clamp01(i / (float)Mathf.Max(1, _body.Count)));
            }

            if (_status == null) return;
            _status.Set(0, _dead ? "Game over - Game > New" : _running ? "Playing" : "Paused");
            _status.Set(1, $"Score {_score}   Best {_best}");
        }

        void Die()
        {
            _dead = true;
            _running = false;
            Os.Audio.PlayUi(UiSfx.Error);
            Raise("lost", _score.ToString());
            Redraw();

            // The best survives closing the window, which is the only thing
            // that makes a high score worth having.
            var beaten = Os.Prefs.SetBest(BestKey, _score, lowerIsBetter: false);
            _best = Os.Prefs.GetInt(BestKey, _score);

            RetroDialog.Alert(Os.Overlay, "Snake", beaten
                ? $"Game over.\n\nScore: {_score} - a new best."
                : $"Game over.\n\nScore: {_score}\nBest: {_best}");
        }

        const string BestKey = "snake.best";
    }
}
