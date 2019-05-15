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

    public class CheckButton : MonoBehaviour
    {

        public int childId = 68;
        public int[] activities = {
            (int)Activity.Internet,
            (int)Activity.Computer
        };

        /// <summary>
        /// Check once and log activity for the given child and activities.
        /// </summary>
        public void Check()
        {
            Debug.Log("Check");
            Allow2.Check(
               this,
               childId,
               activities,
               delegate (string err, Allow2CheckResult result)
               {
                   Debug.Log("Check Error" + err);
                   Debug.Log("Paired: " + Allow2.IsPaired);
                   if (result != null)
                   {
                       Debug.Log("Allowed: " + result.IsAllowed);
                       if (!result.IsAllowed)
                       {
                           Debug.Log(result.Explanation);
                       }
                   }
               },
               true);
        }

        /// <summary>
        /// Start a continuous check (and logging usage) for the given child and activities.
        /// </summary>
        public void StartChecking()
        {
            Debug.Log("Start Checking");
            Allow2.StartChecking(
                this,
                childId,
                activities,
                delegate (string err, Allow2CheckResult result)
                {
                    Debug.Log("Check Error" + err);
                    Debug.Log("Paired: " + Allow2.IsPaired);
                    if (result != null)
                    {
                        Debug.Log("Allowed: " + result.IsAllowed);
                        if (!result.IsAllowed)
                        {
                            Debug.Log(result.Explanation);
                        }
                    }
                },
                true);
        }

        /// <summary>
        /// Stop checking and logging.
        /// </summary>
        public void StopChecking()
        {
            Debug.Log("Stop Checking");
            Allow2.StopChecking();
        }
    }
}
