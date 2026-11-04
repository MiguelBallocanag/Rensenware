namespace Deepwave.RetroOS.Core.FileSystem
{
    // What viewer a file opens in. Values are serialized in profiles - append only.
    public enum FileKind
    {
        Text = 0,
        Image = 1,
        Audio = 2,
        Video = 3,
        Unknown = 4,
        Web = 5,          // HTML pages from the Internet/LAN folders
        Binary = 6,       // shown as a plain binary blob
        Executable = 7    // runnable from the file list
    }
}
