namespace Deepwave.RetroOS.Apps
{
    /// <summary>When the OS asks an app to put back what it remembered.</summary>
    public enum SettingsMoment
    {
        /// <summary>Before the first window is drawn. Anything that changes how the OS looks belongs here.</summary>
        Booting,

        /// <summary>Once the desktop exists and somebody is signed in. For anything that needs a screen or belongs to one account.</summary>
        Desktop
    }

    /// <summary>
    /// An app that remembers something between sessions and puts it back. The OS
    /// knows only when to ask. Implement it on the <see cref="RetroAppBehaviour"/>,
    /// not the view - the behaviour is on the machine from boot; the view is not.
    /// </summary>
    public interface IRemembersSettings
    {
        /// <param name="os">The machine's OS.</param>
        /// <param name="moment">Which of the two restore points this is.</param>
        void Restore(RetroOsContext os, SettingsMoment moment);
    }
}
