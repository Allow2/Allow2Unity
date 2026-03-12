// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;
using System.Security.Cryptography;
using System.Text;

namespace Allow2
{
    /// <summary>
    /// Offline HMAC-SHA256 challenge-response for voice codes.
    ///
    /// Voice code format: T A MM NN
    ///   T  = request type (1 = more time, 2 = day type, 3 = ban lift)
    ///   A  = activity ID (single digit, mod 10 for >9)
    ///   MM = duration in 5-minute increments (00-99 -> 0-495 min)
    ///   NN = nonce (2 digits from HMAC)
    ///
    /// The parent reads the 6-digit challenge code displayed on the child's
    /// device. The parent enters it into their Allow2 app which generates a
    /// 4-digit response code using the shared secret. The child enters the
    /// response code to get their time extended.
    ///
    /// This enables offline time extensions when there is no internet.
    /// </summary>
    public static class Allow2VoiceCode
    {
        /// <summary>
        /// Generate a 6-digit challenge code.
        /// </summary>
        /// <param name="requestType">1=more time, 2=day type, 3=ban lift</param>
        /// <param name="activityId">Activity ID</param>
        /// <param name="durationMinutes">Requested duration in minutes</param>
        /// <returns>6-character challenge string (e.g., "130412")</returns>
        public static string GenerateChallenge(int requestType, int activityId, int durationMinutes)
        {
            int t = requestType;
            if (t < 1 || t > 3) t = 1;

            int a = activityId % 10;
            int mm = durationMinutes / 5;
            if (mm > 99) mm = 99;

            // Generate a 2-digit nonce from current time
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int nn = (int)(now % 100);

            return string.Format("{0}{1}{2:D2}{3:D2}", t, a, mm, nn);
        }

        /// <summary>
        /// Verify a 4-digit response code against a challenge.
        /// </summary>
        /// <param name="challenge">The 6-digit challenge that was displayed</param>
        /// <param name="response">The 4-digit response entered by the child</param>
        /// <param name="sharedSecret">The shared secret (from pairing credentials)</param>
        /// <returns>True if the response is valid</returns>
        public static bool VerifyResponse(string challenge, string response, string sharedSecret)
        {
            if (string.IsNullOrEmpty(challenge) || challenge.Length != 6)
            {
                return false;
            }
            if (string.IsNullOrEmpty(response) || response.Length != 4)
            {
                return false;
            }
            if (string.IsNullOrEmpty(sharedSecret))
            {
                return false;
            }

            string expected = ComputeResponse(challenge, sharedSecret);
            return SafeCompare(expected, response);
        }

        /// <summary>
        /// Compute the expected 4-digit response code for a challenge.
        /// This is what the parent's app would compute.
        /// </summary>
        /// <param name="challenge">The 6-digit challenge</param>
        /// <param name="sharedSecret">The shared secret</param>
        /// <returns>4-digit response string</returns>
        public static string ComputeResponse(string challenge, string sharedSecret)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(sharedSecret);
            byte[] messageBytes = Encoding.UTF8.GetBytes(challenge);

            using (HMACSHA256 hmac = new HMACSHA256(keyBytes))
            {
                byte[] hash = hmac.ComputeHash(messageBytes);

                // Take first 4 bytes and reduce to a 4-digit number
                int code = ((hash[0] & 0x7F) << 24 |
                            (hash[1] & 0xFF) << 16 |
                            (hash[2] & 0xFF) << 8 |
                            (hash[3] & 0xFF)) % 10000;

                return code.ToString("D4");
            }
        }

        /// <summary>
        /// Parse a challenge code into its components.
        /// </summary>
        public static bool ParseChallenge(string challenge, out int requestType,
            out int activityId, out int durationMinutes, out int nonce)
        {
            requestType = 0;
            activityId = 0;
            durationMinutes = 0;
            nonce = 0;

            if (string.IsNullOrEmpty(challenge) || challenge.Length != 6)
            {
                return false;
            }

            int t;
            if (!int.TryParse(challenge.Substring(0, 1), out t)) return false;
            requestType = t;

            int a;
            if (!int.TryParse(challenge.Substring(1, 1), out a)) return false;
            activityId = a;

            int mm;
            if (!int.TryParse(challenge.Substring(2, 2), out mm)) return false;
            durationMinutes = mm * 5;

            int nn;
            if (!int.TryParse(challenge.Substring(4, 2), out nn)) return false;
            nonce = nn;

            return true;
        }

        /// <summary>
        /// Constant-time comparison.
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
    }
}
