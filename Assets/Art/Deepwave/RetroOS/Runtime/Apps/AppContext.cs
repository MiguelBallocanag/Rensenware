using UnityEngine;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.Windowing;

namespace Deepwave.RetroOS.Apps
{
    /// <summary>Everything one running program instance needs: the OS services, its window, the file it was opened for, and a channel for its game events.</summary>
    public class AppContext
    {
        public RetroOsContext Os { get; internal set; }
        public Window Window { get; internal set; }

        // The launching program; views read per-app settings via Config<T>().
        public IRetroApp App { get; internal set; }

        // The rect an app fills with its UI.
        public RectTransform Content => Window.Content;

        // Set when the app was launched to open a file, otherwise null.
        public VfsFile File { get; internal set; }

        /// <summary>Looks up a per-app content contract (<see cref="ICameraSource"/>, <see cref="IMailSource"/>...) on the launching program.</summary>
        /// <returns>The contract, or <c>null</c> when the program does not implement it.</returns>
        public T Config<T>() where T : class => App as T;

        /// <summary>
        /// Raises an app-specific game event - everything the player does in a
        /// program goes through here. Fires the inspector UnityEvents and puts
        /// <c>&lt;appId&gt;.&lt;action&gt;</c> and a generic <c>app.action</c> on the bus.
        /// </summary>
        /// <param name="action">Short name of what happened, e.g. <c>"won"</c>.</param>
        /// <param name="argument">Optional payload, e.g. the score or a path.</param>
        public void Raise(string action, string argument = null)
        {
            if (string.IsNullOrWhiteSpace(action)) return;
            var appId = App?.Id ?? "app";
            var payload = string.IsNullOrEmpty(argument) ? action : action + ":" + argument;

            App?.OnAppEvent(AppLifecycle.Action, this, payload);
            Os?.Events.Raise(appId + "." + action, argument);
            Os?.Events.Raise(Events.OsEvents.AppAction, appId + ":" + payload);
        }
    }
}
