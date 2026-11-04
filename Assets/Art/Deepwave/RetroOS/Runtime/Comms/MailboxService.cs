using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.Events;

namespace Deepwave.RetroOS.Comms
{
    /// <summary>Which tray a message is sitting in.</summary>
    public enum MailFolder
    {
        Inbox,
        Sent,
        Deleted
    }

    // Runtime copy of one mail message (profile assets stay untouched).
    public class MailMessage
    {
        public string Id;
        public string From;
        public string To;
        public string Subject;
        public string Body;
        public bool Unread;
        public bool Delivered;
        public MailFolder Folder = MailFolder.Inbox;
        public DateTime Received;

        /// <summary>Paths of files on this machine sent with the message.</summary>
        public List<string> Attachments = new();
    }

    // Runtime mailbox over the profile's mail list. Delayed messages stay invisible until a rule delivers them.
    // One list with a folder per message (not a list per tray), so a rule watching a mail id works wherever it is moved.
    public class MailboxService
    {
        readonly List<MailMessage> _messages = new();
        readonly Action<string, string> _raise;
        int _composed;

        public event Action Changed;

        public MailboxService(IEnumerable<MailEntry> entries, Action<string, string> raise)
            : this(entries, raise, DateTime.Now) { }

        public MailboxService(IEnumerable<MailEntry> entries, Action<string, string> raise, DateTime now)
        {
            _raise = raise;

            // A null list is handled; so is a hole in a hand-edited one.
            var list = (entries ?? Enumerable.Empty<MailEntry>()).Where(e => e != null).ToList();
            for (var i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                _messages.Add(new MailMessage
                {
                    Id = entry.Id,
                    From = entry.from,
                    Subject = entry.subject,
                    Body = entry.body,
                    Unread = entry.unread,
                    Delivered = !entry.delayed,
                    Folder = MailFolder.Inbox,
                    // Oldest first in the asset reads as oldest first in the
                    // tray, without anyone having to type dates in.
                    Received = now.AddMinutes(-(list.Count - i) * 37),
                    Attachments = entry.attachments != null
                        ? new List<string>(entry.attachments.Where(a => !string.IsNullOrWhiteSpace(a)))
                        : new List<string>()
                });
            }
        }

        /// <summary>Everything that has actually arrived, in every folder.</summary>
        public IEnumerable<MailMessage> All => _messages.Where(m => m.Delivered);

        public IEnumerable<MailMessage> Inbox => Folder(MailFolder.Inbox);
        public IEnumerable<MailMessage> Sent => Folder(MailFolder.Sent);
        public IEnumerable<MailMessage> Deleted => Folder(MailFolder.Deleted);

        /// <summary>The messages in one tray, newest last.</summary>
        public IEnumerable<MailMessage> Folder(MailFolder folder) =>
            _messages.Where(m => m.Delivered && m.Folder == folder);

        public int UnreadCount => Inbox.Count(m => m.Unread);

        /// <summary>How many messages are waiting in a tray.</summary>
        public int CountIn(MailFolder folder) => Folder(folder).Count();

        // Makes a delayed message appear (rules: DeliverMail).
        public bool Deliver(string id)
        {
            var message = _messages.FirstOrDefault(m =>
                string.Equals(m.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase) && !m.Delivered);
            if (message == null)
            {
                Debug.LogWarning($"RetroOS: no undelivered mail with id '{id}'.");
                return false;
            }

            message.Delivered = true;
            message.Unread = true;
            message.Folder = MailFolder.Inbox;
            Changed?.Invoke();
            _raise?.Invoke(OsEvents.MailReceived, message.Id);
            return true;
        }

        public void MarkRead(MailMessage message)
        {
            if (message == null || !message.Unread) return;
            message.Unread = false;
            Changed?.Invoke();
            _raise?.Invoke(OsEvents.MailRead, message.Id);
        }

        /// <summary>Puts a message back in bold, the way the era's Mark Unread did.</summary>
        public void MarkUnread(MailMessage message)
        {
            if (message == null || message.Unread) return;
            message.Unread = true;
            Changed?.Invoke();
        }

        /// Records a message in the Sent tray; there is nobody on the other end, but <c>mail.sent</c> lets the game react to what was written.
        public MailMessage Send(string to, string subject, string body, string from = null)
        {
            var message = new MailMessage
            {
                Id = "sent-" + (++_composed),
                From = string.IsNullOrWhiteSpace(from) ? "me" : from,
                To = to,
                Subject = string.IsNullOrWhiteSpace(subject) ? "(no subject)" : subject,
                Body = body ?? "",
                Unread = false,
                Delivered = true,
                Folder = MailFolder.Sent,
                Received = DateTime.Now
            };

            _messages.Add(message);
            Changed?.Invoke();
            _raise?.Invoke(OsEvents.MailSent, to);
            return message;
        }

        /// <summary>Moves a message to the Deleted tray, or throws it away for good if it was already there.</summary>
        public void Delete(MailMessage message)
        {
            if (message == null) return;

            if (message.Folder == MailFolder.Deleted) _messages.Remove(message);
            else message.Folder = MailFolder.Deleted;

            Changed?.Invoke();
            _raise?.Invoke(OsEvents.MailDeleted, message.Id);
        }

        /// <summary>Puts a deleted message back where it came from.</summary>
        public void Restore(MailMessage message)
        {
            if (message == null || message.Folder != MailFolder.Deleted) return;
            message.Folder = string.IsNullOrEmpty(message.To) ? MailFolder.Inbox : MailFolder.Sent;
            Changed?.Invoke();
        }

        /// <summary>Empties the Deleted tray for good.</summary>
        public int EmptyDeleted()
        {
            var count = _messages.RemoveAll(m => m.Folder == MailFolder.Deleted);
            if (count > 0) Changed?.Invoke();
            return count;
        }
    }
}
