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
        static int _childId;        // ie: 34
        static string pairToken;    // ie: "98hbieg87-ilulieugil-dilufkucy"
        static string _timezone;    // ie: "Australia/Brisbane"

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
                if (!IsPaired)
                {
                    // todo: start a regular timer to keep trying to call home on startup until we confirm we are NOT paired.
                    CheckForBrokenPairing();
                }
            }
        }

        private static void Persist()
        {
            // todo: write to protected namespace storage?
            PlayerPrefs.SetInt("userId", userId);
        }

        // no persistence here
        private static Dictionary<string, Allow2CheckResult> resultCache = new Dictionary<string, Allow2CheckResult>();

        public delegate void resultClosure(string err, Allow2CheckResult result);

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
         * 
         * 
         */
        public static IEnumerator Pair(string user, // ie: "fred@gmail.com",
                         string pass,               // ie: "my super secret password",
                         string deviceName,          // ie: "Fred's iPhone"
                         resultClosure callback
                        )
        {
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

                if (www.isNetworkError || www.isHttpError)
                {
                    Debug.Log(www.error.ToString());
                    callback(www.error, null);
                }
                else
                {
                    Debug.Log(www.downloadHandler.text);
                    var response = Allow2_SimpleJSON.JSON.Parse(www.downloadHandler.text);
                    // extract

                    Persist();

                    // return
                    callback(null, null);
                }
            }
        }

        public static string GetQR(string name)
        {
            return ApiUrl + "/genqr/" +
               WWW.EscapeURL(_deviceToken) + "/" +
                  WWW.EscapeURL(uuid) + "/" +
                  WWW.EscapeURL(name);
        }

        public static IEnumerator Check(int childId,    // childId == 0 ? Get Updated Child List and confirm Pairing
                          int[] activities,
                          resultClosure callback,
                          bool log = false
                         )
        {
            if (!IsPaired)
            {
                callback(Allow2Error.NotPaired, null);
                yield break;
            }

            WWWForm form = new WWWForm();
            form.AddField("userId", userId);
            form.AddField("pairToken", pairToken);
            form.AddField("deviceToken", _deviceToken);
            form.AddField("tz", _timezone);
            //form.AddField("activities", activities);
            form.AddField("log", log ? 1 : 0);
            if (childId > 0)
            {
                form.AddField("childId", childId);
            }

            // check the cache first
            string key = form.ToString();
            if (resultCache.ContainsKey(key)) {
                Allow2CheckResult checkResult = resultCache[key];

                if (checkResult.Expires.CompareTo(new DateTime()) < 0 ) {
                    // not expired yet, use cached value
                    callback(null, checkResult);
                    yield break;
                }

                // clear cached value and ask the server again
                resultCache.Remove(key);
            }

            UnityWebRequest www = UnityWebRequest.Get(ApiUrl + "/api/pairDevice");
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log(www.error);
                callback(www.error, null);
            }
            else
            {
                Debug.Log(www.downloadHandler.text);
                var json = Allow2_SimpleJSON.JSON.Parse(www.downloadHandler.text);

                if ((json["error"] == "invalid pairId") ||
                    (json["error"] == "invalid pairToken"))
                {
                    // todo: || (response?.statusCode == 401)  {
                    // special case, no longer controlled
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
                var _children = json["allDayTypes"];
                response.Add("children", _children);

                // cache the response
                resultCache[key] = response;
                callback(null, response);
            }
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
            if (_childId < 1)
            {
                callback(Allow2Error.MissingChildId, null);
                yield break;
            }

            WWWForm form = new WWWForm();
            form.AddField("userId", userId);
            form.AddField("pairToken", pairToken);
            form.AddField("deviceToken", _deviceToken);
            form.AddField("childId", _childId);
            //form.AddField("lift", lift.asJson);
            //if (dayTypeId != nil) {
            //    body["dayType"] = JSON(dayTypeId!)
            //    body["changeDayType"] = true
            //}


        }
    }
}