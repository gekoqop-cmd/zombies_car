using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>Physics layer names (configured by Tools/Setup/Configure Project) and cached masks.</summary>
    public static class Layers
    {
        public const string c_Player = "Player";
        public const string c_Obstacle = "Obstacle";
        public const string c_Ground = "Ground";
        public const string c_Projectile = "Projectile";
        public const string c_CameraBounds = "CameraBounds";

        public const int c_PlayerIndex = 8;
        public const int c_ObstacleIndex = 9;
        public const int c_GroundIndex = 10;
        public const int c_ProjectileIndex = 11;
        public const int c_CameraBoundsIndex = 12;

        /// <summary>Everything a projectile stops at.</summary>
        public static int ProjectileBlockers => 1 << c_ObstacleIndex;
    }
}
