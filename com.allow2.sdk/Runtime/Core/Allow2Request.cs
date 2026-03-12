// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;

namespace Allow2
{
    /// <summary>
    /// Request More Time manager.
    /// Lets a child request additional time, a day type change, or a ban lift.
    /// Creates the request via the Allow2 API, then polls for parent response.
    ///
    /// Polling is driven by the bridge (coroutine). This class holds state
    /// and interprets responses.
    /// </summary>
    public class Allow2Request
    {
        private string _requestId;
        private string _statusSecret;
        private bool _polling;
        private int _pollCount;
        private readonly int _maxPollCount;

        /// <summary>Fired when the request is created.</summary>
        public event Action<string> OnRequestCreated; // requestId

        /// <summary>Fired when the parent approves.</summary>
        public event Action<string, int> OnRequestApproved; // requestId, extension

        /// <summary>Fired when the parent denies.</summary>
        public event Action<string> OnRequestDenied; // requestId

        /// <summary>Fired when polling times out.</summary>
        public event Action OnRequestTimeout;

        /// <summary>Fired on error.</summary>
        public event Action<string> OnRequestError;

        public string RequestId { get { return _requestId; } }
        public string StatusSecret { get { return _statusSecret; } }
        public bool IsPolling { get { return _polling; } }

        /// <param name="timeoutSeconds">Max wait time in seconds (default 300).</param>
        /// <param name="pollIntervalSeconds">Seconds between polls (default 5).</param>
        public Allow2Request(int timeoutSeconds, int pollIntervalSeconds)
        {
            int timeout = timeoutSeconds > 0 ? timeoutSeconds : 300;
            int interval = pollIntervalSeconds > 0 ? pollIntervalSeconds : 5;
            _maxPollCount = timeout / interval;
            _polling = false;
            _pollCount = 0;
        }

        public Allow2Request() : this(300, 5) { }

        /// <summary>
        /// Handle the API response from createRequest.
        /// </summary>
        public void HandleCreateResponse(Allow2ApiResponse response)
        {
            if (response == null || !response.IsSuccess)
            {
                string error = response != null ? response.ErrorMessage : "Request failed";
                if (OnRequestError != null) OnRequestError(error);
                return;
            }

            _requestId = response.GetString("requestId");
            _statusSecret = response.GetString("statusSecret");
            _polling = true;
            _pollCount = 0;

            if (OnRequestCreated != null)
            {
                OnRequestCreated(_requestId);
            }
        }

        /// <summary>
        /// Handle the API response from getRequestStatus.
        /// Returns true if the request has been resolved (approved/denied/timeout).
        /// </summary>
        public bool HandlePollResponse(Allow2ApiResponse response)
        {
            if (!_polling) return true;

            _pollCount++;
            if (_pollCount >= _maxPollCount)
            {
                StopPolling();
                if (OnRequestTimeout != null) OnRequestTimeout();
                return true;
            }

            if (response == null || !response.IsSuccess)
            {
                // Transient error -- keep polling
                return false;
            }

            string status = response.GetString("status");

            if (status == "approved")
            {
                StopPolling();
                int extension = response.GetInt("extension");
                if (OnRequestApproved != null)
                {
                    OnRequestApproved(_requestId, extension);
                }
                return true;
            }

            if (status == "denied")
            {
                StopPolling();
                if (OnRequestDenied != null)
                {
                    OnRequestDenied(_requestId);
                }
                return true;
            }

            // Still pending
            return false;
        }

        /// <summary>
        /// Stop polling.
        /// </summary>
        public void StopPolling()
        {
            _polling = false;
        }

        /// <summary>
        /// Reset for a new request.
        /// </summary>
        public void Reset()
        {
            _requestId = null;
            _statusSecret = null;
            _polling = false;
            _pollCount = 0;
        }
    }
}
