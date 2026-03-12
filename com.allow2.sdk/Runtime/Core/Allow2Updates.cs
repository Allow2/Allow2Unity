// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;
using System.Collections.Generic;

namespace Allow2
{
    /// <summary>
    /// Update Poller.
    /// Polls GET /api/getUpdates for changes since the last check.
    /// Emits events for extensions, day type changes, quota updates,
    /// bans, and children list refreshes.
    ///
    /// Polling is driven by the bridge (coroutine). This class holds
    /// state and interprets responses.
    /// </summary>
    public class Allow2Updates
    {
        private long _lastTimestamp;
        private bool _running;

        /// <summary>Fired when a parent approves extra time.</summary>
        public event Action<int, int, int> OnExtension; // childId, activityId, additionalMinutes

        /// <summary>Fired when a day type changes.</summary>
        public event Action<int, string> OnDayTypeChanged; // childId, dayType

        /// <summary>Fired when a quota is updated.</summary>
        public event Action<int, int, int> OnQuotaUpdated; // childId, activityId, newQuota

        /// <summary>Fired when a ban is applied/removed.</summary>
        public event Action<int, int, bool> OnBan; // childId, activityId, banned

        /// <summary>Fired when the children list changes.</summary>
        public event Action<Allow2Child[]> OnChildrenUpdated;

        /// <summary>Fired on HTTP 401 (device unpaired).</summary>
        public event Action OnUnpaired;

        public bool IsRunning { get { return _running; } }
        public long LastTimestamp { get { return _lastTimestamp; } }

        public Allow2Updates()
        {
            _lastTimestamp = 0;
            _running = false;
        }

        public void Start()
        {
            _running = true;
        }

        public void Stop()
        {
            _running = false;
        }

        /// <summary>
        /// Handle the API response from getUpdates.
        /// </summary>
        public void HandleResponse(Allow2ApiResponse response)
        {
            if (!_running) return;

            if (response == null)
            {
                return;
            }

            // HTTP 401 = device unpaired
            if (response.StatusCode == 401)
            {
                if (OnUnpaired != null) OnUnpaired();
                Stop();
                return;
            }

            if (!response.IsSuccess || response.Body == null)
            {
                return;
            }

            // Advance timestamp
            long ts = response.GetLong("timestampMillis");
            if (ts > 0)
            {
                _lastTimestamp = ts;
            }

            Dictionary<string, object> body = response.Body;

            // Extensions
            ProcessListEvent(body, "extensions", delegate(Dictionary<string, object> item)
            {
                if (OnExtension != null)
                {
                    OnExtension(
                        GetInt(item, "childId"),
                        GetInt(item, "activity"),
                        GetInt(item, "additionalMinutes")
                    );
                }
            });

            // Day type changes
            ProcessListEvent(body, "dayTypeChanges", delegate(Dictionary<string, object> item)
            {
                if (OnDayTypeChanged != null)
                {
                    OnDayTypeChanged(
                        GetInt(item, "childId"),
                        GetString(item, "dayType")
                    );
                }
            });

            // Quota updates
            ProcessListEvent(body, "quotaUpdates", delegate(Dictionary<string, object> item)
            {
                if (OnQuotaUpdated != null)
                {
                    OnQuotaUpdated(
                        GetInt(item, "childId"),
                        GetInt(item, "activity"),
                        GetInt(item, "newQuota")
                    );
                }
            });

            // Bans
            ProcessListEvent(body, "bans", delegate(Dictionary<string, object> item)
            {
                if (OnBan != null)
                {
                    OnBan(
                        GetInt(item, "childId"),
                        GetInt(item, "activity"),
                        GetBool(item, "banned")
                    );
                }
            });

            // Children
            object childrenObj;
            if (body.TryGetValue("children", out childrenObj))
            {
                List<object> childList = childrenObj as List<object>;
                if (childList != null && childList.Count > 0)
                {
                    Allow2Child[] children = new Allow2Child[childList.Count];
                    for (int i = 0; i < childList.Count; i++)
                    {
                        Dictionary<string, object> cDict = childList[i] as Dictionary<string, object>;
                        if (cDict != null)
                        {
                            Allow2Child child = new Allow2Child();
                            child.Id = GetInt(cDict, "id");
                            child.Name = GetString(cDict, "name");
                            child.PinHash = GetString(cDict, "pinHash");
                            child.PinSalt = GetString(cDict, "pinSalt");
                            children[i] = child;
                        }
                    }
                    if (OnChildrenUpdated != null)
                    {
                        OnChildrenUpdated(children);
                    }
                }
            }
        }

        private void ProcessListEvent(Dictionary<string, object> body, string key,
            Action<Dictionary<string, object>> handler)
        {
            object listObj;
            if (!body.TryGetValue(key, out listObj)) return;
            List<object> list = listObj as List<object>;
            if (list == null || list.Count == 0) return;

            for (int i = 0; i < list.Count; i++)
            {
                Dictionary<string, object> item = list[i] as Dictionary<string, object>;
                if (item != null)
                {
                    handler(item);
                }
            }
        }

        private static int GetInt(Dictionary<string, object> dict, string key)
        {
            object val;
            if (dict.TryGetValue(key, out val))
            {
                if (val is long) return (int)(long)val;
                if (val is double) return (int)(double)val;
                if (val is int) return (int)val;
            }
            return 0;
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            object val;
            if (dict.TryGetValue(key, out val) && val != null)
            {
                return val.ToString();
            }
            return null;
        }

        private static bool GetBool(Dictionary<string, object> dict, string key)
        {
            object val;
            if (dict.TryGetValue(key, out val))
            {
                if (val is bool) return (bool)val;
            }
            return false;
        }
    }
}
