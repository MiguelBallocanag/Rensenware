using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Audio;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps.Games
{
    // Klondike patience. Seven tableau piles, four foundations, a one-card stock. Click
    // to pick up, click to drop; double-click sends a card to its foundation. Finishing raises solitaire.won.
    public class SolitaireView : AppView
    {
        const float ColumnGap = 8f;
        const float FanStep = 18f;

        readonly List<Card> _stock = new();
        readonly List<Card> _waste = new();
        readonly List<Card>[] _foundations = { new(), new(), new(), new() };
        readonly List<Card>[] _tableau = new List<Card>[7];
        readonly HashSet<Card> _faceUp = new();

        // The whole game, copied - Card is a value type, so a snapshot is a few small list copies.
        class Snapshot
        {
            public List<Card> Stock, Waste;
            public List<Card>[] Foundations, Tableau;
            public HashSet<Card> FaceUp;
            public int Score, Moves;
        }

        readonly Stack<Snapshot> _undo = new();

        RectTransform _table;
        RetroStatusStrip _status;
        int _score, _moves;
        bool _won;

        // What the player has picked up: which pile and from which index.
        int _heldPile = -1;    // 0-6 tableau, 7 waste, 8-11 foundations
        int _heldIndex = -1;

        protected override void Build()
        {
            RetroUi.VLayout(Root.gameObject, 2, 2);

            var menu = RetroUi.Menu(Root);
            menu.AddMenu("Game")
                .Command("Deal", Deal, "F2")
                .Separator()
                .Command("Undo", Undo, "Ctrl+Z")
                .Separator()
                .Command("Exit", () => Window.Close());
            menu.AddMenu("Help").Command("How to play", () => RetroDialog.Alert(Os.Overlay, "Solitaire",
                "Build the four foundations up from ace to king in one suit.\n\n" +
                "On the table, build down in alternating colours.\n" +
                "Click a card to lift it, click where it should go to drop it.\n" +
                "Double-click sends a card to its foundation."));

            _table = CardTable.Baize(Root);
            _status = RetroUi.StatusStrip(Root, -1f, 90f);

            Deal();
        }

        void Deal()
        {
            _stock.Clear();
            _waste.Clear();
            _faceUp.Clear();
            foreach (var pile in _foundations) pile.Clear();
            _score = 0;
            _moves = 0;
            _heldPile = -1;
            _won = false;

            var deck = CardTable.NewDeck();
            var next = 0;
            for (var i = 0; i < 7; i++)
            {
                _tableau[i] = new List<Card>();
                for (var j = 0; j <= i; j++)
                {
                    var card = deck[next++];
                    _tableau[i].Add(card);
                    if (j == i) _faceUp.Add(card);
                }
            }
            for (; next < deck.Count; next++) _stock.Add(deck[next]);

            _undo.Clear();
            Raise("deal");
            Redraw();
        }

        // --- undo -------------------------------------------------------------

        // Only so far back: an hour of patience would otherwise keep every
        // board it ever passed through.
        const int UndoDepth = 40;

        void Remember()
        {
            _undo.Push(new Snapshot
            {
                Stock = new List<Card>(_stock),
                Waste = new List<Card>(_waste),
                Foundations = _foundations.Select(f => new List<Card>(f)).ToArray(),
                Tableau = _tableau.Select(t => new List<Card>(t)).ToArray(),
                FaceUp = new HashSet<Card>(_faceUp),
                Score = _score,
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

            _stock.Clear(); _stock.AddRange(was.Stock);
            _waste.Clear(); _waste.AddRange(was.Waste);
            for (var i = 0; i < _foundations.Length; i++)
            {
                _foundations[i].Clear();
                _foundations[i].AddRange(was.Foundations[i]);
            }
            for (var i = 0; i < _tableau.Length; i++) _tableau[i] = new List<Card>(was.Tableau[i]);

            _faceUp.Clear();
            foreach (var card in was.FaceUp) _faceUp.Add(card);

            _score = was.Score;
            _moves = was.Moves;
            _heldPile = -1;
            _won = false;

            Os.Audio.PlayUi(UiSfx.Click);
            Redraw();
        }

        void Redraw()
        {
            for (var i = _table.childCount - 1; i >= 0; i--) Destroy(_table.GetChild(i).gameObject);

            var x = 10f;
            const float top = -10f;

            // Stock and waste.
            DrawStock(new Vector2(x, top));
            x += CardTable.CardWidth + ColumnGap;
            DrawWaste(new Vector2(x, top));
            x += (CardTable.CardWidth + ColumnGap) * 2f;

            // Foundations.
            for (var i = 0; i < 4; i++)
            {
                DrawFoundation(i, new Vector2(x, top));
                x += CardTable.CardWidth + ColumnGap;
            }

            // Tableau.
            var tableauTop = top - CardTable.CardHeight - 14f;
            for (var i = 0; i < 7; i++)
            {
                var px = 10f + i * (CardTable.CardWidth + ColumnGap);
                DrawTableau(i, new Vector2(px, tableauTop));
            }

            _status.Set(0, $"Score {_score}   Moves {_moves}");
            _status.Set(1, _heldPile >= 0 ? "Card lifted" : "");

            if (!_won && _foundations.All(f => f.Count == 13)) Win();
        }

        void Place(RectTransform card, Vector2 position)
        {
            card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = position;
        }

        void DrawStock(Vector2 at)
        {
            var rect = _stock.Count > 0
                ? CardTable.Draw(_table, _stock[^1], false, TurnStock)
                : CardTable.Slot(_table, "O", TurnStock);
            Place(rect, at);
        }

        void DrawWaste(Vector2 at)
        {
            if (_waste.Count == 0)
            {
                Place(CardTable.Slot(_table, ""), at);
                return;
            }

            var index = _waste.Count - 1;
            var rect = CardTable.Draw(_table, _waste[index], true, () => Pick(7, index));
            Place(rect, at);
            Highlight(rect, _heldPile == 7);
            CardTable.AddDoubleClick(rect, () => SendToFoundation(_waste, index));
        }

        void DrawFoundation(int index, Vector2 at)
        {
            var pile = _foundations[index];
            var slot = index;
            var rect = pile.Count > 0
                ? CardTable.Draw(_table, pile[^1], true, () => Drop(8 + slot))
                : CardTable.Slot(_table, "A", () => Drop(8 + slot));
            Place(rect, at);
        }

        void DrawTableau(int column, Vector2 at)
        {
            var pile = _tableau[column];
            if (pile.Count == 0)
            {
                var empty = CardTable.Slot(_table, "", () => Drop(column));
                Place(empty, at);
                return;
            }

            // A long pile fans tighter rather than running off the table (the baize does not clip).
            var step = FanFor(pile.Count, at.y);
            var offset = 0f;

            for (var i = 0; i < pile.Count; i++)
            {
                var index = i;
                var card = pile[i];
                var up = _faceUp.Contains(card);
                var rect = CardTable.Draw(_table, card, up, () =>
                {
                    if (!up) { FlipTop(column); return; }
                    if (_heldPile >= 0) Drop(column);
                    else Pick(column, index);
                });
                Place(rect, at + new Vector2(0f, -offset));
                offset += up ? step : step * 0.55f;
                if (up) CardTable.AddDoubleClick(rect, () => SendToFoundation(pile, index));
                Highlight(rect, _heldPile == column && _heldIndex == index);
            }
        }

        // The most the cards may be spread and still all be on the table.
        float FanFor(int count, float top)
        {
            if (count <= 1) return FanStep;

            // top is measured down from the top of the baize, so it is
            // negative; what is left below it is the rest of the height.
            var room = _table.rect.height + top - CardTable.CardHeight - 6f;
            if (room <= 0f) return FanStep;

            return Mathf.Clamp(room / (count - 1), 6f, FanStep);
        }

        static void Highlight(RectTransform card, bool on)
        {
            if (!on) return;
            var glow = RetroUi.NewRect("Lift", card).gameObject.AddComponent<Image>();
            glow.color = new Color(1f, 1f, 0.3f, 0.35f);
            glow.raycastTarget = false;
            RetroUi.Stretch(glow.rectTransform);
        }

        // --- rules ---------------------------------------------------------

        void TurnStock()
        {
            Remember();

            if (_stock.Count == 0)
            {
                // Recycle the waste back into the stock.
                for (var i = _waste.Count - 1; i >= 0; i--) _stock.Add(_waste[i]);
                _waste.Clear();
                _score = Mathf.Max(0, _score - 20);
            }
            else
            {
                var card = _stock[^1];
                _stock.RemoveAt(_stock.Count - 1);
                _waste.Add(card);
                _faceUp.Add(card);
            }

            _moves++;
            Os.Audio.PlayUi(UiSfx.Click);
            _heldPile = -1;
            Redraw();
        }

        void FlipTop(int column)
        {
            var pile = _tableau[column];
            if (pile.Count == 0) return;
            var top = pile[^1];
            if (_faceUp.Contains(top)) return;
            _faceUp.Add(top);
            _score += 5;
            _moves++;
            Os.Audio.PlayUi(UiSfx.Click);
            Redraw();
        }

        void Pick(int pile, int index)
        {
            _heldPile = pile;
            _heldIndex = index;
            Os.Audio.PlayUi(UiSfx.Click);
            Redraw();
        }

        void Drop(int target)
        {
            if (_heldPile < 0) return;

            Remember();
            var moved = target >= 8
                ? DropOnFoundation(target - 8)
                : DropOnTableau(target);

            if (moved)
            {
                _moves++;
                Os.Audio.PlayUi(UiSfx.Click);
                Raise("move");
            }
            else
            {
                // Nothing happened, so there is nothing to take back.
                Forget();
                Os.Audio.PlayUi(UiSfx.Error);
            }

            _heldPile = -1;
            Redraw();
        }

        List<Card> HeldPile() => _heldPile == 7 ? _waste
            : _heldPile >= 8 ? _foundations[_heldPile - 8]
            : _tableau[_heldPile];

        bool DropOnFoundation(int index)
        {
            var source = HeldPile();
            if (source.Count == 0) return false;
            // Only the single top card can go up to a foundation.
            if (_heldIndex != source.Count - 1) return false;

            var card = source[^1];
            var pile = _foundations[index];
            if (!CanStack(pile, card, foundation: true)) return false;

            source.RemoveAt(source.Count - 1);
            pile.Add(card);
            _score += 10;
            AutoFlip();
            return true;
        }

        bool DropOnTableau(int column)
        {
            var source = HeldPile();
            if (source.Count == 0 || _heldIndex < 0 || _heldIndex >= source.Count) return false;
            if (_heldPile == column) return false;

            var run = source.Skip(_heldIndex).ToList();
            if (run.Any(c => !_faceUp.Contains(c))) return false;
            if (!CanStack(_tableau[column], run[0], foundation: false)) return false;

            source.RemoveRange(_heldIndex, source.Count - _heldIndex);
            _tableau[column].AddRange(run);
            _score += 3;
            AutoFlip();
            return true;
        }

        void SendToFoundation(List<Card> source, int index)
        {
            if (source.Count == 0 || index != source.Count - 1) return;
            var card = source[^1];

            for (var i = 0; i < 4; i++)
            {
                if (!CanStack(_foundations[i], card, foundation: true)) continue;

                Remember();
                source.RemoveAt(source.Count - 1);
                _foundations[i].Add(card);
                _score += 10;
                _moves++;
                Os.Audio.PlayUi(UiSfx.Click);
                AutoFlip();
                _heldPile = -1;
                Redraw();
                return;
            }
            Os.Audio.PlayUi(UiSfx.Error);
        }

        // A pile that has just lost its top card shows the one underneath.
        void AutoFlip()
        {
            foreach (var pile in _tableau)
            {
                if (pile.Count == 0) continue;
                if (!_faceUp.Contains(pile[^1])) { _faceUp.Add(pile[^1]); _score += 5; }
            }
        }

        static bool CanStack(List<Card> pile, Card card, bool foundation)
        {
            if (foundation)
            {
                if (pile.Count == 0) return card.Rank == 1;
                var top = pile[^1];
                return top.Suit == card.Suit && card.Rank == top.Rank + 1;
            }

            if (pile.Count == 0) return card.Rank == 13;      // only a king starts a column
            var bottom = pile[^1];
            return bottom.Red != card.Red && card.Rank == bottom.Rank - 1;
        }

        void Win()
        {
            _won = true;
            Os.Audio.PlayUi(UiSfx.Notify);
            Raise("won", _score.ToString());
            RetroDialog.Alert(Os.Overlay, "Solitaire", $"You win.\n\nFinal score: {_score}");
        }
    }
}
