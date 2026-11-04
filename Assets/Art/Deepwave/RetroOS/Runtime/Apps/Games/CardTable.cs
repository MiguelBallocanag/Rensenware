using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps.Games
{
    public enum Suit { Clubs = 0, Diamonds = 1, Hearts = 2, Spades = 3 }

    // A single playing card. Rank 1 = ace, 11/12/13 = jack/queen/king.
    public struct Card : IEquatable<Card>
    {
        public Suit Suit;
        public int Rank;

        public bool Red => Suit is Suit.Diamonds or Suit.Hearts;

        public string RankText => Rank switch
        {
            1 => "A", 11 => "J", 12 => "Q", 13 => "K", _ => Rank.ToString()
        };

        public string SuitText => Suit switch
        {
            Suit.Clubs => "♣",
            Suit.Diamonds => "♦",
            Suit.Hearts => "♥",
            _ => "♠"
        };

        public bool Equals(Card other) => Suit == other.Suit && Rank == other.Rank;
        public override bool Equals(object obj) => obj is Card card && Equals(card);
        public override int GetHashCode() => (int)Suit * 100 + Rank;
        public override string ToString() => RankText + SuitText;
    }

    // Shared card-table helpers: a shuffled deck and the card face the two
    // patience games draw with, so both look like the same deck of cards.
    public static class CardTable
    {
        public const float CardWidth = 52f;
        public const float CardHeight = 72f;

        /// <summary>Makes a card answer a double-click and swallow the single click that precedes it, so lift and send-home are distinct gestures (unlike <see cref="RetroDoubleClick"/>).</summary>
        public static void AddDoubleClick(RectTransform card, Action action)
        {
            var relay = card.GetComponent<RetroPointerRelay>();
            if (relay == null) return;

            var single = relay.Clicked;
            var lastClick = 0f;
            relay.Clicked = () =>
            {
                if (Time.unscaledTime - lastClick < RetroClicks.Seconds) { action(); return; }
                lastClick = Time.unscaledTime;
                single?.Invoke();
            };
        }

        /// <summary>Drops the oldest snapshots until the undo stack is no deeper than <paramref name="depth"/> (rebuilds the Stack, which cannot drop from the bottom).</summary>
        public static void TrimUndo<T>(Stack<T> undo, int depth)
        {
            while (undo.Count > depth)
            {
                var kept = undo.ToArray();
                undo.Clear();
                for (var i = kept.Length - 2; i >= 0; i--) undo.Push(kept[i]);
            }
        }

        public static List<Card> NewDeck(bool shuffled = true)
        {
            var deck = new List<Card>(52);
            for (var suit = 0; suit < 4; suit++)
                for (var rank = 1; rank <= 13; rank++)
                    deck.Add(new Card { Suit = (Suit)suit, Rank = rank });

            if (shuffled) Shuffle(deck);
            return deck;
        }

        public static void Shuffle<T>(IList<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // Draws one card. faceUp = false gives the patterned back.
        public static RectTransform Draw(Transform parent, Card card, bool faceUp, Action onClick = null,
            float width = CardWidth, float height = CardHeight)
        {
            var root = RetroUi.NewRect(faceUp ? card.ToString() : "Card", parent);
            root.sizeDelta = new Vector2(width, height);

            var face = root.gameObject.AddComponent<Image>();
            if (faceUp) RetroPalette.PaintOff(face, Color.white);
            else RetroPalette.Paint(face, PaletteSlot.TitleActive);
            RetroUi.Bevel(root, true, 1f);

            if (!faceUp)
            {
                // Diagonal weave, drawn rather than typed, so the pattern does
                // not depend on the UI font having block characters.
                var inner = RetroUi.NewRect("Back", root);
                RetroUi.Stretch(inner, 4f);
                inner.gameObject.AddComponent<RectMask2D>();

                var ground = inner.gameObject.AddComponent<Image>();
                ground.color = new Color(1f, 1f, 1f, 0.12f);
                ground.raycastTarget = false;

                var span = width + height;
                for (var offset = -height; offset < width + height; offset += 7f)
                {
                    var stripe = RetroUi.NewRect("Weave", inner).gameObject.AddComponent<Image>();
                    stripe.color = new Color(1f, 1f, 1f, 0.28f);
                    stripe.raycastTarget = false;

                    var srt = stripe.rectTransform;
                    srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
                    srt.pivot = new Vector2(0.5f, 0.5f);
                    srt.sizeDelta = new Vector2(2f, span);
                    srt.anchoredPosition = new Vector2(offset, 0f);
                    srt.localEulerAngles = new Vector3(0f, 0f, 45f);
                }
            }
            else
            {
                var color = card.Red ? new Color(0.75f, 0f, 0f) : Color.black;

                var corner = RetroUi.Label(root, card.RankText + "\n" + card.SuitText, 12,
                    TextAnchor.UpperLeft);
                corner.color = color;
                corner.fontStyle = FontStyle.Bold;
                RetroUi.Stretch(corner.rectTransform, 3f);

                var pip = RetroUi.Label(root, card.SuitText, Mathf.RoundToInt(height * 0.42f),
                    TextAnchor.MiddleCenter);
                pip.color = color;
                RetroUi.Stretch(pip.rectTransform);

                // Court cards get a letter in the middle instead of a big pip.
                if (card.Rank >= 11)
                {
                    pip.text = card.RankText;
                    pip.fontStyle = FontStyle.Bold;
                }
            }

            if (onClick != null) root.gameObject.AddComponent<RetroPointerRelay>().Clicked = onClick;
            return root;
        }

        // Empty slot outline (a foundation or a free cell waiting for a card).
        public static RectTransform Slot(Transform parent, string caption, Action onClick = null,
            float width = CardWidth, float height = CardHeight)
        {
            var root = RetroUi.NewRect("Slot", parent);
            root.sizeDelta = new Vector2(width, height);

            var bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0.35f, 0.15f);
            RetroUi.Bevel(root, false, 1f);

            if (!string.IsNullOrEmpty(caption))
            {
                var label = RetroUi.Label(root, caption, 18, TextAnchor.MiddleCenter);
                RetroPalette.PaintOff(label, new Color(1f, 1f, 1f, 0.3f));
                RetroUi.Stretch(label.rectTransform);
            }

            if (onClick != null) root.gameObject.AddComponent<RetroPointerRelay>().Clicked = onClick;
            return root;
        }

        // The green baize both patience games are played on.
        public static RectTransform Baize(Transform parent)
        {
            var felt = RetroUi.Panel(parent, new Color(0f, 0.42f, 0.18f), "Table");
            RetroUi.Stretch(felt.rectTransform);
            RetroUi.Flexible(felt);
            return felt.rectTransform;
        }
    }
}
