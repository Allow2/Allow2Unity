//
//  Allow2Unity
//  Allow2Child.cs
//
//  Created by Andrew Longhorn in May 2019.
//  Copyright © 2019 Allow2 Pty Ltd. All rights reserved.
//
// LICENSE:
//  See LICENSE file in root directory
//

namespace Allow2
{
    public class Allow2Child   // shortcut implementation for now
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public string Pin { get; private set; }

        Allow2Child(int id, string name, string pin) {
            Id = id;
            Name = name;
            Pin = pin;
        }
    }
}