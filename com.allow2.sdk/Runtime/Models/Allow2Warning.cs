// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;

namespace Allow2
{
    /// <summary>
    /// Warning level emitted when remaining time crosses a threshold.
    /// </summary>
    public enum Allow2WarningLevel
    {
        Info,
        Urgent,
        Final,
        Countdown
    }

    /// <summary>
    /// Warning threshold definition.
    /// </summary>
    [Serializable]
    public class Allow2Warning
    {
        /// <summary>Remaining seconds at which to trigger this warning.</summary>
        public int RemainingSeconds;

        /// <summary>Warning severity level.</summary>
        public Allow2WarningLevel Level;

        public Allow2Warning() { }

        public Allow2Warning(int remainingSeconds, Allow2WarningLevel level)
        {
            RemainingSeconds = remainingSeconds;
            Level = level;
        }
    }

    /// <summary>
    /// Warning event data emitted to subscribers.
    /// </summary>
    public class Allow2WarningEventArgs
    {
        public Allow2WarningLevel Level;
        public int ActivityId;
        public int RemainingSeconds;

        public Allow2WarningEventArgs(Allow2WarningLevel level, int activityId, int remainingSeconds)
        {
            Level = level;
            ActivityId = activityId;
            RemainingSeconds = remainingSeconds;
        }
    }
}
