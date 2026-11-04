using System.IO;
using UnityEditor;
using UnityEngine;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.EditorTools
{
    // Renders the icon set into one contact sheet, for docs and for eyeballing after a painter change.
    public static class RetroIconSheet
    {
        /// <summary>Asks where to put the sheet and writes it. Reached from the Icon Browser window.</summary>
        public static void Export()
        {
            var path = EditorUtility.SaveFilePanel("Icon contact sheet", Application.dataPath,
                "RetroOS-Icons", "png");
            if (!string.IsNullOrEmpty(path)) Write(path, 48, 10);
        }

        // Batch entry point: -executeMethod ...RetroIconSheet.WriteDefault
        public static void WriteDefault()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "RetroOS-Icons.png");
            Write(path, 48, 10);
            Debug.Log("RetroOS: icon sheet written to " + path);
        }

        public static void Write(string path, int size, int columns)
        {
            var icons = RetroIcons.All;
            var cell = size + 8;
            var rows = Mathf.CeilToInt(icons.Length / (float)columns);

            var width = columns * cell;
            var height = rows * cell;
            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);

            var background = new Color32(58, 58, 62, 255);
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = background;
            sheet.SetPixels32(pixels);

            for (var i = 0; i < icons.Length; i++)
            {
                var icon = RetroIcons.CreateTexture(icons[i], size);
                var col = i % columns;
                var row = i / columns;
                var x0 = col * cell + 4;
                // Texture rows run bottom-up, so flip the row index.
                var y0 = (rows - 1 - row) * cell + 4;

                for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                    {
                        var source = icon.GetPixel(x, y);
                        if (source.a <= 0.01f) continue;
                        var blended = Color.Lerp((Color)background, source, source.a);
                        sheet.SetPixel(x0 + x, y0 + y, blended);
                    }

                Object.DestroyImmediate(icon);
            }

            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }
    }
}
