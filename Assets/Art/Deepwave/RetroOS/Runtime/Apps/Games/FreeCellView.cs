using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Audio;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps.Games
{
    // FreeCell: nothing hidden, so every deal is a puzzle. Four free cells, four
    // foundations, eight columns, all face up. Click to lift, click to drop; how many
    // cards move at once depends on free cells and empty columns.
    public class FreeCellView : AppView
    {
        const float ColumnGap = 6f;
        const float FanStep = 17f;

        readonly Card?[] _cells = new Card?[4];
        readonly List<Card>[] _foundations = { new(), new(), new(), new() };
        readonly List<Card>[] _columns = new List<Card>[8];

        // The whole board, copied - Card is a value type, so a snapshot is a few small list copies.
        class Snapshot
        {
            public Card?[] Cells;
            public List<Card>[] Foundations, Columns;
            public int Moves;
        }

        readonly Stack<Snapshot> _undo = new();

        RectTransform _table;
        RetroStatusStrip _status;
        int _moves;
        bool _won;

        int _heldPile = -1;    // 0-7 columns, 8-11 cells, 12-15 foundations
        int _heldIndex = -1;

        protected override void Build()
        {
            RetroUi.VLayout(Root.gameObject, 2, 2);

            var menu = RetroUi.Menu(Root);
            menu.AddMenu("Game")
                .Command("New Deal", Deal, "F2")
                .Command("Auto Play", AutoPlay, "F3")
                .Separator()
                .Command("Undo", Undo, "Ctrl+Z")
                .Separator()
                .Command("Exit", () => Window.Close());
            menu.AddMenu("Help").Command("How to play", () => RetroDialog.Alert(Os.Overlay, "FreeCell",
                "Move every card to the four foundations, ace to king by suit.\n\n" +
                "Columns build down in alternating colours.\n" +
                "The four cells at the top left hold one card each.\n" +
                "Every card is face up - it is a puzzle, not a gamble."));

            _table = CardTable.Baize(Root);
            _status = RetroUi.StatusStrip(Root, -1f, 110f);

            Deal();
        }

        void Deal()
        {
            for (var i = 0; i < 4; i++) { _cells[i] = null; _foundations[i].Clear(); }
            for (var i = 0; i < 8; i++) _columns[i] = new List<Card>();
            _moves = 0;
            _won = false;
            _heldPile = -1;

            var deck = CardTable.NewDeck();
            for (var i = 0; i < deck.Count; i++) _columns[i % 8].Add(deck[i]);

            _undo.Clear();
            Raise("deal");
            Redraw();
        }

        // --- undo -------------------------------------------------------------

        // Only so far back: a long game would otherwise keep every board it
        // ever passed through.
        const int UndoDepth = 60;

        void Remember()
        {
            _undo.Push(new Snapshot
            {
                Cells = (Card?[])_cells.Clone(),
                Foundations = _foundations.Select(f => new List<Card>(f)).ToArray(),
                Columns = _columns.Select(c => new List<Card>(c)).ToArray(),
                Moves = _moves
            });

            CardTable.TrimUndo(_undo, UndoDepth);
        }

        void Forget()
        {
            if (_undo.Count > 0) _undo.Pop();
        }

        void Undo()
        {
            if (_undo.Count == 0)
            {
                _status.Set(1, "Nothing to undo");
                return;
            }

            var was = _undo.Pop();

            for (var i = 0; i < _cells.Length; i++) _cells[i] = was.Cells[i];
            for (var i = 0; i < _foundations.Length; i++)
            {
                _foundations[i].Clear();
                _foundations[i].AddRange(was.Foundations[i]);
            }
            for (var i = 0; i < _columns.Length; i++) _columns[i] = new List<Card>(was.Columns[i]);

            _moves = was.Moves;
            _heldPile = -1;
            _won = false;

            Os.Audio.PlayUi(UiSfx.Click);
            Redraw();
        }

        void Redraw()
        {
            for (var i = _table.childCount - 1; i >= 0; i--) Destroy(_table.GetChild(i).gameObject);

            const float top = -8f;
            var step = CardTable.CardWidth + ColumnGap;

            for (var i = 0; i < 4; i++)
            {
                var index = i;
                var rect = _cells[i].HasValue
                    ? CardTable.Draw(_table, _cells[i].Value, true, () => Tap(8 + index, 0))
                    : CardTable.Slot(_table, "", () => Drop(8 + index));
                Place(rect, new Vector2(8f + i * step, top));
                Highlight(rect, _heldPile == 8 + index);
            }

            for (var i = 0; i < 4; i++)
            {
                var index = i;
                var pile = _foundations[i];
                var rect = pile.Count > 0
                    ? CardTable.Draw(_table, pile[^1], true, () => Drop(12 + index))
                    : CardTable.Slot(_table, "A", () => Drop(12 + index));
                Place(rect, new Vector2(8f + (i + 4.5f) * step, top));
            }

            var columnTop = top - CardTable.CardHeight - 14f;
            for (var c = 0; c < 8; c++)
            {
                var column = c;
                var pile = _columns[c];
                var x = 8f + c * step;

                if (pile.Count == 0)
                {
                    Place(CardTable.Slot(_table, "", () => Drop(column)), new Vector2(x, columnTop));
                    continue;
                }

                for (var i = 0; i < pile.Count; i++)
                {
                    var index = i;
                    var rect = CardTable.Draw(_table, pile[i], true, () => Tap(column, index));
                    Place(rect, new Vector2(x, columnTop - i * FanFor(pile.Count, columnTop)));
                    CardTable.AddDoubleClick(rect, () => SendToFoundation(column, index));
                    Highlight(rect, _heldPile == column && _heldIndex == index);
                }
            }

            _status.Set(0, $"Moves {_moves}   Free cells {_cells.Count(c => !c.HasValue)}");
            _status.Set(1, _heldPile >= 0 ? "Card lifted" : "");

            if (!_won && _foundations.All(f => f.Count == 13)) Win();
        }

        static void Place(RectTransform card, Vector2 position)
        {
            card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = position;
        }

        static void Highlight(RectTransform card, bool on)
        {
            if (!on) return;
            var glow = RetroUi.NewRect("Lift", card).gameObject.AddComponent<Image>();
            glow.color = new Color(1f, 1f, 0.3f, 0.35f);
            glow.raycastTarget = false;
            RetroUi.Stretch(glow.rectTransform);
        }

        // --- moves ---------------------------------------------------------

        void Tap(int pile, int index)
        {
            if (_heldPile < 0) { _heldPile = pile; _heldIndex = index; Os.Audio.PlayUi(UiSfx.Click); }
            else Drop(pile);
            Redraw();
        }

        // A long column fans tighter rather than running off the table (columns are unbounded).
        float FanFor(int count, float top)
        {
            if (count <= 1) return FanStep;

            var room = _table.rect.height + top - CardTable.CardHeight - 6f;
            if (room <= 0f) return FanStep;

            return Mathf.Clamp(room / (count - 1), 6f, FanStep);
        }

        void Drop(int target)
        {
            if (_heldPile < 0) { _heldPile = target; _heldIndex = 0; Redraw(); return; }
            if (_heldPile == target) { _heldPile = -1; Redraw(); return; }

            Remember();
            var moved = target switch
            {
                >= 12 => ToFoundation(target - 12),
                >= 8 => ToCell(target - 8),
                _ => ToColumn(target)
            };

            Os.Audio.PlayUi(moved ? UiSfx.Click : UiSfx.Error);
            if (moved) { _moves++; Raise("move"); }
            else Forget();          // nothing happened, nothing to take back
            _heldPile = -1;
            Redraw();
        }

        // The lifted run: several cards from a column, or a single card.
        List<Card> HeldRun()
        {
            if (_heldPile >= 12) return TopOf(_foundations[_heldPile - 12]);
            if (_heldPile >= 8) return _cells[_heldPile - 8].HasValue
                ? new List<Card> { _cells[_heldPile - 8].Value } : new List<Card>();

            var column = _columns[_heldPile];
            if (_heldIndex < 0 || _heldIndex >= column.Count) return new List<Card>();
            return column.Skip(_heldIndex).ToList();
        }

        static List<Card> TopOf(List<Card> pile) =>
            pile.Count == 0 ? new List<Card>() : new List<Card> { pile[^1] };

        void RemoveHeld(int count)
        {
            if (_heldPile >= 12) _foundations[_heldPile - 12].RemoveAt(_foundations[_heldPile - 12].Count - 1);
            else if (_heldPile >= 8) _cells[_heldPile - 8] = null;
            else _columns[_heldPile].RemoveRange(_heldIndex, count);
        }

        bool ToCell(int index)
        {
            if (_cells[index].HasValue) return false;
            var run = HeldRun();
            if (run.Count != 1) return false;
            _cells[index] = run[0];
            RemoveHeld(1);
            return true;
        }

        bool ToFoundation(int index)
        {
            var run = HeldRun();
            if (run.Count != 1) return false;
            var card = run[0];
            var pile = _foundations[index];
            if (pile.Count == 0 ? card.Rank != 1 : pile[^1].Suit != card.Suit || card.Rank != pile[^1].Rank + 1)
                return false;

            RemoveHeld(1);
            pile.Add(card);
            return true;
        }

        bool ToColumn(int index)
        {
            var run = HeldRun();
            if (run.Count == 0) return false;
            if (!IsRunOrdered(run)) return false;
            if (run.Count > MaxMovable(_columns[index].Count == 0)) return false;

            var target = _columns[index];
            if (target.Count > 0)
            {
                var top = target[^1];
                if (top.Red == run[0].Red || run[0].Rank != top.Rank - 1) return false;
            }

            RemoveHeld(run.Count);
            target.AddRange(run);
            return true;
        }

        // Only a properly alternating descending run can move as a unit.
        static bool IsRunOrdered(List<Card> run)
        {
            for (var i = 1; i < run.Count; i++)
                if (run[i].Rank != run[i - 1].Rank - 1 || run[i].Red == run[i - 1].Red) return false;
            return true;
        }

        // (free cells + 1) x 2^(empty columns) - the classic supermove rule.
        int MaxMovable(bool targetIsEmpty)
        {
            var freeCells = _cells.Count(c => !c.HasValue);
            var emptyColumns = _columns.Count(c => c.Count == 0) - (targetIsEmpty ? 1 : 0);
            return (freeCells + 1) * (1 << Mathf.Max(0, emptyColumns));
        }

        void SendToFoundation(int column, int index)
        {
            var pile = _columns[column];
            if (index != pile.Count - 1) return;
            _heldPile = column;
            _heldIndex = index;

            Remember();
            for (var i = 0; i < 4; i++)
                if (ToFoundation(i))
                {
                    _moves++;
                    Os.Audio.PlayUi(UiSfx.Click);
                    _heldPile = -1;
                    Redraw();
                    return;
                }

            _heldPile = -1;
            Forget();
            Os.Audio.PlayUi(UiSfx.Error);
        }

        // Auto-plays every safe card. One snapshot for the whole sweep, so undo puts the board back to before it.
        void AutoPlay()
        {
            Remember();
            var before = _moves;
            var moved = true;
            while (moved)
            {
                moved = false;
                for (var c = 0; c < 8 && !moved; c++)
                {
                    var pile = _columns[c];
                    if (pile.Count == 0) continue;
                    _heldPile = c;
                    _heldIndex = pile.Count - 1;
                    for (var f = 0; f < 4 && !moved; f++) moved = ToFoundation(f);
                }
                for (var i = 0; i < 4 && !moved; i++)
                {
                    if (!_cells[i].HasValue) continue;
                    _heldPile = 8 + i;
                    _heldIndex = 0;
                    for (var f = 0; f < 4 && !moved; f++) moved = ToFoundation(f);
                }
                if (moved) _moves++;
            }

            if (_moves == before) Forget();
            _heldPile = -1;
            Redraw();
        }

        void Win()
        {
            _won = true;
            Os.Audio.PlayUi(UiSfx.Notify);
            Raise("won", _moves.ToString());
            RetroDialog.Alert(Os.Overlay, "FreeCell", $"Solved in {_moves} moves.");
        }
    }
}
