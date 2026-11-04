using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Windowing;

namespace Deepwave.RetroOS.Apps
{
    /// <summary>Base for a program's interface when it needs its own state and lifecycle. Lives on the window content, so it dies with the window.</summary>
    public abstract class AppView : MonoBehaviour
    {
        protected AppContext Context;
        protected RetroOsContext Os;
        protected Window Window;
        protected RectTransform Root;

        // The program that launched this view.
        protected IRetroApp App;

        /// <summary>Adds a view to the window content and builds it. The usual body of <see cref="RetroAppBehaviour.BuildContent"/>.</summary>
        public static T Attach<T>(AppContext ctx) where T : AppView
        {
            var view = ctx.Content.gameObject.AddComponent<T>();
            view.Setup(ctx);
            return view;
        }

        public void Setup(AppContext ctx)
        {
            Context = ctx;
            Os = ctx.Os;
            Window = ctx.Window;
            Root = ctx.Content;
            App = ctx.App;
            Build();
        }

        /// <summary>Builds the interface. Called once, with <c>Os</c>, <c>Window</c> and <c>Root</c> set.</summary>
        protected abstract void Build();

        /// <summary>The player opened another file with this (single-instance) program while its window was up.</summary>
        public virtual void OnFileOpened(VfsFile file) { }

        /// <summary>Looks up a per-app content contract (<see cref="ICameraSource"/>, <see cref="IMailSource"/>...) on the launching program.</summary>
        protected T Config<T>() where T : class => Context?.Config<T>();

        /// <summary>Whether this view may read the keyboard directly: this window is on top, nothing is being typed into, and the player is at this machine.</summary>
        protected bool HasKeyboard =>
            Os != null &&
            Os.Windows?.Active == Window &&
            Os.IsFocused() &&
            !UI.RetroUi.IsTyping();

        /// <summary>Raises one of this program's own game events (inspector UnityEvents + <c>&lt;appId&gt;.&lt;action&gt;</c> on the bus).</summary>
        protected void Raise(string action, string argument = null) => Context?.Raise(action, argument);
    }
}
