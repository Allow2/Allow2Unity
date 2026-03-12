// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;

namespace Allow2
{
    /// <summary>
    /// C# event definitions for all Allow2 lifecycle transitions.
    /// These are pure C# events (not Unity-specific).
    /// For Inspector-bindable events, see Allow2UnityEvents.
    /// </summary>
    public static class Allow2Events
    {
        // ----------------------------------------------------------------
        // Event argument types
        // ----------------------------------------------------------------

        public class PairingRequiredArgs : EventArgs
        {
            public string Pin;
            public string QrUrl;

            public PairingRequiredArgs(string pin, string qrUrl)
            {
                Pin = pin;
                QrUrl = qrUrl;
            }
        }

        public class PairedArgs : EventArgs
        {
            public Allow2Credentials Credentials;

            public PairedArgs(Allow2Credentials credentials)
            {
                Credentials = credentials;
            }
        }

        public class ChildSelectRequiredArgs : EventArgs
        {
            public Allow2Child[] Children;

            public ChildSelectRequiredArgs(Allow2Child[] children)
            {
                Children = children;
            }
        }

        public class ChildSelectedArgs : EventArgs
        {
            public int ChildId;
            public string ChildName;

            public ChildSelectedArgs(int childId, string childName)
            {
                ChildId = childId;
                ChildName = childName;
            }
        }

        public class CheckResultArgs : EventArgs
        {
            public Allow2CheckResult Result;

            public CheckResultArgs(Allow2CheckResult result)
            {
                Result = result;
            }
        }

        public class ActivityBlockedArgs : EventArgs
        {
            public int ActivityId;
            public string ActivityName;
            public int Remaining;

            public ActivityBlockedArgs(int activityId, string activityName, int remaining)
            {
                ActivityId = activityId;
                ActivityName = activityName;
                Remaining = remaining;
            }
        }

        public class LockArgs : EventArgs
        {
            public string Reason;

            public LockArgs(string reason)
            {
                Reason = reason;
            }
        }

        public class StateChangedArgs : EventArgs
        {
            public Allow2State NewState;

            public StateChangedArgs(Allow2State newState)
            {
                NewState = newState;
            }
        }

        public class RequestStatusArgs : EventArgs
        {
            public string RequestId;
            public Allow2RequestStatus Status;
            public int Extension;

            public RequestStatusArgs(string requestId, Allow2RequestStatus status, int extension)
            {
                RequestId = requestId;
                Status = status;
                Extension = extension;
            }
        }

        public class ErrorArgs : EventArgs
        {
            public string Message;

            public ErrorArgs(string message)
            {
                Message = message;
            }
        }
    }
}
