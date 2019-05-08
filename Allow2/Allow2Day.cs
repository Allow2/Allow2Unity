//
//  Allow2Unity
//  Allow2Day.cs
//
//  Created by Andrew Longhorn in May 2019.
//  Copyright © 2019 Allow2 Pty Ltd. All rights reserved.
//
// LICENSE:
//  See LICENSE file in root directory
//

using Allow2_SimpleJSON;

namespace Allow2
{
    public class Allow2Day   // shortcut implementation for now
    {
        public int Id { get; private set; }
        public string Name { get; private set; }

        public Allow2Day(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public Allow2Day(JSONNode json)
        {
            Id = json["id"].AsInt;
            Name = json["name"];
        }

        public static Allow2Day DayOrNull(JSONNode json)
        {
            if (json == null) {
                return null;
            }

            return new Allow2Day(json["id"].AsInt, json["name"]);
        }
    }
}