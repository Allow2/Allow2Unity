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

        public static string deviceToken = "Not Set";    // ie: "iug893-kjg-fiug23" - not persisted: always set this on start
        public static EnvType env = EnvType.Production;

        //
        // relevant persistence items
        //
        static int userId;          // ie: 27634
        static string pairToken;    // ie: "98hbieg87-ilulieugil-dilufkucy"
        static string timezone;     // ie: "Australia/Brisbane"

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

        private static void persist() {
            // todo: write to protected namespace storage?
        }

        public delegate void resultClosure(string err, Allow2CheckResult result);

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
            form.AddField("deviceToken", deviceToken);
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

                    // persist

                    // return
                    callback(null, null);
                }
            }
        }

        public static IEnumerator Check(int childId,
                          int[] activities,
                          resultClosure callback,
                          bool log = false
                         )
        {
            UnityWebRequest www = UnityWebRequest.Get("http://www.my-server.com");
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
