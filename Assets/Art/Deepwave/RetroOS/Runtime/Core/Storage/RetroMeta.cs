using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Deepwave.RetroOS.Core.Storage
{
    // The RetroOS properties of one file/folder, in a sidecar next to it:
    // `readme.txt` -> `readme.txt.rmeta`, folder `Archive` -> `Archive.rmeta`.
    // One `key: value` per line, editable in Notepad:
    //
    //     hidden: true
    //     encrypted: true
    //     password: 1958
    //     note: sealed after the incident
    //
    // Always optional - it only holds what the OS cannot infer. Not ".meta",
    // which is Unity's.
    public class RetroMeta
    {
        public const string Extension = ".rmeta";

        // Unknown keys are preserved so hand-written lines survive a round trip.
        readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, string> Values => _values;

        // --- properties the OS understands ---

        public bool Hidden
        {
            get => Flag("hidden");
            set => Set("hidden", value ? "true" : null);
        }

        public bool Encrypted
        {
            get => Flag("encrypted");
            set => Set("encrypted", value ? "true" : null);
        }

        public bool ReadOnly
        {
            get => Flag("readonly");
            set => Set("readonly", value ? "true" : null);
        }

        // Password the player has to find in your fiction.
        public string Password
        {
            get => Text("password");
            set => Set("password", value);
        }

        // Volume label of a drive, or a display name overriding the file name.
        public string Label
        {
            get => Text("label");
            set => Set("label", value);
        }

        // Drive letter and kind, on a machine's top-level drive folders.
        public string Drive
        {
            get => Text("drive");
            set => Set("drive", value);
        }

        public string Kind
        {
            get => Text("type");
            set => Set("type", value);
        }

        // Account this node belongs to. Set automatically for anything under
        // Users/<name>; set it by hand to make a file private anywhere else.
        public string Owner
        {
            get => Text("owner");
            set => Set("owner", value);
        }

        // Free text shown in the file's Properties dialog.
        public string Note
        {
            get => Text("note");
            set => Set("note", value);
        }

        // --- sharing ---
        // A folder with a share name is published to the LAN as \\<address>\<name>.
        // It lives here, not on a component, because it is a property of the folder:
        //     share: ARCHIVE / sharecomment: ... / sharepassword: 1958 / sharehidden: true

        /// <summary>Name this folder is published under, or <c>null</c> if not shared.</summary>
        public string Share
        {
            get => Text("share");
            set => Set("share", value);
        }

        /// <summary>Whether this folder is published to the LAN at all.</summary>
        public bool IsShared => !string.IsNullOrWhiteSpace(Share);

        /// <summary>Shown beside the share in Network Neighborhood.</summary>
        public string ShareComment
        {
            get => Text("sharecomment");
            set => Set("sharecomment", value);
        }

        /// <summary>Browsing the share asks for this first. Empty = open to all.</summary>
        public string SharePassword
        {
            get => Text("sharepassword");
            set => Set("sharepassword", value);
        }

        /// <summary>Published but not listed: unreachable until a rule turns it on - how a story reveals network files partway through.</summary>
        public bool ShareHidden
        {
            get => Flag("sharehidden");
            set => Set("sharehidden", value ? "true" : null);
        }

        public bool IsEmpty => _values.Count == 0;

        // --- raw access ---

        public string Text(string key) => _values.TryGetValue(key, out var v) ? v : null;

        public bool Flag(string key)
        {
            var value = Text(key);
            return value != null &&
                   (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1" ||
                    value.Equals("yes", StringComparison.OrdinalIgnoreCase));
        }

        public void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(value)) _values.Remove(key);
            else _values[key] = value;
        }

        // --- files ---

        // Idempotent: a sidecar path is its own sidecar path (no `.rmeta.rmeta`).
        public static string SidecarFor(string nodePath) =>
            nodePath != null && nodePath.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
                ? nodePath
                : nodePath + Extension;

        // Never throws: a machine with a broken sidecar still boots.
        public static RetroMeta Read(string nodePath)
        {
            var meta = new RetroMeta();
            var path = SidecarFor(nodePath);
            if (!File.Exists(path)) return meta;

            try
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    var colon = line.IndexOf(':');
                    if (colon <= 0) continue;
                    meta.Set(line[..colon].Trim(), line[(colon + 1)..].Trim());
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RetroOS: could not read '{path}'. {e.Message}");
            }
            return meta;
        }

        // Writing an empty sidecar deletes it, so clearing every property leaves the folder clean.
        public bool Write(string nodePath)
        {
            // A sidecar never gets a sidecar of its own.
            if (nodePath != null &&
                (nodePath.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) ||
                 nodePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)))
            {
                Debug.LogWarning($"RetroOS: refused to write a sidecar for '{nodePath}'.");
                return false;
            }

            var path = SidecarFor(nodePath);
            try
            {
                if (IsEmpty)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return true;
                }

                var body = string.Join("\n", _values.Select(p => $"{p.Key}: {p.Value}")) + "\n";
                File.WriteAllText(path, body);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RetroOS: could not write '{path}'. {e.Message}");
                return false;
            }
        }
    }
}
