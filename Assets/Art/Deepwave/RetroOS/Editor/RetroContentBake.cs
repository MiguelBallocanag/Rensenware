using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Deepwave.RetroOS.Core.Storage;

namespace Deepwave.RetroOS.EditorTools
{
    /// <summary>
    /// Bakes <c>StreamingAssets/RetroOS/</c> into one file under <c>Resources/</c>,
    /// for the two build targets that cannot open StreamingAssets as a folder.
    ///
    /// The OS shows real files on a real disk, which is why a desktop player can
    /// simply copy the shipped folder into the player's Documents on first run. A
    /// WebGL build cannot: <c>Application.streamingAssetsPath</c> there is a URL,
    /// and Android's is a path inside the .apk. Both answer <c>false</c> to
    /// <c>Directory.Exists</c>, so nothing was ever copied and the OS came up with
    /// no drives, no web sites, no pictures and no sound - with no error, because
    /// an empty folder is not a failure anywhere else in the package.
    /// </summary>
    public static class RetroContentBake
    {
        /// <summary>Whether a build for this target has to be baked to have any content at all.</summary>
        public static bool Needs(BuildTarget target) =>
            target is BuildTarget.WebGL or BuildTarget.Android;

        /// <summary>Whether a pack has been baked into the project.</summary>
        public static bool Baked => File.Exists(RetroContentPack.AssetPath);

        /// <summary>Whether the baked pack still matches the folder it was made from. True when there is no pack.</summary>
        public static bool Stale()
        {
            if (!Baked) return true;

            try
            {
                return RetroContentPack.HashIn(File.ReadAllBytes(RetroContentPack.AssetPath))
                       != RetroContentPack.HashOf(RetroStorage.SourceRoot);
            }
            catch (IOException)
            {
                return true;
            }
        }

        /// <summary>Bakes if a build would need one and there is none, or the one there has fallen behind the folder. Quiet when nothing is due.</summary>
        public static void RebakeIfDue()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Directory.Exists(RetroStorage.SourceRoot)) return;
            if (Baked && !Stale()) return;

            var first = !Baked;
            if (!Bake(log: false)) return;

            Debug.Log(first
                ? "RetroOS: baked the OS's files into the build for WebGL and Android. This "
                  + "keeps itself current - you do not need to run it by hand."
                : "RetroOS: the baked web/Android content was behind the files under "
                  + "StreamingAssets and has been rebuilt.");
        }

        // A person who imports this package to make a browser game is exactly the
        // person who does not yet know a WebGL build needs a bake. Check once per
        // editor session on load; the asset postprocessor covers the rest.
        [InitializeOnLoadMethod]
        static void KeepBakedOnLoad()
        {
            if (SessionState.GetBool("RetroOS.BakeChecked", false)) return;
            SessionState.SetBool("RetroOS.BakeChecked", true);
            EditorApplication.delayCall += RebakeIfDue;
        }

        [MenuItem("Deepwave/RetroOS/Bake Content", priority = 10)]
        public static void BakeMenu() => Bake(log: true);

        [MenuItem("Deepwave/RetroOS/Bake Content", validate = true)]
        static bool CanBake() => Directory.Exists(RetroStorage.SourceRoot);

        /// <summary>Packs the data folder into the project. <c>false</c> when there is nothing to pack.</summary>
        public static bool Bake(bool log)
        {
            var source = RetroStorage.SourceRoot;
            var pack = RetroContentPack.Create(source);

            if (pack == null)
            {
                Debug.LogWarning(
                    $"RetroOS: there is nothing under '{source}' to bake. Write the sample " +
                    "content first (Deepwave > RetroOS > Setup), or point the World at the folder " +
                    "your machines and sites are really in.");
                return false;
            }

            var folder = Path.GetDirectoryName(RetroContentPack.AssetPath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllBytes(RetroContentPack.AssetPath, pack);
            AssetDatabase.ImportAsset(RetroContentPack.AssetPath, ImportAssetOptions.ForceUpdate);

            if (log)
                Debug.Log(
                    $"RetroOS: baked {RetroContentPack.Files(source).Length} file(s), " +
                    $"{pack.Length / 1024} KB, into '{RetroContentPack.AssetPath}'. WebGL and " +
                    "Android builds read the OS's files out of this; every other platform " +
                    "copies the StreamingAssets folder and ignores it.");

            return true;
        }

        // A bake that has fallen behind the folder is worse than none: it ships
        // last week's web site inside this week's game, and looks like it worked.
        // So it follows every change under StreamingAssets, and makes the first
        // one itself the moment content appears with no bake beside it.
        class Watcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                string[] moved, string[] movedFrom)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!Touched(imported) && !Touched(deleted) && !Touched(moved)) return;

                EditorApplication.delayCall += RebakeIfDue;
            }

            static bool Touched(string[] paths)
            {
                var root = "Assets/StreamingAssets/";
                foreach (var path in paths)
                    if (path.StartsWith(root, System.StringComparison.OrdinalIgnoreCase))
                        return true;
                return false;
            }
        }

        // Refusing the build is the point. A WebGL player with no baked content
        // builds perfectly and boots to an OS with nothing in it, which is a fault
        // nobody finds until it is on a web page.
        class Check : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
                var target = report.summary.platform;
                if (!Needs(target)) return;
                if (!Directory.Exists(RetroStorage.SourceRoot)) return;

                // A pack that is present but behind the folder is refreshed here
                // and the build carries on - it is already in the resource set,
                // only its bytes change. A missing one still stops the build: a
                // Resources asset made this late may not be collected, and the
                // editor's own load-time and postprocess bakes had every chance.
                if (Baked && Stale())
                {
                    Bake(log: true);
                    AssetDatabase.Refresh();
                }

                if (!Baked)
                    throw new BuildFailedException(
                        $"RetroOS: a {target} build cannot open StreamingAssets as a folder, so " +
                        "this player would boot with no drives, no web sites, no pictures and " +
                        "no sound. Run Deepwave > RetroOS > Bake Content For Web, then build again.");

                if (Stale())
                    throw new BuildFailedException(
                        "RetroOS: the baked content is older than the files under " +
                        $"'{RetroStorage.SourceRoot}', so this {target} build would ship the " +
                        "previous version of the OS's disks. Run Deepwave > RetroOS > Bake Content " +
                        "For Web, then build again.");
            }
        }
    }
}
