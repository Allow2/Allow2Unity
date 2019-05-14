//
//  Allow2Unity
//  Allow2.cs
//
//  Created by Andrew Longhorn in Jan 2019.
//  Copyright © 2019 Allow2 Pty Ltd. All rights reserved.
//
// LICENSE:
//  See LICENSE file in root directory
//

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Allow2_SimpleJSON;
using System.Text;

namespace Allow2
{

    /// <summary>
    /// Access functionality of the Allow2 platform easily.
    /// </summary>
    public static class Allow2
    {
        private static readonly string uuid;
        private static string _deviceToken = "Not Set";    // ie: "iug893-kjg-fiug23" - not persisted: always set this on start
        public static EnvType env = EnvType.Production;

        //
        // relevant persistence items
        //
        static int userId;          // ie: 27634
        static string pairToken;    // ie: "98hbieg87-ilulieugil-dilufkucy"
        static string _timezone;    // ie: "Australia/Brisbane"
        static Dictionary<int, string> _children = new Dictionary<int, string>();
        static int _childId = 0;

        //HashSet<string> checkers = new HashSet<string>();         // contains uuids for running autocheckers (abort if your uuid is missing)
        static string checkerUuid = null;                           // uuid for the current checker
        static IEnumerator checker = null;                          // the current autochecker
        static IEnumerator qrCall = null;
        static DateTime qrDebounce = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
        static string pairingUuid = null;

        public static int childId;  // ie: 34

        public static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);

        public static string ApiUrl
        {
            get
            {
                switch (env)
                {
                    //case EnvType.Sandbox:
                    //return "https://sandbox-api.allow2.com"
                    case EnvType.Staging:
                        return "https://staging-api.allow2.com";
                    default:
                        return "https://api.allow2.com";
                }
            }
        }

        public static Dictionary<int, string> Children
        {
            get
            {
                return _children;
            }
        }

        public static string ServiceUrl
        {
            get
            {
                switch (env)
                {
                    //case EnvType.Sandbox:
                    //return "https://sandbox-service.allow2.com"
                    case EnvType.Staging:
                        return "https://staging-service.allow2.com";
                    default:
                        return "https://service.allow2.com";
                }
            }
        }

        public static resultClosure checkResultHandler;

        public static bool IsPaired
        {
            get
            {
                return (userId > 0) && (pairToken != null);
            }
        }

        public static string Timezone
        {
            get
            {
                return _timezone;
            }
            set
            {
                _timezone = value;
                PlayerPrefs.SetString("timezone", _timezone);
            }
        }

        /// <summary>
        /// Gets or sets the device token.
        /// The device token is mandatory, this needs to be set before making any calls to the sdk/api.
        /// Generate your device token for free at https://developer.allow2.com
        /// Use it to manage your app/game/device, promote it on the Allow2 platform and track downloads and usage.
        /// </summary>
        /// <value>The device token.</value>
        public static string DeviceToken
        {
            get
            {
                return _deviceToken;
            }
            set
            {
                _deviceToken = value;
                PlayerPrefs.SetString("deviceToken", _deviceToken);
                //if (!IsPaired)
                //{
                // todo: start a regular timer to keep trying to call home on startup until we confirm we are NOT paired.
                CheckForBrokenPairing();
                //}
            }
        }

        private static void Persist()
        {
            // todo: write to protected namespace storage?
            PlayerPrefs.SetInt("userId", userId);
            PlayerPrefs.SetString("pairToken", pairToken);
        }

        // no persistence here
        private static Dictionary<string, Allow2CheckResult> resultCache = new Dictionary<string, Allow2CheckResult>();

        /// <summary>
        /// A result closure provides the result from a call to the Allow2 platform.
        /// </summary>
        public delegate void resultClosure(string err, Allow2CheckResult result);

        /// <summary>
        /// An image closure provides the image returned by the Allow2 platform.
        /// </summary>
        public delegate void imageClosure(string err, Texture2D image);

        static Allow2()
        {
            uuid = SystemInfo.deviceUniqueIdentifier;
            if (uuid == SystemInfo.unsupportedIdentifier)
            {
                // cannot use on this platform, kludge is to generate and store one, use auditing on the server side to detect disconnection in any case
                uuid = PlayerPrefs.GetString("uuid");
                if (uuid == null)
                {
                    uuid = System.Guid.NewGuid().ToString();
                    PlayerPrefs.SetString("uuid", uuid);
                }
            }
            userId = PlayerPrefs.GetInt("userId");
            pairToken = PlayerPrefs.GetString("pairToken");
            _timezone = PlayerPrefs.GetString("timezone");
        }

        static IEnumerator CheckForBrokenPairing()
        {
            // here we just ask the question, was our unique ID paired and somehow it lost it's pairing?
            WWWForm form = new WWWForm();
            form.AddField("uuid", uuid);
            form.AddField("deviceToken", _deviceToken);

            using (UnityWebRequest www = UnityWebRequest.Post(ApiUrl + "/api/isDevicePaired", form))
            {
                yield return www.SendWebRequest();

                // we actually only need to check for a 200 response to know the server is informed and it's been checked.
                if (www.isNetworkError || www.isHttpError)
                {
                    Debug.Log(www.error);
                }
                else
                {
                    Debug.Log(www.downloadHandler.text);
                    var response = Allow2_SimpleJSON.JSON.Parse(www.downloadHandler.text);
                    // extract
                }
            }
        }

        /// <summary>
        /// Pair your game/app/device to a parents Allow2 account.
        /// </summary>
        /// <param name="behaviour">Provide a (any) MonoBehaviour for the sdk to use to call the platform.</param>
        /// <param name="user">The email address of the Allow2 account being paired.</param>
        /// <param name="pass">The associated password for the Allow2 account being paired.</param>
        /// <param name="deviceName">The name the user would like to use to identify this app/game/device.</param>
        /// <param name="callback">Provides the image of the QR Code.</param>
        public static void Pair(MonoBehaviour behaviour, 
                                string user, // ie: "fred@gmail.com",
                                string pass,               // ie: "my super secret password",
                                string deviceName,          // ie: "Fred's iPhone"
                                resultClosure callback)
        {
            behaviour.StartCoroutine(_Pair(user, pass, deviceName, callback));
        }

        static IEnumerator _Pair(string user, // ie: "fred@gmail.com",
                         string pass,               // ie: "my super secret password",
                         string deviceName,          // ie: "Fred's iPhone"
                         resultClosure callback
                        )
        {
            if (IsPaired)
            {
                callback(Allow2Error.AlreadyPaired, null);
                yield break;
            }
            WWWForm form = new WWWForm();
            form.AddField("user", user);
            form.AddField("pass", pass);
            form.AddField("deviceToken", _deviceToken);
            form.AddField("name", deviceName);
            form.AddField("uuid", uuid);

            Debug.Log(ApiUrl + "/api/pairDevice");

            using (UnityWebRequest www = UnityWebRequest.Post(ApiUrl + "/api/pairDevice", form))
            {
                yield return www.SendWebRequest();

                var response = JSONNode.Parse(www.downloadHandler.text);

                if (response == null)
                {
                    if (www.isNetworkError || www.isHttpError)
                    {
                        Debug.Log(www.error.ToString());
                        callback(www.error, null);
                    }
                    yield break;
                }

                //{
                //    "status":"success",
                //    "pairId":21105,
                //    "token":"8314c722-36fe-4256-81f4-8cce6e4da32d"
                //    "name":"Unity Test"
                //    "userId":6
                //    "children": [
                //        {"id":68,"name":"Cody"},
                //        {"id":69,"name":"Mikayla"},
                //        {"id":21423,"name":"Mary"}
                //    ]
                //}

                Debug.Log(www.downloadHandler.text);
                var json = Allow2_SimpleJSON.JSON.Parse(www.downloadHandler.text);

                if (json["status"] != "success")
                {
                    callback(Allow2Error.InvalidResponse, null);
                    yield break;
                }

                userId = json["userId"];
                pairToken = json["token"];
                _children = ParseChildren(json["children"]);

                Persist();

                // return
                callback(null, null);
            }
        }

        const int QRDebounceDelay = 500;

        /// <summary>
        /// Gets a new QR Code texture to show to the user to enable them to pair your game/app/device with Allow2.
        /// Call this to get a new QR Code any time the user changes the name of the device.
        /// Note this is debounced automatically, so just keep calling it immediately (even if the user is still typing).
        /// </summary>
        /// <param name="behaviour">Provide a (any) MonoBehaviour for the sdk to use to call the platform.</param>
        /// <param name="deviceName">The name the user would like to use to identify this app/game/device.</param>
        /// <param name="callback">Callback.</param>
        public static void GetQR(MonoBehaviour behaviour, string deviceName, imageClosure callback) {
            DateTime now = DateTime.Now;
            Debug.Log(qrDebounce.CompareTo(now));
            if ((qrCall != null) && (qrDebounce.CompareTo(now) > 0)) {
                Debug.Log("debounce " + qrDebounce.ToShortTimeString() + " < " + now.ToShortTimeString());
                IEnumerator oldCall = qrCall;
                qrCall = null;
                behaviour.StopCoroutine(oldCall);
            }
            qrDebounce = now.AddMilliseconds(QRDebounceDelay);
            qrCall = _GetQR(deviceName, callback);
            behaviour.StartCoroutine(qrCall);
        }

        static IEnumerator _GetQR(string deviceName, imageClosure callback)
        {
            yield return new WaitForSeconds(QRDebounceDelay/1000);
            string qrURL = ApiUrl + "/genqr/" +
                UnityWebRequest.EscapeURL(_deviceToken) + "/" +
                  UnityWebRequest.EscapeURL(uuid) + "/" +
                  UnityWebRequest.EscapeURL(deviceName);
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(qrURL);
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log("QR LOAD ERROR: " + www.error);
                Texture errorImage = Resources.Load("Allow2/QRError") as Texture2D;
                callback(www.error, null);
                yield break;
            }
            Texture2D qrCode = DownloadHandlerTexture.GetContent(www);
            callback(null, qrCode);
        }

        //public static IEnumerator Check(int[] activities,
        //                  resultClosure callback,
        //                  bool log = false
        //                 )
        //{
        //    return Check(_child, activities,
        //                  resultClosure callback,
        //                  bool log = false
        //                 )
        //}

        private static Dictionary<int, string> ParseChildren(JSONNode json)
        {
            Dictionary<int, string> children = new Dictionary<int, string>();
            foreach (JSONNode child in json)
            {
                children[child["id"]] = child["name"];
            }
            return children;
        }

        /// <summary>
        /// Check if the specified child can use the current activities and optionally log usage.
        /// Note that if you specify log as true, usage will be recorded even if the child is technically not allowed to use one of the
        /// <paramref name="activities"/>. This is to allow you the ability to be flexible in allowing usage, but should be used sparingly.
        /// If you are, for instance, just checking if something CAN be done at this time, then make sure you supply false for the log parameter.
        /// </summary>
        /// <param name="behaviour">Provide a (any) MonoBehaviour for the sdk to use to call the platform.</param>
        /// <param name="childId">Id of the child for which you wish to check (and possibly log) activities.</param>
        /// <param name="activities">The activity ids to check.</param>
        /// <param name="callback">Provides the result of the check.</param>
        /// <param name="log">If set to <c>true</c>, then log the usage of these activities as well.</param>
        public static void Check(MonoBehaviour behaviour,
                                 int childId,    // childId == 0 ? Get Updated Child List and confirm Pairing
                                 int[] activities,
                                 resultClosure callback,
                                 bool log = false
                                )
        {
            behaviour.StartCoroutine(_Check(null, childId, activities, callback, log));
        }

        /// <summary>
        /// Check if the specified child can use the current activities and optionally log usage.
        /// You should ALWAYS log usage when the child is using the activities, otherwise their usage will not be debited from their quota.
        /// Note that if you specify log as true, usage will be recorded even if the child is technically not allowed to use one of the
        /// <paramref name="activities"/>. This is to allow you the ability to be flexible in allowing usage, but should be used sparingly.
        /// If you are, for instance, just checking if something CAN be done at this time, then make sure you supply false for the log parameter.
        /// </summary>
        /// <param name="behaviour">Provide a (any) MonoBehaviour for the sdk to use to call the platform.</param>
        /// <param name="activities">The activity ids to check.</param>
        /// <param name="callback">Provides the result of the check.</param>
        /// <param name="log">If set to <c>true</c>, then log the usage of these activities as well.</param>
        public static void Check(MonoBehaviour behaviour,
                                 int[] activities,
                                 resultClosure callback,
                                 bool log = false
                                )
        {
            behaviour.StartCoroutine(_Check(null, childId, activities, callback, log));
        }

        static IEnumerator _Check(string myUuid,
                                  int childId,    // childId == 0 ? Get Updated Child List and confirm Pairing
                                  int[] activities,
                                  resultClosure callback,
                                  bool log) // if set, then this is an autochecker and we drop it if we replace it. If null, it's adhoc, always return a value
        {
            if (!IsPaired)
            {
                callback(Allow2Error.NotPaired, null);
                yield break;
            }

            Debug.Log(userId);
            Debug.Log(pairToken);
            Debug.Log(_deviceToken);

            JSONNode body = new JSONObject();
            body.Add("userId", userId);
            body.Add("pairToken", pairToken);
            body.Add("deviceToken", _deviceToken);
            body.Add("tz", _timezone);
            JSONArray activityJson = new JSONArray();
            foreach (int activity in activities)
            {
                JSONNode activityParams = new JSONObject();
                activityParams.Add("id", activity);
                activityParams.Add("log", log);
                activityJson.Add(activityParams);
            }
            body.Add("activities", activityJson);
            body.Add("log", log);
            if (childId > 0)
            {
                body.Add("childId", childId);
            }
            string bodyStr = body.ToString();

            // check the cache first
            if (resultCache.ContainsKey(bodyStr))
            {
                Allow2CheckResult checkResult = resultCache[bodyStr];

                if (checkResult.Expires.CompareTo(new DateTime()) < 0)
                {
                    // not expired yet, use cached value
                    callback(null, checkResult);
                    yield break;
                }

                // clear cached value and ask the server again
                resultCache.Remove(bodyStr);
            }

            byte[] bytes = Encoding.UTF8.GetBytes(bodyStr);
            using (UnityWebRequest www = new UnityWebRequest(ServiceUrl + "/serviceapi/check"))
            {
                www.method = UnityWebRequest.kHttpVerbPOST;
                www.uploadHandler = new UploadHandlerRaw(bytes);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.uploadHandler.contentType = "application/json";
                www.chunkedTransfer = false;
                yield return www.SendWebRequest();

                if ((myUuid != null) && (checkerUuid != myUuid)) {
                    Debug.Log("drop response for check: " + myUuid);
                    yield break;    // this check is aborted, just drop the response and don't return;
                }

                Debug.Log(www.downloadHandler.text);
                var json = Allow2_SimpleJSON.JSON.Parse(www.downloadHandler.text);

                if ((www.responseCode == 401) ||
                    ((json["status"] == "error") &&
                     ((json["message"] == "Invalid user.") ||
                      (json["message"] == "invalid pairToken"))))
                {
                    // special case, no longer controlled
                    Debug.Log("No Longer Paired");
                    userId = 0;
                    pairToken = null;
                    Persist();
                    //childId = 0;
                    //_children = []
                    //_dayTypes = []
                    var failOpen = new Allow2CheckResult();
                    failOpen.Add("subscription", new Allow2_SimpleJSON.JSONArray());
                    failOpen.Add("allowed", true);
                    failOpen.Add("activities", new Allow2_SimpleJSON.JSONArray());
                    failOpen.Add("dayTypes", new Allow2_SimpleJSON.JSONArray());
                    failOpen.Add("allDayTypes", new Allow2_SimpleJSON.JSONArray());
                    failOpen.Add("children", new Allow2_SimpleJSON.JSONArray());
                    callback(null, failOpen);
                    yield break;
                }

                if (www.isNetworkError || www.isHttpError)
                {
                    Debug.Log(www.error);
                    callback(www.error, null);
                    yield break;
                }

                if (json["allowed"] == null)
                {
                    callback(Allow2Error.InvalidResponse, null);
                    yield break;
                }

                var response = new Allow2CheckResult();
                response.Add("activities", json["activities"]);
                response.Add("subscription", json["subscription"]);
                response.Add("dayTypes", json["dayTypes"]);
                response.Add("children", json["children"]);
                var _dayTypes = json["allDayTypes"];
                response.Add("allDayTypes", _dayTypes);
                var oldChildIds = _children.Keys;
                var children = json["children"];
                _children = ParseChildren(children);
                response.Add("children", children);

                if (oldChildIds != _children.Keys)
                {
                    Persist(); // only persist if the children change, this won't happen often.
                }

                // cache the response
                resultCache[bodyStr] = response;
                callback(null, response);
            }
        }

        private static IEnumerator CheckLoop(string myUuid,
                                             int childId,
                                             int[] activities,
                                             resultClosure callback,
                                             bool log = false)
        {
            while (checkerUuid == myUuid) {
                Debug.Log("check uuid: " + myUuid);
                yield return _Check(myUuid, childId, activities, delegate (string err, Allow2CheckResult result) {
                    if (!IsPaired && (checkerUuid != myUuid)) {
                        checkerUuid = null; // stop the checker, we have been unpaired
                    }
                    callback(err, result);
                }, log);
                yield return new WaitForSeconds(3);
            }
        }

        /// <summary>
        /// Start checking (and optionally logging) the ability for the child to use the given activities.
        /// This starts a process that regularly checks (and optionally logs) usage until stopped using Allow2.StopChecking.
        /// You can call this repeatedly and change the child id at any time, but there will only ever be one process and
        /// it will continue to use the last provided child id.
        /// Note, that if the child is unable or disallowed to use any of the <paramref name="activities"/>, they will still be continually checked/logged until Allow2.StopChecking() is called.
        /// This is to allow you to selectively allow the child to finish an activity, but will put them in negative credit (which will come off future usage).
        /// You should ALWAYS log usage when the child is using the activities, otherwise their usage will not be debited from their quota.
        /// </summary>
        /// <param name="behaviour">Provide a (any) MonoBehaviour for the sdk to use to call the platform.</param>
        /// <param name="childId">The child for which the activites are being checked (and optionally logged).</param>
        /// <param name="activities">The activity ids to check.</param>
        /// <param name="callback">Provides the result of the check.</param>
        /// <param name="log">If set to <c>true</c>, then log the usage of these activities as well.</param>
        public static void StartChecking(MonoBehaviour behaviour,
                                         int childId,
                                         int[] activities,
                                         resultClosure callback,
                                         bool log)
        {
            // change the parameters
            bool changed = (_childId != childId);
            _childId = childId;
            if (changed || (checkerUuid == null)) {
                //switch checker
                checkerUuid = System.Guid.NewGuid().ToString(); // this will abort the current checker and kill it.
                /*checker = */ behaviour.StartCoroutine(CheckLoop(checkerUuid, childId, activities, callback, log));
            }
        }

        /// <summary>
        /// Stop checking (and logging) usage.
        /// If there is no current checking/logging process started with Allow2.StartChecking(), this call has no effect.
        /// </summary>
        public static void StopChecking()
        {
            checkerUuid = null; // this will kill the running checker
        }

        /// <summary>
        /// Submit a request on behalf of the current child.
        /// </summary>
        /// <returns>Results of the request.</returns>
        /// <param name="behaviour">Provide a (any) MonoBehaviour for the sdk to use to call the platform.</param>
        /// <param name="childId">The Id of the child making the request.</param>
        /// <param name="dayTypeId">(optional)The Id of the day type they are requesting.</param>
        /// <param name="lift">(optional) An Array of ids for Bans they are asking to be lifted.</param>
        /// <param name="message">(optional) Message to send with the request.</param>
        /// <param name="callback">callback that will return the response success or error.</param>
        public static void Request(MonoBehaviour behaviour,
                                   int childId,
                                   int dayTypeId,
                                   int[] lift,
                                   string message,
                                   resultClosure callback)
        {
            behaviour.StartCoroutine(_Request(childId, dayTypeId, lift, message, callback));
        }

        /// <summary>
        /// Submit a request on behalf of the current child.
        /// </summary>
        /// <returns>Results of the request.</returns>
        /// <param name="behaviour">Provide a (any) MonoBehaviour for the sdk to use to call the platform.</param>
        /// <param name="dayTypeId">(optional)The Id of the day type they are requesting.</param>
        /// <param name="lift">(optional) An Array of ids for Bans they are asking to be lifted.</param>
        /// <param name="message">(optional) Message to send with the request.</param>
        /// <param name="callback">callback that will return the response success or error.</param>
        public static void Request(MonoBehaviour behaviour,
                                   int dayTypeId,
                                   int[] lift,
                                   string message,
                                   resultClosure callback)
        {
            behaviour.StartCoroutine(_Request(childId, dayTypeId, lift, message, callback));
        }

        static IEnumerator _Request(int childId,
                                    int dayTypeId,
                                    int[] lift,
                                    string message,
                                    resultClosure callback)
        {
            if (!IsPaired)
            {
                callback(Allow2Error.NotPaired, null);
                yield break;
            }
            if (childId < 1)
            {
                callback(Allow2Error.MissingChildId, null);
                yield break;
            }

            WWWForm body = new WWWForm();
            body.AddField("userId", userId);
            body.AddField("pairToken", pairToken);
            body.AddField("deviceToken", _deviceToken);
            body.AddField("childId", childId);
            //form.AddField("lift", lift.asJson);
            //if (dayTypeId != nil) {
            //    body["dayType"] = JSON(dayTypeId!)
            //    body["changeDayType"] = true
            //}
            string bodyStr = body.ToString();

            byte[] bytes = Encoding.UTF8.GetBytes(bodyStr);
            using (UnityWebRequest www = new UnityWebRequest(ApiUrl + "/api/checkPairing"))
            {
                www.method = UnityWebRequest.kHttpVerbPOST;
                www.uploadHandler = new UploadHandlerRaw(bytes);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.uploadHandler.contentType = "application/json";
                www.chunkedTransfer = false;
                yield return www.SendWebRequest();

                // anything other than a 200 response is a "try again" as far as we are concerned
                if (www.responseCode != 200)
                {
                    callback(Allow2Error.NoConnection, null);   // let the caller know we are having problems
                    yield break;
                }

            }
        }

        /// <summary>
        /// Use this routine to notify Allow2 you are starting a pairing session for a QR Code pairing.
        /// Call this when you are about to display a QR code to the user to allow them to pair with ALlow2.
        /// Get the appropriate QR Code using Allow2.GetQR().
        /// 
        /// </summary>
        /// <param name="behaviour">Provide a (any) MonoBehaviour for the sdk to use to call the platform.</param>
        /// <param name="callback">Callback that will return response success or error</param>
        public static void StartPairing(MonoBehaviour behaviour, resultClosure callback)
        {
            //switch checker
            pairingUuid = System.Guid.NewGuid().ToString(); // this will abort the current poll and kill it.
            behaviour.StartCoroutine(PairingLoop(pairingUuid, callback));
        }

        /// <summary>
        /// Tell Allow2 the QR Code for pairing is no longer being displayed.
        /// Call this when you stop showing the QR code and therefore the user can no longer scan it.
        /// </summary>
        public static void StopPairing()
        {
            pairingUuid = null; // this will kill the running checker
        }

        private static IEnumerator PairingLoop(string myUuid, resultClosure callback)
        {
            while (pairingUuid == myUuid)
            {
                Debug.Log("poll uuid: " + myUuid);
                yield return _PollPairing(myUuid, delegate (string err, Allow2CheckResult result) {
                    if (IsPaired)
                    {
                        pairingUuid = null; // stop the checker, we have been paired
                    }
                    callback(err, result);
                });
                yield return new WaitForSeconds(3);
            }
        }

        static IEnumerator _PollPairing(string myUuid, resultClosure callback)
        {
            JSONNode body = new JSONObject();
            body.Add("uuid", uuid);
            body.Add("deviceToken", _deviceToken);
            string bodyStr = body.ToString();

            byte[] bytes = Encoding.UTF8.GetBytes(bodyStr);
            using (UnityWebRequest www = new UnityWebRequest(ApiUrl + "/api/checkPairing"))
            {
                www.method = UnityWebRequest.kHttpVerbPOST;
                www.uploadHandler = new UploadHandlerRaw(bytes);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.uploadHandler.contentType = "application/json";
                www.chunkedTransfer = false;
                yield return www.SendWebRequest();

                // anything other than a 200 response is a "try again" as far as we are concerned
                if (www.responseCode != 200)
                {
                    callback(Allow2Error.NoConnection, null);   // let the caller know we are having problems
                    yield break;
                }

                Debug.Log(www.downloadHandler.text);
                var json = Allow2_SimpleJSON.JSON.Parse(www.downloadHandler.text);

                string status = json["status"];

                if (status != "success")
                {
                    callback(json["message"] ?? "Unknown Error", null);
                    yield break;
                }

                pairToken = json["pairToken"];
                userId = json["userId"];
                childId = json["childId"];
                _children = childId > 0 ? new Dictionary<int, string>() : ParseChildren(json["children"]);

                callback(null, null);
            }
        }
    }
}