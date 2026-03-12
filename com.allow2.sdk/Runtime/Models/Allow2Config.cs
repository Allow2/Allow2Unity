// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;

namespace Allow2
{
    /// <summary>
    /// Configuration for the Allow2 SDK.
    /// Pass to Allow2Manager.Configure() or Allow2Daemon constructor.
    /// </summary>
    [Serializable]
    public class Allow2Config
    {
        /// <summary>
        /// Version ID registered at developer.allow2.com.
        /// </summary>
        public int Vid;

        /// <summary>
        /// Version token registered at developer.allow2.com.
        /// </summary>
        public string DeviceToken;

        /// <summary>
        /// Human-readable device name shown to parents.
        /// Falls back to SystemInfo.deviceName if empty.
        /// </summary>
        public string DeviceName;

        /// <summary>
        /// Activities this game/app monitors.
        /// Must not be empty.
        /// </summary>
        public Allow2Activity[] Activities;

        /// <summary>
        /// API base URL. Defaults to https://api.allow2.com.
        /// </summary>
        public string ApiUrl;

        /// <summary>
        /// Seconds between permission checks. Default 60.
        /// </summary>
        public int CheckIntervalSeconds;

        /// <summary>
        /// Seconds after soft-lock before hard-lock. Default 300.
        /// </summary>
        public int HardLockTimeoutSeconds;

        /// <summary>
        /// Offline grace period in seconds before deny-by-default. Default 300.
        /// </summary>
        public int GracePeriodSeconds;

        /// <summary>
        /// Custom warning thresholds. Null uses defaults (15m, 5m, 1m, 30s).
        /// </summary>
        public Allow2Warning[] WarningThresholds;

        public Allow2Config()
        {
            ApiUrl = "https://api.allow2.com";
            CheckIntervalSeconds = 60;
            HardLockTimeoutSeconds = 300;
            GracePeriodSeconds = 300;
        }

        public void Validate()
        {
            if (Vid <= 0)
            {
                throw new ArgumentException("Vid must be a positive integer");
            }
            if (string.IsNullOrEmpty(DeviceToken))
            {
                throw new ArgumentException("DeviceToken is required");
            }
            if (Activities == null || Activities.Length == 0)
            {
                throw new ArgumentException("Activities array is required and must not be empty");
            }
        }
    }
}
