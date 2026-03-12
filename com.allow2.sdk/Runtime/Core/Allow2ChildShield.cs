// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Allow2
{
    /// <summary>
    /// Child identification, PIN verification, and session management.
    /// Port of the Brave browser's ChildShield/ChildManager pattern.
    ///
    /// Pure C# -- no Unity dependencies.
    /// </summary>
    public class Allow2ChildShield
    {
        public enum VerificationLevel
        {
            /// <summary>Trust the child's selection without PIN.</summary>
            Honour,
            /// <summary>Require PIN verification.</summary>
            Pin,
            /// <summary>Only parent can select (no child self-select).</summary>
            ParentOnly
        }

        private const int MAX_PIN_ATTEMPTS = 5;
        private const long LOCKOUT_DURATION_MS = 300000; // 5 minutes
        private const long DEFAULT_SESSION_TIMEOUT_MS = 300000; // 5 minutes

        private Allow2Child[] _children;
        private readonly VerificationLevel _verificationLevel;
        private readonly long _sessionTimeoutMs;

        private Allow2Child _currentChild;
        private bool _parentMode;
        private long _lastActivityTime;

        // Rate-limiting: keyed by childId (or -1 for parent)
        private readonly Dictionary<int, AttemptRecord> _attempts;

        // Session timer tracking
        private float _sessionIdleElapsed;
        private bool _sessionTimerActive;

        // Events
        public event Action<Allow2Child[]> OnChildSelectRequired;
        public event Action<int, string> OnChildSelected;
        public event Action OnParentModeEntered;
        public event Action OnSessionTimeout;
        public event Action<int, int> OnChildPinFailed; // (failedCount, maxAttempts)
        public event Action<int> OnChildLockedOut; // (lockoutSeconds)

        private class AttemptRecord
        {
            public int Failed;
            public long LockoutUntil;
        }

        public Allow2ChildShield(VerificationLevel verificationLevel, long sessionTimeoutMs)
        {
            _verificationLevel = verificationLevel;
            _sessionTimeoutMs = sessionTimeoutMs > 0 ? sessionTimeoutMs : DEFAULT_SESSION_TIMEOUT_MS;
            _children = new Allow2Child[0];
            _attempts = new Dictionary<int, AttemptRecord>();
            _sessionIdleElapsed = 0f;
            _sessionTimerActive = false;
        }

        public Allow2ChildShield() : this(VerificationLevel.Pin, DEFAULT_SESSION_TIMEOUT_MS) { }

        // ---------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------

        public Allow2Child CurrentChild { get { return _currentChild; } }
        public bool IsParentMode { get { return _parentMode; } }

        /// <summary>
        /// Set the children list (from pairing or getUpdates).
        /// </summary>
        public void SetChildren(Allow2Child[] children)
        {
            _children = children != null ? children : new Allow2Child[0];

            // If current child was removed, force re-selection
            if (_currentChild != null)
            {
                bool stillExists = false;
                for (int i = 0; i < _children.Length; i++)
                {
                    if (_children[i].Id == _currentChild.Id)
                    {
                        stillExists = true;
                        _currentChild = _children[i];
                        break;
                    }
                }
                if (!stillExists)
                {
                    ClearSelection();
                }
            }
        }

        /// <summary>
        /// Select a child by ID, optionally verifying their PIN.
        /// Returns true if the child was successfully selected.
        /// </summary>
        public bool SelectChild(int childId, string pin)
        {
            if (_verificationLevel == VerificationLevel.ParentOnly)
            {
                return false;
            }

            Allow2Child child = FindChild(childId);
            if (child == null)
            {
                return false;
            }

            // Check lockout
            if (IsLockedOut(childId))
            {
                long remaining = LockoutRemaining(childId);
                if (OnChildLockedOut != null)
                {
                    OnChildLockedOut((int)(remaining / 1000));
                }
                return false;
            }

            // PIN verification
            if (_verificationLevel == VerificationLevel.Pin)
            {
                if (string.IsNullOrEmpty(pin))
                {
                    return false;
                }
                if (!string.IsNullOrEmpty(child.PinHash) && !string.IsNullOrEmpty(child.PinSalt))
                {
                    string computed = HashPin(pin, child.PinSalt);
                    if (!SafeCompare(computed, child.PinHash))
                    {
                        RecordFailedAttempt(childId);
                        return false;
                    }
                }
                // If child has no PIN set, treat as honour system
            }

            // Success
            ClearAttempts(childId);
            ActivateChild(child);
            return true;
        }

        /// <summary>
        /// Authenticate as parent. Returns true if parent mode was entered.
        /// </summary>
        public bool SelectParent(string pin)
        {
            if (string.IsNullOrEmpty(pin))
            {
                return false;
            }

            Allow2Child parentEntry = FindParentEntry();
            if (parentEntry == null)
            {
                return false;
            }

            if (IsLockedOut(-1))
            {
                long remaining = LockoutRemaining(-1);
                if (OnChildLockedOut != null)
                {
                    OnChildLockedOut((int)(remaining / 1000));
                }
                return false;
            }

            if (string.IsNullOrEmpty(parentEntry.PinHash) || string.IsNullOrEmpty(parentEntry.PinSalt))
            {
                return false;
            }

            string computed = HashPin(pin, parentEntry.PinSalt);
            if (!SafeCompare(computed, parentEntry.PinHash))
            {
                RecordFailedAttempt(-1);
                return false;
            }

            ClearAttempts(-1);
            EnterParentMode();
            return true;
        }

        /// <summary>
        /// End the current session and request child re-selection.
        /// </summary>
        public void ClearSelection()
        {
            _currentChild = null;
            _parentMode = false;
            _sessionTimerActive = false;
            _sessionIdleElapsed = 0f;
            _lastActivityTime = 0;
            if (OnChildSelectRequired != null)
            {
                OnChildSelectRequired(GetDisplayChildren());
            }
        }

        /// <summary>
        /// Record user activity to keep the session alive.
        /// </summary>
        public void RecordActivity()
        {
            _lastActivityTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _sessionIdleElapsed = 0f;
        }

        /// <summary>
        /// Called each frame by the bridge to track session idle time.
        /// </summary>
        public void UpdateSessionTimer(float deltaTime)
        {
            if (!_sessionTimerActive) return;
            if (_sessionTimeoutMs <= 0) return;

            _sessionIdleElapsed += deltaTime * 1000f;
            if (_sessionIdleElapsed >= _sessionTimeoutMs)
            {
                _sessionTimerActive = false;
                _currentChild = null;
                _parentMode = false;
                _lastActivityTime = 0;
                _sessionIdleElapsed = 0f;

                if (OnSessionTimeout != null)
                {
                    OnSessionTimeout();
                }
                if (OnChildSelectRequired != null)
                {
                    OnChildSelectRequired(GetDisplayChildren());
                }
            }
        }

        /// <summary>
        /// Returns a safe copy of the children list (no PIN data).
        /// </summary>
        public Allow2Child[] GetDisplayChildren()
        {
            Allow2Child[] display = new Allow2Child[_children.Length];
            for (int i = 0; i < _children.Length; i++)
            {
                display[i] = _children[i].ToDisplayChild();
            }
            return display;
        }

        public void Destroy()
        {
            _sessionTimerActive = false;
            OnChildSelectRequired = null;
            OnChildSelected = null;
            OnParentModeEntered = null;
            OnSessionTimeout = null;
            OnChildPinFailed = null;
            OnChildLockedOut = null;
        }

        // ---------------------------------------------------------------
        // Private helpers
        // ---------------------------------------------------------------

        private Allow2Child FindChild(int childId)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                if (_children[i].Id == childId)
                {
                    return _children[i];
                }
            }
            return null;
        }

        private Allow2Child FindParentEntry()
        {
            for (int i = 0; i < _children.Length; i++)
            {
                if (_children[i].Id == 0 || _children[i].Name == "__parent__")
                {
                    return _children[i];
                }
            }
            return null;
        }

        private void ActivateChild(Allow2Child child)
        {
            _parentMode = false;
            _currentChild = child;
            _lastActivityTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _sessionIdleElapsed = 0f;
            _sessionTimerActive = true;

            if (OnChildSelected != null)
            {
                OnChildSelected(child.Id, child.Name);
            }
        }

        private void EnterParentMode()
        {
            _currentChild = null;
            _parentMode = true;
            _lastActivityTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _sessionIdleElapsed = 0f;
            _sessionTimerActive = true;

            if (OnParentModeEntered != null)
            {
                OnParentModeEntered();
            }
        }

        // ---------------------------------------------------------------
        // PIN hashing (SHA-256 + salt)
        // ---------------------------------------------------------------

        /// <summary>
        /// Hash a PIN with the given salt using SHA-256.
        /// </summary>
        public static string HashPin(string pin, string salt)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(pin + salt);
                byte[] hashBytes = sha256.ComputeHash(inputBytes);
                StringBuilder sb = new StringBuilder(hashBytes.Length * 2);
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Constant-time comparison of two hex hash strings.
        /// </summary>
        private static bool SafeCompare(string a, string b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        // ---------------------------------------------------------------
        // Rate limiting
        // ---------------------------------------------------------------

        private AttemptRecord GetAttemptRecord(int key)
        {
            AttemptRecord record;
            if (!_attempts.TryGetValue(key, out record))
            {
                record = new AttemptRecord();
                record.Failed = 0;
                record.LockoutUntil = 0;
                _attempts[key] = record;
            }
            return record;
        }

        private bool IsLockedOut(int key)
        {
            AttemptRecord record = GetAttemptRecord(key);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (record.LockoutUntil > 0 && now < record.LockoutUntil)
            {
                return true;
            }
            // Lockout expired
            if (record.LockoutUntil > 0 && now >= record.LockoutUntil)
            {
                record.Failed = 0;
                record.LockoutUntil = 0;
            }
            return false;
        }

        private long LockoutRemaining(int key)
        {
            AttemptRecord record = GetAttemptRecord(key);
            long remaining = record.LockoutUntil - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return remaining > 0 ? remaining : 0;
        }

        private void RecordFailedAttempt(int key)
        {
            AttemptRecord record = GetAttemptRecord(key);
            record.Failed += 1;

            if (record.Failed >= MAX_PIN_ATTEMPTS)
            {
                record.LockoutUntil = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + LOCKOUT_DURATION_MS;
                if (OnChildLockedOut != null)
                {
                    OnChildLockedOut((int)(LOCKOUT_DURATION_MS / 1000));
                }
            }
            else
            {
                if (OnChildPinFailed != null)
                {
                    OnChildPinFailed(record.Failed, MAX_PIN_ATTEMPTS);
                }
            }
        }

        private void ClearAttempts(int key)
        {
            _attempts.Remove(key);
        }
    }
}
