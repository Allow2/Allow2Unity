// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;
using System.Collections.Generic;

namespace Allow2
{
    /// <summary>
    /// Check Loop + Per-Activity Enforcement.
    /// Periodically calls the Allow2 check API and tracks per-activity
    /// state transitions. Delegates warning scheduling to Allow2Warnings.
    ///
    /// This is pure C# -- the Unity MonoBehaviour bridge drives the
    /// coroutine-based check loop and calls ProcessResult / HandleError.
    /// </summary>
    public class Allow2Checker
    {
        private readonly Allow2Activity[] _activities;
        private readonly int _hardLockTimeoutMs;
        private readonly int _gracePeriodMs;
        private readonly Allow2Warnings _warnings;

        private int _childId;
        private readonly Dictionary<int, ActivityState> _state;

        private bool _softLocked;
        private float _softLockElapsed;
        private long _offlineSince;
        private bool _offlineGraceEmitted;
        private bool _running;

        // ScreenTime activity is the master switch
        private const int SCREEN_TIME_ACTIVITY = 8;

        /// <summary>Fired when an activity transitions from allowed to blocked.</summary>
        public event Action<int, string, int> OnActivityBlocked;

        /// <summary>Fired when all activities are blocked (pause game).</summary>
        public event Action<string> OnSoftLock;

        /// <summary>Fired after soft-lock timeout expires (close game).</summary>
        public event Action<string> OnHardLock;

        /// <summary>Fired when at least one activity becomes allowed again.</summary>
        public event Action<string> OnUnlock;

        /// <summary>Fired on HTTP 401 -- device was unpaired.</summary>
        public event Action OnUnpaired;

        /// <summary>Fired during offline grace period.</summary>
        public event Action<long, long> OnOfflineGrace;

        /// <summary>Fired when grace period expires.</summary>
        public event Action<long, long> OnOfflineDeny;

        /// <summary>Fired after each successful check with the full result.</summary>
        public event Action<Allow2CheckResult> OnCheckResult;

        /// <summary>Warning events (delegates to Allow2Warnings).</summary>
        public event Action<Allow2WarningEventArgs> OnWarning
        {
            add { _warnings.OnWarning += value; }
            remove { _warnings.OnWarning -= value; }
        }

        private class ActivityState
        {
            public bool Allowed;
            public int Remaining;
        }

        public Allow2Checker(Allow2Activity[] activities, int hardLockTimeoutSeconds,
            int gracePeriodSeconds, Allow2Warning[] warningThresholds)
        {
            _activities = activities;
            _hardLockTimeoutMs = hardLockTimeoutSeconds * 1000;
            _gracePeriodMs = gracePeriodSeconds * 1000;
            _warnings = new Allow2Warnings(warningThresholds);
            _state = new Dictionary<int, ActivityState>();
            _softLocked = false;
            _softLockElapsed = 0f;
            _offlineSince = 0;
            _offlineGraceEmitted = false;
            _running = false;
        }

        public bool IsRunning { get { return _running; } }
        public bool IsSoftLocked { get { return _softLocked; } }

        public int ChildId
        {
            get { return _childId; }
            set { _childId = value; }
        }

        /// <summary>
        /// Get the activity IDs to check as a dictionary (id -> 1).
        /// </summary>
        public Dictionary<int, int> GetActivityMap()
        {
            Dictionary<int, int> map = new Dictionary<int, int>();
            for (int i = 0; i < _activities.Length; i++)
            {
                map[_activities[i].Id] = 1;
            }
            return map;
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
        /// Called by the bridge after a successful API check.
        /// Parses the response and manages state transitions.
        /// </summary>
        public void ProcessResult(Allow2ApiResponse response)
        {
            if (!_running) return;

            // Clear offline state on successful API call
            if (_offlineSince > 0)
            {
                _offlineSince = 0;
                _offlineGraceEmitted = false;
            }

            if (response == null || response.Body == null) return;

            Allow2CheckResult result = ParseCheckResult(response.Body);
            if (result == null) return;

            if (OnCheckResult != null)
            {
                OnCheckResult(result);
            }

            bool allBlocked = true;
            Dictionary<int, int> warningData = new Dictionary<int, int>();

            foreach (KeyValuePair<int, Allow2ActivityResult> kvp in result.Activities)
            {
                int id = kvp.Key;
                Allow2ActivityResult current = kvp.Value;
                bool allowed = current.IsAllowed;
                int remaining = current.Remaining;

                ActivityState prev;
                bool wasAllowed = true;
                if (_state.TryGetValue(id, out prev))
                {
                    wasAllowed = prev.Allowed;
                }

                // Detect allowed -> blocked transition
                if (wasAllowed && !allowed)
                {
                    if (OnActivityBlocked != null)
                    {
                        OnActivityBlocked(id, current.Name, 0);
                    }

                    if (id == SCREEN_TIME_ACTIVITY)
                    {
                        TriggerSoftLock("screen-time-exhausted");
                    }
                }

                // Detect blocked -> allowed transition
                if (!wasAllowed && allowed && prev != null)
                {
                    _warnings.ResetActivity(id);
                }

                // Update state
                if (!_state.ContainsKey(id))
                {
                    _state[id] = new ActivityState();
                }
                _state[id].Allowed = allowed;
                _state[id].Remaining = remaining;

                if (allowed)
                {
                    allBlocked = false;
                    warningData[id] = remaining;
                }
            }

            // All blocked -> soft-lock
            if (result.Activities.Count > 0 && allBlocked && !_softLocked)
            {
                TriggerSoftLock("all-activities-blocked");
            }

            // Was soft-locked but something is now allowed -> unlock
            if (_softLocked && !allBlocked)
            {
                _softLocked = false;
                _softLockElapsed = 0f;
                if (OnUnlock != null)
                {
                    OnUnlock("activity-unblocked");
                }
            }

            // Feed remaining times to warning scheduler
            if (warningData.Count > 0)
            {
                _warnings.Update(warningData);
            }
        }

        /// <summary>
        /// Called by the bridge when an API check fails.
        /// </summary>
        public void HandleError(Allow2ApiResponse response)
        {
            if (!_running) return;

            // HTTP 401 = device unpaired
            if (response != null && response.StatusCode == 401)
            {
                if (OnUnpaired != null)
                {
                    OnUnpaired();
                }
                Stop();
                return;
            }

            // Network / timeout errors -> offline handling
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_offlineSince == 0)
            {
                _offlineSince = now;
            }

            long offlineDuration = now - _offlineSince;

            if (offlineDuration < _gracePeriodMs)
            {
                if (!_offlineGraceEmitted)
                {
                    _offlineGraceEmitted = true;
                    if (OnOfflineGrace != null)
                    {
                        OnOfflineGrace(_offlineSince, _gracePeriodMs - offlineDuration);
                    }
                }
            }
            else
            {
                if (OnOfflineDeny != null)
                {
                    OnOfflineDeny(_offlineSince, offlineDuration);
                }
            }
        }

        /// <summary>
        /// Notify the checker that time was extended for an activity.
        /// Resets warning state for that activity.
        /// </summary>
        public void OnTimeExtended(int activityId)
        {
            _warnings.ResetActivity(activityId);

            if (_softLocked)
            {
                _softLocked = false;
                _softLockElapsed = 0f;
                if (OnUnlock != null)
                {
                    OnUnlock("time-extended");
                }
            }
        }

        /// <summary>
        /// Called each frame by the bridge to track soft-lock elapsed time.
        /// </summary>
        public void UpdateSoftLockTimer(float deltaTime)
        {
            if (!_softLocked || !_running) return;

            _softLockElapsed += deltaTime * 1000f;
            if (_softLockElapsed >= _hardLockTimeoutMs)
            {
                if (OnHardLock != null)
                {
                    OnHardLock("soft-lock-timeout");
                }
            }
        }

        /// <summary>
        /// Get remaining time for all tracked activities.
        /// Returns null if no state yet.
        /// </summary>
        public Dictionary<int, int> GetRemaining()
        {
            if (_state.Count == 0) return null;
            Dictionary<int, int> result = new Dictionary<int, int>();
            foreach (KeyValuePair<int, ActivityState> kvp in _state)
            {
                result[kvp.Key] = kvp.Value.Remaining;
            }
            return result;
        }

        /// <summary>
        /// Reset all state (e.g., new child selected).
        /// </summary>
        public void Reset(int childId)
        {
            _childId = childId;
            _state.Clear();
            _softLocked = false;
            _softLockElapsed = 0f;
            _offlineSince = 0;
            _offlineGraceEmitted = false;
            _warnings.ResetAll();
        }

        private void TriggerSoftLock(string reason)
        {
            if (_softLocked) return;
            _softLocked = true;
            _softLockElapsed = 0f;

            if (OnSoftLock != null)
            {
                OnSoftLock(reason);
            }
        }

        // ----------------------------------------------------------------
        // Response parsing
        // ----------------------------------------------------------------

        private Allow2CheckResult ParseCheckResult(Dictionary<string, object> body)
        {
            Allow2CheckResult result = new Allow2CheckResult();
            result.Allowed = GetBool(body, "allowed");

            object activitiesObj;
            if (body.TryGetValue("activities", out activitiesObj))
            {
                Dictionary<string, object> activitiesDict = activitiesObj as Dictionary<string, object>;
                if (activitiesDict != null)
                {
                    foreach (KeyValuePair<string, object> kvp in activitiesDict)
                    {
                        int actId;
                        if (!int.TryParse(kvp.Key, out actId)) continue;

                        Dictionary<string, object> actDict = kvp.Value as Dictionary<string, object>;
                        if (actDict == null) continue;

                        Allow2ActivityResult ar = new Allow2ActivityResult();
                        ar.Id = actId;
                        ar.Name = GetString(actDict, "name");
                        ar.IsAllowed = GetBool(actDict, "allowed");
                        ar.Banned = GetBool(actDict, "banned");
                        ar.Timed = GetBool(actDict, "timed");
                        ar.Remaining = GetInt(actDict, "remaining");
                        ar.Units = GetString(actDict, "units");

                        object tbObj;
                        if (actDict.TryGetValue("timeBlock", out tbObj))
                        {
                            Dictionary<string, object> tbDict = tbObj as Dictionary<string, object>;
                            if (tbDict != null)
                            {
                                ar.TimeBlock = new Allow2TimeBlock();
                                ar.TimeBlock.Allowed = GetBool(tbDict, "allowed");
                                ar.TimeBlock.Remaining = GetInt(tbDict, "remaining");
                            }
                        }

                        result.Activities[actId] = ar;
                    }
                }
            }

            // Parse day types
            object dayTypesObj;
            if (body.TryGetValue("dayTypes", out dayTypesObj))
            {
                Dictionary<string, object> dtDict = dayTypesObj as Dictionary<string, object>;
                if (dtDict != null)
                {
                    result.Today = ParseDayType(dtDict, "today");
                    result.Tomorrow = ParseDayType(dtDict, "tomorrow");
                }
            }

            // Parse children
            object childrenObj;
            if (body.TryGetValue("children", out childrenObj))
            {
                List<object> childList = childrenObj as List<object>;
                if (childList != null)
                {
                    result.Children = new Allow2Child[childList.Count];
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
                            result.Children[i] = child;
                        }
                    }
                }
            }

            return result;
        }

        private Allow2DayType ParseDayType(Dictionary<string, object> parent, string key)
        {
            object obj;
            if (parent.TryGetValue(key, out obj))
            {
                Dictionary<string, object> dtDict = obj as Dictionary<string, object>;
                if (dtDict != null)
                {
                    return new Allow2DayType(GetInt(dtDict, "id"), GetString(dtDict, "name"));
                }
            }
            return null;
        }

        // -- Helpers for reading Dictionary<string,object> safely --

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
    }
}
