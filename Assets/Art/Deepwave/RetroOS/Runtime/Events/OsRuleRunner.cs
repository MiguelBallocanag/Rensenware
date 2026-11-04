using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Deepwave.RetroOS.Audio;
using Deepwave.RetroOS.Core.Network;
using Deepwave.RetroOS.UI;

namespace Deepwave.RetroOS.Events
{
    // Watches the event bus and executes matching profile rules.
    public class OsRuleRunner
    {
        readonly RetroOsContext _os;
        readonly IReadOnlyList<OsRule> _rules;
        readonly HashSet<OsRule> _spent = new();

        public OsRuleRunner(RetroOsContext os, IReadOnlyList<OsRule> rules)
        {
            _os = os;
            _rules = rules ?? Array.Empty<OsRule>();
            _os.Events.AnyRaised += OnEvent;
        }

        void OnEvent(string id, string arg)
        {
            foreach (var rule in _rules)
            {
                // A profile is hand-edited data: a list can carry a hole.
                if (rule == null) continue;
                if (_spent.Contains(rule) || !rule.Matches(id, arg, _os.Flags)) continue;
                if (rule.once) _spent.Add(rule);
                if (rule.actions == null) continue;

                foreach (var action in rule.actions)
                    if (action != null) Execute(action);
            }
        }

        void Execute(OsRuleAction a)
        {
            switch (a.kind)
            {
                case OsActionKind.SetFlag: _os.Flags.Set(a.value); break;
                case OsActionKind.ClearFlag: _os.Flags.Clear(a.value); break;

                case OsActionKind.RevealNode: SetHidden(a.value, false); break;
                case OsActionKind.HideNode: SetHidden(a.value, true); break;

                case OsActionKind.UnlockNode:
                    // A rule is the author speaking, not the player, so it
                    // reaches a node whoever happens to be signed in.
                    var node = _os.Fs.Resolve(a.value, null, ignoreAccess: true);
                    if (node == null) { Warn(a); break; }
                    node.ForceUnlock();
                    _os.Events.Raise(OsEvents.NodeUnlocked, node.Path);
                    _os.NotifyFsChanged(node.Path, FsChange.Unlocked);
                    break;

                case OsActionKind.DeliverMail:
                    if (_os.Mail != null) _os.Mail.Deliver(a.value);
                    else Debug.LogWarning($"RetroOS rule: cannot deliver '{a.value}' - this " +
                                          "machine has no mail program installed.");
                    break;
                case OsActionKind.OpenApp: _os.Apps?.Launch(a.value); break;

                case OsActionKind.ShowAlert:
                    RetroDialog.Alert(_os.Overlay, string.IsNullOrEmpty(a.value) ? "System" : a.value, a.text ?? "");
                    break;

                case OsActionKind.PlaySound:
                    if (a.clip) _os.Audio?.Play(a.clip);
                    else if (Enum.TryParse<UiSfx>(a.value, true, out var sfx)) _os.Audio?.PlayUi(sfx);
                    else Debug.LogWarning($"RetroOS rule: no sound to play - '{a.value}' is not " +
                                          "one of the built-in sounds, and no clip was given.");
                    break;

                case OsActionKind.RaiseEvent: _os.Events.Raise(a.value, a.text); break;

                case OsActionKind.Bsod:
                    _os.Bsod(string.IsNullOrWhiteSpace(a.value) ? "Windows" : a.value,
                             string.IsNullOrWhiteSpace(a.text) ? DefaultCrash : a.text);
                    break;

                case OsActionKind.LogOff: _os.Logout(); break;
                case OsActionKind.Shutdown: _os.Shutdown(); break;

                case OsActionKind.Notify:
                    _os.Notify(string.IsNullOrWhiteSpace(a.value) ? "System" : a.value, a.text ?? "");
                    break;

                case OsActionKind.HostOnline: SetHostOnline(a, true); break;
                case OsActionKind.HostOffline: SetHostOnline(a, false); break;

                case OsActionKind.ChatOnline: SetChatOnline(a, true); break;
                case OsActionKind.ChatOffline: SetChatOnline(a, false); break;
            }
        }

        // Taking a site off the air by a domain nothing serves is the commonest way a story rule does nothing - so warn.
        void SetHostOnline(OsRuleAction a, bool online)
        {
            if (_os.Net == null) { Warn(a); return; }

            if (!RetroNetwork.Host(a.value))
            {
                Debug.LogWarning($"RetroOS rule: nothing in this scene serves '{a.value}', so " +
                                 "taking it on or off the air does nothing. Check the domain " +
                                 "against the RetroInternetHost components in the scene.");
                return;
            }

            _os.Net.SetOnline(a.value, online);
        }

        // The messenger is a program, not a service, so the rule reaches it
        // through the installed app rather than through the OS.
        void SetChatOnline(OsRuleAction a, bool online)
        {
            var chat = _os.Apps?.All.OfType<Apps.Components.RetroChatApp>().FirstOrDefault();
            if (chat == null) { Warn(a); return; }
            chat.SetOnline(a.value, online);
        }

        void SetHidden(string path, bool hidden)
        {
            var node = _os.Fs.Resolve(path, null, ignoreAccess: true);
            if (node == null) { Debug.LogWarning($"RetroOS rule: no node at '{path}'."); return; }
            node.SetHidden(hidden);
            if (!hidden) _os.Events.Raise(OsEvents.NodeRevealed, node.Path);
            _os.NotifyFsChanged(node.Path, FsChange.Revealed);
        }

        // The stock 95 fatal-exception text, used when a Bsod rule leaves
        // the message blank.
        const string DefaultCrash =
@"A fatal exception 0E has occurred at 0028:C0011E36 in VXD VMM(01) +
00010E36.  The current application will be terminated.

*  Press any key to terminate the current application.
*  Press CTRL+ALT+DEL again to restart your computer. You will
   lose any unsaved information in all applications.";

        static void Warn(OsRuleAction a) =>
            Debug.LogWarning($"RetroOS rule: {a.kind} could not resolve '{a.value}'.");
    }
}
