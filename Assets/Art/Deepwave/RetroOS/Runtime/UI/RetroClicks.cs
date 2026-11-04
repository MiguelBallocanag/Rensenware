using UnityEngine;

namespace Deepwave.RetroOS.UI
{
    /// <summary>
    /// How fast two clicks have to be to count as one double-click, and the
    /// counter every widget in the kit uses to decide.
    ///
    /// uGUI keeps its own count in <c>PointerEventData.clickCount</c>, but the
    /// window it counts inside is a private constant in the input module - so a
    /// machine that lets the player set the speed has to count for itself, or the
    /// setting moves the list views and leaves the desktop icons alone, which is
    /// worse than not offering it.
    /// </summary>
    public static class RetroClicks
    {
        /// <summary>What Windows shipped with, and what a machine that has never been asked uses.</summary>
        public const float DefaultSeconds = 0.4f;

        /// <summary>The window two clicks have to land in. Set from the Control Panel.</summary>
        public static float Seconds { get; set; } = DefaultSeconds;

        static object s_target;
        static float s_at;

        /// <summary>Puts the speed back to <see cref="DefaultSeconds"/> - for a machine restoring its settings, and for a test that has just changed it.</summary>
        public static void Reset() => Seconds = DefaultSeconds;

        /// <summary>
        /// Whether this click closes a double-click: the same thing, soon enough
        /// after the last one. A third click starts a new pair rather than firing
        /// again, the way a real desktop counts.
        /// </summary>
        public static bool IsDouble(object target)
        {
            var now = Time.unscaledTime;
            var matched = target != null && ReferenceEquals(target, s_target) &&
                          now - s_at <= Mathf.Max(0.05f, Seconds);

            s_target = matched ? null : target;
            s_at = matched ? 0f : now;
            return matched;
        }
    }
}
