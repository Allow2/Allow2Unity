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
namespace Allow2
{
    public class Allow2CheckResult : Allow2_SimpleJSON.JSONObject   // shortcut implementation for now
    {
        //public Allow2CheckResult()
        //{
        //}
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc); 

        public DateTime Expires {
            get {
                int unixTimeStamp = this["activities"]["0"]["expires"];
                return Epoch.AddSeconds(unixTimeStamp).ToUniversalTime(); // nineteen70.AddSeconds(unixTimeStamp).ToLocalTime();
            }
        }
    }
}
