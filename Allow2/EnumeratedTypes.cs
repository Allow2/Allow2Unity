using System;
namespace Application
{
    /// <summary>
    /// Environment: Use Production ONLY (staging is for internal testing).
    /// </summary>
    public enum EnvType { Production, /*Sandbox,*/ Staging }

    /// <summary>
    /// Activities: These are the current activities for Allow2.
    /// </summary>
    public enum Activity {
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
}
