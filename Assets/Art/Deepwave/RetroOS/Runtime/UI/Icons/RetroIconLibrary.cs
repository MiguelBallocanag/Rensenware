using UnityEngine;

namespace Deepwave.RetroOS.UI.Icons
{
    // Paints every built-in icon: a few IconCanvas primitives per entry, not a bitmap.
    public static class RetroIconLibrary
    {
        public static void Paint(BuiltinIcon icon, IconCanvas c, bool vivid)
        {
            switch (icon)
            {
                // --- system ---
                case BuiltinIcon.Computer: Computer(c, vivid); break;
                case BuiltinIcon.Monitor: Monitor(c, vivid); break;
                case BuiltinIcon.Terminal: Terminal(c, vivid); break;
                case BuiltinIcon.Drive: Drive(c, vivid); break;
                case BuiltinIcon.Floppy: Floppy(c, vivid); break;
                case BuiltinIcon.CdRom: CdRom(c, vivid); break;
                case BuiltinIcon.Disc: Disc(c, vivid); break;
                case BuiltinIcon.Chip: Chip(c, vivid); break;
                case BuiltinIcon.Server: Server(c, vivid); break;
                case BuiltinIcon.Battery: Battery(c, vivid); break;
                case BuiltinIcon.Power: Power(c, vivid); break;

                // --- files ---
                case BuiltinIcon.Folder: Folder(c, vivid, open: false); break;
                case BuiltinIcon.FolderOpen: Folder(c, vivid, open: true); break;
                case BuiltinIcon.Note: Note(c, vivid); break;
                case BuiltinIcon.Book: Book(c, vivid); break;
                case BuiltinIcon.Database: Database(c, vivid); break;
                case BuiltinIcon.Trash: Trash(c, vivid, full: false); break;
                case BuiltinIcon.TrashFull: Trash(c, vivid, full: true); break;

                // --- network ---
                case BuiltinIcon.Globe: Globe(c, vivid); break;
                case BuiltinIcon.Network: Network(c, vivid); break;
                case BuiltinIcon.Mail: Mail(c, vivid); break;
                case BuiltinIcon.Phone: Phone(c, vivid); break;
                case BuiltinIcon.Download: Download(c, vivid); break;
                case BuiltinIcon.Refresh: Refresh(c, vivid); break;
                case BuiltinIcon.Home: Home(c, vivid); break;
                case BuiltinIcon.Search: Search(c, vivid); break;

                // --- media ---
                case BuiltinIcon.Camera: Camera(c, vivid); break;
                case BuiltinIcon.Photos: Photos(c, vivid); break;
                case BuiltinIcon.Movie: Movie(c, vivid); break;
                case BuiltinIcon.Music: Music(c, vivid); break;
                case BuiltinIcon.Speaker: Speaker(c, vivid); break;
                case BuiltinIcon.Cassette: Cassette(c, vivid); break;
                case BuiltinIcon.Microphone: Microphone(c, vivid); break;
                case BuiltinIcon.Paint: Paint(c, vivid); break;

                // --- peripherals ---
                case BuiltinIcon.Printer: Printer(c, vivid); break;
                case BuiltinIcon.Keyboard: Keyboard(c, vivid); break;
                case BuiltinIcon.Mouse: Mouse(c, vivid); break;
                case BuiltinIcon.Joystick: Joystick(c, vivid); break;

                // --- tools / productivity ---
                case BuiltinIcon.Clock: Clock(c, vivid); break;
                case BuiltinIcon.Calendar: Calendar(c, vivid); break;
                case BuiltinIcon.Calculator: Calculator(c, vivid); break;
                case BuiltinIcon.Contacts: Contacts(c, vivid); break;
                case BuiltinIcon.Chart: Chart(c, vivid); break;
                case BuiltinIcon.Settings: Settings(c, vivid); break;
                case BuiltinIcon.Tools: Tools(c, vivid); break;
                case BuiltinIcon.Flag: Flag(c, vivid); break;
                case BuiltinIcon.Star: Star(c, vivid); break;

                // --- security ---
                case BuiltinIcon.Key: Key(c, vivid); break;
                case BuiltinIcon.Lock: Lock(c, vivid); break;
                case BuiltinIcon.Shield: Shield(c, vivid); break;
                case BuiltinIcon.Eye: Eye(c, vivid); break;

                // --- dialogs ---
                case BuiltinIcon.Warning: Warning(c, vivid); break;
                case BuiltinIcon.Error: Error(c, vivid); break;
                case BuiltinIcon.Info: Info(c, vivid); break;
                case BuiltinIcon.Question: Question(c, vivid); break;

                // --- games / story ---
                case BuiltinIcon.Bomb: Bomb(c, vivid); break;
                case BuiltinIcon.Cards: Cards(c, vivid); break;
                case BuiltinIcon.Dice: Dice(c, vivid); break;
                case BuiltinIcon.Ghost: Ghost(c, vivid); break;
                case BuiltinIcon.Skull: Skull(c, vivid); break;

                // --- browser ---
                case BuiltinIcon.Back: Arrow(c, vivid, right: false); break;
                case BuiltinIcon.Forward: Arrow(c, vivid, right: true); break;
                case BuiltinIcon.Stop: Stop(c, vivid); break;
                case BuiltinIcon.Reload: Reload(c, vivid); break;
                case BuiltinIcon.Bookmark: Bookmark(c, vivid); break;
                case BuiltinIcon.BrokenImage: BrokenImage(c, vivid); break;

                // --- programs ---
                case BuiltinIcon.Board: Board(c, vivid); break;
                case BuiltinIcon.Picture: Picture(c, vivid); break;
                case BuiltinIcon.Explorer: Explorer(c, vivid); break;
                case BuiltinIcon.FreeCell: FreeCell(c, vivid); break;

                default: File(c, vivid); break;
            }
        }

        // --- system ---

        static void Computer(IconCanvas c, bool vivid)
        {
            // The 1995 desktop: a CRT on the case with a keyboard, all three-quarter view.
            if (vivid) c.Shadow(4f, 20f, 25f, 8f);

            // Case, then the monitor standing on its lid.
            c.Box(3f, 20f, 21f, 5f, 3f, Palette.Silver, Palette.LightSilver, Palette.Gray);
            c.Fill(5f, 22f, 11f, 1f, Palette.Gray);        // drive slot
            c.Fill(20f, 22f, 2f, 2f, Palette.Green);       // power lamp

            c.Box(5f, 5f, 18f, 12f, 3f, Palette.Silver, Palette.LightSilver, Palette.Gray);
            c.Fill(7f, 7f, 14f, 8f, Palette.Black);
            c.Fill(8f, 8f, 12f, 6f, Palette.Navy);
            if (vivid)
            {
                c.Fill(9f, 9f, 5f, 1f, Palette.Cyan);
                c.Fill(9f, 11f, 3f, 1f, Palette.Cyan);
                c.Fill(8f, 8f, 12f, 1f, new Color32(40, 40, 200, 255));
            }
            c.Fill(7f, 15f, 5f, 1f, Palette.Gray);         // brand strip

            // Keyboard, a shallow wedge in front of the case.
            c.Box(3f, 26f, 14f, 2f, 2f, Palette.Silver, Palette.LightSilver, Palette.Gray);
            for (var i = 0; i < 5; i++) c.Fill(5f + i * 2.4f, 25f, 1.4f, 1f, Palette.Gray);
        }

        static void Monitor(IconCanvas c, bool vivid)
        {
            // The screen alone: the same three-quarter CRT as Computer, no case.
            if (vivid) c.Shadow(4f, 8f, 25f, 17f);
            c.Box(3f, 7f, 22f, 16f, 3f, Palette.Silver, Palette.LightSilver, Palette.Gray);
            c.Fill(5f, 9f, 18f, 11f, Palette.Black);
            c.Gradient(6f, 10f, 16f, 9f, Palette.Navy, new Color32(0, 0, 80, 255));
            if (vivid)
            {
                c.Fill(6f, 10f, 16f, 1f, new Color32(60, 60, 220, 255));
                c.Fill(7f, 12f, 5f, 4f, new Color32(120, 160, 255, 255));
                c.Fill(13f, 14f, 6f, 3f, Palette.Cyan);
            }
            c.Fill(5f, 20f, 4f, 1f, Palette.Gray);
            c.Fill(21f, 20f, 2f, 2f, Palette.Green);

            c.Fill(12f, 23f, 6f, 3f, Palette.Gray);        // neck
            c.Rect(12f, 23f, 6f, 3f, Palette.Black);
            c.Box(8f, 27f, 13f, 2f, 2f, Palette.Silver, Palette.LightSilver, Palette.Gray);
        }

        static void Terminal(IconCanvas c, bool vivid)
        {
            // A console window, face on - Computer and Monitor are already three-quarter CRTs.
            c.Rect(2f, 4f, 28f, 24f, Palette.Black);
            c.Fill(3f, 5f, 26f, 22f, Palette.Silver);
            c.Fill(3f, 5f, 26f, 1f, Palette.White);
            c.Fill(3f, 26f, 26f, 1f, Palette.Gray);

            // Title bar with a caption block and two chrome buttons.
            c.Fill(4f, 6f, 24f, 4f, Palette.Navy);
            c.Fill(5f, 7f, 9f, 2f, Palette.White);
            c.Fill(21f, 7f, 3f, 2f, Palette.Silver);
            c.Fill(25f, 7f, 3f, 2f, Palette.Silver);

            // Client area: black, with the prompt spelt out in white.
            c.Fill(4f, 11f, 24f, 15f, Palette.Black);
            c.Fill(6f, 13f, 4f, 1f, Palette.White);        // C
            c.Fill(6f, 13f, 1f, 5f, Palette.White);
            c.Fill(6f, 17f, 4f, 1f, Palette.White);
            c.Fill(11f, 14f, 1f, 1f, Palette.White);       // :
            c.Fill(11f, 16f, 1f, 1f, Palette.White);
            c.Line(13f, 13f, 16f, 18f, Palette.White, 1f); // backslash
            c.Fill(18f, 17f, 4f, 1f, Palette.White);       // cursor
            c.Fill(6f, 21f, 14f, 1f, Palette.Silver);
            c.Fill(6f, 23f, 9f, 1f, Palette.Silver);
        }

        static void Drive(IconCanvas c, bool vivid)
        {
            // A hard disk three-quarters on: a wide slab with a front seam and a lamp.
            if (vivid) c.Shadow(4f, 15f, 24f, 9f);
            c.Box(3f, 14f, 22f, 9f, 4f, Palette.Silver, Palette.LightSilver, Palette.Gray);
            c.Fill(5f, 16f, 15f, 1f, Palette.Gray);
            c.Fill(5f, 17f, 15f, 1f, Palette.White);
            c.Fill(5f, 20f, 15f, 1f, Palette.Gray);
            c.Fill(22f, 19f, 2f, 2f, Palette.Green);
            if (vivid) c.Fill(4f, 13f, 20f, 1f, Palette.White);
        }

        static void Floppy(IconCanvas c, bool vivid)
        {
            // A 3.5" diskette, face on and light-bodied. Doubles as the Save glyph.
            if (vivid) c.Shadow(4f, 4f, 24f, 24f);
            c.Rect(3f, 3f, 25f, 26f, Palette.Black);
            c.Fill(4f, 4f, 23f, 24f, Palette.Silver);
            c.Fill(4f, 4f, 23f, 1f, Palette.White);
            c.Fill(4f, 4f, 1f, 24f, Palette.White);
            c.Fill(4f, 27f, 23f, 1f, Palette.Gray);
            c.Fill(26f, 4f, 1f, 24f, Palette.Gray);

            // Shutter: metal, with the write-protect block on its shoulder.
            c.Rect(10f, 3f, 12f, 10f, Palette.Black);
            c.Fill(11f, 4f, 10f, 8f, Palette.Gray);
            c.Fill(11f, 4f, 10f, 1f, Palette.LightSilver);
            c.Fill(17f, 4f, 4f, 8f, Palette.DarkGray);

            // Label.
            c.Rect(6f, 16f, 19f, 12f, Palette.Black);
            c.Fill(7f, 17f, 17f, 10f, Palette.Paper);
            c.Fill(9f, 19f, 13f, 1f, Palette.Gray);
            c.Fill(9f, 21f, 9f, 1f, Palette.Gray);
            c.Fill(9f, 23f, 11f, 1f, Palette.Gray);
        }

        static void CdRom(IconCanvas c, bool vivid)
        {
            // The drive as a three-quarter slab with the disc leaning on it.
            if (vivid) c.Shadow(4f, 21f, 24f, 7f);
            c.Box(3f, 20f, 22f, 7f, 4f, Palette.Silver, Palette.LightSilver, Palette.Gray);
            c.Fill(5f, 22f, 12f, 2f, Palette.Black);       // tray slot
            c.Fill(5f, 25f, 3f, 1f, Palette.Gray);
            c.Fill(22f, 24f, 2f, 2f, Palette.Amber);

            c.Disc(17f, 11f, 10f, Palette.Black);
            c.Disc(17f, 11f, 9f, Palette.LightSilver);
            c.DitherRing(17f, 11f, 9f, 9f, Palette.White, Palette.LightSilver);
            if (vivid) Spectrum(c, 17f, 11f, 3.5f, 8.5f);
            c.Disc(17f, 11f, 3.5f, Palette.Black);
            c.Disc(17f, 11f, 2.5f, Palette.Silver);
            c.Disc(17f, 11f, 1.2f, Palette.White);
        }

        static void Disc(IconCanvas c, bool vivid)
        {
            // A CD face-on: black outline, a shell spectrum of radial dashes over a checkered silver face.
            if (vivid) c.DiscShadow(16f, 16f, 13f, 1f);
            c.Disc(16f, 16f, 13.5f, Palette.Black);
            c.Disc(16f, 16f, 12.5f, Palette.LightSilver);
            c.DitherRing(16f, 16f, 12.5f, 12.5f, Palette.Silver, Palette.LightSilver);
            if (vivid) Spectrum(c, 16f, 16f, 5.5f, 12f);
            // The hub: black keyline, silver collar, white spindle hole.
            c.Disc(16f, 16f, 5f, Palette.Black);
            c.Disc(16f, 16f, 4f, Palette.Silver);
            c.Disc(16f, 16f, 2f, Palette.White);
            c.Ring(16f, 16f, 2f, Palette.Gray, 1f);
        }

        // The rainbow flare on a disc, as radial dashes in the upper left (a line stays inside the disc).
        static void Spectrum(IconCanvas c, float cx, float cy, float inner, float outer)
        {
            var hues = new[] { Palette.Cyan, Palette.Green, Palette.Yellow, Palette.Magenta };
            for (var i = 0; i < 4; i++)
            {
                var a = Mathf.PI * (0.66f + i * 0.14f);
                c.Line(cx + Mathf.Cos(a) * inner, cy + Mathf.Sin(a) * inner,
                    cx + Mathf.Cos(a) * outer, cy + Mathf.Sin(a) * outer,
                    hues[i], 2f);
            }
        }

        static void Chip(IconCanvas c, bool vivid)
        {
            // A processor three-quarters on, pins down the two near edges.
            for (var i = 0; i < 6; i++)
            {
                c.Fill(7f + i * 3f, 23f, 2f, 4f, Palette.Black);   // front pins
                c.Fill(7.5f + i * 3f, 23f, 1f, 3f, Palette.Gray);
                c.Fill(25f, 8f + i * 3f, 4f, 2f, Palette.Black);   // side pins
                c.Fill(25f, 8.5f + i * 3f, 3f, 1f, Palette.Gray);
            }
            c.Box(6f, 15f, 19f, 8f, 7f, new Color32(30, 30, 36, 255),
                Palette.DarkGray, new Color32(20, 20, 24, 255));
            // A gold die on the lid, sheared with it.
            for (var i = 0; i < 5; i++) c.Fill(11f + i, 12f - i, 8f, 1f, Palette.Amber);
            c.Fill(13f, 9f, 4f, 1f, Palette.Gold);
            if (vivid) c.Fill(7f, 15f, 18f, 1f, Palette.Gray);
        }

        static void Server(IconCanvas c, bool vivid)
        {
            // A tower case - the 1995 server was the big beige box under the desk.
            if (vivid) c.Shadow(9f, 7f, 14f, 20f);
            c.Box(8f, 6f, 14f, 21f, 4f, Palette.Silver, Palette.LightSilver, Palette.Gray);
            for (var i = 0; i < 2; i++)
            {
                var y = 8f + i * 4f;
                c.Fill(10f, y, 10f, 3f, Palette.Gray);      // drive bays
                c.Rect(10f, y, 10f, 3f, Palette.DarkGray);
                c.Fill(11f, y + 1f, 5f, 1f, Palette.LightSilver);
            }
            c.Fill(10f, 17f, 10f, 1f, Palette.Gray);
            c.Fill(10f, 23f, 4f, 2f, Palette.Green);
            c.Fill(16f, 23f, 2f, 2f, Palette.Amber);
            if (vivid) c.Fill(9f, 6f, 12f, 1f, Palette.White);
        }

        static void Battery(IconCanvas c, bool vivid)
        {
            // A cell standing on end (a flat bar in a rectangle would read as a gauge).
            c.Fill(9f, 4f, 3f, 3f, Palette.Black);          // terminals
            c.Fill(14f, 3f, 5f, 4f, Palette.Black);
            c.Fill(15f, 4f, 3f, 3f, Palette.Gold);

            c.Ellipse(16f, 7f, 8f, 3f, Palette.Black);      // top rim
            c.Fill(8f, 7f, 17f, 20f, Palette.Black);
            c.Ellipse(16f, 27f, 8f, 3f, Palette.Black);

            c.Ellipse(16f, 7f, 7f, 2f, Palette.LightSilver);
            c.Fill(9f, 7f, 15f, 19f, Palette.Silver);
            c.Ellipse(16f, 26f, 7f, 2f, Palette.Gray);
            c.Dither(9f, 12f, 15f, 8f, Palette.Gold, Palette.Amber);
            c.Fill(9f, 11f, 15f, 1f, Palette.DarkGray);
            c.Fill(9f, 20f, 15f, 1f, Palette.DarkGray);
            if (vivid)
            {
                c.Fill(10f, 8f, 2f, 18f, Palette.White);
                c.Fill(22f, 8f, 1f, 18f, Palette.Gray);
            }
        }

        static void Power(IconCanvas c, bool vivid)
        {
            // Black rim, grey bevel inside: a silver button on a silver toolbar is invisible.
            c.Disc(16f, 16f, 13.5f, Palette.Black);
            c.Disc(16f, 16f, 12.5f, Palette.Silver);
            c.Ring(16f, 16f, 12.5f, Palette.Gray, 1.5f);
            c.Ring(16f, 17f, 8f, Palette.DarkGreen, 2.5f);
            c.Fill(13f, 5f, 6f, 8f, Palette.Silver);
            c.Fill(14.5f, 5f, 3f, 10f, Palette.DarkGreen);
            if (vivid) c.Sheen(6f, 6f, 20f, 6f, 45);
        }

        // --- files ---

        static void File(IconCanvas c, bool vivid)
        {
            if (vivid) c.Shadow(7f, 3f, 18f, 26f);
            Sheet(c, 6f, 2f, 19f, 27f, 6f);
            for (var i = 0; i < 6; i++) c.Fill(9f, 11f + i * 3f, 13f, 1f, Palette.Gray);
        }

        // A sheet of paper with the top-right corner turned down. The shell's document shape; five icons reuse it.
        static void Sheet(IconCanvas c, float x, float y, float w, float h, float fold)
        {
            var f = Mathf.Max(2, Mathf.RoundToInt(fold));
            for (var j = 0; j < f; j++) c.Fill(x, y + j, w - f + j + 1f, 1f, Palette.Black);
            c.Fill(x, y + f, w, h - f, Palette.Black);

            for (var j = 1; j < f; j++) c.Fill(x + 1f, y + j, w - f + j - 1f, 1f, Palette.Paper);
            c.Fill(x + 1f, y + f, w - 2f, h - f - 1f, Palette.Paper);

            // The turned-down corner, shaded to read as the back of the page.
            for (var j = 1; j <= f - 1; j++)
                c.Fill(x + w - f, y + j, j, 1f, Palette.Silver);
            c.Fill(x + w - f, y + f - 1f, f - 1f, 1f, Palette.Gray);
        }

        static void Folder(IconCanvas c, bool vivid, bool open)
        {
            // The shell folder: a Yellow/White checkerboard in a black outline, tab left under a silver cap.
            if (vivid) c.Shadow(2f, 9f, 25f, 17f);

            if (open)
            {
                // Tab and back panel, standing up behind the flap.
                Tab(c, 3f, 4f);
                c.Fill(2f, 7f, 22f, 15f, Palette.Black);
                c.Dither(3f, 8f, 20f, 13f, Palette.Yellow, Palette.White);
                c.Fill(3f, 8f, 20f, 1f, Palette.White);

                // A sheet lifting out of it.
                c.Fill(9f, 5f, 15f, 12f, Palette.Black);
                c.Fill(10f, 6f, 13f, 10f, Palette.Paper);
                for (var i = 0; i < 3; i++) c.Fill(12f, 8f + i * 3f, 9f, 1f, Palette.Gray);

                // Front flap swung open: a right-leaning parallelogram, black-outlined first.
                for (var i = 0; i < 14; i++)
                {
                    var slide = i * 0.7f;
                    c.Fill(2f + slide, 15f + i, 25f - slide, 1f, Palette.Black);
                }
                for (var i = 1; i < 13; i++)
                {
                    var slide = i * 0.7f;
                    c.Dither(3f + slide, 15f + i, 23f - slide, 1f, Palette.Yellow, Palette.White);
                }
                c.Fill(3f, 15f, 23f, 1f, Palette.White);
                return;
            }

            Tab(c, 3f, 5f);
            c.Fill(2f, 8f, 27f, 20f, Palette.Black);
            c.Dither(3f, 9f, 25f, 18f, Palette.Yellow, Palette.White);
            c.Fill(3f, 9f, 25f, 1f, Palette.White);        // lit top edge
            c.Fill(3f, 9f, 1f, 18f, Palette.White);
            c.Fill(3f, 26f, 25f, 1f, Palette.Olive);       // shaded bottom
            c.Fill(27f, 9f, 1f, 18f, Palette.Olive);
        }

        // The folder tab: a stub above the top-left corner, capped in silver.
        static void Tab(IconCanvas c, float x, float top)
        {
            c.Fill(x - 1f, top - 1f, 14f, 6f, Palette.Black);
            c.Fill(x, top, 12f, 5f, Palette.Silver);
            c.Fill(x, top, 12f, 1f, Palette.White);
            c.Dither(x, top + 2f, 12f, 3f, Palette.Yellow, Palette.White);
        }

        static void Note(IconCanvas c, bool vivid)
        {
            // A spiral pad - the shell's text-file glyph, distinct from plain File.
            if (vivid) c.Shadow(5f, 4f, 20f, 24f);
            c.Fill(5f, 4f, 22f, 25f, Palette.Black);
            c.Fill(6f, 5f, 20f, 23f, Palette.Paper);
            c.Fill(6f, 5f, 20f, 4f, Palette.Silver);       // the board at the top
            c.Fill(6f, 8f, 20f, 1f, Palette.Gray);

            // The wire: a loop over the head of the pad for every third unit.
            for (var i = 0; i < 5; i++)
            {
                var x = 8f + i * 4f;
                c.Fill(x, 2f, 2f, 5f, Palette.Black);
                c.Fill(x + 0.5f, 2.5f, 1f, 4f, Palette.Silver);
            }
            for (var i = 0; i < 5; i++) c.Fill(8f, 12f + i * 3f, 16f, 1f, Palette.Gray);
            c.Fill(8f, 12f, 10f, 1f, Palette.Black);
        }

        static void Book(IconCanvas c, bool vivid)
        {
            // A closed book three-quarters on: cover for the lid, cut page edges for the face.
            if (vivid) c.Shadow(5f, 15f, 22f, 11f);
            c.Box(4f, 14f, 21f, 11f, 6f, Palette.Paper, Palette.Purple,
                new Color32(80, 0, 96, 255));
            for (var i = 0; i < 4; i++) c.Fill(5f, 16f + i * 2.4f, 19f, 1f, Palette.Silver);

            // Spine down the near edge, and the cover lit along its back edge.
            c.Fill(4f, 14f, 2f, 11f, Palette.Purple);
            c.Fill(4f, 14f, 21f, 1f, new Color32(80, 0, 96, 255));
            if (vivid) for (var i = 0; i < 6; i++) c.Fill(6f + i, 13f - i, 15f, 1f,
                i == 0 ? Palette.Lavender : Palette.Purple);
        }

        static void Database(IconCanvas c, bool vivid)
        {
            // Two drums, checkerboarded the way the shell drew a data store.
            Drum(c, 4f, 10f, vivid);
            Drum(c, 16f, 10f, vivid);
        }

        // One drum: black silhouette, dithered barrel, lit lid.
        static void Drum(IconCanvas c, float top, float h, bool vivid)
        {
            c.Ellipse(16f, top, 11f, 3.5f, Palette.Black);
            c.Fill(5f, top, 22f, h, Palette.Black);
            c.Ellipse(16f, top + h, 11f, 3.5f, Palette.Black);

            c.Ellipse(16f, top + h, 10f, 2.8f, Palette.Olive);
            c.Dither(6f, top, 20f, h, Palette.Yellow, Palette.White);
            c.Dither(20f, top, 6f, h, Palette.Yellow, Palette.Olive);   // shaded flank
            c.Ellipse(16f, top, 10f, 2.8f, Palette.Yellow);
            c.EllipseRing(16f, top, 10f, 2.8f, Palette.Olive);
            if (vivid) c.Ellipse(12f, top - 0.5f, 3f, 0.8f, Palette.White);
        }

        static void Trash(IconCanvas c, bool vivid, bool full)
        {
            // An open basket tapering to its foot, three-quarters on - the shell bin.
            if (c.Small)
            {
                // Two boxes and a mouth, not a tapered wall: the taper turns to a blob at 16px.
                c.Fill(7f, 8f, 18f, 11f, Palette.Black);
                c.Fill(9f, 19f, 14f, 10f, Palette.Black);
                c.Fill(9f, 9f, 14f, 10f, Palette.Silver);
                c.Fill(11f, 19f, 10f, 9f, Palette.Silver);
                c.Ellipse(16f, 8f, 9f, 3f, Palette.Black);
                c.Ellipse(16f, 8f, 7f, 2f, Palette.DarkGray);
                if (full)
                {
                    c.Fill(9f, 2f, 7f, 7f, Palette.Black);
                    c.Fill(10f, 3f, 5f, 5f, Palette.Paper);
                    c.Fill(17f, 3f, 7f, 6f, Palette.Black);
                    c.Fill(18f, 4f, 5f, 4f, Palette.SkyBlue);
                }
                Recycle(c, 16f, 19f, 6f, Palette.Silver);
                return;
            }

            if (vivid) c.Shadow(9f, 9f, 15f, 18f);
            Basket(c, 0f, Palette.Black);
            Basket(c, 1f, Palette.Silver);
            for (var j = 0; j < 18; j++)
            {
                var half = Mathf.Lerp(9f, 6.5f, j / 18f);
                c.Fill(16f + half - 3f, 9f + j, 1f, 1f, Palette.Gray);
                c.Fill(16f - half + 2f, 9f + j, 1f, 1f, Palette.White);
            }

            // Rim: an oval mouth with the inside of the bin showing.
            c.Ellipse(16f, 9f, 10f, 3.5f, Palette.Black);
            c.Ellipse(16f, 9f, 9f, 2.5f, Palette.DarkGray);
            c.Ellipse(16f, 8.5f, 8f, 2f, full ? Palette.Silver : Palette.Gray);

            if (full)
            {
                c.Fill(9f, 3f, 6f, 6f, Palette.Black);
                c.Fill(10f, 4f, 5f, 5f, Palette.Paper);
                c.Fill(17f, 2f, 6f, 7f, Palette.Black);
                c.Fill(18f, 3f, 5f, 6f, Palette.SkyBlue);
                c.Fill(13f, 2f, 6f, 5f, Palette.Black);
                c.Dither(14f, 3f, 5f, 4f, Palette.Yellow, Palette.White);
            }

            Recycle(c, 16f, 19f, 6f, Palette.Silver);
            if (vivid) c.Fill(11f, 11f, 2f, 1f, Palette.White);
        }

        // The recycling mark: one heavy green triangular band with arrowheads on its corners.
        static void Recycle(IconCanvas c, float cx, float cy, float r, Color32 hollow)
        {
            float w = r * 2.2f, h = r * 2f;
            float x = cx - w * 0.5f, y = cy - h * 0.55f;
            // Green with no black under it: Triangle steps its edge, so a proud outline over-thickens the slopes.
            c.Triangle(x, y, w, h, Palette.DarkGreen, 0);
            if (c.Small) return;

            // Hollowed to an even band: corner nicks and a head come out as stray pixels at this size.
            c.Triangle(x + 2.4f, y + 4.1f, w - 4.8f, h - 6f, hollow, 0);
        }

        // The tapered wall of the bin, inset by the given number of units.
        static void Basket(IconCanvas c, float inset, Color32 fill)
        {
            for (var j = 0; j < 20 - inset; j++)
            {
                var half = Mathf.Lerp(9.5f, 7f, j / 20f) - inset;
                c.Fill(16f - half, 9f + j, half * 2f, 1f, fill);
            }
        }

        // --- network ---

        static void Globe(IconCanvas c, bool vivid)
        {
            if (vivid) c.DiscShadow(16f, 16f, 13f, 1f);
            c.Disc(16f, 16f, 14f, Palette.Black);
            c.Disc(16f, 16f, 13f, Palette.Blue);
            // Ocean: two blues checkerboarded for a third colour.
            c.DitherRing(16f, 16f, 13f, 13f, Palette.SkyBlue, Palette.Blue);

            // Continents blocked in like 90s clip-art, kept inside the rim. No meridian cross (reads as a gunsight).
            Land(c, 8f, 8f, 8f, 5f);
            Land(c, 11f, 13f, 5f, 7f);
            Land(c, 18f, 10f, 7f, 4f);
            Land(c, 19f, 16f, 5f, 5f);
            Land(c, 11f, 22f, 7f, 3f);
            c.Ring(16f, 16f, 13f, Palette.Navy, 1f);
            if (vivid)
            {
                c.Fill(9f, 6f, 5f, 1f, Palette.White);
                c.Fill(7f, 7f, 2f, 3f, Palette.White);
            }
        }

        static void Land(IconCanvas c, float x, float y, float w, float h)
        {
            c.Fill(x, y, w, h, Palette.DarkGreen);
            c.Dither(x, y, w, 1f, Palette.Green, Palette.DarkGreen);
        }

        static void Network(IconCanvas c, bool vivid)
        {
            // Two machines wired together, both three-quarters on.
            c.Fill(14f, 14f, 2f, 6f, Palette.Black);       // the cable between
            c.Fill(14.5f, 14f, 1f, 6f, Palette.Gray);

            Node(c, 2f, 4f, vivid);
            Node(c, 13f, 16f, vivid);
        }

        // One little computer: screen box on a case, at eleven units wide.
        static void Node(IconCanvas c, float x, float y, bool vivid)
        {
            c.Box(x + 1f, y, 11f, 8f, 2f, Palette.Silver, Palette.LightSilver, Palette.Gray);
            c.Fill(x + 2f, y + 1f, 9f, 5f, Palette.Black);
            c.Fill(x + 3f, y + 2f, 7f, 3f, Palette.Navy);
            if (vivid) c.Fill(x + 4f, y + 3f, 3f, 1f, Palette.Cyan);
            c.Box(x, y + 9f, 13f, 3f, 2f, Palette.Silver, Palette.LightSilver, Palette.Gray);
            c.Fill(x + 1f, y + 10f, 2f, 1f, Palette.Green);
        }

        static void Mail(IconCanvas c, bool vivid)
        {
            // The shell envelope: white paper, hard black outline, lining showing as sparse yellow dots.
            if (vivid) c.Shadow(2f, 7f, 26f, 17f);
            c.Fill(2f, 7f, 27f, 18f, Palette.Black);
            c.Fill(3f, 8f, 25f, 16f, Palette.Paper);

            // Lining: a quarter-density scatter, so the envelope still reads as white paper.
            for (var y = 0; y < 14; y++)
                for (var x = 0; x < 23; x++)
                    if ((x & 1) == 0 && (y & 1) == 0 && x + y > 8 && x - y < 15)
                        c.Fill(4f + x, 9f + y, 1f, 1f, Palette.Yellow);

            // The flap, folded down the middle, and the two back creases.
            c.Line(3f, 8f, 15.5f, 18f, Palette.Gray, 1f);
            c.Line(28f, 8f, 15.5f, 18f, Palette.Gray, 1f);
            c.Line(3f, 9f, 15.5f, 19f, Palette.Silver, 1f);
            c.Line(28f, 9f, 15.5f, 19f, Palette.Silver, 1f);
            c.Line(3f, 23f, 12f, 16f, Palette.Silver, 1f);
            c.Line(28f, 23f, 19f, 16f, Palette.Silver, 1f);
            if (vivid) c.Fill(3f, 8f, 25f, 1f, Palette.White);
        }

        static void Phone(IconCanvas c, bool vivid)
        {
            // A desk telephone in shell yellow, three-quarters on, handset across the cradle.
            if (vivid) c.Shadow(4f, 19f, 24f, 9f);
            c.Box(3f, 18f, 22f, 9f, 4f, Palette.Gold, Palette.Yellow, Palette.Amber);
            for (var r = 0; r < 2; r++)
                for (var i = 0; i < 3; i++)
                    c.Fill(6f + i * 5f, 20f + r * 4f, 4f, 3f, Palette.Black);
            c.Fill(21f, 20f, 2f, 2f, Palette.Maroon);

            // Cradle posts, so the handset sits on the phone rather than floating.
            c.Fill(6f, 12f, 4f, 5f, Palette.Black);
            c.Fill(21f, 10f, 4f, 5f, Palette.Black);
            c.Fill(7f, 12f, 2f, 5f, Palette.Amber);
            c.Fill(22f, 10f, 2f, 5f, Palette.Amber);

            // Handset: a bar with earpieces hooking down, black-outlined first.
            c.Fill(3f, 5f, 9f, 10f, Palette.Black);
            c.Fill(19f, 3f, 9f, 10f, Palette.Black);
            c.Fill(10f, 6f, 11f, 6f, Palette.Black);
            c.Fill(4f, 6f, 7f, 8f, Palette.Gold);
            c.Fill(20f, 4f, 7f, 8f, Palette.Gold);
            c.Fill(11f, 7f, 9f, 4f, Palette.Gold);
            c.Fill(5f, 9f, 5f, 4f, Palette.Amber);          // the earpiece cups
            c.Fill(21f, 7f, 5f, 4f, Palette.Amber);
            if (vivid)
            {
                c.Fill(4f, 6f, 7f, 1f, Palette.Yellow);
                c.Fill(20f, 4f, 7f, 1f, Palette.Yellow);
                c.Fill(11f, 7f, 9f, 1f, Palette.Yellow);
            }
        }

        static void Download(IconCanvas c, bool vivid)
        {
            // A red arrow into a drive - the shell colour for "coming in" (green is Refresh).
            c.Box(3f, 22f, 22f, 6f, 3f, Palette.Silver, Palette.LightSilver, Palette.Gray);
            c.Fill(5f, 24f, 12f, 1f, Palette.Gray);
            c.Fill(21f, 24f, 2f, 2f, Palette.Green);

            c.Fill(11f, 1f, 10f, 12f, Palette.Black);
            c.Triangle(6f, 12f, 20f, 10f, Palette.Black, 1);
            c.Fill(12f, 2f, 8f, 11f, Palette.Red);
            c.Triangle(8f, 12.5f, 16f, 8f, Palette.Red, 1);
            if (vivid)
            {
                c.Fill(12f, 2f, 2f, 11f, Palette.Salmon);
                c.Fill(19f, 2f, 1f, 11f, Palette.Maroon);
            }
        }

        static void Refresh(IconCanvas c, bool vivid)
        {
            // Two blue arrows chasing round a circle (one green arrow would be Reload).
            c.Ring(16f, 16f, 12f, Palette.Black, 5.5f);
            c.Ring(16f, 16f, 11f, Palette.Blue, 3.5f);
            if (vivid) c.Ring(16f, 16f, 11f, Palette.SkyBlue, 1.5f);

            c.Erase(16f, 1f, 16f, 11f);       // gap at the top right
            c.Erase(0f, 20f, 16f, 12f);       // gap at the bottom left
            c.Triangle(15f, 2f, 12f, 11f, Palette.Black, 2);
            c.Triangle(16f, 3.5f, 10f, 8f, Palette.Blue, 2);
            c.Triangle(5f, 19f, 12f, 11f, Palette.Black, 3);
            c.Triangle(6f, 20.5f, 10f, 8f, Palette.Blue, 3);
        }

        static void Home(IconCanvas c, bool vivid)
        {
            // A house with a dithered roof over white walls - the shell home page.
            if (vivid) c.Shadow(4f, 5f, 25f, 23f);
            c.Fill(23f, 4f, 4f, 8f, Palette.Black);        // chimney
            c.Fill(24f, 5f, 2f, 7f, Palette.Maroon);

            c.Triangle(1f, 3f, 30f, 14f, Palette.Black, 0);
            c.Triangle(3f, 5f, 26f, 11f, Palette.Yellow, 0);
            for (var j = 2; j < 11; j++)
                c.Dither(16f - j * 1.18f, 5f + j, j * 2.36f, 1f, Palette.Yellow, Palette.White);

            c.Fill(5f, 16f, 22f, 14f, Palette.Black);
            c.Fill(6f, 17f, 20f, 12f, Palette.Paper);
            c.Fill(6f, 17f, 20f, 1f, Palette.White);
            c.Fill(13f, 21f, 7f, 8f, Palette.Black);       // door
            c.Fill(14f, 22f, 5f, 7f, Palette.Maroon);
            c.Fill(17.5f, 25f, 1f, 1f, Palette.Gold);
            c.Fill(7f, 20f, 5f, 5f, Palette.Black);        // windows
            c.Fill(8f, 21f, 3f, 3f, Palette.SkyBlue);
            c.Fill(21f, 20f, 5f, 5f, Palette.Black);
            c.Fill(22f, 21f, 3f, 3f, Palette.SkyBlue);
        }

        static void Search(IconCanvas c, bool vivid)
        {
            // A magnifier: black rim, blue barrel, glass checkerboarded cyan.
            c.Line(17f, 17f, 28f, 28f, Palette.Black, 6f);
            c.Line(18f, 18f, 27f, 27f, Palette.Navy, 4f);
            c.Line(18f, 18f, 26f, 26f, Palette.Blue, 2f);

            c.Disc(13f, 13f, 10f, Palette.Black);
            c.Disc(13f, 13f, 9f, Palette.Silver);
            c.Ring(13f, 13f, 9f, Palette.White, 1f);
            c.Disc(13f, 13f, 7f, Palette.Black);
            c.Disc(13f, 13f, 6.5f, Palette.Cyan);
            c.DitherRing(13f, 13f, 6.5f, 6.5f, Palette.Cyan, Palette.White);
            // Hard-edged glint, two bars: the 95 way of saying "glass".
            c.Fill(9f, 8f, 4f, 1f, Palette.White);
            c.Fill(8f, 9f, 1f, 3f, Palette.White);
        }

        // --- media ---

        static void Camera(IconCanvas c, bool vivid)
        {
            // A camera body three-quarters on with the lens barrel on the front.
            if (vivid) c.Shadow(4f, 12f, 22f, 12f);
            c.Box(6f, 6f, 8f, 4f, 2f, Palette.Gray, Palette.Silver, Palette.DarkGray);
            c.Box(3f, 11f, 20f, 12f, 4f, Palette.Silver, Palette.LightSilver, Palette.Gray);
            c.Fill(5f, 13f, 5f, 3f, Palette.Black);        // viewfinder window
            c.Fill(5f, 13f, 5f, 1f, Palette.Navy);
            c.Fill(5f, 19f, 7f, 2f, Palette.Gray);
            c.Fill(20f, 13f, 2f, 2f, Palette.Red);         // record lamp

            c.Disc(19f, 17f, 8f, Palette.Black);           // lens
            c.Disc(19f, 17f, 7f, Palette.DarkGray);
            c.Ring(19f, 17f, 7f, Palette.Gray, 1f);
            c.Disc(19f, 17f, 5f, Palette.Black);
            c.Disc(19f, 17f, 4f, Palette.Navy);
            c.Disc(19f, 17f, 2f, Palette.Blue);
            if (vivid) c.Fill(16f, 14f, 2f, 1f, Palette.White);
        }

        static void Photos(IconCanvas c, bool vivid)
        {
            // A stack of prints, back ones showing as white edges; the picture drawn as dithered noise.
            if (vivid) c.Shadow(6f, 7f, 23f, 20f);
            c.Fill(2f, 3f, 22f, 18f, Palette.Black);
            c.Fill(3f, 4f, 20f, 16f, Palette.Paper);
            c.Fill(5f, 6f, 16f, 12f, Palette.Silver);

            c.Fill(6f, 9f, 23f, 19f, Palette.Black);
            c.Fill(7f, 10f, 21f, 17f, Palette.Paper);
            c.Fill(9f, 12f, 17f, 13f, Palette.Black);
            c.Fill(10f, 13f, 15f, 11f, Palette.SkyBlue);
            c.Disc(13f, 16f, 2.2f, Palette.Yellow);        // sun
            c.Triangle(11f, 17f, 8f, 7f, Palette.DarkGreen, 0);
            c.Triangle(17f, 15f, 9f, 9f, Palette.Green, 0);
            c.Fill(10f, 22f, 15f, 2f, Palette.DarkGreen);
            if (vivid) c.Dither(10f, 13f, 15f, 2f, Palette.White, Palette.SkyBlue);
        }

        static void Movie(IconCanvas c, bool vivid)
        {
            // A clapperboard: white slate, black stick - every other film object is a circle.
            if (vivid && !c.Small) c.Shadow(4f, 12f, 25f, 16f);
            c.Fill(3f, 11f, 26f, 17f, Palette.Black);
            c.Fill(4f, 12f, 24f, 15f, Palette.Paper);

            // Two fat marks, not the count-in: a digit is grit at 16px. The video-file glyph.
            if (c.Small)
            {
                c.Fill(6f, 15f, 20f, 4f, Palette.Navy);
                c.Fill(6f, 21f, 13f, 4f, Palette.Navy);
                c.Fill(3f, 3f, 26f, 8f, Palette.Black);
                c.Fill(6f, 4f, 6f, 6f, Palette.White);
                c.Fill(18f, 4f, 6f, 6f, Palette.White);
                return;
            }

            Digit(c, 6f, 15f, 3, Palette.Red);             // the count-in
            Digit(c, 13f, 15f, 2, Palette.DarkGreen);
            Digit(c, 20f, 15f, 1, Palette.Navy);
            c.Fill(6f, 25f, 19f, 1f, Palette.Gray);

            // The clapper stick, hinged at the left and striped.
            c.Fill(3f, 4f, 26f, 7f, Palette.Black);
            for (var i = 0; i < 4; i++)
                c.Fill(5f + i * 6f, 5.5f, 3f, 4f, Palette.White);
        }

        // A 5x8 seven-segment digit; only 1, 2 and 3 exist (what the clapperboard counts).
        static void Digit(IconCanvas c, float x, float y, int d, Color32 ink)
        {
            const float t = 1.5f;
            if (d == 1)
            {
                c.Fill(x + 2f, y, t, 8f, ink);
                c.Fill(x + 0.5f, y + 1f, 1.5f, t, ink);
                c.Fill(x, y + 8f - t, 5f, t, ink);
                return;
            }
            c.Fill(x, y, 5f, t, ink);                      // top
            c.Fill(x, y + 4f - t * 0.5f, 5f, t, ink);      // middle
            c.Fill(x, y + 8f - t, 5f, t, ink);             // bottom
            c.Fill(x + 5f - t, y, t, 4f, ink);             // upper right
            if (d == 3) c.Fill(x + 5f - t, y + 4f, t, 4f, ink);
            else c.Fill(x, y + 4f, t, 4f, ink);            // lower left for 2
        }

        static void Music(IconCanvas c, bool vivid)
        {
            // Two beamed quavers, outlined black so they survive the toolbar.
            c.Fill(12f, 2f, 5f, 21f, Palette.Black);
            c.Fill(23f, 1f, 5f, 20f, Palette.Black);
            c.Fill(12f, 1f, 16f, 5f, Palette.Black);
            c.Disc(10f, 23f, 5.5f, Palette.Black);
            c.Disc(21f, 21f, 5.5f, Palette.Black);

            c.Fill(13f, 3f, 3f, 19f, Palette.Navy);
            c.Fill(24f, 2f, 3f, 18f, Palette.Navy);
            c.Fill(13f, 2f, 14f, 3f, Palette.Navy);
            c.Disc(10f, 23f, 4.2f, Palette.Blue);
            c.Disc(21f, 21f, 4.2f, Palette.Blue);
            if (vivid)
            {
                c.Fill(13f, 2f, 14f, 1f, Palette.SkyBlue);
                c.Disc(8.5f, 21.5f, 1.4f, Palette.SkyBlue);
                c.Disc(19.5f, 19.5f, 1.4f, Palette.SkyBlue);
            }
        }

        static void Speaker(IconCanvas c, bool vivid)
        {
            // The shell loudspeaker: a horn opening right, sound leaving as short dashes.
            if (c.Small)
            {
                // One wedge, not sixteen half-unit steps that share columns at 16px.
                c.Fill(3f, 12f, 9f, 9f, Palette.Black);
                c.Fill(5f, 14f, 5f, 5f, Palette.Gray);
                c.Triangle(11f, 2f, 12f, 28f, Palette.Black, 3);
                c.Triangle(12f, 5f, 10f, 22f, Palette.Yellow, 3);
                c.Fill(24f, 10f, 4f, 2f, Palette.Gray);
                c.Fill(24f, 20f, 4f, 2f, Palette.Gray);
                c.Fill(26f, 15f, 3f, 2f, Palette.Gray);
                return;
            }

            for (var i = 0; i < 3; i++)
            {
                var x = 23f + i * 2.5f;
                c.Fill(x, 11f - i * 2f, 2f, 1f, Palette.Gray);
                c.Fill(x, 21f + i * 2f, 2f, 1f, Palette.Gray);
            }

            // Horn, drawn column by column so the flare keeps its outline.
            for (var i = 0; i < 16; i++)
            {
                var half = 4f + i * 0.68f;
                c.Fill(5f + i, 16f - half, 1f, half * 2f, Palette.Black);
            }
            for (var i = 1; i < 15; i++)
            {
                var half = 4f + i * 0.68f - 1f;
                c.Dither(5f + i, 16f - half, 1f, half * 2f, Palette.Yellow, Palette.Gold);
            }
            c.Fill(6f, 13f, 14f, 1f, Palette.Yellow);

            // Magnet block behind it, and the diaphragm across the mouth.
            c.Fill(2f, 11f, 5f, 10f, Palette.Black);
            c.Fill(3f, 12f, 3f, 8f, Palette.Gray);
            c.Fill(3f, 12f, 3f, 1f, Palette.LightSilver);
            c.Ellipse(20f, 16f, 3f, 11f, Palette.Black);
            c.Ellipse(20f, 16f, 2.2f, 10f, Palette.Paper);
            if (vivid) c.Ellipse(19.5f, 11f, 1f, 3f, Palette.White);
        }

        static void Cassette(IconCanvas c, bool vivid)
        {
            // Black shell, silver window, two hubs - the shell audio tape (silver-on-silver had no outline).
            if (vivid) c.Shadow(3f, 8f, 26f, 17f);
            c.Fill(2f, 7f, 28f, 18f, Palette.Black);
            c.Fill(3f, 8f, 26f, 16f, Palette.DarkGray);
            c.Fill(3f, 8f, 26f, 1f, Palette.Gray);

            c.Fill(5f, 10f, 22f, 9f, Palette.Black);
            c.Fill(6f, 11f, 20f, 7f, Palette.Silver);
            c.Fill(6f, 11f, 20f, 1f, Palette.White);
            c.Disc(11f, 14.5f, 3.5f, Palette.Black);
            c.Disc(21f, 14.5f, 3.5f, Palette.Black);
            c.Disc(11f, 14.5f, 2.5f, Palette.LightSilver);
            c.Disc(21f, 14.5f, 2.5f, Palette.LightSilver);
            c.Disc(11f, 14.5f, 1.2f, Palette.DarkGray);
            c.Disc(21f, 14.5f, 1.2f, Palette.DarkGray);
            c.Fill(12f, 13f, 8f, 3f, Palette.Black);       // the tape between

            c.Fill(6f, 20f, 20f, 3f, Palette.Black);       // head window
            c.Fill(8f, 21f, 4f, 2f, Palette.Gray);
            c.Fill(20f, 21f, 4f, 2f, Palette.Gray);
        }

        static void Microphone(IconCanvas c, bool vivid)
        {
            // Capsule, cradle, stem, base. Face on, so the grille reads.
            if (vivid) c.Shadow(11f, 3f, 11f, 23f);
            c.Fill(11f, 3f, 10f, 14f, Palette.Black);
            c.Disc(16f, 5f, 5f, Palette.Black);
            c.Disc(16f, 15f, 5f, Palette.Black);
            c.Fill(12f, 4f, 8f, 12f, Palette.Gray);
            c.Disc(16f, 5f, 4f, Palette.Gray);
            c.Disc(16f, 15f, 4f, Palette.Gray);
            for (var i = 0; i < 6; i++) c.Fill(12f, 4f + i * 2.2f, 8f, 1f, Palette.DarkGray);
            c.Line(9f, 14f, 9f, 20f, Palette.Black, 2f);    // cradle
            c.Line(23f, 14f, 23f, 20f, Palette.Black, 2f);
            c.Line(9f, 20f, 23f, 20f, Palette.Black, 2f);
            c.Fill(14.5f, 20f, 3f, 6f, Palette.Black);      // stem
            c.Fill(15.5f, 20f, 1f, 6f, Palette.Silver);
            c.Fill(9f, 26f, 14f, 3f, Palette.Black);        // base
            c.Fill(10f, 26.5f, 12f, 2f, Palette.Silver);
            if (vivid) c.Fill(13f, 5f, 2f, 10f, Palette.LightSilver);
        }

        static void Paint(IconCanvas c, bool vivid)
        {
            // Palette board with paint blobs and a brush, dithered yellow (no manila in the palette).
            c.Disc(14f, 18f, 11.5f, Palette.Black);
            c.Disc(14f, 18f, 10.5f, Palette.Yellow);
            c.DitherRing(14f, 18f, 10.5f, 10.5f, Palette.Yellow, Palette.White);
            c.Ring(14f, 18f, 10.5f, Palette.Olive, 1f);
            c.Disc(19f, 22f, 3f, Palette.Black);           // thumb hole
            c.Disc(19f, 22f, 2.2f, Palette.Teal);

            Blob(c, 9f, 13f, Palette.Red);
            Blob(c, 15f, 11f, Palette.Blue);
            Blob(c, 20f, 14f, Palette.Green);
            Blob(c, 8f, 20f, Palette.Purple);

            c.Line(20f, 12f, 27f, 5f, Palette.Black, 3f);
            c.Line(20f, 12f, 26f, 6f, Palette.Amber, 1f);
            c.Fill(24f, 3f, 5f, 5f, Palette.Black);
            c.Fill(25f, 4f, 3f, 3f, Palette.Silver);
        }

        static void Blob(IconCanvas c, float cx, float cy, Color32 fill)
        {
            c.Disc(cx, cy, 2.6f, Palette.Black);
            c.Disc(cx, cy, 2f, fill);
        }

        // --- peripherals ---

        static void Printer(IconCanvas c, bool vivid)
        {
            // Three-quarters on with the sheet out of the top - the shell printer.
            if (vivid) c.Shadow(4f, 16f, 24f, 11f);

            // Paper leaving the top, sheared back with the lid.
            c.Fill(9f, 2f, 15f, 12f, Palette.Black);
            c.Fill(10f, 3f, 13f, 10f, Palette.Paper);
            for (var i = 0; i < 4; i++) c.Fill(12f, 5f + i * 2f, 9f, 1f, Palette.Navy);

            c.Box(3f, 15f, 21f, 11f, 5f, Palette.Silver, Palette.LightSilver, Palette.Gray);
            c.Fill(5f, 17f, 13f, 2f, Palette.Black);       // out slot
            c.Fill(5f, 17f, 13f, 1f, Palette.DarkGray);
            c.Fill(5f, 22f, 9f, 1f, Palette.Gray);
            c.Fill(20f, 21f, 2f, 2f, Palette.Green);
            c.Fill(17f, 21f, 2f, 2f, Palette.Amber);
        }

        static void Keyboard(IconCanvas c, bool vivid)
        {
            // White body, black keys - light keys on a silver body vanished into the toolbar.
            if (c.Small)
            {
                // Two rows of fat keys: a 2-unit key is one pixel at 16px, the gap none.
                c.Fill(3f, 11f, 26f, 14f, Palette.Black);
                c.Fill(4f, 12f, 24f, 12f, Palette.LightSilver);
                for (var i = 0; i < 6; i++)
                {
                    c.Fill(6f + i * 4f, 14f, 2f, 2f, Palette.Black);
                    c.Fill(6f + i * 4f, 18f, 2f, 2f, Palette.Black);
                }
                c.Fill(8f, 21f, 12f, 2f, Palette.Black);
                return;
            }

            c.Line(9f, 3f, 9f, 9f, Palette.Black, 2f);     // cord
            c.Line(9f, 3f, 15f, 3f, Palette.Black, 2f);
            c.Fill(9.5f, 4f, 1f, 5f, Palette.Gray);

            c.Box(3f, 13f, 23f, 12f, 3f, Palette.LightSilver, Palette.White, Palette.Gray);
            for (var r = 0; r < 3; r++)
                for (var i = 0; i < 9; i++)
                {
                    c.Fill(5f + i * 2.3f, 15f + r * 2.5f, 2f, 2f, Palette.Black);
                    c.Fill(5f + i * 2.3f, 15f + r * 2.5f, 1.4f, 1.4f, Palette.LightSilver);
                }
            c.Fill(9f, 22f, 11f, 2f, Palette.Black);       // space bar
            c.Fill(9f, 22f, 10f, 1.4f, Palette.LightSilver);
        }

        static void Mouse(IconCanvas c, bool vivid)
        {
            // Top-down with a rounded head, white-bodied like the shell one (square corners read as a remote).
            if (c.Small)
            {
                // Outline drawn last here, not first: at 16px the body would eat a proud 1-unit rim.
                c.Fill(9f, 6f, 14f, 22f, Palette.LightSilver);
                c.Rect(9f, 6f, 14f, 22f, Palette.Black, 2f);
                c.Fill(9f, 14f, 14f, 2f, Palette.Black);
                c.Fill(15f, 6f, 2f, 8f, Palette.Black);
                c.Fill(14f, 2f, 2f, 4f, Palette.Black);
                return;
            }

            c.Line(12f, 1f, 15f, 6f, Palette.Black, 2f);   // cord
            c.Fill(13f, 1f, 1f, 4f, Palette.Gray);

            c.Disc(16f, 12f, 8f, Palette.Black);           // head
            c.Fill(8f, 12f, 16f, 17f, Palette.Black);
            c.Disc(16f, 12f, 7f, Palette.LightSilver);
            c.Fill(9f, 12f, 14f, 16f, Palette.LightSilver);

            c.Fill(15.5f, 6f, 1f, 10f, Palette.Black);     // split between buttons
            c.Fill(9f, 16f, 14f, 1f, Palette.Black);
            c.Fill(11f, 19f, 10f, 7f, Palette.Silver);     // palm shading
            if (vivid)
            {
                c.Fill(11f, 8f, 3f, 3f, Palette.White);
                c.Fill(21f, 18f, 2f, 9f, Palette.Silver);
            }
        }

        static void Joystick(IconCanvas c, bool vivid)
        {
            // Stick, grip and base, three-quarters on like every desk object in the set.
            c.Fill(14f, 12f, 4f, 11f, Palette.Black);      // shaft
            c.Fill(15f, 12f, 2f, 11f, Palette.Silver);
            c.Fill(16f, 12f, 1f, 11f, Palette.Gray);

            c.Fill(11f, 3f, 9f, 10f, Palette.Black);       // grip
            c.Fill(12f, 4f, 7f, 8f, Palette.Silver);
            c.Fill(12f, 4f, 7f, 1f, Palette.White);
            c.Fill(17f, 4f, 2f, 8f, Palette.Gray);
            c.Fill(13f, 6f, 3f, 2f, Palette.Red);          // thumb button

            if (vivid) c.Shadow(5f, 22f, 22f, 6f);
            c.Box(4f, 21f, 22f, 6f, 3f, Palette.Silver, Palette.LightSilver, Palette.Gray);
            c.Fill(6f, 23f, 4f, 3f, Palette.Black);
            c.Fill(6.5f, 23.5f, 3f, 2f, Palette.Maroon);
            c.Fill(20f, 23f, 4f, 3f, Palette.Black);
            c.Fill(20.5f, 23.5f, 3f, 2f, Palette.Navy);
        }

        // --- tools ---

        static void Clock(IconCanvas c, bool vivid)
        {
            // A red-rimmed wall clock with a white dial - a grey bezel is gone on the toolbar.
            if (vivid) c.DiscShadow(16f, 16f, 13f, 1f);
            c.Disc(16f, 16f, 14f, Palette.Black);
            c.Disc(16f, 16f, 13f, Palette.Red);
            c.Disc(16f, 16f, 12f, Palette.Maroon);
            c.Disc(16f, 16f, 11f, Palette.Red);
            c.Disc(16f, 16f, 10f, Palette.Black);
            c.Disc(16f, 16f, 9.5f, Palette.Paper);

            for (var i = 0; i < 12; i++)
            {
                var a = i * Mathf.PI / 6f;
                var big = i % 3 == 0;
                var s = big ? 2f : 1f;
                c.Fill(16f + Mathf.Sin(a) * 8f - s * 0.5f, 16f - Mathf.Cos(a) * 8f - s * 0.5f,
                    s, s, Palette.Black);
            }
            c.Line(16f, 16f, 16f, 10f, Palette.Navy, 1.5f);   // hour
            c.Line(16f, 16f, 21f, 18f, Palette.Navy, 1f);     // minute
            c.Disc(16f, 16f, 1.4f, Palette.Black);
            if (vivid) c.Disc(12f, 12f, 3f, Palette.White);
        }

        static void Calendar(IconCanvas c, bool vivid)
        {
            // A wall calendar: navy banner, ruled grid, one day ringed.
            if (vivid) c.Shadow(4f, 6f, 25f, 23f);
            c.Fill(6f, 3f, 3f, 6f, Palette.Black);         // hangers
            c.Fill(6.5f, 3.5f, 2f, 5f, Palette.Silver);
            c.Fill(23f, 3f, 3f, 6f, Palette.Black);
            c.Fill(23.5f, 3.5f, 2f, 5f, Palette.Silver);

            c.Fill(2f, 5f, 28f, 24f, Palette.Black);
            c.Fill(3f, 6f, 26f, 22f, Palette.Paper);
            c.Fill(3f, 6f, 26f, 6f, Palette.Navy);
            c.Fill(3f, 6f, 26f, 1f, Palette.Blue);
            c.Fill(6f, 8f, 12f, 2f, Palette.White);

            for (var r = 0; r < 3; r++)
                for (var i = 0; i < 6; i++)
                    c.Fill(4f + i * 4.2f, 14f + r * 4.5f, 3f, 3f,
                        r == 1 && i == 3 ? Palette.Red : Palette.Silver);
            c.Fill(3f, 12f, 26f, 1f, Palette.Gray);
        }

        static void Calculator(IconCanvas c, bool vivid)
        {
            // The shell calculator: a teal case, a pale readout, grey keys (silver-on-silver vanished).
            if (vivid) c.Shadow(4f, 2f, 22f, 26f);
            c.Fill(4f, 2f, 24f, 27f, Palette.Black);
            c.Fill(5f, 3f, 22f, 25f, Palette.Teal);
            c.Fill(5f, 3f, 22f, 1f, Palette.Cyan);
            c.Fill(5f, 3f, 1f, 25f, Palette.Cyan);

            c.Fill(7f, 5f, 18f, 5f, Palette.Black);
            c.Fill(8f, 6f, 16f, 3f, Palette.LightSilver);
            c.Fill(18f, 6.5f, 5f, 2f, Palette.Gray);
            for (var r = 0; r < 4; r++)
                for (var i = 0; i < 4; i++)
                {
                    c.Fill(7f + i * 4.6f, 12f + r * 4f, 4f, 3f, Palette.Black);
                    c.Fill(7f + i * 4.6f, 12f + r * 4f, 3.4f, 2.4f,
                        i == 3 ? Palette.Gold : Palette.Silver);
                }
        }

        static void Contacts(IconCanvas c, bool vivid)
        {
            // An address card in its binder: white stock, green rules, rings down the left.
            if (vivid) c.Shadow(6f, 4f, 23f, 25f);
            c.Fill(5f, 3f, 24f, 26f, Palette.Black);
            c.Fill(6f, 4f, 22f, 24f, Palette.Paper);
            for (var i = 0; i < 3; i++)
            {
                c.Fill(2f, 6f + i * 9f, 6f, 3f, Palette.Black);
                c.Fill(2.5f, 6.5f + i * 9f, 5f, 2f, Palette.Silver);
            }
            c.Disc(14f, 11f, 4f, Palette.Black);           // portrait
            c.Disc(14f, 11f, 3.2f, Palette.Skin);
            c.Fill(10f, 16f, 9f, 6f, Palette.Black);
            c.Fill(11f, 17f, 7f, 5f, Palette.Navy);
            for (var i = 0; i < 4; i++) c.Fill(21f, 9f + i * 3f, 6f, 1f, Palette.DarkGreen);
            c.Fill(10f, 24f, 17f, 1f, Palette.DarkGreen);
        }

        static void Chart(IconCanvas c, bool vivid)
        {
            // Bars in a frame, each checkerboarded from two palette colours.
            if (vivid) c.Shadow(4f, 4f, 23f, 23f);
            c.Fill(3f, 3f, 25f, 25f, Palette.Black);
            c.Fill(4f, 4f, 23f, 23f, Palette.Paper);
            c.Fill(6f, 24f, 20f, 1f, Palette.Black);
            c.Fill(6f, 8f, 1f, 17f, Palette.Black);

            Bar(c, 8f, 16f, Palette.Navy, Palette.Blue);
            Bar(c, 13f, 12f, Palette.Maroon, Palette.Red);
            Bar(c, 18f, 18f, Palette.DarkGreen, Palette.Green);
            Bar(c, 22.5f, 9f, Palette.Olive, Palette.Yellow);
        }

        static void Bar(IconCanvas c, float x, float top, Color32 dark, Color32 light)
        {
            c.Fill(x, top, 4f, 24f - top, Palette.Black);
            c.Dither(x + 1f, top + 1f, 3f, 23f - top, light, dark);
            c.Fill(x + 1f, top + 1f, 3f, 1f, light);
        }

        static void Settings(IconCanvas c, bool vivid)
        {
            // One gold cog (two overlapping is a blob at 16px); radii keep it inside the 28-unit box.
            if (c.Small)
            {
                Cog(c, 16f, 16f, 11f, Palette.Black, Palette.Black);
                Cog(c, 16f, 16f, 10f, Palette.Gold, Palette.Olive);
                c.Disc(16f, 16f, 4f, Palette.Black);
                c.Disc(16f, 16f, 3f, Palette.Amber);
                return;
            }

            // Big cog plus a small one; each over a black cog a size larger (gold teeth need a keyline).
            Cog(c, 13.5f, 13.5f, 9f, Palette.Black, Palette.Black);
            Cog(c, 22.5f, 22.5f, 6f, Palette.Black, Palette.Black);
            Cog(c, 13.5f, 13.5f, 8f, Palette.Gold, Palette.Olive);
            Cog(c, 22.5f, 22.5f, 5f, Palette.Amber, Palette.Olive);
            if (vivid) c.Fill(9f, 9f, 3f, 1f, Palette.Yellow);
        }

        static void Cog(IconCanvas c, float cx, float cy, float r, Color32 body, Color32 edge)
        {
            for (var i = 0; i < 8; i++)
            {
                var a = i * Mathf.PI / 4f;
                c.Fill(cx + Mathf.Sin(a) * r - r * 0.22f, cy - Mathf.Cos(a) * r - r * 0.22f,
                    r * 0.45f, r * 0.45f, edge);
            }
            c.Disc(cx, cy, r * 0.85f, body);
            c.Ring(cx, cy, r * 0.85f, edge, 1f);
            c.Disc(cx, cy, r * 0.3f, edge);
        }

        static void Tools(IconCanvas c, bool vivid)
        {
            // A spanner crossed with a screwdriver, both black-outlined first.
            c.Line(4f, 24f, 20f, 8f, Palette.Black, 5f);
            c.Disc(21f, 7f, 6.5f, Palette.Black);
            c.Line(5f, 23f, 20f, 8f, Palette.Silver, 3f);
            c.Disc(21f, 7f, 5f, Palette.Silver);
            c.Fill(19f, 5f, 5f, 1f, Palette.White);
            c.EraseDisc(21f, 7f, 2.6f);
            c.Erase(20f, 0f, 9f, 7f);

            c.Line(24f, 24f, 11f, 9f, Palette.Black, 5f);
            c.Fill(7f, 4f, 8f, 8f, Palette.Black);
            c.Line(23f, 23f, 12f, 10f, Palette.Gold, 3f);
            c.Fill(8f, 5f, 6f, 6f, Palette.Maroon);
            c.Fill(8f, 5f, 6f, 1f, Palette.Salmon);
            if (vivid) c.Line(7f, 22f, 18f, 11f, Palette.LightSilver, 1f);
        }

        static void Flag(IconCanvas c, bool vivid)
        {
            // A plain pennant on a pole (four coloured panes belong to another icon).
            Pennant(c, 8f, 3f, 21f, 16f, Palette.Black);
            Pennant(c, 9f, 4.5f, 18f, 13f, Palette.Red);
            if (vivid) c.Fill(10f, 6f, 9f, 1f, Palette.Salmon);

            c.Fill(4f, 2f, 5f, 27f, Palette.Silver);   // pole
            c.Rect(4f, 2f, 5f, 27f, Palette.Black);
            c.Fill(5f, 3f, 1f, 25f, Palette.White);
            c.Fill(7f, 3f, 1f, 25f, Palette.Gray);
            c.Fill(2f, 28f, 9f, 3f, Palette.Silver);   // foot
            c.Rect(2f, 28f, 9f, 3f, Palette.Black);
        }

        // A flag at full height, tapering to the fly a column at a time so it stays on whole pixels.
        static void Pennant(IconCanvas c, float x, float y, float w, float h, Color32 fill)
        {
            var columns = Mathf.RoundToInt(w);
            for (var i = 0; i < columns; i++)
            {
                var half = h * 0.5f * (1f - i / w);
                c.Fill(x + i, y + h * 0.5f - half, 1f, half * 2f, fill);
            }
        }

        static void Star(IconCanvas c, bool vivid)
        {
            // Five points, drawn heavy in black then gold inside (else a yellow smear on silver).
            StarPass(c, 13.5f, 5.5f, Palette.Black);
            c.Disc(16f, 16f, 7.5f, Palette.Black);
            StarPass(c, 12.5f, 3f, Palette.Gold);
            c.Disc(16f, 16f, 6f, Palette.Gold);
            if (vivid)
            {
                StarPass(c, 11f, 1.5f, Palette.Yellow);
                c.Disc(14f, 14f, 2.4f, new Color32(255, 255, 255, 150));
            }
        }

        static void StarPass(IconCanvas c, float radius, float thickness, Color32 fill)
        {
            for (var i = 0; i < 5; i++)
            {
                var a = i * Mathf.PI * 4f / 5f - Mathf.PI / 2f;
                var b = (i + 1) * Mathf.PI * 4f / 5f - Mathf.PI / 2f;
                c.Line(16f + Mathf.Cos(a) * radius, 16f + Mathf.Sin(a) * radius,
                    16f + Mathf.Cos(b) * radius, 16f + Mathf.Sin(b) * radius,
                    fill, thickness);
            }
        }

        // --- security ---

        static void Key(IconCanvas c, bool vivid)
        {
            // Bow, shank and two wards, black-outlined first. Steel, not brass (Lock is the gold object).
            if (c.Small)
            {
                // Squared off and upright: at 16px a 1-unit black ring rounds to the silver radius.
                c.Fill(10f, 2f, 12f, 13f, Palette.Black);
                c.Fill(12f, 4f, 8f, 9f, Palette.Silver);
                c.Fill(14f, 6f, 4f, 5f, Palette.Black);
                c.Fill(13f, 14f, 6f, 16f, Palette.Black);
                c.Fill(14f, 15f, 4f, 14f, Palette.Silver);
                c.Fill(19f, 18f, 5f, 4f, Palette.Black);
                c.Fill(19f, 18f, 4f, 2f, Palette.Silver);
                c.Fill(19f, 24f, 5f, 4f, Palette.Black);
                c.Fill(19f, 24f, 4f, 2f, Palette.Silver);
                return;
            }

            c.Ring(9.5f, 10.5f, 8.5f, Palette.Black, 6f);
            c.Line(11.5f, 13.5f, 22f, 24f, Palette.Black, 6f);
            c.Fill(15f, 20f, 7f, 6f, Palette.Black);
            c.Fill(19f, 16f, 7f, 6f, Palette.Black);

            c.Ring(9.5f, 10.5f, 7.5f, Palette.Silver, 4f);
            c.DitherRing(9.5f, 10.5f, 7.5f, 4f, Palette.White, Palette.Silver);
            c.Line(12f, 14f, 21f, 23f, Palette.Silver, 4f);
            c.Fill(16f, 21f, 5f, 4f, Palette.Silver);
            c.Fill(20f, 17f, 5f, 4f, Palette.Silver);
            if (vivid)
            {
                c.Line(12f, 14f, 20f, 22f, Palette.LightSilver, 2f);
                c.Ring(9.5f, 10.5f, 7.5f, Palette.White, 1f);
            }
        }

        static void Lock(IconCanvas c, bool vivid)
        {
            // Shackle: a ring cut at the body line, legs down into the case. Steel shackle, brass case.
            c.Ring(16f, 12f, 8f, Palette.Black, 5f);
            c.Ring(16f, 12f, 7f, Palette.Silver, 3f);
            c.Erase(2f, 12f, 28f, 20f);
            c.Fill(7f, 11f, 4f, 7f, Palette.Black);
            c.Fill(21f, 11f, 4f, 7f, Palette.Black);
            c.Fill(8f, 11f, 2f, 7f, Palette.Silver);
            c.Fill(22f, 11f, 2f, 7f, Palette.Silver);

            c.Fill(4f, 15f, 24f, 15f, Palette.Black);
            c.Fill(5f, 16f, 22f, 13f, Palette.Gold);
            c.Fill(5f, 16f, 22f, 1f, Palette.Yellow);
            c.Fill(5f, 28f, 22f, 1f, Palette.Amber);
            for (var i = 0; i < 4; i++) c.Fill(7f, 19f + i * 2.6f, 18f, 1f, Palette.Amber);
            c.Disc(16f, 22f, 2.4f, Palette.Black);
            c.Fill(15f, 22f, 2f, 5f, Palette.Black);
        }

        static void Shield(IconCanvas c, bool vivid)
        {
            for (var y = 0; y < 26; y++)
            {
                var t = y / 26f;
                var w = Mathf.Lerp(24f, 2f, t * t);
                c.Fill(16f - w * 0.5f, 3f + y, w, 1f, Palette.Navy);
            }
            for (var y = 0; y < 22; y++)
            {
                var t = y / 22f;
                var w = Mathf.Lerp(18f, 1.5f, t * t);
                c.Fill(16f - w * 0.5f, 5f + y, w, 1f, Palette.SkyBlue);
            }
            c.Fill(14.5f, 9f, 3f, 12f, Palette.White);
            c.Fill(10f, 13f, 12f, 3f, Palette.White);
            if (vivid) c.Fill(8f, 5f, 6f, 8f, new Color32(255, 255, 255, 50));
        }

        static void Eye(IconCanvas c, bool vivid)
        {
            // An almond eye, opened up so it has a body rather than reading as a wire.
            Almond(c, 22f, Palette.Black);
            Almond(c, 19f, Palette.Paper);
            c.Disc(16f, 16f, 7f, Palette.Black);
            // Iris and pupil two units apart - one pixel at 16px, so the ring speckles.
            if (!c.Small)
            {
                c.Disc(16f, 16f, 6f, Palette.SkyBlue);
                c.Disc(16f, 16f, 3.5f, Palette.Black);
            }
            if (vivid && !c.Small) c.Disc(13.5f, 13.5f, 1.8f, Palette.White);
        }

        // The eye outline: a lens shape, drawn a column at a time.
        static void Almond(IconCanvas c, float height, Color32 fill)
        {
            for (var x = 0; x < 28; x++)
            {
                var h = Mathf.Sin(x / 27f * Mathf.PI) * height;
                if (h < 1f) continue;
                c.Fill(2f + x, 16f - h * 0.5f, 1f, h, fill);
            }
        }

        // --- dialogs ---

        static void Warning(IconCanvas c, bool vivid)
        {
            // Flat yellow in a black triangle, light top-left edge (a translucent wedge smudges at 64).
            c.Triangle(1f, 3f, 30f, 26f, Palette.Black, 0);
            c.Triangle(3f, 5f, 26f, 23f, Palette.Yellow, 0);
            if (vivid)
            {
                c.Line(16f, 5f, 4f, 27f, new Color32(255, 255, 190, 255), 1f);
                c.Line(4f, 27f, 28f, 27f, Palette.Amber, 1f);
            }
            c.Fill(14.5f, 12f, 3f, 10f, Palette.Black);
            c.Fill(14.5f, 24f, 3f, 3f, Palette.Black);
        }

        static void Error(IconCanvas c, bool vivid)
        {
            c.Disc(16f, 16f, 14f, Palette.Maroon);
            c.Disc(16f, 16f, 12.5f, Palette.Red);
            c.Line(10f, 10f, 22f, 22f, Palette.White, 3f);
            c.Line(22f, 10f, 10f, 22f, Palette.White, 3f);
            if (vivid) c.Disc(11f, 11f, 3.5f, new Color32(255, 255, 255, 60));
        }

        static void Info(IconCanvas c, bool vivid)
        {
            c.Disc(16f, 16f, 14f, Palette.Navy);
            c.Disc(16f, 16f, 12.5f, Palette.Blue);
            c.Fill(14.5f, 8f, 3f, 3f, Palette.White);
            c.Fill(14.5f, 13f, 3f, 11f, Palette.White);
            if (vivid) c.Disc(11f, 11f, 3.5f, new Color32(255, 255, 255, 70));
        }

        static void Question(IconCanvas c, bool vivid)
        {
            if (vivid) c.DiscShadow(16f, 16f, 13f, 1f);
            c.Disc(16f, 16f, 14f, Palette.Navy);
            c.Disc(16f, 16f, 12.5f, Palette.SkyBlue);
            // The hook: a ring opened at the lower left, then a neck and dot.
            c.Ring(16f, 11f, 5.5f, Palette.White, 2.5f);
            c.Erase(9f, 11f, 7.5f, 8f);
            c.Fill(14.5f, 13f, 3f, 7f, Palette.White);
            c.Fill(14.5f, 22f, 3f, 3f, Palette.White);
            if (vivid) c.Disc(11f, 11f, 3.5f, new Color32(255, 255, 255, 70));
        }

        // --- games / story ---

        static void Bomb(IconCanvas c, bool vivid)
        {
            // The minesweeper mine: a spiked ball, checkerboarded for a surface (a smooth disc is a cannonball).
            for (var k = 0; k < 8; k++)
            {
                var a = k * Mathf.PI / 4f;
                c.Line(16f, 17f, 16f + Mathf.Cos(a) * 13f, 17f + Mathf.Sin(a) * 13f,
                    Palette.Black, 4f);
            }
            c.Disc(16f, 17f, 11f, Palette.Black);
            c.Disc(16f, 17f, 9.5f, Palette.DarkGray);
            // The checker is grit once a pixel is two design units across.
            if (!c.Small) c.DitherRing(16f, 17f, 9.5f, 9.5f, Palette.Gray, Palette.Black);
            c.Fill(14f, 4f, 4f, 5f, Palette.Black);        // the plunger
            c.Fill(15f, 5f, 2f, 4f, Palette.Silver);
            if (vivid)
            {
                c.Disc(12f, 13f, 2.4f, Palette.Silver);
                c.Disc(11.5f, 12.5f, 1.2f, Palette.White);
            }
        }

        static void Cards(IconCanvas c, bool vivid)
        {
            // A patterned back with a face card off it - the shell's card-game glyph.
            if (vivid) c.Shadow(4f, 6f, 15f, 22f);
            c.Fill(3f, 5f, 15f, 23f, Palette.Black);
            c.Fill(4f, 6f, 13f, 21f, Palette.Paper);
            c.Fill(5f, 7f, 11f, 19f, Palette.Navy);
            c.Dither(5f, 7f, 11f, 19f, Palette.Blue, Palette.Navy);

            c.Fill(13f, 4f, 16f, 23f, Palette.Black);
            c.Fill(14f, 5f, 14f, 21f, Palette.Paper);
            // Spade: point at the top, two lobes under it, then the stem.
            c.Triangle(17f, 8f, 8f, 7f, Palette.Black, 0);
            c.Disc(19f, 15f, 3f, Palette.Black);
            c.Disc(23f, 15f, 3f, Palette.Black);
            c.Fill(19f, 12f, 4f, 5f, Palette.Black);
            c.Fill(20.5f, 18f, 2f, 3f, Palette.Black);
            c.Fill(19f, 20f, 5f, 1f, Palette.Black);
            c.Fill(15f, 6f, 2f, 3f, Palette.Black);
        }

        static void Dice(IconCanvas c, bool vivid)
        {
            if (vivid) c.Shadow(5f, 6f, 22f, 22f);
            c.Bevel(4f, 5f, 23f, 23f, Palette.Paper);
            c.Disc(11f, 12f, 2.2f, Palette.Black);
            c.Disc(20f, 12f, 2.2f, Palette.Black);
            c.Disc(15.5f, 16.5f, 2.2f, Palette.Red);
            c.Disc(11f, 21f, 2.2f, Palette.Black);
            c.Disc(20f, 21f, 2.2f, Palette.Black);
        }

        static void Ghost(IconCanvas c, bool vivid)
        {
            // The body twice, black a unit proud: a white ghost has 0% rim, gone on the toolbar.
            GhostBody(c, 11f, 10f, 5f, Palette.Black);
            GhostBody(c, 10f, 9f, 4f, Palette.Paper);
            c.Disc(12f, 12f, 2.6f, Palette.Black);
            c.Disc(20f, 12f, 2.6f, Palette.Black);
            c.Fill(14f, 18f, 4f, 3f, Palette.DarkGray);
            if (vivid) c.Fill(10f, 8f, 3f, 1f, Palette.White);
        }

        // A dome on a skirt that ends in scallops.
        static void GhostBody(IconCanvas c, float r, float skirt, float scallop, Color32 fill)
        {
            c.Disc(16f, 14f, r, fill);
            c.Fill(16f - r, 14f, r * 2f, skirt, fill);
            var w = r * 2f / 4f;
            for (var i = 0; i < 4; i++)
                c.Triangle(16f - r + i * w, 14f + skirt - 1f, w, scallop, fill, 1);
        }

        // --- browser ---

        // The rounded navigation buttons of a 1995 browser.
        static void Arrow(IconCanvas c, bool vivid, bool right)
        {
            // Back/Forward: a solid arrow on a raised round button; the black rim survives a silver toolbar.
            if (vivid) c.DiscShadow(16f, 16f, 13f);
            c.Disc(16f, 16f, 13f, Palette.Silver);
            c.Ring(16f, 16f, 13f, Palette.Black, 1f);
            c.Ring(16f, 16f, 12f, Palette.White, 1f);
            if (vivid) c.Disc(11.5f, 11.5f, 4f, new Color32(255, 255, 255, 55));

            c.Triangle(right ? 16f : 8f, 8f, 8f, 16f, Palette.Navy, right ? 2 : 3);
            c.Fill(right ? 8f : 16f, 13.5f, 8f, 5f, Palette.Navy);
        }

        static void Stop(IconCanvas c, bool vivid)
        {
            // A stop sign - an octagon, not the cross three rectangles made.
            Octagon(c, 2f, 2f, 28f, Palette.Black);
            Octagon(c, 3f, 3f, 26f, Palette.White);
            Octagon(c, 4.5f, 4.5f, 23f, Palette.Red);
            c.Fill(8f, 14f, 16f, 4f, Palette.White);
            if (vivid) Octagon(c, 4.5f, 4.5f, 11f, new Color32(255, 255, 255, 45));
        }

        // A regular octagon inscribed in a square, one row at a time.
        static void Octagon(IconCanvas c, float x, float y, float size, Color32 fill)
        {
            var chamfer = size * 0.2929f;
            var rows = Mathf.RoundToInt(size);
            for (var i = 0; i < rows; i++)
            {
                var inset = i < chamfer ? chamfer - i
                    : i > size - chamfer ? i - (size - chamfer)
                    : 0f;
                c.Fill(x + inset, y + i, size - inset * 2f, 1f, fill);
            }
        }

        static void Reload(IconCanvas c, bool vivid)
        {
            // Navigator's Reload: the page fetched again, same blue as Refresh (which is the bare arrow).
            if (c.Small)
            {
                // A straight arrow bending round the corner: a bitten ring is four pixels of arc here.
                Sheet(c, 3f, 2f, 16f, 22f, 6f);
                c.Fill(6f, 8f, 9f, 2f, Palette.Gray);
                c.Fill(6f, 13f, 9f, 2f, Palette.Gray);
                c.Fill(14f, 20f, 4f, 10f, Palette.Black);
                c.Fill(15f, 20f, 2f, 9f, Palette.Blue);
                c.Fill(16f, 24f, 12f, 4f, Palette.Black);
                c.Fill(16f, 25f, 10f, 2f, Palette.Blue);
                c.Triangle(24f, 19f, 8f, 8f, Palette.Black, 2);
                c.Triangle(24f, 21f, 6f, 5f, Palette.Blue, 2);
                return;
            }

            if (vivid) c.Shadow(5f, 2f, 15f, 23f);
            Sheet(c, 3f, 2f, 16f, 24f, 5f);
            for (var i = 0; i < 5; i++) c.Fill(6f, 8f + i * 3.5f, 9f, 1f, Palette.Gray);

            // The arrow sweeps round the page's bottom-right corner. Blue, not navy (no highlight with vivid off).
            c.Ring(21f, 21f, 8f, Palette.Black, 4.5f);
            c.Ring(21f, 21f, 8f, Palette.Blue, 2.5f);
            if (vivid) c.Ring(21f, 21f, 8f, Palette.SkyBlue, 1f);
            c.Erase(21f, 21f, 11f, 11f);
            c.Triangle(23f, 16f, 9f, 8f, Palette.Black, 1);
            c.Triangle(24f, 17f, 7f, 6f, Palette.Blue, 1);
        }

        static void Bookmark(IconCanvas c, bool vivid)
        {
            c.Fill(9f, 3f, 15f, 26f, Palette.Maroon);
            c.Fill(11f, 5f, 11f, 22f, Palette.Red);
            // The swallowtail, cut up out of the bottom edge.
            for (var i = 0; i < 8; i++)
            {
                var half = 8f - i;
                c.Erase(16.5f - half, 29f - i, half * 2f, 1f);
            }
            c.Line(9f, 29f, 16.5f, 21f, Palette.Maroon, 1f);
            c.Line(24f, 29f, 16.5f, 21f, Palette.Maroon, 1f);
            if (vivid) c.Fill(11f, 5f, 3f, 16f, new Color32(255, 255, 255, 70));
        }

        // --- programs ---

        // Reversi. A board game needs a board: dice belong to a different one.
        static void Board(IconCanvas c, bool vivid)
        {
            if (vivid) c.Shadow(4f, 5f, 23f, 22f);
            c.Fill(3f, 4f, 26f, 24f, Palette.Black);
            c.Fill(4f, 5f, 24f, 22f, Palette.DarkGreen);
            for (var i = 1; i < 4; i++)
            {
                c.Fill(4f + i * 6f, 5f, 1f, 22f, new Color32(0, 90, 0, 255));
                c.Fill(4f, 5f + i * 5.5f, 24f, 1f, new Color32(0, 90, 0, 255));
            }
            Piece(c, 10f, 10.5f, Palette.Black, Palette.DarkGray, vivid);
            Piece(c, 22f, 10.5f, Palette.White, Palette.Silver, vivid);
            Piece(c, 10f, 21.5f, Palette.White, Palette.Silver, vivid);
            Piece(c, 22f, 21.5f, Palette.Black, Palette.DarkGray, vivid);
        }

        static void Piece(IconCanvas c, float cx, float cy, Color32 face, Color32 edge,
            bool vivid)
        {
            c.Disc(cx, cy + 0.5f, 5f, Palette.Black);
            c.Disc(cx, cy, 4.6f, edge);
            c.Disc(cx, cy, 3.8f, face);
            if (vivid) c.Disc(cx - 1.3f, cy - 1.3f, 1.2f, new Color32(255, 255, 255, 110));
        }

        // One picture in a frame, as against Photos, which is a stack of them.
        static void Picture(IconCanvas c, bool vivid)
        {
            if (vivid) c.Shadow(3f, 6f, 25f, 20f);
            c.Fill(2f, 5f, 27f, 22f, Palette.Black);       // frame
            c.Fill(3f, 6f, 25f, 20f, Palette.Gold);
            c.Fill(3f, 6f, 25f, 1f, Palette.Yellow);
            c.Fill(3f, 25f, 25f, 1f, Palette.Amber);
            c.Fill(5f, 8f, 21f, 16f, Palette.Black);       // mount
            c.Fill(6f, 9f, 19f, 14f, Palette.SkyBlue);     // sky
            c.Disc(10f, 13f, 2.6f, Palette.Yellow);        // sun
            c.Triangle(13f, 12f, 9f, 11f, Palette.DarkGreen, 0);
            c.Triangle(18f, 14f, 7f, 9f, Palette.Green, 0);
            c.Fill(6f, 21f, 19f, 2f, Palette.DarkGreen);
            if (vivid) c.Dither(6f, 9f, 19f, 2f, Palette.White, Palette.SkyBlue);
        }

        // The file shell: a window, tree left, files right. My Computer is the machine; this is the view.
        static void Explorer(IconCanvas c, bool vivid)
        {
            if (vivid) c.Shadow(3f, 5f, 25f, 21f);
            c.Bevel(2f, 4f, 27f, 23f, Palette.Silver);
            c.Fill(3f, 5f, 25f, 4f, Palette.Navy);         // title bar
            c.Fill(4f, 6f, 8f, 2f, Palette.White);
            c.Fill(3f, 10f, 25f, 16f, Palette.Paper);
            c.Fill(13f, 10f, 1f, 16f, Palette.Gray);       // the split
            // Tree on the left, in the same dithered yellow as Folder.
            for (var i = 0; i < 3; i++)
            {
                c.Fill(5f, 12f + i * 4.5f, 5f, 3f, Palette.Black);
                c.Dither(5.5f, 12.5f + i * 4.5f, 4f, 2f, Palette.Yellow, Palette.White);
            }
            // Files on the right.
            for (var i = 0; i < 3; i++)
            {
                c.Fill(16f, 12f + i * 4.5f, 4f, 3f, Palette.Black);
                c.Fill(16.5f, 12.5f + i * 4.5f, 3f, 2f, Palette.Paper);
                c.Fill(21f, 13f + i * 4.5f, 5f, 1f, Palette.Gray);
            }
        }

        // FreeCell (not Solitaire): four cells above a tableau, where Cards is a fanned hand.
        static void FreeCell(IconCanvas c, bool vivid)
        {
            if (vivid) c.Shadow(3f, 4f, 24f, 23f);
            c.Fill(3f, 3f, 25f, 25f, Palette.Black);
            c.Fill(4f, 4f, 23f, 23f, Palette.DarkGreen);

            // The four free cells, two of them holding a card.
            for (var i = 0; i < 4; i++)
            {
                var x = 5f + i * 5.5f;
                c.Rect(x, 5f, 5f, 7f, Palette.Black);
                if (i >= 2) continue;
                c.Fill(x + 0.5f, 5.5f, 4f, 6f, Palette.Paper);
                c.Fill(x + 1.5f, 6.5f, 1.5f, 2f, i == 0 ? Palette.Red : Palette.Black);
            }

            // The tableau: three columns of cards, overlapping down the board.
            for (var col = 0; col < 3; col++)
                for (var row = 0; row < 3; row++)
                {
                    var x = 5f + col * 7f;
                    var y = 15f + row * 3.5f;
                    c.Fill(x, y, 6.5f, 4.5f, Palette.Black);
                    c.Fill(x + 0.5f, y + 0.5f, 5.5f, 3.5f, Palette.Paper);
                    c.Fill(x + 1.5f, y + 1.5f, 1.5f, 1.5f,
                        (col + row) % 2 == 0 ? Palette.Red : Palette.Black);
                }
            if (vivid) c.Fill(4f, 4f, 23f, 1f, new Color32(120, 200, 120, 120));
        }

        // The placeholder a browser draws when a picture will not load.
        static void BrokenImage(IconCanvas c, bool vivid)
        {
            c.Fill(3f, 6f, 26f, 20f, Palette.Paper);
            c.Rect(3f, 6f, 26f, 20f, Palette.Black);
            c.Fill(5f, 8f, 22f, 16f, new Color32(236, 236, 236, 255));

            // A torn page: two halves with a jagged split.
            c.Fill(15f, 6f, 2f, 20f, Palette.Paper);
            for (var i = 0; i < 10; i++)
                c.Fill(15f + (i % 2 == 0 ? -1.5f : 1.5f), 6f + i * 2f, 2f, 2f, Palette.Gray);

            c.Disc(9f, 12f, 2.2f, Palette.Maroon);
            c.Triangle(19f, 14f, 8f, 8f, Palette.Gray, 0);
            if (vivid) c.Fill(5f, 8f, 22f, 2f, new Color32(255, 255, 255, 90));
        }

        static void Skull(IconCanvas c, bool vivid)
        {
            // Black cranium and jaw a unit proud: the set's other 0%-rim shape.
            c.Disc(16f, 14f, 12f, Palette.Black);
            c.Fill(8f, 18f, 16f, 8f, Palette.Black);
            for (var i = 0; i < 3; i++) c.Fill(10.5f + i * 4f, 24f, 4f, 5f, Palette.Black);

            c.Disc(16f, 14f, 11f, Palette.Paper);
            c.Fill(9f, 18f, 14f, 7f, Palette.Paper);
            c.Disc(11.5f, 14f, 3.4f, Palette.Black);
            c.Disc(20.5f, 14f, 3.4f, Palette.Black);
            c.Triangle(14f, 18f, 4f, 4f, Palette.Black, 1);
            for (var i = 0; i < 3; i++) c.Fill(11f + i * 4f, 24f, 3f, 4f, Palette.Paper);
            for (var i = 0; i < 2; i++) c.Fill(13.5f + i * 4f, 24f, 1f, 4f, Palette.Black);
            if (vivid) c.Fill(10f, 8f, 3f, 1f, Palette.White);
        }
    }
}
