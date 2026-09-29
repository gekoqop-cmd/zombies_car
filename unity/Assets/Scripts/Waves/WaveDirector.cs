using UnityEngine;
using ZombiCar.Enemies;

namespace ZombiCar.Waves
{
    /// <summary>
    /// Runs the waves of a map from <see cref="WaveConfig"/>: a countdown, then every wave spawns its zombies spread
    /// over the first part of the wave time (never more than MaxAlive at once). A wave ends when its zombies are all
    /// dead or its time is up (zombies that did not fit are carried into the next wave), then the break counts down
    /// to the next one. Boss waves (10 / 20 / 30) bring their boss out at the farthest spawn point at the start, with
    /// fewer regular zombies, and last until the boss and every zombie are dead (no time limit).
    /// Publishes the wave number, countdown, phase and boss to <see cref="WaveState"/> (the HUD, rewards, music and
    /// the between-waves services listen to it); during a break the HUD shows the upcoming wave.
    /// </summary>
    public sealed class WaveDirector : MonoBehaviour
    {
        private const int c_PrewarmPerType = 3;
        private const float c_SpawnIntervalJitter = 0.35f;

        private enum Phase { Countdown, Wave, Finished }

        [SerializeField] private WaveConfig m_Config;
        [SerializeField] private WaveState m_WaveState;
        [SerializeField] private ZombieSpawner m_Spawner;

        private Phase m_Phase;
        private int m_Wave;
        private float m_TimeLeft;
        private int m_ToSpawn;
        private float m_SpawnInterval;
        private float m_NextSpawnTime;
        private ZombieController m_Boss;
        private bool m_BossWave;

        public int Wave => m_Wave;
        public bool IsWaveRunning => m_Phase == Phase.Wave;
        public bool IsFinished => m_Phase == Phase.Finished;

        /// <summary>The boss of the running wave (null when there is none or it died).</summary>
        public ZombieController Boss => m_Boss != null && m_Boss.IsAlive ? m_Boss : null;

        private void Start()
        {
            if (m_Config == null || m_Spawner == null)
            {
                Debug.LogError("[WaveDirector] Wave config or spawner is missing.", this);
                enabled = false;
                return;
            }
            foreach (WaveConfig.Entry entry in m_Config.Enemies)
            {
                if (entry.Zombie != null)
                    m_Spawner.Prewarm(entry.Zombie, c_PrewarmPerType);
            }
            foreach (WaveConfig.BossWave boss in m_Config.Bosses)
            {
                if (boss != null && boss.Boss != null)
                    m_Spawner.Prewarm(boss.Boss, 1);                       // no hitch when the boss comes out
            }
            m_Wave = 1;
            if (m_WaveState != null)
                m_WaveState.Begin(m_Config.WaveCount);
            StartCountdown(m_Config.FirstWaveDelay);
        }

        private void Update()
        {
            if (m_Phase == Phase.Finished)
                return;
            m_TimeLeft = Mathf.Max(0f, m_TimeLeft - Time.deltaTime);

            if (m_Phase == Phase.Countdown)
            {
                if (m_TimeLeft <= 0f)
                    StartWave();
            }
            else
            {
                SpawnDue();
                if (m_BossWave && m_Boss != null && !m_Boss.IsAlive)
                    BossDown();
                bool cleared = m_ToSpawn == 0 && m_Spawner.Alive == 0 && (!m_BossWave || m_Boss == null);
                bool timeUp = !m_BossWave && m_TimeLeft <= 0f;
                if (cleared || timeUp)
                    EndWave();
            }
            Publish();
        }

        private void StartCountdown(float seconds)
        {
            m_Phase = Phase.Countdown;
            m_TimeLeft = seconds;
            Publish();
        }

        /// <summary>Starts the next wave now (tests; the countdown does it otherwise).</summary>
        public void StartWave()
        {
            m_Phase = Phase.Wave;
            m_TimeLeft = m_Config.WaveDuration;
            WaveConfig.BossWave boss = m_Config.BossFor(m_Wave);
            m_BossWave = boss != null;
            int regular = m_Config.EnemiesInWave(m_Wave);
            if (m_BossWave)
                regular = Mathf.RoundToInt(regular * boss.RegularShare);
            m_ToSpawn += regular;
            m_SpawnInterval = m_Config.WaveDuration * m_Config.SpawnWindow / Mathf.Max(1, m_ToSpawn);
            m_NextSpawnTime = Time.time;
            Debug.Log($"[WaveDirector] Wave {m_Wave}: {m_ToSpawn} zombies, one every {m_SpawnInterval:F1} s" +
                      (m_BossWave ? $", boss {boss.Boss.DisplayName}." : "."));
            if (m_WaveState != null)
                m_WaveState.StartWave(m_Wave);
            if (m_BossWave)
                SpawnBoss(boss.Boss);
        }

        /// <summary>Jumps to <paramref name="wave"/> (tests): the countdown to it starts now.</summary>
        public void SkipTo(int wave, float countdown = 1f)
        {
            m_Wave = Mathf.Clamp(wave, 1, m_Config.WaveCount);
            m_ToSpawn = 0;
            if (m_BossWave || m_Boss != null)
            {
                m_BossWave = false;                          // the boss of the wave left behind is gone from the HUD
                m_Boss = null;
                if (m_WaveState != null)
                    m_WaveState.SetBoss(null);
            }
            StartCountdown(countdown);
        }

        // Play-mode shortcuts to the boss waves (the component's context menu in the Inspector).
        [ContextMenu("Debug/Skip to wave 10 (boss)")]
        private void DebugWave10() => SkipTo(10, 3f);

        [ContextMenu("Debug/Skip to wave 20 (boss)")]
        private void DebugWave20() => SkipTo(20, 3f);

        [ContextMenu("Debug/Skip to wave 30 (boss)")]
        private void DebugWave30() => SkipTo(30, 3f);

        private void SpawnBoss(ZombieConfig boss)
        {
            m_Boss = m_Spawner.SpawnFar(boss);
            if (m_Boss == null)
            {
                Debug.LogError($"[WaveDirector] Boss {boss.name} could not be placed on the NavMesh.", this);
                m_BossWave = false;
                return;
            }
            if (m_WaveState != null)
                m_WaveState.SetBoss(boss.DisplayName);
        }

        private void BossDown()
        {
            Debug.Log($"[WaveDirector] Boss {m_Boss.Config.DisplayName} killed in wave {m_Wave}.");
            m_Boss = null;
            if (m_WaveState != null)
                m_WaveState.SetBoss(null);
        }

        private void SpawnDue()
        {
            while (m_ToSpawn > 0 && Time.time >= m_NextSpawnTime && m_Spawner.Alive < m_Config.MaxAlive)
            {
                if (!m_Spawner.TrySpawn(m_Config.Pick(m_Wave)))
                    return;
                m_ToSpawn--;
                // Counted from now: after a pause at MaxAlive the zombies trickle in again instead of all at once.
                m_NextSpawnTime = Time.time + m_SpawnInterval * Random.Range(1f - c_SpawnIntervalJitter, 1f + c_SpawnIntervalJitter);
            }
        }

        private void EndWave()
        {
            m_BossWave = false;
            m_Boss = null;
            bool last = m_Wave >= m_Config.WaveCount;
            if (m_WaveState != null)
                m_WaveState.EndWave(m_Wave, last);
            if (last)
            {
                m_Phase = Phase.Finished;
                m_TimeLeft = 0f;
                Debug.Log($"[WaveDirector] All {m_Config.WaveCount} waves survived.");
                Publish();
                return;
            }
            m_Wave++;
            StartCountdown(m_Config.BreakBetweenWaves);
        }

        // WaveState ignores repeated values, so the HUD only updates once per second.
        private void Publish()
        {
            if (m_WaveState != null)
                m_WaveState.Set(m_Wave, Mathf.CeilToInt(m_TimeLeft));
        }
    }
}
