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
        public const string c_Enemy = "Enemy";
        public const string c_CarObstacle = "CarObstacle";

        public const int c_PlayerIndex = 8;
        public const int c_ObstacleIndex = 9;
        public const int c_GroundIndex = 10;
        public const int c_ProjectileIndex = 11;
        public const int c_CameraBoundsIndex = 12;
        public const int c_EnemyIndex = 13;
        // The player's car: its own layer so bullets pass through it (the player can shoot zombies hiding behind
        // it) while it still blocks the player's CharacterController and carves the zombie NavMesh like Obstacle.
        public const int c_CarObstacleIndex = 14;

        /// <summary>Everything a projectile stops at.</summary>
        public static int ProjectileBlockers => (1 << c_ObstacleIndex) | (1 << c_EnemyIndex);

        /// <summary>Colliders that bullets damage.</summary>
        public static int Enemies => 1 << c_EnemyIndex;

        /// <summary>Geometry the zombie NavMesh is baked from.</summary>
        public static int NavMeshSources => (1 << c_GroundIndex) | (1 << c_ObstacleIndex);
    }
}
