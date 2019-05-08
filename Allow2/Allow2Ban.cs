//
//  Allow2Unity
//  Allow2Ban.cs
//
//  Created by Andrew Longhorn in May 2019.
//  Copyright © 2019 Allow2 Pty Ltd. All rights reserved.
//
// LICENSE:
//  See LICENSE file in root directory
//

using System;
using Allow2_SimpleJSON;

namespace Allow2
{
    public class Allow2Ban
    {
        public int Id { get; private set; }
        public string Title { get; private set; }
        public DateTime AppliedAt { get; private set; }
        public int Duration { get; private set; }
        public bool Selected { get; private set; }

        public Allow2Ban(string name, JSONNode val)
        {
            Id = val["id"].AsInt;
            Title = name;
            AppliedAt = Allow2.Epoch.AddSeconds(val["appliedAt"].AsInt).ToUniversalTime();
            Duration = val["durationMinutes"].AsInt;
            Selected = false;
        }
    }
}