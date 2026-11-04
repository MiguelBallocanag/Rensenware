using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;

namespace Deepwave.RetroOS.UI.Components
{
    /// <summary>
    /// The row of files hanging off a message - Inbox attachments, files sent
    /// over the messenger. Opens through <see cref="RetroOsContext.OpenFile"/>,
    /// so access checks and a since-deleted file are handled there.
    /// </summary>
    public static class RetroFileStrip
    {
        const float MaxChip = 180f;
        const float MinChip = 46f;

        /// <summary>Refills a row with one button per path; the row hides itself when empty.</summary>
        /// <param name="paths">VFS paths, e.g. <c>/C:/Documents/report.txt</c>.</param>
        /// <param name="title">Program name for the "file has gone" box; empty uses a generic one.</param>
        public static void Fill(RectTransform row, RetroOsContext os,
            IEnumerable<string> paths, string caption = "Attached:",
            System.Action<string> onOpen = null, string title = null)
        {
            if (!row || os == null) return;

            foreach (Transform child in row) Object.Destroy(child.gameObject);

            var any = false;

            foreach (var raw in paths ?? System.Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var path = raw.Trim();

                if (!any)
                {
                    any = true;
                    row.gameObject.SetActive(true);
                    if (!string.IsNullOrEmpty(caption))
                        RetroUi.Size(RetroUi.Label(row, caption, 12, TextAnchor.MiddleLeft),
                            w: RetroUi.TextWidth(caption, 12f) + 6f);
                }

                var file = os.Fs.Resolve(path) as VfsFile;
                var name = file?.Name ?? Path.GetFileName(path);

                var button = RetroUi.PushButton(row, name,
                    () => Open(os, path, file, onOpen, title), RetroIcons.Get(BuiltinIcon.File));

                // Many attachments crowd rather than run off the side.
                RetroUi.SoftSize(button, Mathf.Min(MaxChip, RetroUi.TextWidth(name) + 34f), MinChip);
                if (file == null) button.Interactable = false;
                RetroUi.ToolTip(button, file == null ? path + " (missing)" : path);
            }

            if (!any) { row.gameObject.SetActive(false); return; }
            RetroUi.Spacer(row);
        }

        static void Open(RetroOsContext os, string path, VfsFile file,
            System.Action<string> onOpen, string title)
        {
            if (file == null)
            {
                RetroDialog.Alert(os.Overlay,
                    string.IsNullOrWhiteSpace(title) ? "Open" : title,
                    $"'{path}' is not on this machine any more.");
                return;
            }

            os.OpenFile(file);
            onOpen?.Invoke(path);
        }
    }
}
