// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;

namespace Allow2
{
    /// <summary>
    /// Stored pairing credentials for an Allow2 device.
    /// </summary>
    [Serializable]
    public class Allow2Credentials
    {
        public string Uuid;
        public int UserId;
        public int PairId;
        public string PairToken;
        public Allow2Child[] Children;

        public bool IsValid
        {
            get
            {
                return PairId > 0 && !string.IsNullOrEmpty(PairToken);
            }
        }
    }
}
