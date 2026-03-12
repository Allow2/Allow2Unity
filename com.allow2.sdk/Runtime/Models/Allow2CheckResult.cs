// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;
using System.Collections.Generic;

namespace Allow2
{
    /// <summary>
    /// Result from a permission check API call.
    /// Contains per-activity status, day types, children, and subscription info.
    /// </summary>
    [Serializable]
    public class Allow2CheckResult
    {
        /// <summary>Top-level allowed flag.</summary>
        public bool Allowed;

        /// <summary>Per-activity results keyed by activity ID.</summary>
        public Dictionary<int, Allow2ActivityResult> Activities;

        /// <summary>Today's day type.</summary>
        public Allow2DayType Today;

        /// <summary>Tomorrow's day type.</summary>
        public Allow2DayType Tomorrow;

        /// <summary>All configured day types.</summary>
        public Allow2DayType[] AllDayTypes;

        /// <summary>Children on this account.</summary>
        public Allow2Child[] Children;

        /// <summary>Subscription status.</summary>
        public Allow2Subscription Subscription;

        public Allow2CheckResult()
        {
            Activities = new Dictionary<int, Allow2ActivityResult>();
        }

        /// <summary>
        /// Get result for a specific activity.
        /// Returns null if the activity was not in the check response.
        /// </summary>
        public Allow2ActivityResult GetActivity(int activityId)
        {
            Allow2ActivityResult result;
            if (Activities != null && Activities.TryGetValue(activityId, out result))
            {
                return result;
            }
            return null;
        }

        /// <summary>
        /// Returns true if ALL checked activities are blocked.
        /// </summary>
        public bool AllBlocked
        {
            get
            {
                if (Activities == null || Activities.Count == 0) return false;
                foreach (KeyValuePair<int, Allow2ActivityResult> kvp in Activities)
                {
                    if (kvp.Value.IsAllowed) return false;
                }
                return true;
            }
        }
    }

    /// <summary>
    /// Per-activity result from a check call.
    /// </summary>
    [Serializable]
    public class Allow2ActivityResult
    {
        public int Id;
        public string Name;
        public bool IsAllowed;
        public bool Banned;
        public bool Timed;
        public int Remaining;
        public string Units;
        public Allow2TimeBlock TimeBlock;

        public Allow2ActivityResult() { }
    }

    /// <summary>
    /// Time block information within an activity result.
    /// </summary>
    [Serializable]
    public class Allow2TimeBlock
    {
        public bool Allowed;
        public int Remaining;
    }

    /// <summary>
    /// Day type (e.g., School Day, Weekend, Holiday).
    /// </summary>
    [Serializable]
    public class Allow2DayType
    {
        public int Id;
        public string Name;

        public Allow2DayType() { }

        public Allow2DayType(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    /// <summary>
    /// Subscription status from the Allow2 platform.
    /// </summary>
    [Serializable]
    public class Allow2Subscription
    {
        public bool Active;
        public int Type;
        public int MaxChildren;
        public int ChildCount;
        public int DeviceCount;
        public int ServiceCount;
        public bool Financial;
    }
}
