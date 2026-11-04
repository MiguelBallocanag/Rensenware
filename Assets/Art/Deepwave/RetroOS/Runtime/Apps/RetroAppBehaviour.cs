using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;

namespace Deepwave.RetroOS.Apps
{
    /// <summary>
    /// A program installed on the machine: a component on a child of the RetroOS
    /// bootstrap, configured in the inspector (icon, window, placement, UnityEvents).
    /// Write your own by subclassing this and building uGUI in <see cref="BuildContent"/>
    /// (see <c>Samples/Scripts/Apps/ClockApp.cs</c>).
    /// </summary>
    public abstract class RetroAppBehaviour : MonoBehaviour, IRetroApp
    {
        [SerializeField] protected AppSettings app = new();

        [Header("Game Events")]
        [Tooltip("Inspector reactions to what the player does in this program.")]
        [SerializeField] protected RetroAppEvents events = new();

        public AppSettings Settings => app;
        public RetroAppEvents Events => events;

        public string Id => app.ResolveId(name);
        public string Title => app.ResolveTitle(name);
        public Sprite IconSprite => app.ResolveIcon();

        public Vector2 WindowSize => app.windowSize;
        public Vector2 MinWindowSize => app.minWindowSize;
        public bool Resizable => app.resizable;
        public bool StartMaximized => app.startMaximized;
        public bool SingleInstance => app.singleInstance;
        public bool ShowOnDesktop => app.showOnDesktop;
        public bool ShowInStartMenu => app.showInStartMenu;
        public string Category => app.category;
        public bool LaunchOnBoot => app.launchOnBoot;
        public int SortOrder => app.sortOrder;

        /// <summary>Caption for the window. Override to show the open file's name, like Notepad.</summary>
        public virtual string WindowTitle(VfsFile file) => Title;

        /// <summary>Whether this program handles a double-clicked file. Override to claim a kind or extension.</summary>
        public virtual bool CanOpen(VfsFile file) => false;

        /// <summary>Builds the program's interface under <c>ctx.Content</c>. Called once per window.</summary>
        public abstract void BuildContent(AppContext ctx);

        /// <summary>Called at each window moment. Base fires the matching inspector UnityEvent; override and call <c>base</c> to also react in code.</summary>
        public virtual void OnAppEvent(AppLifecycle stage, AppContext ctx, string argument) =>
            events.Fire(stage, argument);

        // Editor: seed sensible values the first time the component is added.
        protected virtual void Reset() => ApplyDefaults();

        /// <summary>Fills in default id, title, icon and window size. Called on add and by the setup window.</summary>
        public virtual void ApplyDefaults() { }
    }
}
