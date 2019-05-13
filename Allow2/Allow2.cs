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
        static Coroutine qrCall = null;
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

        public delegate void resultClosure(string err, Allow2CheckResult result);
        public delegate void imageClosure(string err, Texture qrCode);

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

        /*
         * Pairing
         * 
         */
        public static void Pair(MonoBehaviour behavior, 
                                string user, // ie: "fred@gmail.com",
                                string pass,               // ie: "my super secret password",
                                string deviceName,          // ie: "Fred's iPhone"
                                resultClosure callback )
        {
            behavior.StartCoroutine(_Pair(user, pass, deviceName, callback));
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

        public static void GetQR(MonoBehaviour behavior, string name, imageClosure callback) {
            DateTime now = new DateTime();
            if ((qrCall != null) && (qrDebounce.CompareTo(now) > 0)) {
                Debug.Log("debounce");
                Coroutine oldCall = qrCall;
                qrCall = null;
                behavior.StopCoroutine(oldCall);
            }
            qrDebounce = now.AddMilliseconds(QRDebounceDelay);
            qrCall = behavior.StartCoroutine(_GetQR(name, callback));
        }

        static IEnumerator _GetQR(string name, imageClosure callback)
        {
            yield return new WaitForSeconds(QRDebounceDelay/1000);
            string qrURL = ApiUrl + "/genqr/" +
                UnityWebRequest.EscapeURL(_deviceToken) + "/" +
                  UnityWebRequest.EscapeURL(uuid) + "/" +
                  UnityWebRequest.EscapeURL(name);
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(qrURL);
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log(www.error);
                Texture errorImage = Resources.Load("Allow2/QRError") as Texture2D;
                callback(www.error, null);
                yield break;
            }
            Texture qrCode = ((DownloadHandlerTexture)www.downloadHandler).texture;
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

        public static void Check(MonoBehaviour behaviour,
                                 int childId,    // childId == 0 ? Get Updated Child List and confirm Pairing
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

        public static void StartChecking(MonoBehaviour behavior,
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
                /*checker = */ behavior.StartCoroutine(CheckLoop(checkerUuid, childId, activities, callback, log));
            }
        }

        public static void StopChecking()
        {
            checkerUuid = null; // this will kill the running checker
        }

        public static IEnumerator Request(
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

        public static void StartPairing(MonoBehaviour behavior, resultClosure callback)
        {
            //switch checker
            pairingUuid = System.Guid.NewGuid().ToString(); // this will abort the current poll and kill it.
            behavior.StartCoroutine(PairingLoop(pairingUuid, callback));
        }

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