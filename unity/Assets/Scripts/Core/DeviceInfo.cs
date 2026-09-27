using System;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// Which device the game runs on. Phones and tablets get the touch controls and the mobile defaults. Can be forced
    /// with the -forceMobile command-line argument (editor tests) or the PlayerPrefs key below (debugging on a PC).
    /// </summary>
    public static class DeviceInfo
    {
        public const string c_ForceMobileKey = "debug.forceMobile";
        private static bool? s_IsMobile;

        public static bool IsMobile
        {
            get
            {
                if (s_IsMobile == null)
                    s_IsMobile = Application.isMobilePlatform ||
                                 Array.IndexOf(Environment.GetCommandLineArgs(), "-forceMobile") >= 0 ||
                                 PlayerPrefs.GetInt(c_ForceMobileKey, 0) == 1;
                return s_IsMobile.Value;
            }
        }
    }
}
