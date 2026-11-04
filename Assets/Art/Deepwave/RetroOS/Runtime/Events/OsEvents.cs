namespace Deepwave.RetroOS.Events
{
    /// <summary>The standard event ids the OS raises, with each one's argument noted in the comment beside it.</summary>
    public static class OsEvents
    {
        public const string Boot = "os.boot";                    // ()
        public const string Login = "os.login";                  // (username)
        public const string Logout = "os.logout";                // (username)
        public const string Shutdown = "os.shutdown";            // ()
        public const string PowerOn = "os.poweron";              // () - booted again after a shutdown
        public const string Bsod = "os.bsod";                    // (title) - the machine crashed

        public const string AppOpened = "app.opened";            // (appId)
        public const string AppClosed = "app.closed";            // (appId)
        public const string AppFocused = "app.focused";          // (appId)
        public const string AppMinimized = "app.minimized";      // (appId)
        public const string AppMaximized = "app.maximized";      // (appId)
        public const string AppAction = "app.action";            // (appId:action[:arg])

        public const string FileOpened = "file.opened";          // (path)
        public const string AccessDenied = "file.denied";        // (path) - another user's
        public const string FileSaved = "file.saved";            // (path)
        public const string NodeUnlocked = "node.unlocked";      // (path)
        public const string NodeRevealed = "node.revealed";      // (path)
        public const string FsChanged = "fs.changed";            // (reason) - views refresh on this

        public const string ExplorerNavigated = "explorer.navigated"; // (path)
        public const string TerminalCommand = "terminal.command";     // (full line)

        public const string MailReceived = "mail.received";      // (mailId)
        public const string MailRead = "mail.read";              // (mailId)
        public const string MailSent = "mail.sent";              // (recipient)
        public const string MailDeleted = "mail.deleted";        // (mailId)

        public const string ReminderDue = "reminder.due";        // (reminder id)

        public const string PrinterJob = "printer.job";          // (path of the printout)

        public const string ChatMessage = "chat.message";        // (contact id)
        public const string ChatSaid = "chat.said";              // (what was typed)

        public const string WebVisited = "web.visited";          // (url)
        public const string WebFailed = "web.failed";            // (url) - 404 / host not found
        public const string WebDownload = "web.download";        // (url)

        public const string CctvSelected = "cctv.selected";      // (camera label)

        public const string DiscInserted = "disc.inserted";      // (label)
        public const string DiscEjected = "disc.ejected";        // (label)

        // Storage zones mounted from real folders on disk.
        public const string ZoneMounted = "net.mounted";         // (zone/host)
        public const string HostReached = "net.reached";         // (host)
        public const string HostUnreachable = "net.unreachable"; // (host)

        public const string FlagSet = "flag.set";                // (flag name)
        public const string FlagCleared = "flag.cleared";        // (flag name)
    }
}
