namespace Deepwave.RetroOS.Core.FileSystem
{
    // What a node is beyond "file"/"folder". Drives folder privacy and icon choice,
    // so terminal, file dialog and Explorer all agree.
    public enum NodeRole
    {
        Normal = 0,

        Drive,              // a drive letter at the root: C:, A:, D:

        // Network Neighborhood branch, a machine in it, a published folder
        NetworkRoot,
        NetworkHost,
        Share,

        UserRoot,           // C:\Users - holds everybody's home
        UserHome,           // one person's home, and the folders inside it
        Documents,
        Pictures,
        Desktop,

        MyDocuments,        // My Computer shortcut to the signed-in user's documents

        // system folders a player should recognise but rarely open
        System,
        Programs
    }
}
