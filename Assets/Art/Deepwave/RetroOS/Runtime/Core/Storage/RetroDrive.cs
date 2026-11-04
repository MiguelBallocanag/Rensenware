using Deepwave.RetroOS.Core.FileSystem;

namespace Deepwave.RetroOS.Core.Storage
{
    // What kind of drive a letter is. Decides the icon, whether it can be
    // ejected, and whether an empty folder means "no disk in the drive".
    public enum DriveKind
    {
        Fixed,      // C: - the hard disk
        Floppy,     // A: - removable, usually empty
        CdRom,      // D: - removable, read only
        Removable,  // anything else you can take out
        Network     // \\machine\share, mapped to a letter
    }

    // One drive: a letter, a volume label, and the folder on disk behind it.
    // Drives are the top-level folders of a machine's folder (folder `C` = C:);
    // a `<name>.rmeta` sidecar can override letter, kind and label.
    public class RetroDrive
    {
        public char Letter { get; internal set; }
        public string Label { get; internal set; }
        public DriveKind Kind { get; internal set; }

        // Folder on disk. Empty for a removable drive with nothing in it.
        public string Path { get; internal set; }

        /// <summary>Accounts of the owning machine, carried so a remount can tell a home folder from an ordinary one.</summary>
        public System.Collections.Generic.IReadOnlyList<string> Accounts { get; internal set; }

        // The mounted tree. Always present, possibly empty.
        public VfsDirectory Root { get; internal set; }

        // A removable drive with no media reports not ready, like clicking an empty A:.
        public bool Ready { get; internal set; } = true;

        public bool Removable => Kind is DriveKind.Floppy or DriveKind.CdRom or DriveKind.Removable;

        public bool ReadOnly => Kind == DriveKind.CdRom;

        // "MAIN (C:)" / "(A:)" - the Explorer caption.
        public string DisplayName => string.IsNullOrEmpty(Label)
            ? $"({Letter}:)"
            : $"{Label} ({Letter}:)";

        // The node name inside the virtual file system, e.g. "C:".
        public string NodeName => Letter + ":";

        public override string ToString() => DisplayName;
    }
}
