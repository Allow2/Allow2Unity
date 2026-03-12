// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Allow2
{
    /// <summary>
    /// MonoBehaviour singleton that bridges the pure C# Allow2Daemon
    /// with Unity's coroutine system and lifecycle.
    ///
    /// This is the main Unity entry point. Add it to a GameObject or
    /// call Allow2Manager.Instance to auto-create one.
    ///
    /// Responsibilities:
    /// - DontDestroyOnLoad persistence
    /// - Coroutine-based API calls (check loop, pairing polls, etc.)
    /// - Unity lifecycle hooks (OnApplicationPause, OnApplicationQuit)
    /// - Inspector-bindable UnityEvents
    /// </summary>
    public class Allow2Manager : MonoBehaviour
    {
        // ----------------------------------------------------------------
        // Singleton
        // ----------------------------------------------------------------

        private static Allow2Manager _instance;
        private static bool _applicationQuitting;

        public static Allow2Manager Instance
        {
            get
            {
                if (_applicationQuitting) return null;
                if (_instance == null)
                {
                    _instance = FindObjectOfType<Allow2Manager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("Allow2Manager");
                        _instance = go.AddComponent<Allow2Manager>();
                    }
                }
                return _instance;
            }
        }

        // ----------------------------------------------------------------
        // Inspector fields
        // ----------------------------------------------------------------

        [Header("Allow2 Configuration")]
        [Tooltip("Version ID from developer.allow2.com")]
        public int Vid;

        [Tooltip("Device token from developer.allow2.com")]
        public string DeviceToken;

        [Tooltip("Activities this game monitors")]
        public Allow2Activity[] Activities;

        [Tooltip("API base URL (leave empty for production)")]
        public string ApiUrl;

        [Header("Behaviour")]
        [Tooltip("Seconds between permission checks")]
        public int CheckIntervalSeconds = 60;

        [Tooltip("Auto-pause game on soft-lock (Time.timeScale = 0)")]
        public bool AutoPauseOnLock = true;

        [Header("Unity Events (Inspector Binding)")]
        public Allow2PairingRequiredEvent OnPairingRequiredEvent;
        public Allow2PairedEvent OnPairedEvent;
        public Allow2ChildSelectRequiredEvent OnChildSelectRequiredEvent;
        public Allow2ChildSelectedEvent OnChildSelectedEvent;
        public Allow2SoftLockEvent OnSoftLockEvent;
        public Allow2HardLockEvent OnHardLockEvent;
        public Allow2UnlockEvent OnUnlockEvent;
        public Allow2WarningEvent OnWarningEvent;
        public Allow2CheckResultEvent OnCheckResultEvent;
        public Allow2StateChangedEvent OnStateChangedEvent;
        public Allow2UnpairedEvent OnUnpairedEvent;
        public Allow2ParentModeEvent OnParentModeEvent;
        public Allow2SessionTimeoutEvent OnSessionTimeoutEvent;
        public Allow2ErrorEvent OnErrorEvent;

        // ----------------------------------------------------------------
        // Internal state
        // ----------------------------------------------------------------

        private Allow2Daemon _daemon;
        private Allow2Coroutines _coroutines;
        private ICredentialStore _credentialStore;
        private bool _configured;
        private bool _started;

        // Coroutine handles
        private Coroutine _checkLoopCoroutine;
        private Coroutine _pairingPollCoroutine;
        private Coroutine _updatePollCoroutine;
        private Coroutine _requestPollCoroutine;

        // ----------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------

        /// <summary>
        /// The underlying daemon for advanced usage.
        /// </summary>
        public Allow2Daemon Daemon { get { return _daemon; } }

        /// <summary>
        /// Current SDK state.
        /// </summary>
        public Allow2State State
        {
            get { return _daemon != null ? _daemon.State : Allow2State.Unpaired; }
        }

        /// <summary>
        /// Configure the SDK with the given config.
        /// Call this before StartDaemon() if not using Inspector fields.
        /// </summary>
        public void Configure(Allow2Config config)
        {
            Configure(config, null);
        }

        /// <summary>
        /// Configure with a custom credential store.
        /// </summary>
        public void Configure(Allow2Config config, ICredentialStore credentialStore)
        {
            if (credentialStore == null)
            {
                credentialStore = new PlayerPrefsStore();
            }
            _credentialStore = credentialStore;

            _daemon = new Allow2Daemon(config, credentialStore);
            _coroutines = new Allow2Coroutines(this, _daemon.Api);

            WireDaemonEvents();
            _configured = true;
        }

        /// <summary>
        /// Start the daemon. Loads credentials and begins enforcement if paired.
        /// </summary>
        public void StartDaemon()
        {
            if (!_configured)
            {
                // Auto-configure from Inspector fields
                AutoConfigureFromInspector();
            }

            if (_daemon == null)
            {
                Debug.LogError("[Allow2] Cannot start: not configured. Call Configure() first.");
                return;
            }

            _daemon.Start();
            _started = true;

            // Start the check loop if already enforcing
            if (_daemon.State == Allow2State.Enforcing)
            {
                StartCheckLoop();
            }

            // Start the pairing flow if unpaired
            if (_daemon.State == Allow2State.Pairing)
            {
                StartPairingFlow();
            }
        }

        /// <summary>
        /// Stop the daemon and all coroutines.
        /// </summary>
        public void StopDaemon()
        {
            _started = false;
            StopAllLoops();
            if (_daemon != null)
            {
                _daemon.Stop();
            }
        }

        /// <summary>
        /// Open the Allow2 app (triggers pairing if unpaired).
        /// </summary>
        public void OpenApp()
        {
            if (_daemon != null)
            {
                _daemon.OpenApp();
            }
        }

        /// <summary>
        /// Select a child by ID with PIN verification.
        /// </summary>
        public bool SelectChild(int childId, string pin)
        {
            if (_daemon == null) return false;
            bool success = _daemon.SelectChild(childId, pin);
            if (success)
            {
                StartCheckLoop();
            }
            return success;
        }

        /// <summary>
        /// Select a child without PIN (honour system).
        /// </summary>
        public bool SelectChild(int childId)
        {
            return SelectChild(childId, null);
        }

        /// <summary>
        /// Enter parent mode with PIN.
        /// </summary>
        public bool EnterParentMode(string pin)
        {
            if (_daemon == null) return false;
            bool success = _daemon.EnterParentMode(pin);
            if (success)
            {
                StopCheckLoop();
            }
            return success;
        }

        /// <summary>
        /// End the current child/parent session.
        /// </summary>
        public void EndSession()
        {
            StopCheckLoop();
            if (_daemon != null)
            {
                _daemon.EndSession();
            }
        }

        /// <summary>
        /// Request more time for an activity.
        /// </summary>
        public void RequestMoreTime(int activityId, int durationMinutes, string message)
        {
            if (_daemon == null || _daemon.Credentials == null) return;
            Allow2Credentials creds = _daemon.Credentials;

            _coroutines.RunCreateRequest(
                creds.UserId, creds.PairId, creds.PairToken,
                _daemon.ChildId, durationMinutes, activityId, message,
                delegate(Allow2ApiResponse response)
                {
                    _daemon.Request.HandleCreateResponse(response);
                    if (_daemon.Request.IsPolling)
                    {
                        StartRequestPolling();
                    }
                }
            );
        }

        /// <summary>
        /// Submit feedback.
        /// </summary>
        public void SubmitFeedback(string category, string message)
        {
            if (_daemon == null || _daemon.Credentials == null) return;

            string error = _daemon.Feedback.ValidateSubmission(category, message);
            if (error != null)
            {
                Debug.LogWarning("[Allow2] Feedback validation: " + error);
                return;
            }

            Allow2Credentials creds = _daemon.Credentials;
            Dictionary<string, string> deviceContext = new Dictionary<string, string>();
            deviceContext["deviceName"] = _daemon.Config.DeviceName;
            deviceContext["platform"] = Application.platform.ToString();
            deviceContext["sdkVersion"] = "2.0.0-alpha.1";
            deviceContext["productName"] = Application.productName;

            _coroutines.RunSubmitFeedback(
                creds.UserId, creds.PairId, creds.PairToken,
                _daemon.ChildId, category, message, deviceContext,
                delegate(Allow2ApiResponse response)
                {
                    _daemon.Feedback.HandleSubmitResponse(response, category);
                }
            );
        }

        // ----------------------------------------------------------------
        // Unity Lifecycle
        // ----------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (OnPairingRequiredEvent == null) OnPairingRequiredEvent = new Allow2PairingRequiredEvent();
            if (OnPairedEvent == null) OnPairedEvent = new Allow2PairedEvent();
            if (OnChildSelectRequiredEvent == null) OnChildSelectRequiredEvent = new Allow2ChildSelectRequiredEvent();
            if (OnChildSelectedEvent == null) OnChildSelectedEvent = new Allow2ChildSelectedEvent();
            if (OnSoftLockEvent == null) OnSoftLockEvent = new Allow2SoftLockEvent();
            if (OnHardLockEvent == null) OnHardLockEvent = new Allow2HardLockEvent();
            if (OnUnlockEvent == null) OnUnlockEvent = new Allow2UnlockEvent();
            if (OnWarningEvent == null) OnWarningEvent = new Allow2WarningEvent();
            if (OnCheckResultEvent == null) OnCheckResultEvent = new Allow2CheckResultEvent();
            if (OnStateChangedEvent == null) OnStateChangedEvent = new Allow2StateChangedEvent();
            if (OnUnpairedEvent == null) OnUnpairedEvent = new Allow2UnpairedEvent();
            if (OnParentModeEvent == null) OnParentModeEvent = new Allow2ParentModeEvent();
            if (OnSessionTimeoutEvent == null) OnSessionTimeoutEvent = new Allow2SessionTimeoutEvent();
            if (OnErrorEvent == null) OnErrorEvent = new Allow2ErrorEvent();
        }

        private void Update()
        {
            if (_daemon == null) return;

            // Tick the soft-lock timer
            if (_daemon.Checker != null && _daemon.Checker.IsRunning)
            {
                _daemon.Checker.UpdateSoftLockTimer(Time.unscaledDeltaTime);
            }

            // Tick the session timer
            if (_daemon.ChildShield != null)
            {
                _daemon.ChildShield.UpdateSessionTimer(Time.unscaledDeltaTime);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (_daemon == null) return;

            if (paused)
            {
                // App backgrounded -- stop polling to save battery
                StopCheckLoop();
                StopUpdateLoop();
            }
            else
            {
                // App resumed -- restart loops if enforcing
                if (_started && _daemon.State == Allow2State.Enforcing)
                {
                    StartCheckLoop();
                    StartUpdateLoop();
                }
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Record activity for session timer
            if (hasFocus && _daemon != null && _daemon.ChildShield != null)
            {
                _daemon.ChildShield.RecordActivity();
            }
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
            StopDaemon();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            StopAllLoops();
        }

        // ----------------------------------------------------------------
        // Coroutine loops
        // ----------------------------------------------------------------

        private void StartCheckLoop()
        {
            StopCheckLoop();
            if (_daemon == null || _daemon.Credentials == null) return;
            _daemon.Checker.Start();
            _checkLoopCoroutine = StartCoroutine(CheckLoopCoroutine());
        }

        private void StopCheckLoop()
        {
            if (_checkLoopCoroutine != null)
            {
                StopCoroutine(_checkLoopCoroutine);
                _checkLoopCoroutine = null;
            }
            if (_daemon != null && _daemon.Checker != null)
            {
                _daemon.Checker.Stop();
            }
        }

        private IEnumerator CheckLoopCoroutine()
        {
            while (_daemon != null && _daemon.Checker != null && _daemon.Checker.IsRunning)
            {
                Allow2Credentials creds = _daemon.Credentials;
                if (creds == null || !creds.IsValid)
                {
                    yield break;
                }

                bool done = false;
                _coroutines.RunCheck(
                    creds.UserId, creds.PairId, creds.PairToken,
                    _daemon.ChildId, _daemon.Checker.GetActivityMap(),
                    _daemon.Timezone,
                    delegate(Allow2ApiResponse response)
                    {
                        if (response.IsSuccess)
                        {
                            _daemon.Checker.ProcessResult(response);
                            // Cache for offline
                            if (response.Body != null)
                            {
                                _daemon.Offline.CacheResult(response.Body);
                            }
                        }
                        else
                        {
                            _daemon.Checker.HandleError(response);
                        }
                        done = true;
                    }
                );

                // Wait for the request to complete
                while (!done)
                {
                    yield return null;
                }

                // Wait for check interval
                yield return new WaitForSecondsRealtime(_daemon.Config.CheckIntervalSeconds);
            }
        }

        private void StartPairingFlow()
        {
            if (_daemon == null || _daemon.Pairing == null) return;

            string uuid = _daemon.Pairing.GetOrCreateUuid();
            string deviceName = _daemon.Config.DeviceName;
            if (string.IsNullOrEmpty(deviceName))
            {
                deviceName = SystemInfo.deviceName;
            }

            _coroutines.RunInitPairing(uuid, deviceName, Application.platform.ToString(),
                delegate(Allow2ApiResponse response)
                {
                    _daemon.Pairing.HandleInitResponse(response);

                    if (!string.IsNullOrEmpty(_daemon.Pairing.SessionId))
                    {
                        StartPairingPoll();
                    }
                    else
                    {
                        // Retry init after 5 seconds
                        StartCoroutine(RetryPairingInit());
                    }
                }
            );
        }

        private IEnumerator RetryPairingInit()
        {
            while (_daemon != null && _daemon.State == Allow2State.Pairing
                && string.IsNullOrEmpty(_daemon.Pairing.SessionId)
                && !_daemon.Pairing.IsPaired)
            {
                yield return new WaitForSecondsRealtime(5f);

                if (_daemon == null || _daemon.State != Allow2State.Pairing) yield break;

                string uuid = _daemon.Pairing.GetOrCreateUuid();
                string deviceName = _daemon.Config.DeviceName;
                if (string.IsNullOrEmpty(deviceName))
                {
                    deviceName = SystemInfo.deviceName;
                }

                bool done = false;
                _coroutines.RunInitPairing(uuid, deviceName, Application.platform.ToString(),
                    delegate(Allow2ApiResponse response)
                    {
                        _daemon.Pairing.HandleInitResponse(response);
                        done = true;
                    }
                );

                while (!done) yield return null;

                if (!string.IsNullOrEmpty(_daemon.Pairing.SessionId))
                {
                    StartPairingPoll();
                    yield break;
                }
            }
        }

        private void StartPairingPoll()
        {
            StopPairingPoll();
            _pairingPollCoroutine = StartCoroutine(PairingPollCoroutine());
        }

        private void StopPairingPoll()
        {
            if (_pairingPollCoroutine != null)
            {
                StopCoroutine(_pairingPollCoroutine);
                _pairingPollCoroutine = null;
            }
        }

        private IEnumerator PairingPollCoroutine()
        {
            while (_daemon != null && _daemon.State == Allow2State.Pairing
                && !_daemon.Pairing.IsPaired)
            {
                yield return new WaitForSecondsRealtime(5f);

                if (_daemon == null || _daemon.Pairing.IsPaired) yield break;

                string sessionId = _daemon.Pairing.SessionId;
                if (string.IsNullOrEmpty(sessionId)) continue;

                bool done = false;
                _coroutines.RunCheckPairingStatus(sessionId,
                    delegate(Allow2ApiResponse response)
                    {
                        _daemon.Pairing.HandlePollResponse(response);
                        done = true;
                    }
                );

                while (!done) yield return null;

                if (_daemon.Pairing.IsPaired)
                {
                    // Pairing succeeded -- start enforcement
                    if (_daemon.State == Allow2State.Enforcing)
                    {
                        StartCheckLoop();
                        StartUpdateLoop();
                    }
                    yield break;
                }
            }
        }

        private void StartUpdateLoop()
        {
            StopUpdateLoop();
            if (_daemon == null || _daemon.Credentials == null) return;
            _daemon.Updates.Start();
            _updatePollCoroutine = StartCoroutine(UpdatePollCoroutine());
        }

        private void StopUpdateLoop()
        {
            if (_updatePollCoroutine != null)
            {
                StopCoroutine(_updatePollCoroutine);
                _updatePollCoroutine = null;
            }
            if (_daemon != null && _daemon.Updates != null)
            {
                _daemon.Updates.Stop();
            }
        }

        private IEnumerator UpdatePollCoroutine()
        {
            while (_daemon != null && _daemon.Updates != null && _daemon.Updates.IsRunning)
            {
                Allow2Credentials creds = _daemon.Credentials;
                if (creds == null || !creds.IsValid)
                {
                    yield break;
                }

                bool done = false;
                _coroutines.RunGetUpdates(
                    creds.UserId, creds.PairId, creds.PairToken,
                    _daemon.Updates.LastTimestamp,
                    delegate(Allow2ApiResponse response)
                    {
                        _daemon.Updates.HandleResponse(response);
                        done = true;
                    }
                );

                while (!done) yield return null;

                yield return new WaitForSecondsRealtime(30f);
            }
        }

        private void StartRequestPolling()
        {
            StopRequestPolling();
            _requestPollCoroutine = StartCoroutine(RequestPollCoroutine());
        }

        private void StopRequestPolling()
        {
            if (_requestPollCoroutine != null)
            {
                StopCoroutine(_requestPollCoroutine);
                _requestPollCoroutine = null;
            }
        }

        private IEnumerator RequestPollCoroutine()
        {
            while (_daemon != null && _daemon.Request != null && _daemon.Request.IsPolling)
            {
                yield return new WaitForSecondsRealtime(5f);

                if (_daemon == null || !_daemon.Request.IsPolling) yield break;

                string requestId = _daemon.Request.RequestId;
                string statusSecret = _daemon.Request.StatusSecret;
                if (string.IsNullOrEmpty(requestId)) yield break;

                bool done = false;
                _coroutines.RunGetRequestStatus(requestId, statusSecret,
                    delegate(Allow2ApiResponse response)
                    {
                        _daemon.Request.HandlePollResponse(response);
                        done = true;
                    }
                );

                while (!done) yield return null;
            }
        }

        private void StopAllLoops()
        {
            StopCheckLoop();
            StopPairingPoll();
            StopUpdateLoop();
            StopRequestPolling();
        }

        // ----------------------------------------------------------------
        // Auto-configure from Inspector
        // ----------------------------------------------------------------

        private void AutoConfigureFromInspector()
        {
            if (Vid <= 0 || string.IsNullOrEmpty(DeviceToken))
            {
                Debug.LogWarning("[Allow2] Vid and DeviceToken must be set. Configure via Inspector or call Configure().");
                return;
            }

            Allow2Config config = new Allow2Config();
            config.Vid = Vid;
            config.DeviceToken = DeviceToken;
            config.DeviceName = SystemInfo.deviceName;
            config.Activities = Activities;
            config.ApiUrl = ApiUrl;
            config.CheckIntervalSeconds = CheckIntervalSeconds > 0 ? CheckIntervalSeconds : 60;

            Configure(config);
        }

        // ----------------------------------------------------------------
        // Wire daemon events to UnityEvents
        // ----------------------------------------------------------------

        private void WireDaemonEvents()
        {
            _daemon.OnPairingRequired += delegate(string pin, string qrUrl)
            {
                if (OnPairingRequiredEvent != null) OnPairingRequiredEvent.Invoke(pin, qrUrl);
                // Start the pairing poll loop
                if (_daemon.State == Allow2State.Pairing)
                {
                    StartPairingFlow();
                }
            };

            _daemon.OnPaired += delegate(Allow2Credentials creds)
            {
                if (OnPairedEvent != null) OnPairedEvent.Invoke();
            };

            _daemon.OnChildSelectRequired += delegate(Allow2Child[] children)
            {
                if (OnChildSelectRequiredEvent != null) OnChildSelectRequiredEvent.Invoke();
            };

            _daemon.OnChildSelected += delegate(int childId, string name)
            {
                if (OnChildSelectedEvent != null) OnChildSelectedEvent.Invoke(childId, name);
            };

            _daemon.OnSoftLock += delegate(string reason)
            {
                if (AutoPauseOnLock)
                {
                    Time.timeScale = 0f;
                }
                if (OnSoftLockEvent != null) OnSoftLockEvent.Invoke(reason);
            };

            _daemon.OnHardLock += delegate(string reason)
            {
                if (OnHardLockEvent != null) OnHardLockEvent.Invoke(reason);
            };

            _daemon.OnUnlock += delegate(string reason)
            {
                if (AutoPauseOnLock)
                {
                    Time.timeScale = 1f;
                }
                if (OnUnlockEvent != null) OnUnlockEvent.Invoke(reason);
            };

            _daemon.OnWarning += delegate(Allow2WarningEventArgs args)
            {
                if (OnWarningEvent != null)
                {
                    OnWarningEvent.Invoke(args.Level.ToString(), args.ActivityId, args.RemainingSeconds);
                }
            };

            _daemon.OnCheckResult += delegate(Allow2CheckResult result)
            {
                if (OnCheckResultEvent != null) OnCheckResultEvent.Invoke();
            };

            _daemon.OnStateChanged += delegate(Allow2State newState)
            {
                if (OnStateChangedEvent != null) OnStateChangedEvent.Invoke((int)newState);
            };

            _daemon.OnUnpaired += delegate()
            {
                StopAllLoops();
                if (OnUnpairedEvent != null) OnUnpairedEvent.Invoke();
            };

            _daemon.OnParentMode += delegate()
            {
                if (OnParentModeEvent != null) OnParentModeEvent.Invoke();
            };

            _daemon.OnSessionTimeout += delegate()
            {
                StopCheckLoop();
                if (OnSessionTimeoutEvent != null) OnSessionTimeoutEvent.Invoke();
            };

            _daemon.OnPairingError += delegate(string error)
            {
                if (OnErrorEvent != null) OnErrorEvent.Invoke(error);
            };
        }
    }
}
