using System;
using System.IO;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Events;

namespace Deepwave.RetroOS.Core.Storage
{
    /// <summary>The printer on the parallel cable. A printout is a real file in a Printouts folder, and <c>printer.job</c> fires.</summary>
    public static class RetroPrinter
    {
        /// <summary>Folder printouts are filed into, inside the user's documents.</summary>
        public const string FolderName = "Printouts";

        /// <summary>What came back from a print.</summary>
        public readonly struct Job
        {
            public bool Ok { get; }

            /// <summary>What to tell the player.</summary>
            public string Message { get; }

            /// <summary>Where the printout landed, in the machine's own paths.</summary>
            public string Path { get; }

            public Job(bool ok, string message, string path)
            {
                Ok = ok;
                Message = message;
                Path = path;
            }
        }

        /// <summary>Prints a document into the signed-in user's Printouts folder.</summary>
        /// <param name="title">Job name; becomes the file name.</param>
        public static Job Print(RetroOsContext os, string title, string body)
        {
            if (os == null) return new Job(false, "There is no printer.", null);

            var folder = Destination(os);
            if (folder == null || string.IsNullOrWhiteSpace(folder.SourcePath))
                return new Job(false,
                    "The printer has nowhere to file this.\n\n" +
                    "Sign in to an account with a Documents folder.", null);

            var name = FileName(title);
            var target = System.IO.Path.Combine(folder.SourcePath, name);

            var stamp = os.Clock?.Now ?? DateTime.Now;
            var page = $"{title}\n{stamp:dddd, d MMMM yyyy  HH:mm}\n" +
                       new string('-', 60) + "\n\n" + (body ?? "");

            if (!DiskMount.SafeWriteText(target, page))
                return new Job(false, "The printer did not respond.", null);

            os.Drives.Refresh();

            var path = folder.Path + "/" + name;
            os.Events.Raise(OsEvents.PrinterJob, path);
            os.NotifyFsChanged(path, Events.FsChange.Created);

            return new Job(true, "Printed to " + path, path);
        }

        // The signed-in user's Printouts folder, made if it is not there yet.
        static VfsDirectory Destination(RetroOsContext os)
        {
            var documents = os.Drives.DocumentsOf(os.CurrentUser);
            if (documents == null) return null;

            if (documents.Find(FolderName) is VfsDirectory existing) return existing;

            // First print of the game makes the tray.
            if (string.IsNullOrWhiteSpace(documents.SourcePath)) return null;
            if (!DiskOperations.CreateFolder(documents.SourcePath, FolderName).Ok) return null;

            os.Drives.RefreshAt(documents.SourcePath);
            return os.Fs.Resolve(documents.Path + "/" + FolderName) as VfsDirectory;
        }

        // A job title comes from a page heading and can be anything.
        static string FileName(string title)
        {
            var name = (title ?? "").Trim();
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, ' ');
            name = name.Trim();

            if (name.Length == 0) name = "Document";
            if (name.Length > 48) name = name[..48].TrimEnd();

            return name + ".txt";
        }
    }
}
