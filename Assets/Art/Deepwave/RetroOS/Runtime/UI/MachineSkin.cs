using System;
using UnityEngine;

namespace Deepwave.RetroOS.UI
{
    /// <summary>
    /// One machine's look - theme, colour scheme, widget sounds - pressed into
    /// the process-wide <see cref="RetroSkin"/> as the machine builds its
    /// interface and when the player turns to it. Lets two machines on one desk
    /// keep their own schemes.
    /// </summary>
    public class MachineSkin
    {
        readonly Func<Transform> _root;

        /// <summary>The theme asset this machine boots with.</summary>
        public RetroTheme Theme { get; }

        /// <summary>The scheme over the theme, or <c>null</c> for the theme's own. What Control Panel changes and the machine remembers between sessions.</summary>
        public RetroColorScheme Scheme { get; private set; }

        /// <summary>Whether this machine's widgets click and beep, or <c>null</c> for the theme's setting.</summary>
        public bool? Sounds { get; private set; }

        /// <param name="root">The machine's work area, read on repaint. A function because the skin is built before the OS has a screen.</param>
        public MachineSkin(RetroTheme theme, Func<Transform> root = null)
        {
            Theme = theme;
            _root = root;
            Book.Owner = root;
        }

        /// <summary>What this machine has drawn and which palette slot each part used, so it can be redrawn in another scheme.</summary>
        public PaletteBook Book { get; } = new();

        /// <summary>Makes this the look interface is built with from here on - colours and book. Called on boot and when the player turns to the machine.</summary>
        public void Claim() => RetroSkin.Use(Theme, Scheme, Sounds, Book);

        /// <summary>
        /// Claims this skin for the length of <paramref name="work"/>, then
        /// restores the previous holder. For work triggered by another machine
        /// (e.g. a network event), where a bare <see cref="Claim"/> would leave
        /// the wrong skin active in both directions.
        /// </summary>
        public void While(Action work)
        {
            if (work == null) return;

            var held = RetroSkin.Held;
            Claim();
            try
            {
                work();
            }
            finally
            {
                RetroSkin.Give(held);
            }
        }

        /// <summary>Changes this machine's colour scheme and repaints its screen only. The scheme is installed first, then each graphic's recorded slot is re-read from the palette.</summary>
        public void Repaint(RetroColorScheme scheme)
        {
            Scheme = scheme;
            Claim();
            RetroPalette.Repaint(_root?.Invoke());
        }

        /// <summary>Turns this machine's interface sounds on or off.</summary>
        public void SetSounds(bool on)
        {
            Sounds = on;
            RetroSkin.WidgetSounds = on;
        }

        /// <summary>Restores a remembered scheme before anything is drawn. Silent - nothing to repaint yet.</summary>
        public void Restore(RetroColorScheme scheme)
        {
            Scheme = scheme;
            Claim();
        }
    }
}
