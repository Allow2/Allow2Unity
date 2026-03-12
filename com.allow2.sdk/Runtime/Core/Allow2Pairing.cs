// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;
using System.Collections.Generic;

namespace Allow2
{
    /// <summary>
    /// Manages one-time device pairing with the Allow2 platform.
    /// Parents NEVER enter credentials on the child's device.
    ///
    /// Flow:
    /// 1. Call Start() to register a pairing session via the API
    /// 2. API returns a server-assigned PIN and session ID
    /// 3. Device displays the PIN (and QR code deep link) to the user
    /// 4. Parent opens Allow2 app on their phone, enters the PIN
    /// 5. Poll CheckPairingStatus until parent confirms
    /// 6. On confirmation, receives credentials
    /// 7. Stores credentials via ICredentialStore
    ///
    /// The coroutine-based polling is driven by the Allow2Manager bridge.
    /// This class holds the state and provides callbacks.
    /// </summary>
    public class Allow2Pairing
    {
        private readonly ICredentialStore _credentialStore;

        private string _pin;
        private string _sessionId;
        private string _uuid;
        private string _qrUrl;
        private bool _paired;
        private bool _started;
        private int _pollCount;
        private int _consecutiveErrors;
        private bool _connected;

        private const int MAX_POLLS = 360; // 30 minutes at 5s intervals

        /// <summary>Fired when a PIN and session are ready for display.</summary>
        public event Action<string, string> OnPairingReady; // (pin, qrUrl)

        /// <summary>Fired when pairing completes successfully.</summary>
        public event Action<Allow2Credentials> OnPaired;

        /// <summary>Fired on pairing error.</summary>
        public event Action<string> OnError;

        /// <summary>Fired when connection status changes.</summary>
        public event Action<bool, string, string> OnConnectionStatus; // (connected, pin, qrUrl)

        /// <summary>Fired when pairing times out.</summary>
        public event Action OnTimeout;

        public string Pin { get { return _pin; } }
        public string QrUrl { get { return _qrUrl; } }
        public string SessionId { get { return _sessionId; } }
        public bool IsPaired { get { return _paired; } }
        public bool IsStarted { get { return _started; } }
        public bool IsConnected { get { return _connected; } }

        public Allow2Pairing(ICredentialStore credentialStore)
        {
            _credentialStore = credentialStore;
            _paired = false;
            _started = false;
            _pollCount = 0;
            _consecutiveErrors = 0;
            _connected = false;
        }

        /// <summary>
        /// Get or create a device UUID.
        /// </summary>
        public string GetOrCreateUuid()
        {
            if (!string.IsNullOrEmpty(_uuid)) return _uuid;

            // Try loading from credential store
            Allow2Credentials creds = _credentialStore.Load();
            if (creds != null && !string.IsNullOrEmpty(creds.Uuid))
            {
                _uuid = creds.Uuid;
                return _uuid;
            }

            // Generate new UUID
            _uuid = Guid.NewGuid().ToString();
            Allow2Credentials uuidCreds = new Allow2Credentials();
            uuidCreds.Uuid = _uuid;
            try
            {
                _credentialStore.Store(uuidCreds);
            }
            catch (Exception)
            {
                // Best effort
            }
            return _uuid;
        }

        /// <summary>
        /// Handle the API response from initPINPairing.
        /// Called by the bridge after the coroutine completes.
        /// </summary>
        public void HandleInitResponse(Allow2ApiResponse response)
        {
            _started = true;

            if (response == null || !response.IsSuccess)
            {
                // API unreachable -- no valid PIN yet
                _pin = "------";
                _sessionId = null;
                _connected = false;
                _qrUrl = "https://app.allow2.com/pair?pin=" + _pin;

                if (OnPairingReady != null)
                {
                    OnPairingReady(_pin, _qrUrl);
                }
                return;
            }

            _pin = response.GetString("pin");
            if (string.IsNullOrEmpty(_pin))
            {
                _pin = "------";
            }

            _sessionId = response.GetString("sessionId");
            if (string.IsNullOrEmpty(_sessionId))
            {
                _sessionId = response.GetString("pairingSessionId");
            }

            _connected = !string.IsNullOrEmpty(_sessionId);
            _consecutiveErrors = 0;
            _qrUrl = "https://app.allow2.com/pair?pin=" + _pin;

            if (OnPairingReady != null)
            {
                OnPairingReady(_pin, _qrUrl);
            }
        }

        /// <summary>
        /// Handle the API response from checkPairingStatus.
        /// Returns true if pairing is complete.
        /// </summary>
        public bool HandlePollResponse(Allow2ApiResponse response)
        {
            if (_paired) return true;

            _pollCount++;
            if (_pollCount > MAX_POLLS)
            {
                if (OnTimeout != null) OnTimeout();
                return false;
            }

            if (response == null || !response.IsSuccess)
            {
                _consecutiveErrors++;
                if (_consecutiveErrors >= 2 && _connected)
                {
                    _connected = false;
                    if (OnConnectionStatus != null)
                    {
                        OnConnectionStatus(false, _pin, _qrUrl);
                    }
                }
                return false;
            }

            // Successful poll -- mark as connected
            if (!_connected)
            {
                _connected = true;
                _consecutiveErrors = 0;
                if (OnConnectionStatus != null)
                {
                    OnConnectionStatus(true, _pin, _qrUrl);
                }
            }

            // Check if parent confirmed
            bool paired = response.GetBool("paired");
            int userId = response.GetInt("userId");
            int pairId = response.GetInt("pairId");
            string pairToken = response.GetString("pairToken");

            if (paired && userId > 0 && pairId > 0 && !string.IsNullOrEmpty(pairToken))
            {
                CompletePairing(response);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Complete pairing with the given API response data.
        /// </summary>
        public void CompletePairing(Allow2ApiResponse response)
        {
            if (response == null || response.Body == null)
            {
                if (OnError != null) OnError("Invalid pairing response");
                return;
            }

            Allow2Credentials credentials = new Allow2Credentials();
            credentials.Uuid = _uuid;
            credentials.UserId = response.GetInt("userId");
            credentials.PairId = response.GetInt("pairId");
            credentials.PairToken = response.GetString("pairToken");

            // Parse children array
            object childrenObj;
            if (response.Body.TryGetValue("children", out childrenObj))
            {
                List<object> childList = childrenObj as List<object>;
                if (childList != null)
                {
                    credentials.Children = new Allow2Child[childList.Count];
                    for (int i = 0; i < childList.Count; i++)
                    {
                        Dictionary<string, object> cDict = childList[i] as Dictionary<string, object>;
                        if (cDict != null)
                        {
                            Allow2Child child = new Allow2Child();
                            object val;
                            if (cDict.TryGetValue("id", out val))
                            {
                                if (val is long) child.Id = (int)(long)val;
                                else if (val is double) child.Id = (int)(double)val;
                            }
                            if (cDict.TryGetValue("name", out val) && val != null)
                            {
                                child.Name = val.ToString();
                            }
                            if (cDict.TryGetValue("pinHash", out val) && val != null)
                            {
                                child.PinHash = val.ToString();
                            }
                            if (cDict.TryGetValue("pinSalt", out val) && val != null)
                            {
                                child.PinSalt = val.ToString();
                            }
                            credentials.Children[i] = child;
                        }
                    }
                }
            }

            if (credentials.Children == null)
            {
                credentials.Children = new Allow2Child[0];
            }

            // Persist credentials
            try
            {
                _credentialStore.Store(credentials);
            }
            catch (Exception ex)
            {
                if (OnError != null) OnError("Failed to store credentials: " + ex.Message);
                return;
            }

            _paired = true;

            if (OnPaired != null)
            {
                OnPaired(credentials);
            }
        }

        /// <summary>
        /// Reset pairing state for a new attempt.
        /// </summary>
        public void Reset()
        {
            _pin = null;
            _sessionId = null;
            _qrUrl = null;
            _paired = false;
            _started = false;
            _pollCount = 0;
            _consecutiveErrors = 0;
            _connected = false;
        }

        /// <summary>
        /// Handle an external pairing completion (e.g., from a callback).
        /// </summary>
        public void CompletePairingExternal(Allow2Credentials credentials)
        {
            if (credentials == null || !credentials.IsValid)
            {
                if (OnError != null) OnError("Invalid credentials");
                return;
            }

            try
            {
                _credentialStore.Store(credentials);
            }
            catch (Exception ex)
            {
                if (OnError != null) OnError("Failed to store credentials: " + ex.Message);
                return;
            }

            _paired = true;
            if (OnPaired != null)
            {
                OnPaired(credentials);
            }
        }
    }
}
