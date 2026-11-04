using System.Collections.Generic;
using Deepwave.RetroOS.Core.Storage;

namespace Deepwave.RetroOS.Desktop
{
    /// <summary>
    /// Which grid cell each program's shortcut sits in. Worked out for the whole
    /// desktop in one pass, because an icon placed on its own cannot know which
    /// cells the icons after it are about to claim - that is how a program with
    /// nothing remembered ended up drawn on top of one that had a cell written
    /// down. Plain arithmetic over <see cref="MachinePrefs"/>, no screen needed.
    /// </summary>
    public static class DesktopLayout
    {
        /// <summary>The settings key a cell is filed under. Positions go under the app id, not the caption: a renamed program would otherwise move every tidied icon.</summary>
        public static string KeyFor(string idOrTitle) => "desktop." + (idOrTitle ?? "").Trim();

        /// <summary>
        /// Gives every program a cell of its own: the ones this machine remembers
        /// keep theirs, and the rest take the free cells down one column and on
        /// to the next, the way 95 stacked new shortcuts.
        /// </summary>
        /// <returns>One cell per program, in the order they were passed.</returns>
        public static (int Col, int Row)[] Plan(IReadOnlyList<(string Id, string Title)> programs,
            int cols, int rows, MachinePrefs prefs)
        {
            var cells = new (int Col, int Row)[programs?.Count ?? 0];
            if (cells.Length == 0) return cells;

            if (cols < 1) cols = 1;
            if (rows < 1) rows = 1;

            var taken = new HashSet<(int, int)>();
            var remembered = new bool[cells.Length];

            // Every remembered cell is claimed before anything is auto-placed, so
            // the order the programs happen to be installed in cannot decide who
            // keeps the spot the player put them in.
            for (var i = 0; i < cells.Length; i++)
            {
                if (!TryReadCell(prefs, programs[i].Id, programs[i].Title, out var cell)) continue;

                // A cell off the edge of a smaller screen comes back on; a settings
                // file that files two programs under one cell loses the second to
                // auto-placement rather than stacking them.
                cell = (Clamp(cell.Col, cols), Clamp(cell.Row, rows));
                if (!taken.Add(cell)) continue;

                cells[i] = cell;
                remembered[i] = true;
            }

            // The rest fill the gaps. Filling by free cell rather than by position
            // in the program list is what keeps the grid still when a program is
            // installed or removed: the icons that stayed do not shuffle.
            var scan = 0;
            for (var i = 0; i < cells.Length; i++)
                if (!remembered[i]) cells[i] = NextFreeCell(taken, ref scan, cols, rows);

            return cells;
        }

        /// <summary>The cell a program was last left in, if this machine wrote one down.</summary>
        public static bool TryReadCell(MachinePrefs prefs, string id, string title,
            out (int Col, int Row) cell)
        {
            cell = default;

            var saved = prefs?.Get(KeyFor(id), null);

            // Cells were filed under the caption before they were filed under the
            // id. The old key is read so a desktop tidied back then is not
            // shuffled, and never written, so it ages out.
            if (string.IsNullOrEmpty(saved)) saved = prefs?.Get(KeyFor(title), null);
            if (string.IsNullOrEmpty(saved)) return false;

            var parts = saved.Split(',');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out var col) ||
                !int.TryParse(parts[1], out var row) ||
                col < 0 || row < 0) return false;

            cell = (col, row);
            return true;
        }

        // Down a column, then on to the top of the next. More programs than the
        // desktop has cells is a desktop that has run out; the last cell takes
        // the overflow rather than the icons marching off the edge.
        static (int Col, int Row) NextFreeCell(HashSet<(int, int)> taken, ref int scan,
            int cols, int rows)
        {
            for (; scan < cols * rows; scan++)
            {
                var cell = (scan / rows, scan % rows);
                if (!taken.Add(cell)) continue;

                scan++;
                return cell;
            }

            return (cols - 1, rows - 1);
        }

        static int Clamp(int value, int count) =>
            value < 0 ? 0 : value >= count ? count - 1 : value;
    }
}
