//
//  Allow2Unity
//  Allow2CheckResult.cs
//
//  Created by Andrew Longhorn in Jan 2019.
//  Copyright © 2019 Allow2 Pty Ltd. All rights reserved.
//
// LICENSE:
//  See LICENSE file in root directory
//

using System;
using System.Collections.Generic;
using Allow2_SimpleJSON;

namespace Allow2
{
    // Allow2Response JSON structure
    //{
    //    "allowed":false,
    //    "activities":{
    //        "1":{
    //            "id":1,
    //            "name":"Internet",
    //            "timed":true,
    //            "units":"minutes",
    //            "banned":false,
    //            "remaining":0,
    //            "cached":false,
    //            "expires":1557067560,
    //            "timeBlock":{
    //                "allowed":true,
    //                "remaining":495
    //            }
    //        },
    //        "2":{
    //            "id":2,
    //            "name":"Computer",
    //            "timed":true,
    //            "units":"minutes",
    //            "banned":false,
    //            "remaining":0,
    //            "cached":false,
    //            "expires":1557067560,
    //            "timeBlock":{
    //                "allowed":true,
    //                "remaining":375
    //            }
    //        }
    //    },
    //    "dayTypes":{
    //        "today":{"id":57,"name":"Weekend"},
    //        "tomorrow":{"id":61,"name":"School Day"}
    //    },
    //    "allDayTypes":[
    //        {"id":57,"name":"Weekend"},
    //        {"id":59,"name":"Weekday"},
    //        {"id":61,"name":"School Day"},
    //        {"id":64,"name":"Sick Day"},
    //        {"id":66,"name":"Holiday"},
    //        {"id":16997,"name":"No Limit"}
    //    ],
    //    "children":[
    //        {"id":681,"name":"Bob","pin":"1234"},
    //        {"id":639,"name":"Mary","pin":"4567"},
    //        {"id":21423,"name":"Milly","pin":"5566"}
    //    ],
    //    "subscription":{
    //        "active":false,
    //        "type":1,
    //        "maxChildren":6,
    //        "childCount":3,
    //        "deviceCount":12,
    //        "serviceCount":0,
    //        "financial":false
    //    }
    //}

    public class Allow2CheckResult : JSONObject   // shortcut implementation for now
    {
        //public Allow2CheckResult()
        //{
        //}

        public JSONNode Activities {
            get {
                return this["activities"];
            }
        }

        public JSONNode Subscription
        {
            get
            {
                return this["subscription"];
            }
        }

        public DateTime Expires {
            get {
                int unixTimeStamp = ((Activities != null) && (Activities[0] != null)) ?
                    this["activities"]["0"]["expires"].AsInt : 0;
                return Allow2.Epoch.AddSeconds(unixTimeStamp).ToUniversalTime(); // nineteen70.AddSeconds(unixTimeStamp).ToLocalTime();
            }
        }

        public string NeedSubscription {
            get {
                if (!Subscription["financial"].AsBool) {
                    int childCount = Subscription["childCount"].AsInt;
                    int maxChildren = Subscription["maxChildren"].AsInt;
                    //int serviceCount = subscription["serviceCount"].AsInt;
                    //int deviceCount = subscription["deviceCount"].AsInt;
                    int type = Subscription["type"].AsInt;
                        
                    if ((maxChildren > 0 ) && (childCount > maxChildren) && (type == 1)) {
                        return "Subscription Upgrade Required.";
                    }

                    return "Subscription Required.";
                }
                return null;
            }
        }
    
        public bool IsAllowed {
            get {
                return this["allowed"].AsBool;
            }
        }

        public string Explanation {
            get {
                List<string> reasons = new List<string>();
                string subscriptionString = NeedSubscription;
                if (subscriptionString != null) {
                    reasons.Add(subscriptionString);
                }
                foreach (JSONNode activity in Activities) {
                    if (activity["banned"].IsBoolean && activity["banned"].AsBool)
                    {
                        reasons.Add("You are currently banned from " + activity["name"].ToString());
                    }
                    else
                    {
                        JSONNode timeblock = activity["timeblock"];
                        if ((timeblock == null) || !timeblock["allowed"].IsBoolean || !timeblock["allowed"].AsBool)
                        {
                            reasons.Add("You cannot use " + activity["name"].ToString() + " at this time.");
                        }
                        else
                        {
                            // todo: reasons.append("You have \(activity["remaining"]) to use \(activity["name"]).")
                        }
                    }
                }
                return String.Join("/n", reasons.ToArray());
            }
        }

        public Allow2Ban[] CurrentBans {
            get {
                List<Allow2Ban> bans = new List<Allow2Ban>();
                foreach (JSONNode activity in Activities) {

                    if (activity["banned"].AsBool) {
                        //int id = activity.dictionary?["id"]?.uInt64Value,
                        string name = activity["name"].ToString();
                        if ((activity["bans"] != null) && (activity["bans"]["bans"] != null) && (activity["bans"]["bans"].IsArray))
                        {
                            JSONArray items = activity["bans"]["bans"].AsArray;
                            foreach (JSONNode item in items)
                            {
                                //bans.Add(new Allow2Ban(name, item));
                            }
                        }
                        else
                        {
                            // todo: reasons.append("You have \(activity["remaining"]) to use \(activity["name"]).")
                        }
                    }
                }
                return bans.ToArray();
            }
        }
    }

}
