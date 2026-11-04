using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deepwave.RetroOS.Data
{
    /// <summary>Start-up text shown before the logon prompt.</summary>
    [Serializable]
    public class BootSettings
    {
        [Tooltip("Print the start-up sequence before the logon screen.")]
        public bool enabled;

        [Tooltip("First line - the firmware's own banner.")]
        public string firmwareBanner = "Deepwave BIOS v2.11  (C) 1996 Deepwave Systems";

        [Tooltip("Processor, as the firmware reports it.")]
        public string cpuName = "Pentium(R) 133 MHz";

        [Tooltip("Memory the machine counts. 16384 = 16 MB, the period-typical figure.")]
        public int memoryKilobytes = 16384;

        [Tooltip("Lines added before 'Starting...' - a warning, an owner, a fault.")]
        public List<string> extraLines = new();

        [Tooltip("Seconds between lines. Lower is a faster machine.")]
        [Range(0.01f, 0.6f)] public float lineDelay = 0.09f;

        [Tooltip("Seconds the finished screen is held before the logon prompt.")]
        [Range(0f, 5f)] public float holdSeconds = 0.8f;
    }
}
