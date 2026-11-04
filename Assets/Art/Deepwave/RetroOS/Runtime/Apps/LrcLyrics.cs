using System;
using System.Collections.Generic;
using System.Globalization;

namespace Deepwave.RetroOS.Apps
{
    /// <summary>One timed line of a song.</summary>
    public readonly struct LrcLine
    {
        /// <summary>Seconds into the track when it is sung.</summary>
        public readonly float Time;

        public readonly string Text;

        public LrcLine(float time, string text)
        {
            Time = time;
            Text = text ?? "";
        }
    }

    /// <summary>
    /// Reads .lrc lyric files - a plain text file of <c>[mm:ss.xx]</c> stamps.
    /// Forgiving of hand-written malformed files: a stampless line is dropped, a
    /// lineless stamp is kept (an instrumental break), a line may carry several stamps.
    /// </summary>
    public static class LrcLyrics
    {
        /// <summary>Parses a .lrc file. Always returns a list, sorted by time.</summary>
        public static List<LrcLine> Parse(string text)
        {
            var lines = new List<LrcLine>();
            if (string.IsNullOrWhiteSpace(text)) return lines;

            var offset = 0f;

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim('\r', ' ', '\t');
                if (line.Length == 0 || line[0] != '[') continue;

                // The stamps run together at the front: [00:12.00][01:20.00]Chorus
                var stamps = new List<float>();
                var at = 0;

                while (at < line.Length && line[at] == '[')
                {
                    var close = line.IndexOf(']', at);
                    if (close < 0) break;

                    var body = line.Substring(at + 1, close - at - 1);
                    at = close + 1;

                    if (TryTime(body, out var seconds)) stamps.Add(seconds);
                    else if (TryOffset(body, out var ms)) offset = ms / 1000f;
                    // Anything else is an id tag - [ar:], [ti:], [by:] - and is
                    // not ours to show.
                }

                if (stamps.Count == 0) continue;

                var words = line.Substring(at).Trim();
                foreach (var seconds in stamps) lines.Add(new LrcLine(seconds, words));
            }

            // An offset is the file telling us its stamps run early or late.
            if (Math.Abs(offset) > 0.0001f)
                for (var i = 0; i < lines.Count; i++)
                    lines[i] = new LrcLine(Math.Max(0f, lines[i].Time - offset), lines[i].Text);

            lines.Sort((a, b) => a.Time.CompareTo(b.Time));
            return lines;
        }

        /// <summary>The line being sung at this point in the track, or -1 before the first one.</summary>
        public static int LineAt(IReadOnlyList<LrcLine> lines, float seconds)
        {
            if (lines == null || lines.Count == 0) return -1;

            var found = -1;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Time > seconds) break;
                found = i;
            }

            return found;
        }

        // [mm:ss], [mm:ss.xx] and [mm:ss:xx], which all three turn up.
        static bool TryTime(string body, out float seconds)
        {
            seconds = 0f;

            var colon = body.IndexOf(':');
            if (colon <= 0) return false;

            if (!int.TryParse(body.Substring(0, colon), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var minutes)) return false;

            var rest = body.Substring(colon + 1).Replace(':', '.');
            if (!float.TryParse(rest, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var secs)) return false;

            if (minutes < 0 || secs < 0f) return false;

            seconds = minutes * 60f + secs;
            return true;
        }

        static bool TryOffset(string body, out float milliseconds)
        {
            milliseconds = 0f;

            if (!body.StartsWith("offset:", StringComparison.OrdinalIgnoreCase)) return false;

            return float.TryParse(body.Substring(7).Trim().TrimStart('+'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out milliseconds);
        }
    }
}
