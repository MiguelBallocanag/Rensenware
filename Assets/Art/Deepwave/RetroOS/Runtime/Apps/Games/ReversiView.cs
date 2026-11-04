using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Audio;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps.Games
{
    // Reversi: place a disc to trap a line of the opponent's and the line turns over.
    // The built-in opponent plays weighted-greedy (values corners, avoids their neighbours).
    public class ReversiView : AppView
    {
        const int Size = 8;
        const float CellSize = 34f;

        static readonly Vector2Int[] Directions =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
            new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
        };

        // How much each square is worth to the computer player.
        static readonly int[,] Weights =
        {
            { 120, -20, 20, 5, 5, 20, -20, 120 },
            { -20, -40, -5, -5, -5, -5, -40, -20 },
            { 20, -5, 15, 3, 3, 15, -5, 20 },
            { 5, -5, 3, 3, 3, 3, -5, 5 },
            { 5, -5, 3, 3, 3, 3, -5, 5 },
            { 20, -5, 15, 3, 3, 15, -5, 20 },
            { -20, -40, -5, -5, -5, -5, -40, -20 },
            { 120, -20, 20, 5, 5, 20, -20, 120 }
        };

        readonly int[,] _board = new int[Size, Size];   // 0 empty, 1 black, 2 white
        RectTransform _grid;
        RetroStatusStrip _status;
        int _turn = 1;
        bool _vsComputer = true;
        bool _over;
        float _thinkAt = -1f;

        protected override void Build()
        {
            RetroUi.VLayout(Root.gameObject, 2, 2);

            var menu = RetroUi.Menu(Root);
            menu.AddMenu("Game")
                .Command("New", NewGame, "F2")
                .Separator()
                .Check("Play the computer", _vsComputer, () => { _vsComputer = !_vsComputer; NewGame(); })
                .Separator()
                .Command("Exit", () => Window.Close());
            menu.AddMenu("Help").Command("How to play", () => RetroDialog.Alert(Os.Overlay, "Reversi",
                "Place a disc so that it traps a straight line of your opponent's " +
                "discs between the new disc and one of yours.\n\n" +
                "Every trapped disc turns over. Most discs at the end wins."));

            var board = RetroUi.Panel(Root, new Color(0f, 0.36f, 0.16f), "Board");
            RetroUi.Flexible(board);
            RetroUi.Bevel(board.rectTransform, false, 3f);

            _grid = RetroUi.NewRect("Grid", board.transform);
            _grid.anchorMin = _grid.anchorMax = _grid.pivot = new Vector2(0.5f, 0.5f);
            _grid.sizeDelta = new Vector2(Size * CellSize, Size * CellSize);

            _status = RetroUi.StatusStrip(Root, -1f, 120f);
            NewGame();
        }

        void NewGame()
        {
            for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                    _board[x, y] = 0;

            _board[3, 3] = _board[4, 4] = 2;
            _board[3, 4] = _board[4, 3] = 1;
            _turn = 1;
            _over = false;

            Raise("new");
            Redraw();
        }

        void Redraw()
        {
            for (var i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);

            var legal = LegalMoves(_turn);

            for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                {
                    var cx = x;
                    var cy = y;

                    var cell = RetroUi.Panel(_grid, new Color(0f, 0.42f, 0.19f), $"{x},{y}");
                    var rt = cell.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.sizeDelta = new Vector2(CellSize - 1f, CellSize - 1f);
                    rt.anchoredPosition = new Vector2(x * CellSize, -y * CellSize);

                    if (legal.Contains(new Vector2Int(x, y)))
                    {
                        var hint = RetroUi.NewRect("Hint", rt).gameObject.AddComponent<Image>();
                        hint.color = new Color(1f, 1f, 1f, 0.16f);
                        hint.raycastTarget = false;
                        RetroUi.Stretch(hint.rectTransform, 10f);
                    }

                    if (_board[x, y] != 0) Disc(rt, _board[x, y]);

                    cell.gameObject.AddComponent<RetroPointerRelay>().Clicked = () => Play(cx, cy);
                }

            var (black, white) = Counts();
            _status.Set(0, _over
                ? black == white ? "Draw" : black > white ? "Black wins" : "White wins"
                : $"{(_turn == 1 ? "Black" : "White")} to play");
            _status.Set(1, $"● {black}   ○ {white}");
        }

        static void Disc(RectTransform cell, int player)
        {
            var disc = RetroUi.NewRect("Disc", cell).gameObject.AddComponent<RetroDiscShape>();
            disc.Tint = player == 1 ? new Color(0.08f, 0.08f, 0.08f) : Color.white;
            disc.raycastTarget = false;
            RetroUi.Stretch(disc.rectTransform, 4f);
        }

        void Play(int x, int y)
        {
            if (_over || _board[x, y] != 0) return;
            var flips = Flips(x, y, _turn);
            if (flips.Count == 0) { Os.Audio.PlayUi(UiSfx.Error); return; }

            Commit(x, y, _turn, flips);
            Os.Audio.PlayUi(UiSfx.Click);
            Raise("move", $"{x},{y}");

            Advance();
            if (_vsComputer && !_over && _turn == 2) _thinkAt = Time.unscaledTime + 0.4f;
        }

        void Commit(int x, int y, int player, List<Vector2Int> flips)
        {
            _board[x, y] = player;
            foreach (var flip in flips) _board[flip.x, flip.y] = player;
        }

        // Hands the turn over, skipping a player with no legal move, and ends
        // the game when neither side can play.
        void Advance()
        {
            var next = 3 - _turn;
            if (LegalMoves(next).Count > 0) _turn = next;
            else if (LegalMoves(_turn).Count == 0) Finish();
            Redraw();
        }

        void Update()
        {
            if (_thinkAt < 0f || Time.unscaledTime < _thinkAt) return;
            _thinkAt = -1f;
            ComputerMove();
        }

        void ComputerMove()
        {
            if (_over || _turn != 2) return;

            var best = new Vector2Int(-1, -1);
            var bestScore = int.MinValue;

            foreach (var move in LegalMoves(2))
            {
                var flips = Flips(move.x, move.y, 2);
                var score = Weights[move.x, move.y] + flips.Count;
                if (score <= bestScore) continue;
                bestScore = score;
                best = move;
            }

            if (best.x < 0) { Advance(); return; }

            Commit(best.x, best.y, 2, Flips(best.x, best.y, 2));
            Os.Audio.PlayUi(UiSfx.Click);
            Advance();
            if (!_over && _turn == 2) _thinkAt = Time.unscaledTime + 0.4f;
        }

        List<Vector2Int> LegalMoves(int player)
        {
            var moves = new List<Vector2Int>();
            for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                {
                    if (_board[x, y] != 0) continue;
                    if (Flips(x, y, player).Count > 0) moves.Add(new Vector2Int(x, y));
                }
            return moves;
        }

        List<Vector2Int> Flips(int x, int y, int player)
        {
            var result = new List<Vector2Int>();
            if (_board[x, y] != 0) return result;

            var opponent = 3 - player;
            foreach (var dir in Directions)
            {
                var line = new List<Vector2Int>();
                var cx = x + dir.x;
                var cy = y + dir.y;

                while (cx >= 0 && cy >= 0 && cx < Size && cy < Size && _board[cx, cy] == opponent)
                {
                    line.Add(new Vector2Int(cx, cy));
                    cx += dir.x;
                    cy += dir.y;
                }

                if (line.Count > 0 && cx >= 0 && cy >= 0 && cx < Size && cy < Size &&
                    _board[cx, cy] == player)
                    result.AddRange(line);
            }
            return result;
        }

        (int black, int white) Counts()
        {
            int black = 0, white = 0;
            foreach (var value in _board)
            {
                if (value == 1) black++;
                else if (value == 2) white++;
            }
            return (black, white);
        }

        void Finish()
        {
            _over = true;
            var (black, white) = Counts();
            Os.Audio.PlayUi(black >= white ? UiSfx.Notify : UiSfx.Error);
            Raise(black > white ? "won" : black < white ? "lost" : "draw", $"{black}-{white}");
            RetroDialog.Alert(Os.Overlay, "Reversi",
                black == white ? $"Draw, {black} each."
                : $"{(black > white ? "Black" : "White")} wins {Mathf.Max(black, white)} to {Mathf.Min(black, white)}.");
        }
    }

    // Draws a filled circle for a board token without needing a sprite asset.
    public class RetroDiscShape : MaskableGraphic
    {
        public Color Tint = Color.white;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            const int segments = 24;
            var radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
            var centre = rectTransform.rect.center;

            var vertex = UIVertex.simpleVert;
            vertex.color = Tint;
            vertex.position = centre;
            vh.AddVert(vertex);

            for (var i = 0; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                vertex.position = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                // A touch of shading so the disc reads as a physical counter.
                vertex.color = Color.Lerp(Tint, Tint * 0.75f, Mathf.Clamp01(Mathf.Sin(angle) * -0.5f + 0.5f));
                vertex.color.a = 255;
                vh.AddVert(vertex);
            }

            for (var i = 1; i <= segments; i++) vh.AddTriangle(0, i, i + 1);
        }
    }
}
