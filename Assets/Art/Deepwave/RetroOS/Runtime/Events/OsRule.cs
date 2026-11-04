using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deepwave.RetroOS.Events
{
    // Designer-authored reaction: "when this OS event happens, do these things".
    // Lives in the profile, no code required.
    [Serializable]
    public class OsRule
    {
        [Tooltip("Editor-only note about what this rule does.")]
        public string note;

        [Tooltip("Event id to react to, e.g. 'file.opened'. See OsEvents.")]
        public string eventId;

        [Tooltip("Argument to match. Empty = any. Trailing * = prefix match (e.g. '/Archive/*').")]
        public string argument;

        [Tooltip("Optional flag gate: 'flag' must be set, '!flag' must be unset.")]
        public string requiredFlag;

        [Tooltip("Run only the first time it matches.")]
        public bool once = true;

        public List<OsRuleAction> actions = new();

        public bool Matches(string id, string arg, GameFlags flags)
        {
            if (!string.Equals(eventId?.Trim(), id, StringComparison.OrdinalIgnoreCase)) return false;
            if (!flags.Check(requiredFlag)) return false;

            var pattern = argument?.Trim();
            if (string.IsNullOrEmpty(pattern)) return true;
            arg ??= "";
            return pattern.EndsWith("*")
                ? arg.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.OrdinalIgnoreCase)
                : string.Equals(arg, pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>What a rule does when it fires.</summary>
    public enum OsActionKind
    {
        SetFlag = 0,        // value = flag name
        ClearFlag = 1,      // value = flag name
        RevealNode = 2,     // value = vfs path (unhide)
        HideNode = 3,       // value = vfs path
        UnlockNode = 4,     // value = vfs path (force open)
        DeliverMail = 5,    // value = mail id
        OpenApp = 6,        // value = appId
        ShowAlert = 7,      // value = title, text = message
        PlaySound = 8,      // clip, or value = UiSfx name (Click, Error, Notify, ...)
        RaiseEvent = 9,     // value = event id, text = argument (chain custom events)

        Bsod = 10,          // value = title, text = message (blank = a stock crash)
        LogOff = 11,        // back to the login screen
        Shutdown = 12,      // turn the in-fiction machine off

        Notify = 13,        // value = title, text = the line under it
        HostOnline = 14,    // value = domain - put a web site back on the air
        HostOffline = 15,   // value = domain - take it off
        ChatOnline = 16,    // value = contact id - bring somebody into Messenger
        ChatOffline = 17    // value = contact id - grey them out
    }

    [Serializable]
    public class OsRuleAction
    {
        public OsActionKind kind;

        [Tooltip("Main parameter; meaning depends on the action kind.")]
        public string value;

        [Tooltip("Secondary parameter (alert message / event argument).")]
        public string text;

        [Tooltip("Audio clip for PlaySound.")]
        public AudioClip clip;
    }
}
