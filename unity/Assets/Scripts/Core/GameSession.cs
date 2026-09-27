using System;
using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>Whether a map is being played (set by <see cref="MapBootstrap"/>); menus are not gameplay.</summary>
    public static class GameSession
    {
        private static int s_Maps;

        public static event Action Changed;

        public static bool InMap => s_Maps > 0;

        public static void EnterMap()
        {
            s_Maps++;
            Changed?.Invoke();
        }

        public static void ExitMap()
        {
            s_Maps = Mathf.Max(0, s_Maps - 1);
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Maps = 0;
            Changed = null;
        }
    }
}
