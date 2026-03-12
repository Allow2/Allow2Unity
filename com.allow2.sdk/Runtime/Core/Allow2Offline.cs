// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Allow2
{
    /// <summary>
    /// Offline Handler.
    /// Caches the last successful check result and enforces a grace period
    /// when the device loses connectivity. After the grace period expires,
    /// defaults to DENY (block all activities).
    ///
    /// Cache is stored in PlayerPrefs to survive app restarts.
    /// </summary>
    public class Allow2Offline
    {
        private const string CacheKey = "allow2_offline_cache";
        private const string TimestampKey = "allow2_offline_ts";

        private readonly int _gracePeriodSeconds;
        private Dictionary<string, object> _cachedResult;
        private long _cachedTimestamp;
        private bool _loaded;

        /// <summary>Fired when entering grace period.</summary>
        public event Action<int> OnOfflineGrace; // elapsed seconds

        /// <summary>Fired when grace period expires.</summary>
        public event Action OnOfflineDeny;

        public Allow2Offline(int gracePeriodSeconds)
        {
            _gracePeriodSeconds = gracePeriodSeconds > 0 ? gracePeriodSeconds : 300;
            _cachedResult = null;
            _cachedTimestamp = 0;
            _loaded = false;
        }

        public Allow2Offline() : this(300) { }

        /// <summary>
        /// Store a successful check result.
        /// </summary>
        public void CacheResult(Dictionary<string, object> checkResult)
        {
            _cachedResult = checkResult;
            _cachedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            try
            {
                string json = MiniJson.Serialize(checkResult);
                PlayerPrefs.SetString(CacheKey, json);
                PlayerPrefs.SetString(TimestampKey, _cachedTimestamp.ToString());
                PlayerPrefs.Save();
            }
            catch (Exception)
            {
                // Persist failure is non-fatal
            }
        }

        /// <summary>
        /// Return the cached check result, loading from PlayerPrefs if needed.
        /// Returns null if no cache exists.
        /// </summary>
        public Dictionary<string, object> GetCachedResult()
        {
            EnsureLoaded();
            return _cachedResult;
        }

        /// <summary>
        /// Seconds elapsed since the last successful check.
        /// Returns int.MaxValue if no cached result exists.
        /// </summary>
        public int GetGraceElapsed()
        {
            EnsureLoaded();
            if (_cachedTimestamp == 0) return int.MaxValue;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return (int)((now - _cachedTimestamp) / 1000);
        }

        /// <summary>
        /// True if we are still within the grace period.
        /// </summary>
        public bool IsInGracePeriod()
        {
            int elapsed = GetGraceElapsed();
            if (elapsed < _gracePeriodSeconds)
            {
                if (OnOfflineGrace != null)
                {
                    OnOfflineGrace(elapsed);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// True if the grace period has expired and we should deny by default.
        /// </summary>
        public bool ShouldDeny()
        {
            int elapsed = GetGraceElapsed();
            if (elapsed >= _gracePeriodSeconds)
            {
                if (OnOfflineDeny != null)
                {
                    OnOfflineDeny();
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clear all cached data.
        /// </summary>
        public void Clear()
        {
            _cachedResult = null;
            _cachedTimestamp = 0;
            PlayerPrefs.DeleteKey(CacheKey);
            PlayerPrefs.DeleteKey(TimestampKey);
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                string json = PlayerPrefs.GetString(CacheKey, "");
                string tsStr = PlayerPrefs.GetString(TimestampKey, "");

                if (!string.IsNullOrEmpty(json) && !string.IsNullOrEmpty(tsStr))
                {
                    _cachedResult = MiniJson.Deserialize(json) as Dictionary<string, object>;
                    long ts;
                    if (long.TryParse(tsStr, out ts))
                    {
                        _cachedTimestamp = ts;
                    }
                }
            }
            catch (Exception)
            {
                // Corrupt cache -- start fresh
                _cachedResult = null;
                _cachedTimestamp = 0;
            }
        }
    }
}
