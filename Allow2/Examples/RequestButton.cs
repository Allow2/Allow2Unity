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

    public class RequestButton : MonoBehaviour
    {

        public int dayTypeId = 23;
        public int[] bansToLift = {};

        /// <summary>
        /// Send a request on behalf of the child.
        /// </summary>
        public void Request()
        {
            Debug.Log("Request");
            Allow2.childId = 68;
            Allow2.Request(
                this,
                dayTypeId,
                bansToLift,
                "test",
                delegate (string err, Allow2CheckResult result)
                {
                   Debug.Log("Request Error" + err);
                   if (result != null)
                   {
                       Debug.Log(result.Explanation);
                   }
               });
        }
    }
}
