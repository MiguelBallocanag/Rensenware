using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Deepwave.RetroOS.Data;
using Deepwave.RetroOS.Events;
using Deepwave.RetroOS.UI;
using Deepwave.RetroOS.UI.Components;

namespace Deepwave.RetroOS.Apps
{
    // Messenger: contacts left, conversation right. A conversation is a script -
    // lines arrive after a pause, and a line can stop and wait for the player to
    // pick a reply. Each choice can set a flag, raise an event and fire a UnityEvent.
    public class ChatView : AppView
    {
        // Where the conversations have got to. The program holds them, so
        // this is the same dictionary the last window was reading.
        readonly Dictionary<string, ChatThread> _own = new();
        Dictionary<string, ChatThread> Threads => Source?.Threads ?? _own;

        RetroListView _contacts;
        Text _transcript;
        ScrollRect _scroll;
        RectTransform _replies;
        RectTransform _files;
        RetroNavDrawer _people;
        RetroTextBox _entry;
        RetroStatusStrip _status;
        Coroutine _playing;
        ChatContact _open;

        IChatSource Source => Config<IChatSource>();
        List<ChatContact> Contacts => Source?.Contacts ?? _none;
        readonly List<ChatContact> _none = new();

        protected override void Build()
        {
            // Before anything is read: the conversations belong to whoever is
            // signed in, and this may not be who was signed in last time.
            Source?.KeepFor(Os.CurrentUser);

            RetroUi.VLayout(Root.gameObject, 2, 2);
            BuildMenu();
            BuildBody();

            _status = RetroUi.StatusStrip(Root, -1f, 110f);

            RefreshContacts();
            _status.Set(0, "Pick somebody to talk to.");
        }

        void BuildMenu()
        {
            var menu = RetroUi.Menu(Root);

            menu.AddMenu("File")
                .Command("Save Transcript...", SaveTranscript, "Ctrl+S",
                    RetroIcons.Get(BuiltinIcon.Floppy))
                .Separator()
                .Command("Close", () => Window.Close(), "Alt+F4");

            menu.AddMenu("People")
                .Command("Contact List", () => { if (_people) _people.Toggle(); })
                .Separator()
                .Command("Refresh", RefreshContacts, "F5", RetroIcons.Refresh);

            menu.AddMenu("Help").Command("About", () => RetroDialog.Info(Os.Overlay, "Messenger",
                "Talk to whoever is online.\n\n" +
                "Some of them will wait for you to choose what to say."));
        }

        void BuildBody()
        {
            var split = RetroUi.NewRect("Split", Root);
            RetroUi.Flexible(split);
            RetroUi.HLayout(split.gameObject, 4, 0);

            // The contact list folds away, so a narrow window is all
            // conversation.
            _people = RetroNavDrawer.Create(split, "People", 180f, "chat.contacts", Os.Prefs);

            _contacts = RetroUi.ListView(_people.Body, ListViewMode.Details);
            RetroUi.Flexible(_contacts);
            _contacts.SetColumns(new[] { "Name", "Status" }, new[] { -1f, 60f });
            _contacts.SelectionChanged += entry =>
            {
                if (entry?.Tag is ChatContact contact) Open(contact);
            };

            var right = RetroUi.NewRect("Conversation", split);
            RetroUi.Flexible(right);
            RetroUi.VLayout(right.gameObject, 4, 0);

            var content = RetroUi.VScroll(right, out _scroll);
            RetroUi.Flexible(RetroUi.ScrollFrame(_scroll));
            _transcript = RetroUi.Label(content, "", 13, TextAnchor.UpperLeft);

            // Files they sent, on their own strip so the transcript stays readable.
            _files = RetroUi.NewRect("Files", right);
            RetroUi.Size(_files, h: 24f);
            RetroUi.HLayout(_files.gameObject, 4, 0);
            _files.gameObject.SetActive(false);

            // the free-text box below is the way to say something.
            _replies = RetroUi.NewRect("Replies", right);
            RetroUi.VLayout(_replies.gameObject, 2, 0);
            _replies.gameObject.SetActive(false);

            var row = RetroUi.NewRect("Say", right);
            RetroUi.Size(row, h: 26f);
            RetroUi.HLayout(row.gameObject, 4, 0);
            _entry = RetroUi.TextBox(row, "");
            RetroUi.Flexible(_entry);
            _entry.Submitted += Say;
            RetroUi.Size(RetroUi.PushButton(row, "Send", () => Say(_entry.Value)), w: 66f);
        }

        // --- contacts ---------------------------------------------------------

        void RefreshContacts()
        {
            if (!this || _contacts == null) return;

            var keep = _open;
            _contacts.Clear();

            foreach (var contact in Contacts)
            {
                if (contact == null || contact.hidden) continue;
                _contacts.Add(new RetroListEntry
                {
                    Text = contact.displayName,
                    Icon = RetroIcons.Get(BuiltinIcon.Contacts),
                    Bold = contact.online,
                    TextColor = contact.online ? null : (Color?)RetroSkin.TextDisabled,
                    Columns = new[] { contact.online ? contact.status : "Offline" },
                    Tag = contact
                });
            }

            // A freshly added component has no contacts; say so rather than show an empty box.
            if (_contacts.Entries.Count == 0)
                _contacts.Add(new RetroListEntry
                {
                    Text = "(nobody yet)",
                    Columns = new[] { "" },
                    TextColor = RetroSkin.TextDisabled
                });

            if (keep != null) _contacts.SelectTag(keep);
        }

        void Open(ChatContact contact)
        {
            if (contact == null || contact == _open) return;

            if (_playing != null) { StopCoroutine(_playing); _playing = null; }

            _open = contact;
            Window.SetTitle($"{contact.displayName} - {App?.Title ?? "Messenger"}");

            var thread = ThreadFor(contact);
            Redraw(thread);
            BuildFiles(thread);

            if (!contact.online)
            {
                ShowReplies(null);
                _status.Set(0, $"{contact.displayName} is not online.");
                return;
            }

            _status.Set(0, contact.status);
            Raise("opened", contact.Id);

            // Pick the conversation up where it stopped.
            if (thread.Waiting) ShowChoices(thread);
            else _playing = StartCoroutine(Play(contact, thread));
        }

        ChatThread ThreadFor(ChatContact contact)
        {
            var threads = Threads;
            if (threads.TryGetValue(contact.Id, out var thread)) return thread;
            thread = new ChatThread();
            threads[contact.Id] = thread;
            return thread;
        }

        // --- the script -------------------------------------------------------

        IEnumerator Play(ChatContact contact, ChatThread thread)
        {
            while (thread.Line < contact.script.Count)
            {
                var line = contact.script[thread.Line];
                if (line == null) { thread.Line++; continue; }

                // A line can be held back until the story has got somewhere.
                if (!string.IsNullOrWhiteSpace(line.requiredFlag) &&
                    !Os.Flags.IsSet(line.requiredFlag.Trim()))
                {
                    thread.Line++;
                    continue;
                }

                if (line.delay > 0f)
                {
                    _status.Set(1, line.author == ChatAuthor.Them
                        ? contact.displayName + " is typing..." : "");
                    yield return new WaitForSecondsRealtime(line.delay);
                    _status.Set(1, "");
                }

                Write(thread, Speaker(contact, line.author), line.text);

                if (!string.IsNullOrWhiteSpace(line.attachment))
                {
                    var sent = line.attachment.Trim();
                    if (!thread.Files.Contains(sent)) thread.Files.Add(sent);
                    Write(thread, "*", $"{Speaker(contact, line.author)} sent " +
                                       System.IO.Path.GetFileName(sent));
                    BuildFiles(thread);
                }

                if (!string.IsNullOrWhiteSpace(line.setFlag)) Os.Flags.Set(line.setFlag.Trim());
                if (!string.IsNullOrWhiteSpace(line.raiseEvent))
                    Os.Events.Raise(line.raiseEvent.Trim(), contact.Id);

                Os.Events.Raise(OsEvents.ChatMessage, contact.Id);

                // A line with replies stops here until the player picks one.
                if (line.replies is { Count: > 0 })
                {
                    thread.Waiting = true;
                    _playing = null;
                    ShowChoices(thread);
                    yield break;
                }

                thread.Line++;
            }

            _playing = null;
            ShowReplies(null);
            _status.Set(1, "");
        }

        void ShowChoices(ChatThread thread)
        {
            var line = _open != null && thread.Line < _open.script.Count
                ? _open.script[thread.Line] : null;
            ShowReplies(line?.replies);
        }

        void ShowReplies(List<ChatReply> replies)
        {
            foreach (Transform child in _replies) Destroy(child.gameObject);

            if (replies == null || replies.Count == 0)
            {
                _replies.gameObject.SetActive(false);
                return;
            }

            _replies.gameObject.SetActive(true);
            foreach (var reply in replies)
            {
                if (reply == null) continue;
                var chosen = reply;
                var button = RetroUi.PushButton(_replies, reply.text, () => Choose(chosen));
                RetroUi.Size(button, h: 24f);
                if (button.Label) button.Label.alignment = TextAnchor.MiddleLeft;
            }
        }

        void Choose(ChatReply reply)
        {
            if (_open == null) return;

            var thread = ThreadFor(_open);
            thread.Waiting = false;
            ShowReplies(null);

            Write(thread, "You", reply.text);

            if (!string.IsNullOrWhiteSpace(reply.setFlag)) Os.Flags.Set(reply.setFlag.Trim());
            if (!string.IsNullOrWhiteSpace(reply.raiseEvent))
                Os.Events.Raise(reply.raiseEvent.Trim(), _open.Id);

            reply.onChosen?.Invoke();
            Raise("reply", reply.text);

            // A choice can send the conversation somewhere else entirely.
            thread.Line = reply.goTo >= 0 ? reply.goTo : thread.Line + 1;

            if (_playing != null) StopCoroutine(_playing);
            _playing = StartCoroutine(Play(_open, thread));
        }

        // Typed text goes into the log and onto the event bus, so
        // a game can listen for the player typing a name or password rather than picking one.
        void Say(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || _open == null) return;

            _entry.Value = "";

            if (!_open.online)
            {
                _status.Set(0, $"{_open.displayName} is not online.");
                return;
            }

            Write(ThreadFor(_open), "You", text);
            Os.Events.Raise(OsEvents.ChatSaid, text);
            Raise("said", text);
        }

        // A file somebody sent is a real file on this machine, and opens
        // the same way a mail attachment does.
        void BuildFiles(ChatThread thread)
        {
            RetroFileStrip.Fill(_files, Os, thread.Files, "Sent:",
                path => Raise("attachment", path), App?.Title);
        }

        // --- transcript -------------------------------------------------------

        string Speaker(ChatContact contact, ChatAuthor author) => author switch
        {
            ChatAuthor.Me => "You",
            ChatAuthor.System => "*",
            _ => contact.displayName
        };

        void Write(ChatThread thread, string who, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            thread.Log.Add(who == "*"
                ? $"*** {text}"
                : $"<{who}> {text}");

            if (_open != null && thread == ThreadFor(_open)) Redraw(thread);
        }

        void Redraw(ChatThread thread)
        {
            _transcript.text = string.Join("\n", thread.Log);
            Canvas.ForceUpdateCanvases();
            if (_scroll) _scroll.verticalNormalizedPosition = 0f;
        }

        void SaveTranscript()
        {
            if (_open == null) { _status.Set(0, "No conversation to save."); return; }

            var thread = ThreadFor(_open);
            if (thread.Log.Count == 0) { _status.Set(0, "Nothing said yet."); return; }

            var body = string.Join("\n", thread.Log);
            RetroFileDialog.Save(Os, "Save Transcript",
                Core.Storage.RetroStorage.Sanitize(_open.displayName) + ".txt", (folder, name) =>
                {
                    if (folder == null || string.IsNullOrWhiteSpace(folder.SourcePath))
                    {
                        RetroDialog.Alert(Os.Overlay, "Save Transcript",
                            "Files cannot be written here.\n\nChoose a folder on a drive.");
                        return;
                    }

                    var path = System.IO.Path.Combine(folder.SourcePath, name);
                    if (!Core.Storage.DiskMount.SafeWriteText(path, body))
                    {
                        RetroDialog.Alert(Os.Overlay, "Save Transcript",
                            "The file could not be written.");
                        return;
                    }

                    Os.Drives.Refresh();
                    Os.NotifyFsChanged(folder.Path + "/" + name, FsChange.Created);
                    _status.Set(0, "Saved to " + folder.Path + "/" + name);
                    Raise("saved", name);
                });
        }
    }
}
