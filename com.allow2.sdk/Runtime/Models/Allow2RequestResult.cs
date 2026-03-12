// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;

namespace Allow2
{
    /// <summary>
    /// Status of a request (more time, day type change, ban lift).
    /// </summary>
    public enum Allow2RequestStatus
    {
        Pending,
        Approved,
        Denied,
        TimedOut
    }

    /// <summary>
    /// Result from creating or polling a request.
    /// </summary>
    [Serializable]
    public class Allow2RequestResult
    {
        public string RequestId;
        public string StatusSecret;
        public Allow2RequestStatus Status;
        public int ActivityId;
        public int Duration;
        public string Reason;
    }
}
