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

        public static string deviceToken = "Not Set";    // ie: "346-34269hcubi-187gigi8g-14i3ugkug",
        public static EnvType env = EnvType.Production;

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

        public static IEnumerator Pair(string user,           // ie: "fred@gmail.com",
                         string pass,           // ie: "my super secret password",
                         string deviceName      // ie: "Fred's iPhone"
                        ) {
            WWWForm form = new WWWForm();
            form.AddField("user", user);
            form.AddField("pass", pass);
            form.AddField("deviceToken", deviceToken);
            form.AddField("name", deviceName);

            Debug.Log(apiUrl + "/api/pairDevice");
            Debug.Log(form);

            using (UnityWebRequest www = UnityWebRequest.Post(apiUrl + "/api/pairDevice", form))
            {
                yield return www.SendWebRequest();

                if (www.isNetworkError || www.isHttpError)
                {
                    Debug.Log(www.error);
                }
                else
                {
                    Debug.Log(www.downloadHandler.text);
                }
            }
        }

        public static IEnumerator Check(int userId,
                          string pairToken,     // ie: "98hbieg87-ilulieugil-dilufkucy"
                          string deviceToken,   // ie: "iug893-kjg-fiug23"
                          string timezone,      // ie: "Australia/Brisbane"
                          int childId,
                          int[] activities,
                          bool log = false,
                          bool staging = false  // INTERNAL USE ONLY!
                         ) {
            UnityWebRequest www = UnityWebRequest.Get("http://www.my-server.com");
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log(www.error);
            }
            else
            {
                // Show results as text
                Debug.Log(www.downloadHandler.text);

                // Or retrieve results as binary data
                byte[] results = www.downloadHandler.data;
            }
        }
    }
}
