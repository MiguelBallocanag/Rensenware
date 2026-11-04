using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Deepwave.RetroOS.Data
{
    /// <summary>Who a line in a conversation came from.</summary>
    public enum ChatAuthor
    {
        Them,
        /// <summary>The player, volunteered rather than picked from replies.</summary>
        Me,
        /// <summary>The client itself, e.g. "MSANDOVAL has signed off".</summary>
        System
    }

    /// <summary>One thing the player can say back.</summary>
    [Serializable]
    public class ChatReply
    {
        [Tooltip("What the player says.")]
        public string text = "...";

        [Tooltip("Line to jump to afterwards. -1 carries on with the next one.")]
        public int goTo = -1;

        [Tooltip("Story flag set when it is chosen. Empty sets nothing.")]
        public string setFlag = "";

        [Tooltip("Event id raised when it is chosen. Empty raises nothing.")]
        public string raiseEvent = "";

        [Tooltip("Runs when the player chooses this.")]
        public UnityEvent onChosen = new();
    }

    /// <summary>One line of a scripted conversation.</summary>
    [Serializable]
    public class ChatLine
    {
        public ChatAuthor author = ChatAuthor.Them;

        [TextArea(2, 5)] public string text = "";

        [Tooltip("Seconds before it appears - the pause while they type.")]
        [Range(0f, 20f)] public float delay = 1.2f;

        [Tooltip("Skipped unless this flag is set by the time the conversation " +
                 "reaches it. Empty means always said.")]
        public string requiredFlag = "";

        [Tooltip("Story flag set when it is said.")]
        public string setFlag = "";

        [Tooltip("Event id raised when it is said.")]
        public string raiseEvent = "";

        [Tooltip("A file sent with the line, e.g. /C:/Users/operator/Pictures/x.png - " +
                 "the same path an event argument prints. It opens in whichever " +
                 "program handles it, and obeys the same privacy rules.")]
        public string attachment = "";

        [Tooltip("What the player may say back. Empty just carries on.")]
        public List<ChatReply> replies = new();
    }

    /// <summary>Conversation progress; held by the app so it survives closing the window.</summary>
    public class ChatThread
    {
        /// <summary>Next script line to play.</summary>
        public int Line;

        /// <summary>Everything said so far, in order.</summary>
        public readonly List<string> Log = new();

        /// <summary>Files sent during the conversation, in order.</summary>
        public readonly List<string> Files = new();

        /// <summary>Stopped on a line that is waiting for the player.</summary>
        public bool Waiting;
    }

    /// <summary>Somebody the player can talk to.</summary>
    [Serializable]
    public class ChatContact
    {
        [Tooltip("Stable id used by events and rules. Empty = the name.")]
        public string id = "";

        public string displayName = "Contact";

        [Tooltip("Line under the name in the contact list.")]
        public string status = "Online";

        [Tooltip("Off means they are in the list but greyed out and unreachable.")]
        public bool online = true;

        [Tooltip("Hold them out of the list until a rule brings them online.")]
        public bool hidden;

        [Tooltip("The conversation, in order. Lines with replies wait for the player.")]
        public List<ChatLine> script = new();

        public string Id => string.IsNullOrWhiteSpace(id) ? displayName : id;
    }
}
