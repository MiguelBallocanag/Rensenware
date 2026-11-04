using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deepwave.RetroOS.Data
{
    // One message for the Inbox app.
    [Serializable]
    public class MailEntry
    {
        [Tooltip("Stable id used by rules/events (DeliverMail, mail.read). Empty = subject.")]
        public string id;

        public string from;
        public string subject;
        [TextArea(3, 12)] public string body;
        public bool unread = true;

        [Tooltip("Hold this message back until a DeliverMail rule/action sends it.")]
        public bool delayed;

        [Tooltip("Files on this machine sent with the message, as paths - " +
                 "/C:/Users/operator/Documents/report.txt. Opening one opens it " +
                 "in whichever program handles that kind of file.")]
        public List<string> attachments = new();

        public string Id => string.IsNullOrWhiteSpace(id) ? subject : id;
    }
}
