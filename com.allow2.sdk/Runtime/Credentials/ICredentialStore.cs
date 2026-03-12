// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

namespace Allow2
{
    /// <summary>
    /// Interface for storing and retrieving Allow2 pairing credentials.
    /// Implement this to provide a custom credential storage backend.
    /// </summary>
    public interface ICredentialStore
    {
        /// <summary>
        /// Load stored credentials. Returns null if none exist.
        /// </summary>
        Allow2Credentials Load();

        /// <summary>
        /// Persist credentials to storage.
        /// </summary>
        void Store(Allow2Credentials credentials);

        /// <summary>
        /// Delete all stored credentials.
        /// </summary>
        void Clear();

        /// <summary>
        /// Load the last-used child ID. Returns 0 if none stored.
        /// </summary>
        int LoadLastUsedChildId();

        /// <summary>
        /// Store the last-used child ID for quicker re-selection.
        /// </summary>
        void StoreLastUsedChildId(int childId);
    }
}
