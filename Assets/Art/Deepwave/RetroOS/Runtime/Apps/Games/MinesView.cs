using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Audio;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps.Games
{
    // Minesweeper. Left-click clears, right-click flags, the first click is always safe,
    // a blank square floods outward. Win/lose raise mines.won / mines.lost.
    public class MinesView : AppView
    {
        class Cell
        {
            public Image Bg;
            public RetroBevel Bevel;
            public Text Label;
            public bool Mine;
            public bool Revealed;
            public bool Flagged;
            public int Neighbours;
        }

        static readonly Color[] NumberColors =
        {
            Color.clear,
            new(0f, 0f, 1f), new(0f, 0.5f, 0f), new(1f, 0f, 0f),
            new(0f, 0f, 0.5f), new(0.5f, 0f, 0f), new(0f, 0.5f, 0.5f),
            Color.black, new(0.5f, 0.5f, 0.5f)
        };

        const float CellSize = 20f;

        Cell[,] _cells;
        int _columns, _rows, _mines;
        int _revealedCount;
        int _flags;
        bool _over, _started;
        float _elapsed;
        int _shownSecond = -1;

        Text _mineCounter, _timer;
        RetroPushButton _reset;
        RectTransform _board;

        IMineFieldSource Field => Config<IMineFieldSource>();

        protected override void Build()
        {
            RetroUi.VLayout(Root.gameObject, 3, 3);
            BuildMenu();
            BuildHeader();

            _board = RetroUi.NewRect("Board", Root);
            RetroUi.Flexible(_board);
            var frame = RetroUi.Panel(_board, PaletteSlot.Face, "Frame");
            RetroUi.Stretch(frame.rectTransform);
            RetroUi.Bevel(frame.rectTransform, false, 3f);

            var field = Field;
            NewGame(field?.Columns ?? 9, field?.Rows ?? 9, field?.Mines ?? 10);
        }

        void BuildMenu()
        {
            var menu = RetroUi.Menu(Root);
            var game = menu.AddMenu("Game");
            game.Command("New", () => NewGame(_columns, _rows, _mines), "F2")
                .Separator()
                .Command("Beginner", () => NewGame(9, 9, 10))
                .Command("Intermediate", () => NewGame(16, 16, 40))
                .Command("Expert", () => NewGame(30, 16, 99))
                .Separator()
                .Command("Best Times", ShowBestTimes)
                .Separator()
                .Command("Exit", () => Window.Close());

            menu.AddMenu("Help").Command("How to play", () => RetroDialog.Alert(Os.Overlay, "Mines",
                "Clear every square that has no mine under it.\n\n" +
                "Left click clears a square. Right click plants a flag.\n" +
                "A number says how many mines touch that square."));
        }

        void ShowBestTimes()
        {
            var boards = new (string Name, int C, int R, int M)[]
            {
                ("Beginner", 9, 9, 10),
                ("Intermediate", 16, 16, 40),
                ("Expert", 30, 16, 99)
            };

            var lines = new System.Text.StringBuilder();
            foreach (var (name, c, r, m) in boards)
            {
                var key = $"mines.best.{c}x{r}x{m}";
                lines.Append(name.PadRight(16))
                     .Append(Os.Prefs.Has(key) ? Os.Prefs.GetInt(key) + " seconds" : "-")
                     .Append('\n');
            }

            RetroDialog.Info(Os.Overlay, "Best Times", lines.ToString().TrimEnd());
        }

        void BuildHeader()
        {
            var header = RetroUi.Panel(Root, PaletteSlot.Face, "Header");
            RetroUi.Size(header, h: 34f);
            RetroUi.Bevel(header.rectTransform, false, 2f);
            RetroUi.HLayout(header.gameObject, 6, 5);

            _mineCounter = Counter(header.transform);
            RetroUi.Spacer(header.transform);

            _reset = RetroPushButton.Create(header.transform, ":)",
                () => NewGame(_columns, _rows, _mines));
            RetroUi.Size(_reset, w: 26f);

            RetroUi.Spacer(header.transform);
            _timer = Counter(header.transform);
        }

        static Text Counter(Transform parent)
        {
            var box = RetroUi.Panel(parent, Color.black, "Counter");
            RetroUi.Size(box, w: 46f);
            RetroUi.Bevel(box.rectTransform, false, 1f);
            var label = RetroUi.Label(box.transform, "000", 16, TextAnchor.MiddleCenter, RetroSkin.Mono);
            RetroPalette.PaintOff(label, Color.red);
            RetroUi.Stretch(label.rectTransform);
            return label;
        }

        void NewGame(int columns, int rows, int mines)
        {
            _columns = Mathf.Clamp(columns, 5, 30);
            _rows = Mathf.Clamp(rows, 5, 24);
            _mines = Mathf.Clamp(mines, 1, _columns * _rows - 9);
            _revealedCount = 0;
            _flags = 0;
            _elapsed = 0f;
            _over = false;
            _started = false;
            if (_reset) _reset.Caption = ":)";

            for (var i = _board.childCount - 1; i >= 1; i--) Destroy(_board.GetChild(i).gameObject);

            var grid = RetroUi.NewRect("Grid", _board);
            grid.anchorMin = grid.anchorMax = grid.pivot = new Vector2(0.5f, 0.5f);
            grid.sizeDelta = new Vector2(_columns * CellSize, _rows * CellSize);

            _cells = new Cell[_columns, _rows];
            for (var y = 0; y < _rows; y++)
                for (var x = 0; x < _columns; x++)
                    _cells[x, y] = BuildCell(grid, x, y);

            FitWindow();
            UpdateCounters();
            Raise("new", $"{_columns}x{_rows}");
        }

        // The window is sized to the field, so an Expert board does not paint across the desktop.
        void FitWindow()
        {
            if (Window == null) return;

            // Board, plus the frame, the menu bar, the header and the padding
            // the column puts between them.
            var wanted = new Vector2(
                _columns * CellSize + 26f,
                _rows * CellSize + RetroSkin.TitleBarHeight + RetroSkin.MenuBarHeight + 64f);

            Window.MinSize = Vector2.Min(wanted, new Vector2(600f, 560f));
            Window.SetSize(wanted);
        }

        Cell BuildCell(Transform parent, int x, int y)
        {
            var bg = RetroUi.Panel(parent, PaletteSlot.Face, $"Cell {x},{y}");
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(CellSize, CellSize);
            rt.anchoredPosition = new Vector2(x * CellSize, -y * CellSize);

            var cell = new Cell
            {
                Bg = bg,
                Bevel = RetroUi.Bevel(rt, true, 2f),
                Label = RetroUi.Label(bg.transform, "", 13, TextAnchor.MiddleCenter)
            };
            cell.Label.fontStyle = FontStyle.Bold;
            RetroUi.Stretch(cell.Label.rectTransform);

            var relay = bg.gameObject.AddComponent<RetroPointerRelay>();
            relay.Clicked = () => Reveal(x, y, fromPlayer: true);
            bg.gameObject.AddComponent<RetroRightClickRelay>().Clicked = _ => ToggleFlag(x, y);
            return cell;
        }

        // Mines are laid after the first click so the opening move is safe.
        void LayMines(int safeX, int safeY)
        {
            var spots = new List<Vector2Int>();
            for (var y = 0; y < _rows; y++)
                for (var x = 0; x < _columns; x++)
                    if (Mathf.Abs(x - safeX) > 1 || Mathf.Abs(y - safeY) > 1)
                        spots.Add(new Vector2Int(x, y));

            for (var i = 0; i < _mines && spots.Count > 0; i++)
            {
                var pick = UnityEngine.Random.Range(0, spots.Count);
                var spot = spots[pick];
                spots.RemoveAt(pick);
                _cells[spot.x, spot.y].Mine = true;
            }

            for (var y = 0; y < _rows; y++)
                for (var x = 0; x < _columns; x++)
                    _cells[x, y].Neighbours = CountNeighbours(x, y);
        }

        int CountNeighbours(int x, int y)
        {
            var count = 0;
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= _columns || ny >= _rows) continue;
                    if (_cells[nx, ny].Mine) count++;
                }
            return count;
        }

        void Reveal(int x, int y, bool fromPlayer)
        {
            if (_over) return;
            var cell = _cells[x, y];
            if (cell.Revealed || cell.Flagged) return;

            if (!_started)
            {
                _started = true;
                LayMines(x, y);
            }

            cell.Revealed = true;
            cell.Bevel.SetVisible(false);
            RetroPalette.PaintOff(cell.Bg, Color.Lerp(RetroSkin.Face, RetroSkin.FaceDark, 0.15f));

            if (cell.Mine)
            {
                RetroPalette.PaintOff(cell.Bg, Color.red);
                cell.Label.text = "*";
                RetroPalette.PaintOff(cell.Label, Color.black);
                Lose();
                return;
            }

            _revealedCount++;
            if (cell.Neighbours > 0)
            {
                cell.Label.text = cell.Neighbours.ToString();
                cell.Label.color = NumberColors[Mathf.Clamp(cell.Neighbours, 0, 8)];
            }
            else
            {
                // Empty square: open everything it touches.
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var nx = x + dx;
                        var ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= _columns || ny >= _rows) continue;
                        Reveal(nx, ny, fromPlayer: false);
                    }
            }

            if (fromPlayer) Os.Audio.PlayUi(UiSfx.Click);

            // Guarded: a flood-fill clear unwinds through this test once per level, and Win() opens a dialog.
            if (!_over && _revealedCount >= _columns * _rows - _mines) Win();
        }

        void ToggleFlag(int x, int y)
        {
            if (_over) return;
            var cell = _cells[x, y];
            if (cell.Revealed) return;

            cell.Flagged = !cell.Flagged;
            _flags += cell.Flagged ? 1 : -1;
            cell.Label.text = cell.Flagged ? "F" : "";
            RetroPalette.PaintOff(cell.Label, Color.red);
            Os.Audio.PlayUi(UiSfx.Click);
            UpdateCounters();
            Raise("flag", $"{x},{y}");
        }

        void Win()
        {
            _over = true;
            _reset.Caption = "B)";
            Os.Audio.PlayUi(UiSfx.Notify);

            var seconds = Mathf.FloorToInt(_elapsed);
            Raise("won", $"{_columns}x{_rows}:{seconds}s");

            // The machine keeps the best time per field size, which is the
            // whole reason anyone played this twice.
            var best = Os.Prefs.SetBest(BestKey, seconds);
            RetroDialog.Alert(Os.Overlay, "Mines", best
                ? $"Field cleared in {seconds} seconds.\n\nA new best time for {FieldName}."
                : $"Field cleared in {seconds} seconds.\n\nBest for {FieldName}: " +
                  $"{Os.Prefs.GetInt(BestKey)} seconds.");
        }

        // Best times are per field size: a nine-by-nine record means nothing
        // on an expert board.
        string BestKey => $"mines.best.{_columns}x{_rows}x{_mines}";

        string FieldName => (_columns, _rows, _mines) switch
        {
            (9, 9, 10) => "Beginner",
            (16, 16, 40) => "Intermediate",
            (30, 16, 99) => "Expert",
            _ => $"{_columns}x{_rows}"
        };

        void Lose()
        {
            _over = true;
            _reset.Caption = ":(";
            Os.Audio.PlayUi(UiSfx.Error);

            for (var y = 0; y < _rows; y++)
                for (var x = 0; x < _columns; x++)
                {
                    var cell = _cells[x, y];
                    if (!cell.Mine || cell.Revealed) continue;
                    cell.Label.text = "*";
                    RetroPalette.PaintOff(cell.Label, Color.black);
                }

            Raise("lost", $"{_columns}x{_rows}");
        }

        void UpdateCounters()
        {
            if (_mineCounter) _mineCounter.text = Mathf.Clamp(_mines - _flags, -99, 999).ToString("000");
            if (_timer) _timer.text = Mathf.Clamp(Mathf.FloorToInt(_elapsed), 0, 999).ToString("000");
        }

        void Update()
        {
            if (!_started || _over) return;
            _elapsed += Time.unscaledDeltaTime;

            // The counters only ever show whole seconds, so rebuild their
            // strings once a second instead of once a frame.
            var second = Mathf.FloorToInt(_elapsed);
            if (second == _shownSecond) return;
            _shownSecond = second;
            UpdateCounters();
        }
    }
}
