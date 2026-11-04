using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Deepwave.RetroOS.EditorTools
{
    /// The Inspector/Hierarchy/Project icons for this package's main components. Not the
    /// OS's own desktop icons: those are 32px against teal, these are 16px on two skins.
    /// Laid out on a 16x16 grid, written at x4 point-filtered. Only the nine components a scene is built from get one.
    public static class RetroInspectorIcons
    {
        const string Folder = "Assets/Deepwave/RetroOS/Editor/Icons";
        const string PackageRoot = "Assets/Deepwave/RetroOS";

        // The design grid, and how many real pixels each cell becomes.
        const int Grid = 16;
        const int Scale = 4;

        static readonly Dictionary<char, Color32> Palette = new()
        {
            { '.', new Color32(0, 0, 0, 0) },         // transparent
            { 'K', new Color32(22, 22, 27, 255) },    // outline
            { 'D', new Color32(85, 85, 95, 255) },    // recess
            { 'G', new Color32(156, 156, 168, 255) }, // shaded face
            { 'L', new Color32(217, 217, 226, 255) }, // face
            { 'W', new Color32(255, 255, 255, 255) }, // top edge
            { 'T', new Color32(15, 125, 125, 255) },  // RetroOS teal
            { 'C', new Color32(79, 210, 210, 255) },  // lit teal
            { 'B', new Color32(27, 46, 134, 255) },   // title-bar navy
            { 'Y', new Color32(242, 195, 60, 255) },  // amber
            { 'R', new Color32(192, 57, 43, 255) },   // red
            { 'N', new Color32(63, 163, 77, 255) },   // green
        };

        // Nine components, nothing else. There is no rule that infers an icon - a rule is how the package ended up marking every script.
        static readonly Dictionary<string, string[]> Art = new()
        {
            // The computer: a tube on a stand with a lit desktop - the package's mark, the only icon showing the OS running.
            {
                "RetroOsBootstrap",
                new[]
                {
                    "................",
                    ".KKKKKKKKKKKKKK.",
                    ".KWWWWWWWWWWWWK.",
                    ".KWKKKKKKKKKKGK.",
                    ".KWKTTTTTTTTKGK.",
                    ".KWKTTTTTTTTKGK.",
                    ".KWKTTTTTTTTKGK.",
                    ".KWKTTTTTTTTKGK.",
                    ".KWKWLLLLLLLKGK.",
                    ".KWKKKKKKKKKKGK.",
                    ".KWLLLLLLLNLLGK.",
                    ".KGGGGGGGGGGGGK.",
                    ".KKKKKKKKKKKKKK.",
                    ".....KKLLKK.....",
                    "...KKLLLLLLKK...",
                    "...KKKKKKKKKK...",
                }
            },

            // The case: tall on purpose, so at 16px it is not the same wide rectangle as the screen.
            {
                "RetroMachine",
                new[]
                {
                    "................",
                    "...KKKKKKKKKK...",
                    "...KWWWWWWWWK...",
                    "...KLLLLLLLLK...",
                    "...KLKKKKKKLK...",
                    "...KLKDDDDKLK...",
                    "...KLKKKKKKLK...",
                    "...KLLLLLLLLK...",
                    "...KLKKKKKKLK...",
                    "...KLKDDDDKLK...",
                    "...KLKKKKKKLK...",
                    "...KLLLLLLLLK...",
                    "...KLLLNLLLLK...",
                    "...KGGGGGGGGK...",
                    "...KKKKKKKKKK...",
                    "................",
                }
            },

            // The screen alone: bars, no stand - the stand stays on the computer icon.
            {
                "RetroMonitor",
                new[]
                {
                    "................",
                    "................",
                    "KKKKKKKKKKKKKKKK",
                    "KWWWWWWWWWWWWWWK",
                    "KWKKKKKKKKKKKKGK",
                    "KWKWWYYCCNNRRKGK",
                    "KWKWWYYCCNNRRKGK",
                    "KWKWWYYCCNNRRKGK",
                    "KWKWWYYCCNNRRKGK",
                    "KWKWWYYCCNNRRKGK",
                    "KWKKKKKKKKKKKKGK",
                    "KWLLLLNLLLLLDDGK",
                    "KGGGGGGGGGGGGGGK",
                    "KKKKKKKKKKKKKKKK",
                    "................",
                    "................",
                }
            },

            // Where every machine's files live: a stack of platters.
            {
                "RetroWorld",
                new[]
                {
                    "................",
                    "...KKKKKKKKKK...",
                    ".KKCCWWCCCCCCKK.",
                    ".KCCCCCCCCCCCCK.",
                    ".KKKKKKKKKKKKKK.",
                    ".KTTTTTTTTTTTTK.",
                    ".KTTTTTTTTTTTTK.",
                    ".KKKKKKKKKKKKKK.",
                    ".KTTTTTTTTTTTTK.",
                    ".KTTTTTTTTTTTTK.",
                    ".KKKKKKKKKKKKKK.",
                    ".KTTTTTTTTTTTTK.",
                    ".KTTTTTTTTTTTTK.",
                    ".KKKKKKKKKKKKKK.",
                    "...KKKKKKKKKK...",
                    "................",
                }
            },

            // The line to the outside world.
            {
                "RetroInternet",
                new[]
                {
                    "................",
                    "....KKKKKKKK....",
                    "..KKTTTTTTTTKK..",
                    ".KTTTTCCCCTTTTK.",
                    ".KTTTCTTTTCTTTK.",
                    "KTTTCTTTTTTCTTTK",
                    "KCCCCCCCCCCCCCCK",
                    "KTTTCTTTTTTCTTTK",
                    "KTTTCTTTTTTCTTTK",
                    "KCCCCCCCCCCCCCCK",
                    "KTTTCTTTTTTCTTTK",
                    ".KTTTCTTTTCTTTK.",
                    ".KTTTTCCCCTTTTK.",
                    "..KKTTTTTTTTKK..",
                    "....KKKKKKKK....",
                    "................",
                }
            },

            // One site on it.
            {
                "RetroInternetHost",
                new[]
                {
                    "................",
                    "..KKKKKKKKKKKK..",
                    "..KBBBBBBBBBBK..",
                    "..KBWWBBBBBBBK..",
                    "..KBBBBBBBBBBK..",
                    "..KLLLLLLLLLLK..",
                    "..KLGGGGGGGGLK..",
                    "..KLLLLLLLLLLK..",
                    "..KLGGGGGGGGLK..",
                    "..KLLLLLLLLLLK..",
                    "..KLGGGGGGGGLK..",
                    "..KLLLLLLLLLLK..",
                    "..KLGGGGGLLLLK..",
                    "..KLLLLLLLLLLK..",
                    "..KKKKKKKKKKKK..",
                    "................",
                }
            },

            // Something happened in the OS, and this is what answers.
            {
                "RetroOsEventListener",
                new[]
                {
                    "................",
                    "........KKKKK...",
                    ".......KKYYYK...",
                    "......KKYYYK....",
                    ".....KKYYYK.....",
                    "....KKYYYK......",
                    "....KYYYKKKKK...",
                    "....KYYYYYYYK...",
                    "....KKKKKYYYK...",
                    "........KYYK....",
                    ".......KYYK.....",
                    "......KYYK......",
                    ".....KYYK.......",
                    ".....KYK........",
                    ".....KK.........",
                    "................",
                }
            },

            // A camera in the room, hanging from its bracket and looking right.
            {
                "CctvCameraDevice",
                new[]
                {
                    "................",
                    "................",
                    "....KKKKKK......",
                    "......KK........",
                    "......KK........",
                    ".KKKKKKKKKKK....",
                    ".KWWWWWWWWWKKK..",
                    ".KLLLLLLLLLKCCK.",
                    ".KLLLLLLLLLKCCK.",
                    ".KLRLLLLLLLKCCK.",
                    ".KGGGGGGGGGKKK..",
                    ".KKKKKKKKKKK....",
                    "................",
                    "................",
                    "................",
                    "................",
                }
            },

            // The look of the whole system, as the swatches it is made of.
            {
                "RetroTheme",
                new[]
                {
                    "................",
                    ".KKKKKKKKKKKKKK.",
                    ".KLLLLLLLLLLLLK.",
                    ".KLKKKKKKKKKKLK.",
                    ".KLKBBBBTTTTKLK.",
                    ".KLKBBBBTTTTKLK.",
                    ".KLKBBBBTTTTKLK.",
                    ".KLKKKKKKKKKKLK.",
                    ".KLKYYYYRRRRKLK.",
                    ".KLKYYYYRRRRKLK.",
                    ".KLKYYYYRRRRKLK.",
                    ".KLKKKKKKKKKKLK.",
                    ".KLLLLLLLLLLLLK.",
                    ".KKKKKKKKKKKKKK.",
                    "................",
                    "................",
                }
            },
        };

        /// <summary>Paints the icons, assigns them, and takes the icon off every other script in the package.</summary>
        /// <returns>How many scripts were given an icon.</returns>
        public static int Rebuild()
        {
            Directory.CreateDirectory(Folder);

            var scripts = AssetDatabase
                .FindAssets("t:MonoScript", new[] { PackageRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => (path, script: AssetDatabase.LoadAssetAtPath<MonoScript>(path)))
                .Where(s => s.script && s.script.GetClass() != null)
                .ToList();

            var painted = 0;
            var cleared = 0;
            var wanted = new HashSet<string>();

            foreach (var (path, script) in scripts)
            {
                var name = script.GetClass().Name;

                if (AssetImporter.GetAtPath(path) is not MonoImporter importer)
                {
                    // Only worth saying for a script that was meant to get one:
                    // one that was not keeps Unity's icon either way.
                    if (Art.ContainsKey(name))
                        Debug.LogWarning(
                            $"RetroOS: '{path}' has no script importer, so {name} keeps Unity's "
                                + "default icon."
                        );
                    continue;
                }

                if (Art.TryGetValue(name, out var art))
                {
                    var texture = Write(name, art);
                    if (!texture)
                        continue;

                    wanted.Add($"{Folder}/{name}.png");

                    // Through the importer, and saved. EditorGUIUtility.SetIconForObject is memory-only - it never reaches the .meta, so never a customer.
                    importer.SetIcon(texture);
                    importer.SaveAndReimport();
                    painted++;
                }
                else if (importer.GetIcon())
                {
                    importer.SetIcon(null);
                    importer.SaveAndReimport();
                    cleared++;
                }
            }

            var stale = Directory
                .GetFiles(Folder, "*.png")
                .Select(p => p.Replace('\\', '/'))
                .Where(p => !wanted.Contains(p))
                .ToList();
            foreach (var path in stale)
                AssetDatabase.DeleteAsset(path);

            foreach (var name in Art.Keys.Where(n => scripts.All(s => s.script.GetClass().Name != n)))
                Debug.LogWarning(
                    $"RetroOS: there is icon art for '{name}', but no script by that name is in "
                        + "the package. Rename the art or delete it."
                );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"RetroOS: painted {painted} component icon(s) into {Folder}"
                    + (cleared > 0 ? $", cleared {cleared} from scripts that no longer carry one" : "")
                    + (stale.Count > 0 ? $", deleted {stale.Count} unused icon file(s)" : "")
                    + "."
            );
            return painted;
        }

        // One glyph, magnified by whole pixels and imported unfiltered - a
        // pixel-art icon Unity has smoothed is worse than no icon at all.
        static Texture2D Write(string name, string[] art)
        {
            if (art.Length != Grid)
            {
                Debug.LogWarning(
                    $"RetroOS: the icon art for '{name}' is {art.Length} rows, not {Grid}. It was "
                        + "skipped."
                );
                return null;
            }

            const int size = Grid * Scale;
            var pixels = new Color32[size * size];

            for (var row = 0; row < Grid; row++)
            {
                var line = art[row];
                if (line.Length != Grid)
                {
                    Debug.LogWarning(
                        $"RetroOS: row {row} of the icon art for '{name}' is {line.Length} "
                            + $"characters, not {Grid}. It was skipped."
                    );
                    return null;
                }

                for (var column = 0; column < Grid; column++)
                {
                    if (!Palette.TryGetValue(line[column], out var colour))
                    {
                        Debug.LogWarning(
                            $"RetroOS: the icon art for '{name}' uses '{line[column]}', which is "
                                + "not in the palette. It was skipped."
                        );
                        return null;
                    }

                    // Row 0 of the art is the top of the icon; row 0 of a
                    // texture is the bottom.
                    for (var y = 0; y < Scale; y++)
                    for (var x = 0; x < Scale; x++)
                        pixels[(size - 1 - (row * Scale + y)) * size + column * Scale + x] = colour;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();

            var path = $"{Folder}/{name}.png";
            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"RetroOS: could not write '{path}'. {e.Message}");
                return null;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.GUI;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            else
            {
                Debug.LogWarning(
                    $"RetroOS: '{path}' did not import as a texture, so it may be drawn blurred "
                        + "in the Inspector."
                );
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
