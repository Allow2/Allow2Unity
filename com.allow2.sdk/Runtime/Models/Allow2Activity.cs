// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;

namespace Allow2
{
    /// <summary>
    /// Represents an Allow2 activity (e.g., Gaming, Screen Time).
    /// </summary>
    [Serializable]
    public class Allow2Activity
    {
        /// <summary>Activity ID from the Allow2 platform.</summary>
        public int Id;

        /// <summary>Human-readable activity name.</summary>
        public string Name;

        public Allow2Activity() { }

        public Allow2Activity(int id, string name)
        {
            Id = id;
            Name = name;
        }

        // Well-known activity IDs
        public const int Internet = 1;
        public const int Computer = 2;
        public const int Gaming = 3;
        public const int Messaging = 4;
        public const int JunkFood = 5;
        public const int Social = 6;
        public const int Electricity = 7;
        public const int ScreenTime = 8;
    }
}
