using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI
{
    /// <summary>
    /// Which palette slot each graphic on one machine's screen was painted from,
    /// so the screen can be repainted in another scheme. One book per machine,
    /// reached via <see cref="RetroSkin.Book"/>.
    /// </summary>
    public class PaletteBook
    {
        readonly Dictionary<Graphic, Entry> _slots = new();

        // The slot a graphic was painted from, plus the colour, so Repaint can
        // tell whether it still wears what the book gave it.
        struct Entry
        {
            public PaletteSlot Slot;
            public Color Painted;
        }

        /// <summary>The owning machine's work area. Editor-only check: a graphic written here but not on this screen means its build forgot to claim the machine's skin.</summary>
        public Func<Transform> Owner { get; set; }

        // A gradient paints two slots, so it is repainted separately from a plain graphic.
        readonly Dictionary<RetroGradient, (PaletteSlot From, PaletteSlot To)> _gradients = new();

        /// <summary>How many graphics this machine is keeping track of.</summary>
        public int Count => _slots.Count;

        /// <summary>Records that <paramref name="graphic"/> is painted from <paramref name="slot"/>; a new slot replaces the old (e.g. a bevel flipping raised/sunken).</summary>
        public void Record(Graphic graphic, PaletteSlot slot)
        {
            if (!graphic) return;
            if (slot == PaletteSlot.None) { _slots.Remove(graphic); return; }
            WarnIfStranger(graphic);
            _slots[graphic] = new Entry { Slot = slot, Painted = graphic.color };
            SweepIfDue();
        }

        // How many graphics may be written down between sweeps.
        const int SweepEvery = 256;

        int _sinceSweep;

        // Entries only leave the book in Repaint, which most games never run, so
        // the book is the last reference to graphics destroyed as windows close
        // and lists refill. Sweep on an interval, not every write, to keep it screen-sized.
        void SweepIfDue()
        {
            if (++_sinceSweep < SweepEvery) return;
            _sinceSweep = 0;

            List<Graphic> dead = null;
            foreach (var pair in _slots)
                if (!pair.Key) (dead ??= new List<Graphic>()).Add(pair.Key);
            if (dead != null)
                foreach (var graphic in dead) _slots.Remove(graphic);

            List<RetroGradient> deadGradients = null;
            foreach (var pair in _gradients)
                if (!pair.Key) (deadGradients ??= new List<RetroGradient>()).Add(pair.Key);
            if (deadGradients != null)
                foreach (var gradient in deadGradients) _gradients.Remove(gradient);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        void WarnIfStranger(Graphic graphic)
        {
            var root = Owner?.Invoke();
            if (!root || _slots.ContainsKey(graphic)) return;
            if (graphic.transform.IsChildOf(root)) return;

            Debug.LogWarning(
                $"RetroOS: '{graphic.name}' was painted from the palette while '{root.name}' " +
                "held it, but it is not on that screen. Whatever built it did not claim its " +
                "own machine's skin first, so it is wearing the other computer's colours and " +
                "will be repainted with the other computer's windows.", graphic);
        }

        /// <summary>Writes down both ends of a gradient.</summary>
        public void Record(RetroGradient gradient, PaletteSlot from, PaletteSlot to)
        {
            if (!gradient) return;
            if (from == PaletteSlot.None && to == PaletteSlot.None)
            {
                _gradients.Remove(gradient);
                return;
            }
            _gradients[gradient] = (from, to);
        }

        /// <summary>Drops a graphic from the book - for something deliberately repainted off-palette that Repaint must leave alone.</summary>
        public void Forget(Graphic graphic)
        {
            if (graphic) _slots.Remove(graphic);
        }

        /// <summary>What a graphic was painted with, if this book knows.</summary>
        public bool TryGet(Graphic graphic, out PaletteSlot slot)
        {
            var known = _slots.TryGetValue(graphic, out var entry);
            slot = known ? entry.Slot : PaletteSlot.None;
            return known;
        }

        /// <summary>
        /// Repaints every tracked graphic from the current palette. Drops
        /// graphics that were destroyed or repainted off-book. Caller has
        /// already made this machine's skin current.
        /// </summary>
        public void Repaint()
        {
            List<Graphic> dead = null;
            List<Graphic> repainted = null;
            List<Entry> entries = null;

            foreach (var pair in _slots)
            {
                var graphic = pair.Key;
                if (!graphic) { (dead ??= new List<Graphic>()).Add(graphic); continue; }

                // Alpha is out of the comparison: a fading window still wears the book's colour.
                if (!SameRgb(graphic.color, pair.Value.Painted))
                {
                    (dead ??= new List<Graphic>()).Add(graphic);
                    continue;
                }

                var now = RetroSkin.Read(pair.Value.Slot);
                // Alpha belongs to the graphic, not the scheme.
                var colour = new Color(now.r, now.g, now.b, graphic.color.a);
                graphic.color = colour;

                (repainted ??= new List<Graphic>()).Add(graphic);
                (entries ??= new List<Entry>()).Add(
                    new Entry { Slot = pair.Value.Slot, Painted = colour });
            }

            if (dead != null)
                foreach (var graphic in dead) _slots.Remove(graphic);

            if (repainted != null)
                for (var i = 0; i < repainted.Count; i++) _slots[repainted[i]] = entries[i];

            List<RetroGradient> deadGradients = null;
            foreach (var pair in _gradients)
            {
                var gradient = pair.Key;
                if (!gradient) { (deadGradients ??= new List<RetroGradient>()).Add(gradient); continue; }

                var (from, to) = pair.Value;
                gradient.Set(RetroSkin.Read(from), RetroSkin.Read(to));
            }

            if (deadGradients != null)
                foreach (var gradient in deadGradients) _gradients.Remove(gradient);
        }

        static bool SameRgb(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.002f &&
            Mathf.Abs(a.g - b.g) < 0.002f &&
            Mathf.Abs(a.b - b.b) < 0.002f;

        /// <summary>Empties the book. For teardown; a repaint must never call this.</summary>
        public void Clear()
        {
            _slots.Clear();
            _gradients.Clear();
        }
    }
}
