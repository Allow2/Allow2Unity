// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;
using System.Collections.Generic;

namespace Allow2
{
    /// <summary>
    /// Main entry point for the Allow2 Device SDK.
    ///
    /// Manages the full device lifecycle:
    ///   1. Unpaired   - sits idle, waits for OpenApp() to start pairing
    ///   2. Pairing    - pairing wizard active
    ///   3. Paired     - paired but no child selected yet
    ///   4. Enforcing  - child selected, check loop running
    ///   5. Parent     - parent mode, no enforcement
    ///
    /// This is pure C# (no Unity dependencies). The MonoBehaviour bridge
    /// (Allow2Manager) drives coroutines for API calls and timers.
    /// </summary>
    public class Allow2Daemon
    {
        private readonly Allow2Config _config;
        private readonly ICredentialStore _credentialStore;
        private readonly Allow2Api _api;
        private readonly Allow2Checker _checker;
        private readonly Allow2ChildShield _childShield;
        private readonly Allow2Pairing _pairing;
        private readonly Allow2Offline _offline;
        private readonly Allow2Updates _updates;
        private readonly Allow2Request _request;
        private readonly Allow2Feedback _feedback;

        private Allow2Credentials _credentials;
        private int _childId;
        private bool _running;
        private Allow2State _state;
        private string _timezone;

        // ----------------------------------------------------------------
        // Events
        // ----------------------------------------------------------------

        /// <summary>Fired when the device needs pairing (show pairing UI).</summary>
        public event Action<string, string> OnPairingRequired; // (pin, qrUrl)

        /// <summary>Fired when pairing completes.</summary>
        public event Action<Allow2Credentials> OnPaired;

        /// <summary>Fired on pairing error.</summary>
        public event Action<string> OnPairingError;

        /// <summary>Fired when a child must be selected (show child selector).</summary>
        public event Action<Allow2Child[]> OnChildSelectRequired;

        /// <summary>Fired when a child is selected.</summary>
        public event Action<int, string> OnChildSelected;

        /// <summary>Fired when entering parent mode.</summary>
        public event Action OnParentMode;

        /// <summary>Fired on session timeout (need child re-identification).</summary>
        public event Action OnSessionTimeout;

        /// <summary>Fired with each check result.</summary>
        public event Action<Allow2CheckResult> OnCheckResult;

        /// <summary>Fired when an activity is blocked.</summary>
        public event Action<int, string, int> OnActivityBlocked;

        /// <summary>Fired when all activities are blocked (pause game).</summary>
        public event Action<string> OnSoftLock;

        /// <summary>Fired after soft-lock timeout (close game).</summary>
        public event Action<string> OnHardLock;

        /// <summary>Fired when at least one activity becomes allowed again.</summary>
        public event Action<string> OnUnlock;

        /// <summary>Warning events.</summary>
        public event Action<Allow2WarningEventArgs> OnWarning;

        /// <summary>Fired on HTTP 401 (device released by parent).</summary>
        public event Action OnUnpaired;

        /// <summary>Fired when state changes.</summary>
        public event Action<Allow2State> OnStateChanged;

        /// <summary>Fired when children list is updated.</summary>
        public event Action<Allow2Child[]> OnChildrenUpdated;

        // ----------------------------------------------------------------
        // Properties
        // ----------------------------------------------------------------

        public Allow2State State { get { return _state; } }
        public Allow2Api Api { get { return _api; } }
        public Allow2Checker Checker { get { return _checker; } }
        public Allow2ChildShield ChildShield { get { return _childShield; } }
        public Allow2Pairing Pairing { get { return _pairing; } }
        public Allow2Offline Offline { get { return _offline; } }
        public Allow2Updates Updates { get { return _updates; } }
        public Allow2Request Request { get { return _request; } }
        public Allow2Feedback Feedback { get { return _feedback; } }
        public Allow2Config Config { get { return _config; } }
        public Allow2Credentials Credentials { get { return _credentials; } }
        public int ChildId { get { return _childId; } }
        public bool IsRunning { get { return _running; } }
        public bool IsPaired
        {
            get { return _credentials != null && _credentials.IsValid; }
        }
        public bool IsParentMode { get { return _state == Allow2State.Parent; } }

        public string Timezone
        {
            get { return _timezone; }
            set { _timezone = value; }
        }

        // ----------------------------------------------------------------
        // Constructor
        // ----------------------------------------------------------------

        public Allow2Daemon(Allow2Config config, ICredentialStore credentialStore)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (credentialStore == null) throw new ArgumentNullException("credentialStore");
            config.Validate();

            _config = config;
            _credentialStore = credentialStore;
            _state = Allow2State.Unpaired;
            _running = false;

            _api = new Allow2Api(config.ApiUrl, config.Vid, config.DeviceToken);

            _checker = new Allow2Checker(
                config.Activities,
                config.HardLockTimeoutSeconds,
                config.GracePeriodSeconds,
                config.WarningThresholds
            );

            _childShield = new Allow2ChildShield();
            _pairing = new Allow2Pairing(credentialStore);
            _offline = new Allow2Offline(config.GracePeriodSeconds);
            _updates = new Allow2Updates();
            _request = new Allow2Request();
            _feedback = new Allow2Feedback();

            // Default timezone
            _timezone = TimeZoneInfo.Local.Id;

            WireInternalEvents();
        }

        // ----------------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------------

        /// <summary>
        /// Start the daemon. Checks for stored credentials.
        /// If unpaired, sits idle. If paired, proceeds to child identification.
        /// </summary>
        public void Start()
        {
            if (_running) return;
            _running = true;

            // Load stored credentials
            try
            {
                _credentials = _credentialStore.Load();
            }
            catch (Exception)
            {
                _credentials = null;
            }

            // If not paired, sit idle
            if (_credentials == null || !_credentials.IsValid)
            {
                SetState(Allow2State.Unpaired);
                return;
            }

            // Already paired -- proceed to child identification
            SetState(Allow2State.Paired);
            BeginEnforcement();
        }

        /// <summary>
        /// Stop the daemon.
        /// </summary>
        public void Stop()
        {
            _running = false;
            _checker.Stop();
            _updates.Stop();
            _request.StopPolling();
            _childId = 0;

            if (_credentials != null && _credentials.IsValid)
            {
                SetState(Allow2State.Paired);
            }
            else
            {
                SetState(Allow2State.Unpaired);
            }
        }

        /// <summary>
        /// Called when the user opens the Allow2 app/UI.
        /// If unpaired, starts pairing. If paired, requests status.
        /// </summary>
        public void OpenApp()
        {
            if (_state == Allow2State.Unpaired || _credentials == null || !_credentials.IsValid)
            {
                StartPairing();
            }
        }

        /// <summary>
        /// Called when the user closes the Allow2 app/UI.
        /// </summary>
        public void CloseApp()
        {
            if (_credentials == null || !_credentials.IsValid)
            {
                SetState(Allow2State.Unpaired);
            }
        }

        // ----------------------------------------------------------------
        // Child Management
        // ----------------------------------------------------------------

        /// <summary>
        /// Select a child (from the child selector UI).
        /// </summary>
        public bool SelectChild(int childId, string pin)
        {
            bool success = _childShield.SelectChild(childId, pin);
            if (success)
            {
                _childId = childId;
                _credentialStore.StoreLastUsedChildId(childId);
                SetState(Allow2State.Enforcing);
                _checker.Reset(childId);
                _checker.Start();
            }
            return success;
        }

        /// <summary>
        /// Enter parent mode (no enforcement).
        /// </summary>
        public bool EnterParentMode(string pin)
        {
            bool success = _childShield.SelectParent(pin);
            if (success)
            {
                _checker.Stop();
                _childId = 0;
                SetState(Allow2State.Parent);
                if (OnParentMode != null) OnParentMode();
            }
            return success;
        }

        /// <summary>
        /// End the current session. Stops checker and requests re-identification.
        /// </summary>
        public void EndSession()
        {
            _checker.Stop();
            _childId = 0;
            _childShield.ClearSelection();
            SetState(Allow2State.Paired);

            if (_running && _credentials != null && _credentials.IsValid)
            {
                BeginEnforcement();
            }
        }

        /// <summary>
        /// Called when pairing completes externally (e.g., from a callback).
        /// </summary>
        public void OnPairingComplete(Allow2Credentials credentials)
        {
            HandlePaired(credentials);
        }

        // ----------------------------------------------------------------
        // Internal
        // ----------------------------------------------------------------

        private void SetState(Allow2State newState)
        {
            if (_state == newState) return;
            _state = newState;
            if (OnStateChanged != null) OnStateChanged(newState);
        }

        private void StartPairing()
        {
            if (_state == Allow2State.Pairing) return;
            SetState(Allow2State.Pairing);
            _pairing.Reset();
            _pairing.GetOrCreateUuid();
            // The bridge will call the API coroutine and feed the response back
        }

        private void HandlePaired(Allow2Credentials credentials)
        {
            _credentials = credentials;
            SetState(Allow2State.Paired);

            if (OnPaired != null) OnPaired(credentials);

            if (_running)
            {
                BeginEnforcement();
            }
        }

        private void BeginEnforcement()
        {
            if (_credentials == null || _credentials.Children == null)
            {
                return;
            }

            _childShield.SetChildren(_credentials.Children);

            // Try auto-resolve: check lastUsedChildId
            int lastChildId = _credentialStore.LoadLastUsedChildId();
            if (lastChildId > 0)
            {
                // Check if that child still exists
                bool exists = false;
                for (int i = 0; i < _credentials.Children.Length; i++)
                {
                    if (_credentials.Children[i].Id == lastChildId)
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists)
                {
                    // Auto-select without PIN (the child was already verified previously)
                    _childId = lastChildId;
                    _credentialStore.StoreLastUsedChildId(lastChildId);
                    SetState(Allow2State.Enforcing);
                    _checker.Reset(lastChildId);
                    _checker.Start();

                    string childName = null;
                    for (int i = 0; i < _credentials.Children.Length; i++)
                    {
                        if (_credentials.Children[i].Id == lastChildId)
                        {
                            childName = _credentials.Children[i].Name;
                            break;
                        }
                    }
                    if (OnChildSelected != null)
                    {
                        OnChildSelected(lastChildId, childName);
                    }
                    return;
                }
            }

            // No auto-resolve -- need interactive selection
            if (OnChildSelectRequired != null)
            {
                OnChildSelectRequired(_childShield.GetDisplayChildren());
            }
        }

        private void HandleUnpaired()
        {
            _checker.Stop();
            _updates.Stop();
            _childId = 0;
            _credentials = null;

            try
            {
                _credentialStore.Clear();
            }
            catch (Exception)
            {
                // Best effort
            }

            SetState(Allow2State.Unpaired);
            if (OnUnpaired != null) OnUnpaired();
        }

        private void WireInternalEvents()
        {
            // Pairing events
            _pairing.OnPairingReady += delegate(string pin, string qrUrl)
            {
                if (OnPairingRequired != null) OnPairingRequired(pin, qrUrl);
            };
            _pairing.OnPaired += delegate(Allow2Credentials creds)
            {
                HandlePaired(creds);
            };
            _pairing.OnError += delegate(string error)
            {
                if (OnPairingError != null) OnPairingError(error);
            };

            // Checker events
            _checker.OnCheckResult += delegate(Allow2CheckResult result)
            {
                if (OnCheckResult != null) OnCheckResult(result);
            };
            _checker.OnActivityBlocked += delegate(int actId, string actName, int remaining)
            {
                if (OnActivityBlocked != null) OnActivityBlocked(actId, actName, remaining);
            };
            _checker.OnSoftLock += delegate(string reason)
            {
                if (OnSoftLock != null) OnSoftLock(reason);
            };
            _checker.OnHardLock += delegate(string reason)
            {
                if (OnHardLock != null) OnHardLock(reason);
            };
            _checker.OnUnlock += delegate(string reason)
            {
                if (OnUnlock != null) OnUnlock(reason);
            };
            _checker.OnWarning += delegate(Allow2WarningEventArgs args)
            {
                if (OnWarning != null) OnWarning(args);
            };
            _checker.OnUnpaired += delegate()
            {
                HandleUnpaired();
            };

            // ChildShield events
            _childShield.OnChildSelected += delegate(int childId, string name)
            {
                if (OnChildSelected != null) OnChildSelected(childId, name);
            };
            _childShield.OnChildSelectRequired += delegate(Allow2Child[] children)
            {
                if (OnChildSelectRequired != null) OnChildSelectRequired(children);
            };
            _childShield.OnSessionTimeout += delegate()
            {
                _checker.Stop();
                _childId = 0;
                SetState(Allow2State.Paired);
                if (OnSessionTimeout != null) OnSessionTimeout();
                if (_running && _credentials != null && _credentials.IsValid)
                {
                    BeginEnforcement();
                }
            };

            // Updates events
            _updates.OnChildrenUpdated += delegate(Allow2Child[] children)
            {
                if (_credentials != null)
                {
                    _credentials.Children = children;
                    _childShield.SetChildren(children);
                    try { _credentialStore.Store(_credentials); }
                    catch (Exception) { /* best effort */ }
                }
                if (OnChildrenUpdated != null) OnChildrenUpdated(children);
            };
            _updates.OnUnpaired += delegate()
            {
                HandleUnpaired();
            };
        }
    }
}
