// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;

namespace Allow2
{
    /// <summary>
    /// Represents a child entity in a controller's Allow2 account.
    /// </summary>
    [Serializable]
    public class Allow2Child
    {
        public int Id;
        public string Name;
        public string PinHash;
        public string PinSalt;
        public string AvatarUrl;
        public string Color;
        public bool HasAccount;
        public int LinkedUserId;
        public string LastUsedAt;

        public Allow2Child() { }

        public Allow2Child(int id, string name)
        {
            Id = id;
            Name = name;
        }

        /// <summary>
        /// Returns a copy safe for display (no PIN data).
        /// </summary>
        public Allow2Child ToDisplayChild()
        {
            Allow2Child copy = new Allow2Child();
            copy.Id = Id;
            copy.Name = Name;
            copy.AvatarUrl = AvatarUrl;
            copy.Color = Color;
            copy.HasAccount = HasAccount;
            copy.LastUsedAt = LastUsedAt;
            return copy;
        }
    }
}
