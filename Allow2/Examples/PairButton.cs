//
// Copyright (C) 2019 Allow2
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
// the License.
//
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
// an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
// See the License for the specific language governing permissions and limitations under the License.
//
namespace Allow2.Allow2Examples
{

    using UnityEngine;
    using UnityEngine.UI;

    public class PairButton : MonoBehaviour
    {

        public InputField UsernameField;
        public InputField PasswordField;
        public InputField DeviceNameField;

        /// <summary>
        /// Manually pair with Allow2 by providing the username and password entered by the user in your pairing interface.
        /// </summary>
        public void Pair()
        {
            Debug.Log("Start Pairing");
            Allow2.Pair(
                this,
                UsernameField.text,
                PasswordField.text,
                DeviceNameField.text,
                delegate (string err, Allow2CheckResult result)
                {
                    Debug.Log("Stop Pairing");
                    Debug.Log("Pairing Error" + err);
                    if (result)
                    {
                        Debug.Log("Pairing Result" + result.ToString());
                    }
                }
            );
        }
    }
}
