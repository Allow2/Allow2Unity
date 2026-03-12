// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;
using System.Collections.Generic;

namespace Allow2
{
    /// <summary>
    /// Warning Scheduler.
    /// Tracks remaining time per activity and fires warnings
    /// when configurable thresholds are crossed. Prevents duplicate
    /// warnings for the same level+activity combination.
    /// </summary>
    public class Allow2Warnings
    {
        private readonly Allow2Warning[] _thresholds;
        private readonly Dictionary<int, HashSet<Allow2WarningLevel>> _fired;

        /// <summary>
        /// Fired when a warning threshold is crossed for an activity.
        /// </summary>
        public event Action<Allow2WarningEventArgs> OnWarning;

        private static readonly Allow2Warning[] DefaultThresholds = new Allow2Warning[]
        {
            new Allow2Warning(15 * 60, Allow2WarningLevel.Info),
            new Allow2Warning(5 * 60, Allow2WarningLevel.Urgent),
            new Allow2Warning(60, Allow2WarningLevel.Final),
            new Allow2Warning(30, Allow2WarningLevel.Countdown),
        };

        public Allow2Warnings(Allow2Warning[] customThresholds)
        {
            if (customThresholds != null && customThresholds.Length > 0)
            {
                _thresholds = (Allow2Warning[])customThresholds.Clone();
            }
            else
            {
                _thresholds = DefaultThresholds;
            }

            // Sort descending by remaining seconds
            Array.Sort(_thresholds, (a, b) => b.RemainingSeconds.CompareTo(a.RemainingSeconds));

            _fired = new Dictionary<int, HashSet<Allow2WarningLevel>>();
        }

        /// <summary>
        /// Called by the Checker after each check response.
        /// Evaluates every activity's remaining time against thresholds.
        /// </summary>
        public void Update(Dictionary<int, int> activityRemaining)
        {
            if (activityRemaining == null) return;

            foreach (KeyValuePair<int, int> kvp in activityRemaining)
            {
                int activityId = kvp.Key;
                int remaining = kvp.Value;

                if (remaining < 0) continue;

                HashSet<Allow2WarningLevel> firedSet;
                if (!_fired.TryGetValue(activityId, out firedSet))
                {
                    firedSet = new HashSet<Allow2WarningLevel>();
                    _fired[activityId] = firedSet;
                }

                for (int t = 0; t < _thresholds.Length; t++)
                {
                    Allow2Warning threshold = _thresholds[t];
                    if (remaining <= threshold.RemainingSeconds && !firedSet.Contains(threshold.Level))
                    {
                        firedSet.Add(threshold.Level);
                        if (OnWarning != null)
                        {
                            OnWarning(new Allow2WarningEventArgs(threshold.Level, activityId, remaining));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reset warnings for an activity (e.g., parent approved more time).
        /// </summary>
        public void ResetActivity(int activityId)
        {
            _fired.Remove(activityId);
        }

        /// <summary>
        /// Reset all warning state (e.g., new child session).
        /// </summary>
        public void ResetAll()
        {
            _fired.Clear();
        }
    }
}
