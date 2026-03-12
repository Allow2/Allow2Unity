// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;
using UnityEngine.Events;

namespace Allow2
{
    /// <summary>
    /// UnityEvent wrappers for Inspector binding.
    /// These can be configured in the Unity Editor's Inspector panel
    /// on the Allow2Manager component.
    /// </summary>

    [Serializable]
    public class Allow2PairingRequiredEvent : UnityEvent<string, string> { }

    [Serializable]
    public class Allow2PairedEvent : UnityEvent { }

    [Serializable]
    public class Allow2ChildSelectRequiredEvent : UnityEvent { }

    [Serializable]
    public class Allow2ChildSelectedEvent : UnityEvent<int, string> { }

    [Serializable]
    public class Allow2SoftLockEvent : UnityEvent<string> { }

    [Serializable]
    public class Allow2HardLockEvent : UnityEvent<string> { }

    [Serializable]
    public class Allow2UnlockEvent : UnityEvent<string> { }

    [Serializable]
    public class Allow2WarningEvent : UnityEvent<string, int, int> { }

    [Serializable]
    public class Allow2CheckResultEvent : UnityEvent { }

    [Serializable]
    public class Allow2StateChangedEvent : UnityEvent<int> { }

    [Serializable]
    public class Allow2UnpairedEvent : UnityEvent { }

    [Serializable]
    public class Allow2ParentModeEvent : UnityEvent { }

    [Serializable]
    public class Allow2SessionTimeoutEvent : UnityEvent { }

    [Serializable]
    public class Allow2ErrorEvent : UnityEvent<string> { }
}
