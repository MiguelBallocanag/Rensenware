using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Comms;
using Deepwave.RetroOS.Core.FileSystem;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps
{
    // Inbox reader over the runtime mailbox: tray list, message list, body underneath.
    // New mail delivered by rules pops in live.
    public class MailView : AppView
    {
        readonly MailFolder[] _folders =
        {
            MailFolder.Inbox, MailFolder.Sent, MailFolder.Deleted
        };

        RetroListView _trays;
        RetroNavDrawer _trayPane;
        RetroListView _list;
        Text _header;
        Text _body;
        RectTransform _attachmentRow;
        RetroStatusStrip _status;
        MailMessage _selected;
        MailFolder _folder = MailFolder.Inbox;

        protected override void Build()
        {
            RetroUi.VLayout(Root.gameObject, 2, 2);
            BuildMenu();
            BuildToolbar();
            BuildBody();

            _status = RetroUi.StatusStrip(Root, -1f, 110f);

            Os.Mail.Changed += Refresh;
            Refresh();
        }

        void BuildMenu()
        {
            var menu = RetroUi.Menu(Root);

            menu.AddMenu("File")
                .Command("New Message", Compose, "Ctrl+N", RetroIcons.Mail)
                .Separator()
                .Command("Empty Deleted Items", EmptyDeleted)
                .Separator()
                .Command("Close", () => Window.Close(), "Alt+F4");

            menu.AddMenu("Edit")
                .Command("Delete", DeleteSelected, "Del")
                .Command("Mark as Unread", MarkUnread)
                .Separator()
                .Command("Restore", RestoreSelected);

            menu.AddMenu("View")
                .Command("Inbox", () => SelectFolder(MailFolder.Inbox))
                .Command("Sent Items", () => SelectFolder(MailFolder.Sent))
                .Command("Deleted Items", () => SelectFolder(MailFolder.Deleted))
                .Separator()
                .Command("Folder List", () => { if (_trayPane) _trayPane.Toggle(); });

            menu.AddMenu("Compose")
                .Command("New Message", Compose)
                .Command("Reply", Reply)
                .Command("Forward", Forward);

            menu.AddMenu("Help").Command("About", () => RetroDialog.Info(Os.Overlay, "Inbox",
                "Mail on this machine. Attachments open in whichever program " +
                "handles that kind of file."));
        }

        void BuildToolbar()
        {
            var bar = RetroUi.ToolStrip(Root);
            bar.Add(RetroIcons.Mail, "New message", Compose, "New");
            bar.Add(RetroIcons.Get(BuiltinIcon.Book), "Reply", Reply, "Reply");
            bar.Add(RetroIcons.Get(BuiltinIcon.Globe), "Forward", Forward, "Forward");
            bar.AddSeparator();
            bar.Add(RetroIcons.Get(BuiltinIcon.Trash), "Delete", DeleteSelected, "Delete");
        }

        void BuildBody()
        {
            var split = RetroUi.NewRect("Split", Root);
            RetroUi.Flexible(split);
            RetroUi.HLayout(split.gameObject, 4, 0);

            // The trays fold away, which is the difference between a usable
            // and an unusable Inbox in a small window.
            _trayPane = RetroNavDrawer.Create(split, "Folders", 150f, "mail.trays", Os.Prefs);

            _trays = RetroUi.ListView(_trayPane.Body, ListViewMode.List);
            RetroUi.Flexible(_trays);
            _trays.SelectionChanged += entry =>
            {
                if (entry?.Tag is MailFolder folder && folder != _folder) SelectFolder(folder);
            };

            var right = RetroUi.NewRect("Right", split);
            RetroUi.Flexible(right);
            RetroUi.VLayout(right.gameObject, 4, 0);

            _list = RetroUi.ListView(right, ListViewMode.Details);
            RetroUi.Size(_list, h: 150f);
            _list.SetColumns(new[] { "From", "Subject", "Received" }, new[] { 130f, -1f, 110f });
            _list.SelectionChanged += entry => Open(entry?.Tag as MailMessage);
            _list.ContextRequested += (entry, _) => Open(entry?.Tag as MailMessage);

            var reader = RetroUi.NewRect("Reader", right);
            RetroUi.Flexible(reader);
            RetroUi.VLayout(reader.gameObject, 4, 0);

            var headerBox = RetroUi.Panel(reader, PaletteSlot.Face, "Header");
            RetroUi.Size(headerBox, h: 40);
            RetroUi.Bevel(headerBox.rectTransform, false, 1);
            // Two lines of somebody else's subject line, in a box that is
            // forty pixels tall whatever they wrote.
            _header = RetroUi.Label(RetroUi.Clipped(headerBox.transform, 4),
                "Select a message.", 13, TextAnchor.MiddleLeft);
            _header.horizontalOverflow = HorizontalWrapMode.Overflow;
            RetroUi.Stretch(_header.rectTransform);

            _attachmentRow = RetroUi.NewRect("Attachments", reader);
            RetroUi.Size(_attachmentRow, h: 24f);
            RetroUi.HLayout(_attachmentRow.gameObject, 4, 0);
            _attachmentRow.gameObject.SetActive(false);

            var bodyContent = RetroUi.VScroll(reader, out var bodyScroll);
            RetroUi.Flexible(RetroUi.ScrollFrame(bodyScroll));
            _body = RetroUi.Label(bodyContent, "", 14);
        }

        // --- trays ----------------------------------------------------------

        void SelectFolder(MailFolder folder)
        {
            _folder = folder;
            _selected = null;
            Refresh();
            ShowBody(null);
        }

        static string Caption(MailFolder folder) => folder switch
        {
            MailFolder.Sent => "Sent Items",
            MailFolder.Deleted => "Deleted Items",
            _ => "Inbox"
        };

        void Refresh()
        {
            if (!this || _list == null) return;

            RefreshTrays();

            var keep = _selected;
            _list.Clear();
            foreach (var message in Os.Mail.Folder(_folder))
            {
                var who = _folder == MailFolder.Sent ? message.To : message.From;
                _list.Add(new RetroListEntry
                {
                    Text = string.IsNullOrEmpty(who) ? "(unknown)" : who,
                    Icon = RetroIcons.Mail,
                    Bold = message.Unread,
                    Columns = new[]
                    {
                        string.IsNullOrEmpty(message.Subject) ? "(no subject)" : message.Subject,
                        message.Received.ToString("dd/MM HH:mm")
                    },
                    Tag = message
                });
            }

            if (_list.Entries.Count == 0)
                _list.Add(new RetroListEntry
                {
                    Text = "",
                    Columns = new[] { "(no messages in this folder)", "" },
                    TextColor = RetroSkin.TextDisabled
                });

            if (keep != null && keep.Folder == _folder) _list.SelectTag(keep);
            else _selected = null;

            _status.Set(0, $"{Caption(_folder)} - {Os.Mail.CountIn(_folder)} message(s)");
            _status.Set(1, $"{Os.Mail.UnreadCount} unread");
            if (_selected != null) ShowBody(_selected);
        }

        void RefreshTrays()
        {
            _trays.Clear();
            foreach (var folder in _folders)
            {
                var count = Os.Mail.CountIn(folder);
                var unread = folder == MailFolder.Inbox ? Os.Mail.UnreadCount : 0;
                var caption = unread > 0
                    ? $"{Caption(folder)} ({unread})"
                    : $"{Caption(folder)} [{count}]";

                _trays.Add(new RetroListEntry
                {
                    Text = caption,
                    Bold = unread > 0,
                    Icon = RetroIcons.Get(folder == MailFolder.Deleted
                        ? BuiltinIcon.Trash
                        : folder == MailFolder.Sent ? BuiltinIcon.Download : BuiltinIcon.Mail),
                    Tag = folder
                });
            }
            _trays.SelectTag(_folder);
        }

        // --- reading --------------------------------------------------------

        void Open(MailMessage message)
        {
            if (message == null) return;
            _selected = message;
            Os.Mail.MarkRead(message);
            ShowBody(message);
        }

        void ShowBody(MailMessage message)
        {
            if (message == null)
            {
                _header.text = "Select a message.";
                _body.text = "";
                _attachmentRow.gameObject.SetActive(false);
                return;
            }

            var who = string.IsNullOrEmpty(message.To)
                ? $"From: {message.From}"
                : $"To: {message.To}";
            _header.text = $"{who}\nSubject: {message.Subject}    {message.Received:dd/MM/yyyy HH:mm}";
            _body.text = message.Body;
            BuildAttachments(message);
        }

        // An attachment is a real file: opens through the OS, with the same access checks as Explorer.
        void BuildAttachments(MailMessage message)
        {
            RetroFileStrip.Fill(_attachmentRow, Os, message?.Attachments, "Attached:",
                path => Raise("attachment", path), App?.Title);
        }

        // --- writing --------------------------------------------------------

        void Compose() => Composer(null, null, null);

        void Reply()
        {
            if (_selected == null) { RetroDialog.Info(Os.Overlay, "Reply", "Select a message first."); return; }
            Composer(_selected.From, "Re: " + Strip(_selected.Subject, "Re: "), Quote(_selected));
        }

        void Forward()
        {
            if (_selected == null) { RetroDialog.Info(Os.Overlay, "Forward", "Select a message first."); return; }
            Composer("", "Fw: " + Strip(_selected.Subject, "Fw: "), Quote(_selected));
        }

        static string Strip(string subject, string prefix) =>
            string.IsNullOrEmpty(subject) ? "(no subject)"
            : subject.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)
                ? subject[prefix.Length..] : subject;

        static string Quote(MailMessage message) =>
            $"\n\n----- Original message -----\nFrom: {message.From}\n" +
            $"Subject: {message.Subject}\n\n{message.Body}";

        void Composer(string to, string subject, string body)
        {
            var (blocker, panel) = RetroDialog.Custom(Os.Overlay, "New Message", 460f, 340f, BuiltinIcon.Mail);

            RetroUi.Size(RetroUi.NoWrap(RetroUi.Label(panel, "To:", 12, TextAnchor.LowerLeft)), h: 16f);
            var toField = RetroUi.TextBox(panel, to ?? "");
            RetroUi.Size(toField, h: 24f);

            RetroUi.Size(RetroUi.NoWrap(RetroUi.Label(panel, "Subject:", 12, TextAnchor.LowerLeft)), h: 16f);
            var subjectField = RetroUi.TextBox(panel, subject ?? "");
            RetroUi.Size(subjectField, h: 24f);

            var bodyField = RetroUi.TextBox(panel, body ?? "", multiline: true);
            RetroUi.Flexible(bodyField);
            bodyField.Field.lineType = InputField.LineType.MultiLineNewline;

            var row = RetroUi.NewRect("Buttons", panel);
            RetroUi.Size(row, h: 26f);
            RetroUi.HLayout(row.gameObject, 6, 0);
            RetroUi.Spacer(row);

            RetroUi.Size(RetroUi.PushButton(row, "Send", () =>
            {
                var recipient = toField.Value.Trim();
                if (recipient.Length == 0)
                {
                    RetroDialog.Alert(Os.Overlay, "New Message", "Enter a recipient first.");
                    return;
                }

                var sent = Os.Mail.Send(recipient, subjectField.Value, bodyField.Value,
                    Os.Auth.Current?.DisplayName);
                RetroDialog.Dismiss(blocker);
                SelectFolder(MailFolder.Sent);
                _list.SelectTag(sent);
                Raise("sent", recipient);
            }), w: 76f);

            RetroUi.Size(RetroUi.PushButton(row, "Cancel", () => RetroDialog.Dismiss(blocker)), w: 76f);

            toField.Focus();
        }

        // --- filing ---------------------------------------------------------

        void DeleteSelected()
        {
            if (_selected == null) return;

            var message = _selected;
            if (message.Folder != MailFolder.Deleted)
            {
                Os.Mail.Delete(message);
                _selected = null;
                ShowBody(null);
                return;
            }

            RetroDialog.Confirm(Os.Overlay, "Delete",
                $"Delete '{message.Subject}' for good?", ok =>
                {
                    if (!ok) return;
                    Os.Mail.Delete(message);
                    _selected = null;
                    ShowBody(null);
                });
        }

        void RestoreSelected()
        {
            if (_selected == null || _selected.Folder != MailFolder.Deleted) return;
            Os.Mail.Restore(_selected);
        }

        void MarkUnread()
        {
            if (_selected != null) Os.Mail.MarkUnread(_selected);
        }

        void EmptyDeleted()
        {
            var count = Os.Mail.CountIn(MailFolder.Deleted);
            if (count == 0) { RetroDialog.Info(Os.Overlay, "Inbox", "There is nothing to empty."); return; }

            RetroDialog.Confirm(Os.Overlay, "Empty Deleted Items",
                $"Throw away {count} message(s) for good?", ok =>
                {
                    if (!ok) return;
                    Os.Mail.EmptyDeleted();
                    _selected = null;
                    ShowBody(null);
                });
        }

        void OnDestroy()
        {
            if (Os?.Mail != null) Os.Mail.Changed -= Refresh;
        }
    }
}
