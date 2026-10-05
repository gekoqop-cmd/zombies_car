using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Progression
{
    /// <summary>
    /// The coins a run pays for the cases (kept scarce: a case of 100-150 takes a few runs, a full victory pays for
    /// about four): zombies killed, waves reached, the scrap left exchanged, the victory reward — all of it times the
    /// difficulty's share. Counted since the run's base (<see cref="RunState.BaseWave"/>): a checkpoint pays its stage
    /// when it is saved, and a run going on from it is paid only for what came after.
    /// </summary>
    public static class CoinRules
    {
        public const int KillsPerCoin = 5;
        public const int CoinsPerWave = 2;
        public const int VictoryCoins = 250;
        public const int ScrapPerCoin = 50;

        public static int KillCoins(RunState run) => Scale(run, Mathf.Max(0, run.Kills - run.BaseKills) / KillsPerCoin);

        /// <summary>Coins for the waves from the base up to <paramref name="wave"/> (the wave reached, not cleared).</summary>
        public static int WaveCoins(RunState run, int wave) => Scale(run, Mathf.Max(0, wave - run.BaseWave) * CoinsPerWave);

        /// <summary>The scrap left exchanged: all of it on a victory, only what came since the checkpoint on a defeat.</summary>
        public static int ScrapCoins(RunState run) =>
            Scale(run, Mathf.Max(0, run.Outcome == RunOutcome.Victory ? run.Money : run.Money - run.BaseMoney) / ScrapPerCoin);

        public static int VictoryBonus(RunState run) => run.Outcome == RunOutcome.Victory ? Scale(run, VictoryCoins) : 0;

        /// <summary>The coins of a finished run (twice after the ×2 for an ad).</summary>
        public static int Total(RunState run) =>
            (KillCoins(run) + WaveCoins(run, run.Wave) + ScrapCoins(run) + VictoryBonus(run)) * (run.CoinsDoubled ? 2 : 1);

        /// <summary>The coins of a stage cleared on Easy / Normal (paid at its checkpoint; the next starts at <paramref name="nextWave"/>).</summary>
        public static int Stage(RunState run, int nextWave) => KillCoins(run) + WaveCoins(run, nextWave);

        private static int Scale(RunState run, int coins) => coins <= 0 ? 0 : Mathf.RoundToInt(coins * DifficultyRules.Coins(run.Difficulty));
    }
}
