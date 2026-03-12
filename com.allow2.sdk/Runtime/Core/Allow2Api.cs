// Allow2 Unity SDK v2
// Copyright (c) 2026 Allow2 Pty Ltd. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Allow2
{
    /// <summary>
    /// UnityWebRequest-based API client for the Allow2 REST API.
    /// All methods return data via callbacks (coroutine-compatible).
    /// </summary>
    public class Allow2Api
    {
        public string BaseUrl { get; private set; }
        public int Vid { get; private set; }
        public string Token { get; private set; }
        public int TimeoutSeconds { get; set; }

        public Allow2Api(string baseUrl, int vid, string token)
        {
            BaseUrl = string.IsNullOrEmpty(baseUrl) ? "https://api.allow2.com" : baseUrl;
            Vid = vid;
            Token = token;
            TimeoutSeconds = 15;
        }

        // ----------------------------------------------------------------
        // Pairing
        // ----------------------------------------------------------------

        /// <summary>
        /// Initiate PIN-code pairing. Returns session info via callback.
        /// </summary>
        public IEnumerator InitPINPairing(string uuid, string deviceName, string platform,
            Action<Allow2ApiResponse> callback)
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["uuid"] = uuid;
            body["name"] = deviceName;
            body["deviceToken"] = Token;
            body["vid"] = Vid;
            body["platform"] = string.IsNullOrEmpty(platform) ? "unity" : platform;

            yield return PostJson("/api/pair/pin/init", body, callback);
        }

        /// <summary>
        /// Poll pairing status.
        /// </summary>
        public IEnumerator CheckPairingStatus(string sessionId, Action<Allow2ApiResponse> callback)
        {
            string url = BaseUrl + "/api/pair/status/" + sessionId;
            yield return GetJson(url, null, callback);
        }

        // ----------------------------------------------------------------
        // Check
        // ----------------------------------------------------------------

        /// <summary>
        /// Check permissions for a child + activities.
        /// </summary>
        public IEnumerator Check(int userId, int pairId, string pairToken,
            int childId, Dictionary<int, int> activities, string tz, bool log,
            Action<Allow2ApiResponse> callback)
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["userId"] = userId;
            body["pairId"] = pairId;
            body["pairToken"] = pairToken;
            body["deviceToken"] = Token;
            body["tz"] = tz;
            body["childId"] = childId;
            body["log"] = log;

            // Activities as object keyed by id
            Dictionary<string, object> actObj = new Dictionary<string, object>();
            foreach (KeyValuePair<int, int> kvp in activities)
            {
                actObj[kvp.Key.ToString()] = kvp.Value;
            }
            body["activities"] = actObj;

            yield return PostJson("/serviceapi/check", body, callback);
        }

        // ----------------------------------------------------------------
        // Updates
        // ----------------------------------------------------------------

        /// <summary>
        /// Poll for updates (extensions, day type changes, etc.).
        /// </summary>
        public IEnumerator GetUpdates(int userId, int pairId, string pairToken,
            long timestampMillis, Action<Allow2ApiResponse> callback)
        {
            StringBuilder sb = new StringBuilder(BaseUrl);
            sb.Append("/api/getUpdates?userId=").Append(userId);
            sb.Append("&pairId=").Append(pairId);
            sb.Append("&pairToken=").Append(UnityWebRequest.EscapeURL(pairToken));
            sb.Append("&deviceToken=").Append(UnityWebRequest.EscapeURL(Token));
            if (timestampMillis > 0)
            {
                sb.Append("&timestampMillis=").Append(timestampMillis);
            }

            yield return GetJson(sb.ToString(), null, callback);
        }

        // ----------------------------------------------------------------
        // Requests
        // ----------------------------------------------------------------

        /// <summary>
        /// Create a "Request More Time" request.
        /// </summary>
        public IEnumerator CreateRequest(int userId, int pairId, string pairToken,
            int childId, int duration, int activityId, string message,
            Action<Allow2ApiResponse> callback)
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["userId"] = userId;
            body["pairId"] = pairId;
            body["pairToken"] = pairToken;
            body["childId"] = childId;
            body["duration"] = duration;
            body["activity"] = activityId;
            if (!string.IsNullOrEmpty(message))
            {
                body["message"] = message;
            }

            yield return PostJson("/api/request/createRequest", body, callback);
        }

        /// <summary>
        /// Poll request approval status.
        /// </summary>
        public IEnumerator GetRequestStatus(string requestId, string statusSecret,
            Action<Allow2ApiResponse> callback)
        {
            string url = BaseUrl + "/api/request/" + requestId + "/status";
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["X-Status-Secret"] = statusSecret;

            yield return GetJson(url, headers, callback);
        }

        // ----------------------------------------------------------------
        // Feedback
        // ----------------------------------------------------------------

        /// <summary>
        /// Submit feedback to the Allow2 server.
        /// </summary>
        public IEnumerator SubmitFeedback(int userId, int pairId, string pairToken,
            int childId, string category, string message,
            Dictionary<string, string> deviceContext, Action<Allow2ApiResponse> callback)
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["userId"] = userId;
            body["pairId"] = pairId;
            body["pairToken"] = pairToken;
            body["childId"] = childId;
            body["vid"] = Vid;
            body["category"] = category;
            body["message"] = message;
            if (deviceContext != null)
            {
                body["deviceContext"] = deviceContext;
            }

            yield return PostJson("/api/feedback/submit", body, callback);
        }

        /// <summary>
        /// Load feedback discussions for this device.
        /// </summary>
        public IEnumerator LoadFeedback(int userId, int pairId, string pairToken,
            Action<Allow2ApiResponse> callback)
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["userId"] = userId;
            body["pairId"] = pairId;
            body["pairToken"] = pairToken;

            yield return PostJson("/api/feedback/load", body, callback);
        }

        /// <summary>
        /// Reply to an existing feedback discussion.
        /// </summary>
        public IEnumerator FeedbackReply(int userId, int pairId, string pairToken,
            string discussionId, string message, Action<Allow2ApiResponse> callback)
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["userId"] = userId;
            body["pairId"] = pairId;
            body["pairToken"] = pairToken;
            body["discussionId"] = discussionId;
            body["message"] = message;

            yield return PostJson("/api/feedback/reply", body, callback);
        }

        // ----------------------------------------------------------------
        // Internal HTTP helpers
        // ----------------------------------------------------------------

        private IEnumerator PostJson(string path, Dictionary<string, object> body,
            Action<Allow2ApiResponse> callback)
        {
            string url = path.StartsWith("http") ? path : BaseUrl + path;
            string jsonBody = MiniJson.Serialize(body);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = TimeoutSeconds;

                yield return request.SendWebRequest();

                Allow2ApiResponse response = ParseResponse(request);
                if (callback != null)
                {
                    callback(response);
                }
            }
        }

        private IEnumerator GetJson(string url, Dictionary<string, string> headers,
            Action<Allow2ApiResponse> callback)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = TimeoutSeconds;

                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> kvp in headers)
                    {
                        request.SetRequestHeader(kvp.Key, kvp.Value);
                    }
                }

                yield return request.SendWebRequest();

                Allow2ApiResponse response = ParseResponse(request);
                if (callback != null)
                {
                    callback(response);
                }
            }
        }

        private Allow2ApiResponse ParseResponse(UnityWebRequest request)
        {
            Allow2ApiResponse response = new Allow2ApiResponse();
            response.StatusCode = (int)request.responseCode;

#if UNITY_2020_1_OR_NEWER
            bool isError = request.result != UnityWebRequest.Result.Success;
#else
            bool isError = request.isNetworkError || request.isHttpError;
#endif

            if (isError && request.responseCode == 0)
            {
                // Network error (no response at all)
                response.IsNetworkError = true;
                response.ErrorMessage = request.error;
                return response;
            }

            string text = request.downloadHandler != null ? request.downloadHandler.text : "";
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    response.Body = MiniJson.Deserialize(text) as Dictionary<string, object>;
                }
                catch (Exception)
                {
                    response.Body = null;
                }
            }

            if (isError)
            {
                response.IsHttpError = true;
                response.ErrorMessage = request.error;
                if (response.Body != null && response.Body.ContainsKey("message"))
                {
                    response.ErrorMessage = response.Body["message"].ToString();
                }
            }

            return response;
        }
    }

    /// <summary>
    /// API response wrapper.
    /// </summary>
    public class Allow2ApiResponse
    {
        public int StatusCode;
        public bool IsNetworkError;
        public bool IsHttpError;
        public string ErrorMessage;
        public Dictionary<string, object> Body;

        public bool IsSuccess
        {
            get { return !IsNetworkError && !IsHttpError && StatusCode >= 200 && StatusCode < 300; }
        }

        /// <summary>
        /// Get a string value from the response body.
        /// </summary>
        public string GetString(string key)
        {
            if (Body != null && Body.ContainsKey(key))
            {
                object val = Body[key];
                return val != null ? val.ToString() : null;
            }
            return null;
        }

        /// <summary>
        /// Get an int value from the response body.
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            if (Body != null && Body.ContainsKey(key))
            {
                object val = Body[key];
                if (val is long) return (int)(long)val;
                if (val is double) return (int)(double)val;
                if (val is int) return (int)val;
                int parsed;
                if (val != null && int.TryParse(val.ToString(), out parsed))
                {
                    return parsed;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Get a long value from the response body.
        /// </summary>
        public long GetLong(string key, long defaultValue = 0)
        {
            if (Body != null && Body.ContainsKey(key))
            {
                object val = Body[key];
                if (val is long) return (long)val;
                if (val is double) return (long)(double)val;
                long parsed;
                if (val != null && long.TryParse(val.ToString(), out parsed))
                {
                    return parsed;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Get a bool value from the response body.
        /// </summary>
        public bool GetBool(string key, bool defaultValue = false)
        {
            if (Body != null && Body.ContainsKey(key))
            {
                object val = Body[key];
                if (val is bool) return (bool)val;
            }
            return defaultValue;
        }
    }

    // ----------------------------------------------------------------
    // Minimal JSON serializer/deserializer
    // Unity's JsonUtility doesn't handle Dictionary<string,object>.
    // This is a lightweight alternative.
    // ----------------------------------------------------------------

    internal static class MiniJson
    {
        public static string Serialize(object obj)
        {
            if (obj == null) return "null";

            if (obj is string)
            {
                return "\"" + EscapeString((string)obj) + "\"";
            }

            if (obj is bool)
            {
                return (bool)obj ? "true" : "false";
            }

            if (obj is int || obj is long || obj is float || obj is double)
            {
                return obj.ToString();
            }

            if (obj is Dictionary<string, object>)
            {
                Dictionary<string, object> dict = (Dictionary<string, object>)obj;
                StringBuilder sb = new StringBuilder("{");
                bool first = true;
                foreach (KeyValuePair<string, object> kvp in dict)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("\"").Append(EscapeString(kvp.Key)).Append("\":");
                    sb.Append(Serialize(kvp.Value));
                }
                sb.Append("}");
                return sb.ToString();
            }

            if (obj is Dictionary<string, string>)
            {
                Dictionary<string, string> dict = (Dictionary<string, string>)obj;
                StringBuilder sb = new StringBuilder("{");
                bool first = true;
                foreach (KeyValuePair<string, string> kvp in dict)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("\"").Append(EscapeString(kvp.Key)).Append("\":");
                    sb.Append("\"").Append(EscapeString(kvp.Value)).Append("\"");
                }
                sb.Append("}");
                return sb.ToString();
            }

            if (obj is IList<object>)
            {
                IList<object> list = (IList<object>)obj;
                StringBuilder sb = new StringBuilder("[");
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(Serialize(list[i]));
                }
                sb.Append("]");
                return sb.ToString();
            }

            return "\"" + EscapeString(obj.ToString()) + "\"";
        }

        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int index = 0;
            return ParseValue(json, ref index);
        }

        private static string EscapeString(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }
        }

        private static object ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) return null;

            char c = json[index];
            if (c == '{') return ParseObject(json, ref index);
            if (c == '[') return ParseArray(json, ref index);
            if (c == '"') return ParseString(json, ref index);
            if (c == 't' || c == 'f') return ParseBool(json, ref index);
            if (c == 'n') return ParseNull(json, ref index);
            return ParseNumber(json, ref index);
        }

        private static Dictionary<string, object> ParseObject(string json, ref int index)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            index++; // skip {
            SkipWhitespace(json, ref index);

            while (index < json.Length && json[index] != '}')
            {
                SkipWhitespace(json, ref index);
                string key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ':') index++;
                SkipWhitespace(json, ref index);
                object value = ParseValue(json, ref index);
                dict[key] = value;
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') index++;
            }

            if (index < json.Length) index++; // skip }
            return dict;
        }

        private static List<object> ParseArray(string json, ref int index)
        {
            List<object> list = new List<object>();
            index++; // skip [
            SkipWhitespace(json, ref index);

            while (index < json.Length && json[index] != ']')
            {
                object value = ParseValue(json, ref index);
                list.Add(value);
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') index++;
            }

            if (index < json.Length) index++; // skip ]
            return list;
        }

        private static string ParseString(string json, ref int index)
        {
            if (index >= json.Length || json[index] != '"') return "";
            index++; // skip opening "
            StringBuilder sb = new StringBuilder();

            while (index < json.Length && json[index] != '"')
            {
                if (json[index] == '\\' && index + 1 < json.Length)
                {
                    index++;
                    char esc = json[index];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(esc); break;
                    }
                }
                else
                {
                    sb.Append(json[index]);
                }
                index++;
            }

            if (index < json.Length) index++; // skip closing "
            return sb.ToString();
        }

        private static object ParseNumber(string json, ref int index)
        {
            int start = index;
            bool isFloat = false;

            if (index < json.Length && json[index] == '-') index++;
            while (index < json.Length && char.IsDigit(json[index])) index++;
            if (index < json.Length && json[index] == '.')
            {
                isFloat = true;
                index++;
                while (index < json.Length && char.IsDigit(json[index])) index++;
            }
            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                isFloat = true;
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-')) index++;
                while (index < json.Length && char.IsDigit(json[index])) index++;
            }

            string numStr = json.Substring(start, index - start);

            if (isFloat)
            {
                double d;
                if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out d))
                {
                    return d;
                }
            }
            else
            {
                long l;
                if (long.TryParse(numStr, out l))
                {
                    return l;
                }
            }

            return 0;
        }

        private static bool ParseBool(string json, ref int index)
        {
            if (json.Length - index >= 4 && json.Substring(index, 4) == "true")
            {
                index += 4;
                return true;
            }
            if (json.Length - index >= 5 && json.Substring(index, 5) == "false")
            {
                index += 5;
                return false;
            }
            return false;
        }

        private static object ParseNull(string json, ref int index)
        {
            if (json.Length - index >= 4 && json.Substring(index, 4) == "null")
            {
                index += 4;
            }
            return null;
        }
    }
}
