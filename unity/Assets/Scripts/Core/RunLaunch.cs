using UnityEngine;

namespace ZombiCar.Core
{
    /// <summary>
    /// How the next map starts, chosen in the menu before it loads: the difficulty and whether the run goes on from
    /// its saved checkpoint (<see cref="MetaProgress.GetCheckpoint"/>). The game-over screen's "ЗАНОВО" keeps both.
    /// </summary>
    public static class RunLaunch
    {
        /// <summary>The difficulty of the next run (a map opened in the editor plays the last one chosen).</summary>
        public static Difficulty Difficulty { get; set; } = Difficulty.Normal;

        /// <summary>The next run goes on from the saved checkpoint of its map and difficulty, if there is one.</summary>
        public static bool Resume { get; set; }

        /// <summary>The menu set the launch (a map opened in the editor takes the stored difficulty instead).</summary>
        public static bool Chosen { get; private set; }

        public static void Choose(Difficulty difficulty, bool resume)
        {
            Difficulty = difficulty;
            Resume = resume && DifficultyRules.HasCheckpoints(difficulty);
            Chosen = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Difficulty = Difficulty.Normal;
            Resume = false;
            Chosen = false;
        }
    }
}
