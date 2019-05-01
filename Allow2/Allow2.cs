//
//  Allow2Unity
//
//  Created by Andrew Longhorn in early 2019.
//  Copyright © 2019 Allow2 Pty Ltd. All rights reserved.
//
// LICENSE:
//  See LICENSE file in root directory
//

using Application;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Allow2
{

    /// <summary>
    /// Access functionality of the Allow2 platform easily.
    /// </summary>
    public static class Allow2
    {
        static string uuid;
        static string _deviceToken = "Not Set";    // ie: "iug893-kjg-fiug23" - not persisted: always set this on start
        public static EnvType env = EnvType.Production;

        //
        // relevant persistence items
        //
        static int userId;          // ie: 27634
        static string pairToken;    // ie: "98hbieg87-ilulieugil-dilufkucy"
        static string _timezone;     // ie: "Australia/Brisbane"

        //
        // cannot instantiate this class
        //
        //protected Allow2() {
        //}

        public static string apiUrl
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

        public static string serviceUrl
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

        public static string timezone
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

        public static string deviceToken
        {
            get
            {
                return _deviceToken;
            }
            set
            {
                _deviceToken = value;
                PlayerPrefs.SetString("deviceToken", _deviceToken);
                if ((userId < 1) || (pairToken == null)) {
                    // todo: start a regular timer to keep trying to call home on startup until we confirm we are NOT paired.
                    CheckForBrokenPairing();
                }
            }
        }

        private static void persist() {
            // todo: write to protected namespace storage?
            PlayerPrefs.SetInt("userId", userId);
        }

        public delegate void resultClosure(string err, Allow2CheckResult result);

        static Allow2()
        {
            uuid = SystemInfo.deviceUniqueIdentifier;
            if (uuid == SystemInfo.unsupportedIdentifier) {
                // cannot use on this platform, kludge is to generate and store one, use auditing on the server side to detect disconnection
                uuid = PlayerPrefs.GetString("uuid");
                if (uuid == null) {

                    PlayerPrefs.SetString("uuid", uuid);
                }
            }
            userId = PlayerPrefs.GetInt("userId");
            pairToken = PlayerPrefs.GetString("pairToken");
            timezone = PlayerPrefs.GetString("timezone");
        }

        static IEnumerator CheckForBrokenPairing()
        {
            // here we just ask the question, was this unique ID paired and somehow it lost it's pairing?
            WWWForm form = new WWWForm();
            form.AddField("uuid", uuid);
            form.AddField("deviceToken", _deviceToken);

            using (UnityWebRequest www = UnityWebRequest.Post(apiUrl + "/api/isDevicePaired", form))
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

        //void Awake()
        //{
        //    DontDestroyOnLoad(this);
        //}

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

            Debug.Log(apiUrl + "/api/pairDevice");

            using (UnityWebRequest www = UnityWebRequest.Post(apiUrl + "/api/pairDevice", form))
            {
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError)
                {
                    Debug.Log(www.error);
                    callback(www.error, null);
                }
                else
                {
                    Debug.Log(www.downloadHandler.text);
                    var response = Allow2_SimpleJSON.JSON.Parse(www.downloadHandler.text);
                    // extract

                    persist();

                    // return
                    callback(null, null);
                }
            }
        }

        public static string getQR()
        {
            return "https://api.allow2.com/genqr/" +
               WWW.EscapeURL(_deviceToken) + "/" +
                  WWW.EscapeURL(uuid) + "/" +
                  WWW.EscapeURL(name);
        }

        public static IEnumerator Check(int childId,
                          int[] activities,
                          resultClosure callback,
                          bool log = false
                         )
        {
            if ((userId < 1) || (pairToken == null)) {
                callback(Allow2Error.NotPaired, null);
                yield break;
            }

            WWWForm form = new WWWForm();
            form.AddField("user", user);
            form.AddField("pass", pass);
            form.AddField("deviceToken", _deviceToken);
            form.AddField("name", deviceName);

            UnityWebRequest www = UnityWebRequest.Get(apiUrl + "/api/pairDevice");
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
                    persist();
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

                if (json["allowed"] == null) {
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
                callback(null, response);
            }
        }
    }
}
