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

        public DateTime Expires {
            get {
                return new DateTime();
            }
        }
    }
}
