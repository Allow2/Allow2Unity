//
//  Allow2Unity
//  EnumeratedTypes.cs
//
//  Created by Andrew Longhorn in Jan 2019.
//  Copyright © 2019 Allow2 Pty Ltd. All rights reserved.
//
// LICENSE:
//  See LICENSE file in root directory
//

namespace Allow2
{
    /// <summary>
    /// Environment: Use Production ONLY (staging is for internal testing).
    /// </summary>
    public enum EnvType {
        Production,
        // Sandbox,
        Staging
    }

    /// <summary>
    /// Activities: These are the current activities for Allow2.
    /// </summary>
    public enum Activity: int {
        Internet = 1,
        Computer = 2,
        Gaming = 3,
        Message = 4,
        JunkFood = 5,
        Lollies = 6,
        Electricity = 7,
        ScreenTime = 8,
        Social = 9,
        PhoneTime = 10
    }

    public static class Allow2Error
    {
        public const string NotPaired = "NotPaired";
        public const string AlreadyPaired = "AlreadyPaired";
        public const string MissingChildId = "MissingChildId";
        public const string NotAuthorised = "NotAuthorised";
        public const string InvalidResponse = "InvalidResponse";
        public const string NoConnection = "NoConnection";
    }

}
