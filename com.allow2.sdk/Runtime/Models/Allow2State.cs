// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2
{
    /// <summary>
    /// Daemon lifecycle states.
    /// </summary>
    public enum Allow2State
    {
        /// <summary>Device has no stored pairing credentials.</summary>
        Unpaired,

        /// <summary>Pairing wizard is active, waiting for parent to confirm.</summary>
        Pairing,

        /// <summary>Device is paired but no child has been selected yet.</summary>
        Paired,

        /// <summary>A child is selected and the check loop is running.</summary>
        Enforcing,

        /// <summary>Parent mode: no enforcement applied.</summary>
        Parent
    }
}
