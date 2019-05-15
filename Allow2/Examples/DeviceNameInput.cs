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

    public class DeviceNameInput : MonoBehaviour
    {

        public InputField inputField;
        public RawImage qrImage;

        /// <summary>
        /// On creating the field, we pre-populate it with the system device name.
        /// This will auto-trigger the "InputValueChanged" and update the QR Code for pairing.
        /// </summary>
        void Awake()
        {
            inputField.text = SystemInfo.deviceName;
        }

        /// <summary>
        /// When the input value changes, generate a new QR Code, so the user can interactively edit the name and the pairing process uses that name.
        /// </summary>
        /// <param name="input">Input.</param>
        public void InputValueChanged(string input)
        {
            Allow2.GetQR(this, input, delegate (string err, Texture2D qrCode)
            {
                Debug.Log("Input Value qrcode error: " + (err ?? "No Error") + " : " + (qrCode != null ? qrCode.width.ToString() + "," + qrCode.height.ToString() : "no"));
                qrImage.GetComponent<RawImage>().texture = qrCode;
            });
        }
    }
}
