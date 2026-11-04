using System.IO;
using UnityEditor;
using UnityEngine;
using Deepwave.RetroOS.Core.Storage;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Icons;

namespace Deepwave.RetroOS.EditorTools
{
    // Writes a working example of everything the OS reads: a machine to browse, a second machine on the LAN, and a web site to visit.
    public static class RetroSampleContent
    {
        public const string Workstation = "workstation-04";
        public const string Server = "records-server";
        public const string Site = "northpoint.local";

        // A button in Deepwave > RetroOS > Setup, not its own menu item: files without the computer that reads them is half a setup.
        public static void CreateAll()
        {
            CreateWorkstation();
            CreateServer();
            CreateSampleSite();
            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(RetroStorage.Root);
            Debug.Log("RetroOS: sample content written to " + RetroStorage.Root);
        }

        // The player's PC: a hard disk with users, an empty floppy drive and
        // a CD in the tray.
        public static void CreateWorkstation()
        {
            var machine = RetroStorage.CreateMachine(Workstation, "operator");
            var c = Path.Combine(machine, "C");
            var docs = Path.Combine(c, "Users", "operator", "Documents");

            Write(Path.Combine(docs, "readme.txt"),
                "NORTHPOINT FACILITY - WORKSTATION 04\n" +
                "====================================\n\n" +
                "Night shift log. This is a real file on disk: open it in Notepad inside\n" +
                "the OS, type into it, and the change is written straight back here.\n\n" +
                "Drives are the folders next to this one - C is the hard disk, A the\n" +
                "floppy, D the CD. The network is whatever other machines in the scene\n" +
                "are sharing.\n\n" +
                "The dial-up connection reaches: " + Site + "\n");

            Write(Path.Combine(docs, "shift-notes.txt"),
                "12/08 22:10  Cameras 2 and 3 back online.\n" +
                "12/08 23:47  Door alarm, sub-level. Nobody there.\n" +
                "13/08 01:02  Someone logged in from the LAN. Not me.\n");

            Write(Path.Combine(c, "Windows", "system.ini"),
                "[boot]\nshell=retroos.exe\n\n[network]\nhost=workstation-04\n");

            Write(Path.Combine(c, "Program Files", "install.log"),
                "MINES.EXE      installed 1996-04-02\n" +
                "SOLITAIRE.EXE  installed 1996-04-02\n" +
                "SNAKE.EXE      installed 1997-01-19\n");

            // A CD sitting in the drive, with a volume label.
            var cd = Path.Combine(machine, "D");
            Directory.CreateDirectory(cd);
            new RetroMeta { Drive = "D", Kind = "cdrom", Label = "EVIDENCE_04" }.Write(cd);
            Write(Path.Combine(cd, "transcript.txt"),
                "EVIDENCE DISC 04 - INTERVIEW TRANSCRIPT\n\n" +
                "Q: you said it followed you up?\n" +
                "A: no. i said it was already up here.\n");
        }

        // A second machine on the LAN: nothing of it shows until a RetroMachine exists for it, and then only its shared folders.
        public static void CreateServer()
        {
            var machine = RetroStorage.CreateMachine(Server, "admin");
            var c = Path.Combine(machine, "C");
            new RetroMeta
            {
                Drive = "C",
                Kind = "fixed",
                Label = "RECORDS",
                // The whole drive is what this server is for, so the drive
                // folder itself carries the share.
                Share = "ARCHIVE",
                ShareComment = "Incident records. Sealed folders need the year."
            }.Write(c);

            Write(Path.Combine(c, "notice.txt"),
                "SHARED DRIVE - MAINTENANCE\n\n" +
                "Do not power down workstation 04. The tape drive is mid-write.\n");

            var archive = Path.Combine(c, "archive");
            Directory.CreateDirectory(archive);
            Write(Path.Combine(archive, "incident-0812.txt"),
                "Incident report 08-12. Filed by the night supervisor.\n\n" +
                "Cameras 2 and 3 lost signal for 94 seconds.\n" +
                "Cross-reference: the same corridor, the same 94 seconds, in 1958.\n");

            // The sidecar is how a folder gets its RetroOS properties.
            new RetroMeta
            {
                Encrypted = true,
                Password = "1958",
                Note = "Sealed after the incident."
            }.Write(archive);
        }

        // A real static web site (index.html, a stylesheet, a second page, a generated banner). Browse to the folder name.
        public static void CreateSampleSite()
        {
            var site = RetroStorage.SiteFolder(Site);
            Directory.CreateDirectory(Path.Combine(site, "img"));

            Write(Path.Combine(site, "style.css"), Css);
            Write(Path.Combine(site, "index.html"), IndexHtml);
            Write(Path.Combine(site, "bulletin.html"), BulletinHtml);
            WritePng(Path.Combine(site, "img", "logo.png"), BuiltinIcon.Shield, 64);
            WritePng(Path.Combine(site, "img", "camera.png"), BuiltinIcon.Camera, 64);

            AssetDatabase.Refresh();
        }

        const string Css = @"body {
  background: #c0c0c0;
  color: #000000;
  font-family: sans-serif;
  padding: 12px;
}
h1 { color: #000080; }
h2 { color: #800000; }
a { color: #0000ee; }
.notice {
  background: #ffffcc;
  border: 1px #808080;
  padding: 8px;
}
.stamp { color: #808080; font-size: 12px; }
th { background: #000080; color: #ffffff; }
";

        const string IndexHtml = @"<html>
<head>
  <title>Northpoint Facility - Intranet</title>
  <link rel=""stylesheet"" href=""style.css"">
</head>
<body>
  <img src=""img/logo.png"" width=""64"" height=""64"">
  <h1>Northpoint Facility</h1>
  <p class=""stamp"">Internal network. Last updated 12 August 1997.</p>

  <div class=""notice"">
    <b>Notice:</b> the sub-level door sensor is being replaced.
    Report anything unusual to the night supervisor.
  </div>

  <h2>Sections</h2>
  <ul>
    <li><a href=""bulletin.html"">Staff bulletin board</a></li>
    <li><a href=""img/camera.png"">Camera reference photo</a></li>
  </ul>

  <hr>
  <p class=""stamp"">This page is a plain .html file in the internet folder.
  Edit it in any editor and reload it in the game.</p>
</body>
</html>
";

        const string BulletinHtml = @"<html>
<head>
  <title>Staff bulletin</title>
  <link rel=""stylesheet"" href=""style.css"">
</head>
<body>
  <h1>Staff bulletin</h1>
  <p><a href=""index.html"">&larr; Back to the front page</a></p>

  <h2>Archive access</h2>
  <p>The archive share is locked. The code is the year it happened.
  If you do not know which year, you do not need the archive.</p>

  <div class=""notice"">
    Cameras 2 and 3 were offline for 94 seconds on the 12th.
    Maintenance says nothing is wrong with them.
  </div>
</body>
</html>
";

        static void Write(string path, string content)
        {
            if (File.Exists(path)) return;      // never clobber authored content
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, content);
        }

        static void WritePng(string path, BuiltinIcon icon, int size)
        {
            if (File.Exists(path)) return;
            var canvas = new IconCanvas(size);
            RetroIconLibrary.Paint(icon, canvas, vivid: true);
            var texture = canvas.ToTexture(icon.ToString());
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }
}
