// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Allow2
{
    /// <summary>
    /// Coroutine wrappers for async operations.
    /// Provides WebGL-compatible alternatives to async/await.
    ///
    /// All coroutines run on the Allow2Manager MonoBehaviour.
    /// </summary>
    public class Allow2Coroutines
    {
        private readonly MonoBehaviour _runner;
        private readonly Allow2Api _api;

        public Allow2Coroutines(MonoBehaviour runner, Allow2Api api)
        {
            _runner = runner;
            _api = api;
        }

        /// <summary>
        /// Start a coroutine on the runner MonoBehaviour.
        /// </summary>
        public Coroutine Run(IEnumerator routine)
        {
            if (_runner == null || !_runner.gameObject.activeInHierarchy)
            {
                return null;
            }
            return _runner.StartCoroutine(routine);
        }

        /// <summary>
        /// Stop a coroutine.
        /// </summary>
        public void Cancel(Coroutine coroutine)
        {
            if (_runner != null && coroutine != null)
            {
                _runner.StopCoroutine(coroutine);
            }
        }

        // ----------------------------------------------------------------
        // API coroutine wrappers
        // ----------------------------------------------------------------

        /// <summary>
        /// Run a permission check against the API.
        /// </summary>
        public Coroutine RunCheck(int userId, int pairId, string pairToken,
            int childId, Dictionary<int, int> activities, string tz,
            Action<Allow2ApiResponse> callback)
        {
            return Run(_api.Check(userId, pairId, pairToken, childId, activities, tz, true, callback));
        }

        /// <summary>
        /// Initialize PIN pairing.
        /// </summary>
        public Coroutine RunInitPairing(string uuid, string deviceName, string platform,
            Action<Allow2ApiResponse> callback)
        {
            return Run(_api.InitPINPairing(uuid, deviceName, platform, callback));
        }

        /// <summary>
        /// Poll pairing status.
        /// </summary>
        public Coroutine RunCheckPairingStatus(string sessionId, Action<Allow2ApiResponse> callback)
        {
            return Run(_api.CheckPairingStatus(sessionId, callback));
        }

        /// <summary>
        /// Poll for updates.
        /// </summary>
        public Coroutine RunGetUpdates(int userId, int pairId, string pairToken,
            long timestampMillis, Action<Allow2ApiResponse> callback)
        {
            return Run(_api.GetUpdates(userId, pairId, pairToken, timestampMillis, callback));
        }

        /// <summary>
        /// Create a request (more time, etc.).
        /// </summary>
        public Coroutine RunCreateRequest(int userId, int pairId, string pairToken,
            int childId, int duration, int activityId, string message,
            Action<Allow2ApiResponse> callback)
        {
            return Run(_api.CreateRequest(userId, pairId, pairToken, childId,
                duration, activityId, message, callback));
        }

        /// <summary>
        /// Poll request status.
        /// </summary>
        public Coroutine RunGetRequestStatus(string requestId, string statusSecret,
            Action<Allow2ApiResponse> callback)
        {
            return Run(_api.GetRequestStatus(requestId, statusSecret, callback));
        }

        /// <summary>
        /// Submit feedback.
        /// </summary>
        public Coroutine RunSubmitFeedback(int userId, int pairId, string pairToken,
            int childId, string category, string message,
            Dictionary<string, string> deviceContext, Action<Allow2ApiResponse> callback)
        {
            return Run(_api.SubmitFeedback(userId, pairId, pairToken, childId,
                category, message, deviceContext, callback));
        }

        /// <summary>
        /// Load feedback.
        /// </summary>
        public Coroutine RunLoadFeedback(int userId, int pairId, string pairToken,
            Action<Allow2ApiResponse> callback)
        {
            return Run(_api.LoadFeedback(userId, pairId, pairToken, callback));
        }

        /// <summary>
        /// Reply to feedback.
        /// </summary>
        public Coroutine RunFeedbackReply(int userId, int pairId, string pairToken,
            string discussionId, string message, Action<Allow2ApiResponse> callback)
        {
            return Run(_api.FeedbackReply(userId, pairId, pairToken,
                discussionId, message, callback));
        }

        // ----------------------------------------------------------------
        // Utility coroutines
        // ----------------------------------------------------------------

        /// <summary>
        /// Wait for a number of seconds, then invoke the callback.
        /// </summary>
        public Coroutine Delay(float seconds, Action callback)
        {
            return Run(DelayCoroutine(seconds, callback));
        }

        private IEnumerator DelayCoroutine(float seconds, Action callback)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (callback != null) callback();
        }

        /// <summary>
        /// Repeat an action at a fixed interval.
        /// Stops when the action returns true.
        /// </summary>
        public Coroutine RepeatUntil(float intervalSeconds, Func<bool> action)
        {
            return Run(RepeatCoroutine(intervalSeconds, action));
        }

        private IEnumerator RepeatCoroutine(float intervalSeconds, Func<bool> action)
        {
            while (true)
            {
                bool done = action();
                if (done) yield break;
                yield return new WaitForSecondsRealtime(intervalSeconds);
            }
        }
    }
}
