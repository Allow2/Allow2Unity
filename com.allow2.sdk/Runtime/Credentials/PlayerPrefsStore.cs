// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;
using System.Text;
using UnityEngine;

namespace Allow2
{
    /// <summary>
    /// Default credential store using Unity PlayerPrefs.
    /// Data is obfuscated with a device-specific XOR key.
    /// This is NOT secure encryption -- it prevents casual inspection only.
    /// For production security, use platform-specific keychain/keystore backends.
    /// </summary>
    public class PlayerPrefsStore : ICredentialStore
    {
        private const string KeyPrefix = "allow2_";
        private const string CredKey = KeyPrefix + "cred";
        private const string LastUsedKey = KeyPrefix + "last_child";

        private readonly string _deviceKey;

        public PlayerPrefsStore()
        {
            // Use a device-specific key for obfuscation.
            // SystemInfo.deviceUniqueIdentifier is empty on some platforms;
            // fall back to a stored GUID in that case.
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(deviceId) || deviceId == SystemInfo.unsupportedIdentifier)
            {
                deviceId = PlayerPrefs.GetString(KeyPrefix + "dk", "");
                if (string.IsNullOrEmpty(deviceId))
                {
                    deviceId = Guid.NewGuid().ToString();
                    PlayerPrefs.SetString(KeyPrefix + "dk", deviceId);
                    PlayerPrefs.Save();
                }
            }
            _deviceKey = deviceId;
        }

        public Allow2Credentials Load()
        {
            string stored = PlayerPrefs.GetString(CredKey, "");
            if (string.IsNullOrEmpty(stored))
            {
                return null;
            }

            try
            {
                string json = Deobfuscate(stored);
                return JsonUtility.FromJson<Allow2Credentials>(json);
            }
            catch (Exception)
            {
                // Corrupt data -- clear and return null
                Clear();
                return null;
            }
        }

        public void Store(Allow2Credentials credentials)
        {
            if (credentials == null)
            {
                Clear();
                return;
            }

            string json = JsonUtility.ToJson(credentials);
            string obfuscated = Obfuscate(json);
            PlayerPrefs.SetString(CredKey, obfuscated);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(CredKey);
            PlayerPrefs.DeleteKey(LastUsedKey);
            PlayerPrefs.Save();
        }

        public int LoadLastUsedChildId()
        {
            return PlayerPrefs.GetInt(LastUsedKey, 0);
        }

        public void StoreLastUsedChildId(int childId)
        {
            PlayerPrefs.SetInt(LastUsedKey, childId);
            PlayerPrefs.Save();
        }

        // -- Obfuscation (XOR with device key) --

        private string Obfuscate(string plaintext)
        {
            byte[] data = Encoding.UTF8.GetBytes(plaintext);
            byte[] key = Encoding.UTF8.GetBytes(_deviceKey);
            byte[] result = new byte[data.Length];

            for (int i = 0; i < data.Length; i++)
            {
                result[i] = (byte)(data[i] ^ key[i % key.Length]);
            }

            return Convert.ToBase64String(result);
        }

        private string Deobfuscate(string obfuscated)
        {
            byte[] data = Convert.FromBase64String(obfuscated);
            byte[] key = Encoding.UTF8.GetBytes(_deviceKey);
            byte[] result = new byte[data.Length];

            for (int i = 0; i < data.Length; i++)
            {
                result[i] = (byte)(data[i] ^ key[i % key.Length]);
            }

            return Encoding.UTF8.GetString(result);
        }
    }
}
