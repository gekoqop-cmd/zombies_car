using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>How hard a run is: chosen on the difficulty screen before a map starts. Saved as its number.</summary>
    public enum Difficulty
    {
        Easy = 0,
        Normal = 1,
        Hardcore = 2,
    }

    /// <summary>
    /// What each difficulty changes. Easy: weaker and fewer zombies, smaller rewards. Normal: the game as it was tuned.
    /// Hardcore: more and tougher zombies, bigger rewards, no checkpoints (one run of all 30 waves). Easy and Normal
    /// save a checkpoint after every boss (waves 11 and 21 to go on from). Each difficulty has its own leaderboard.
    /// </summary>
    public static class DifficultyRules
    {
        public const int Count = 3;

        /// <summary>The waves a checkpoint run goes on from (after the bosses of waves 10 and 20).</summary>
        public static readonly int[] CheckpointWaves = { 11, 21 };

        public static float ZombieHealth(Difficulty d) => d switch { Difficulty.Easy => 0.75f, Difficulty.Hardcore => 1.35f, _ => 1f };
        public static float ZombieDamage(Difficulty d) => d switch { Difficulty.Easy => 0.75f, Difficulty.Hardcore => 1.3f, _ => 1f };
        public static float ZombieCount(Difficulty d) => d switch { Difficulty.Easy => 0.85f, Difficulty.Hardcore => 1.3f, _ => 1f };

        /// <summary>Scrap from zombies and waves.</summary>
        public static float Scrap(Difficulty d) => d switch { Difficulty.Easy => 0.85f, Difficulty.Hardcore => 1.25f, _ => 1f };

        /// <summary>Coins at the end of a run.</summary>
        public static float Coins(Difficulty d) => d switch { Difficulty.Easy => 0.75f, Difficulty.Hardcore => 1.6f, _ => 1f };

        public static bool HasCheckpoints(Difficulty d) => d != Difficulty.Hardcore;

        public static string Title(Difficulty d) => d switch { Difficulty.Easy => "ЛЁГКИЙ", Difficulty.Hardcore => "ХАРДКОР", _ => "СРЕДНИЙ" };

        public static string Tagline(Difficulty d) => d switch
        {
            Difficulty.Easy => "МЕНЬШЕ ЗОМБИ, МЕНЬШЕ НАГРАДА",
            Difficulty.Hardcore => "БОЛЬШЕ ЗОМБИ, БОЛЬШЕ НАГРАДЫ",
            _ => "ЧЕСТНЫЙ БОЙ",
        };

        /// <summary>The badge colour: green, orange, red.</summary>
        public static Color Color(Difficulty d) => d switch
        {
            Difficulty.Easy => new Color(0.36f, 0.82f, 0.32f, 1f),
            Difficulty.Hardcore => new Color(0.92f, 0.2f, 0.16f, 1f),
            _ => new Color(0.98f, 0.58f, 0.14f, 1f),
        };

        /// <summary>
        /// The Yandex Games leaderboard of the difficulty: its technical name in the developer console, which takes only
        /// Latin letters and digits ([a-zA-Z0-9], no "_").
        /// </summary>
        public static string Leaderboard(Difficulty d) => d switch
        {
            Difficulty.Easy => "TimeEasy",                   // as created in the console (bestTimeEasy was refused)
            Difficulty.Hardcore => "timeHardcore",
            _ => "bestTimeNormal",
        };

        public static Difficulty Clamp(int value) => (Difficulty)Mathf.Clamp(value, 0, Count - 1);
    }
}
