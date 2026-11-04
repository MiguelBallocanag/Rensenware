using System.Collections.Generic;
using UnityEngine;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Apps.Components
{
    // The messenger. Each contact carries a scripted conversation: lines arrive after a
    // pause, and a line can stop and wait for the player to pick a reply.
    [AddComponentMenu("")]
    public class RetroChatApp : RetroAppBehaviour, IChatSource
    {
        [Header("People")]
        [Tooltip("Who the player can talk to. Mark one hidden to hold it back " +
                 "until a rule brings them online.")]
        [SerializeField] List<ChatContact> contacts = new();

        public List<ChatContact> Contacts => contacts;

        // Kept here rather than in the window, so a conversation the player
        // walked away from is still part-way through when they come back.
        readonly Dictionary<string, ChatThread> _threads = new();

        public Dictionary<string, ChatThread> Threads => _threads;

        string _owner;

        public void KeepFor(string username)
        {
            if (username == _owner) return;
            _owner = username;
            _threads.Clear();
        }

        /// <summary>
        /// Brings a contact online or off - the hook a rule uses to make somebody
        /// available part-way through.
        /// </summary>
        public void SetOnline(string contactId, bool online)
        {
            foreach (var contact in contacts)
            {
                if (contact == null || contact.Id != contactId) continue;
                contact.online = online;
                contact.hidden = false;
                return;
            }
        }

        // One-argument forms, so a UnityEvent in the inspector can call them
        // straight from a RetroOS Event Listener or an app hook.

        /// <summary>Puts a contact in the list and marks them online.</summary>
        public void BringOnline(string contactId) => SetOnline(contactId, true);

        /// <summary>Greys a contact out. They stay in the list.</summary>
        public void TakeOffline(string contactId) => SetOnline(contactId, false);

        public override void BuildContent(AppContext ctx) => AppView.Attach<ChatView>(ctx);

        public override void ApplyDefaults()
        {
            app.appId = "chat";
            app.title = "Messenger";
            app.builtinIcon = BuiltinIcon.Contacts;
            app.category = "Accessories";
            app.windowSize = new Vector2(600, 420);
            app.minWindowSize = new Vector2(420, 300);
            app.showOnDesktop = false;
        }
    }
}
