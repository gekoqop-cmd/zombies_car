using UnityEngine;
using ZombiCar.Enemies;
using ZombiCar.Player;

namespace ZombiCar.Waves
{
    /// <summary>
    /// Runs the waves of a map from <see cref="WaveConfig"/>: a countdown, then every wave spawns its zombies spread
    /// over the first part of the wave time (never more than MaxAlive at once). A wave ends when its zombies are all
    /// dead or its time is up (zombies that did not fit are carried into the next wave), then the break counts down
    /// to the next one. Publishes the wave number, countdown and phase to <see cref="WaveState"/> (the HUD, rewards and
    /// the between-waves services listen to its wave start / end); during a break the HUD shows the upcoming wave.
    /// </summary>
    public sealed class WaveDirector : MonoBehaviour
    {
        private const int c_PrewarmPerType = 3;
        private const float c_SpawnIntervalJitter = 0.35f;

        private enum Phase { Countdown, Wave, Finished }

        [SerializeField] private WaveConfig m_Config;
        [SerializeField] private WaveState m_WaveState;
        [SerializeField] private PlayerWeapon m_PlayerWeapon;
        [SerializeField] private ZombieSpawner m_Spawner;

        private Phase m_Phase;
        private int m_Wave;
        private float m_TimeLeft;
        private int m_ToSpawn;
        private float m_SpawnInterval;
        private float m_NextSpawnTime;

        public int Wave => m_Wave;
        public bool IsWaveRunning => m_Phase == Phase.Wave;

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
                bool cleared = m_ToSpawn == 0 && m_Spawner.Alive == 0;
                if (cleared || m_TimeLeft <= 0f)
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

        private void StartWave()
        {
            m_Phase = Phase.Wave;
            m_TimeLeft = m_Config.WaveDuration;
            m_ToSpawn += m_Config.EnemiesInWave(m_Wave);
            m_SpawnInterval = m_Config.WaveDuration * m_Config.SpawnWindow / Mathf.Max(1, m_ToSpawn);
            m_NextSpawnTime = Time.time;
            Debug.Log($"[WaveDirector] Wave {m_Wave}: {m_ToSpawn} zombies, one every {m_SpawnInterval:F1} s.");
            if (m_WaveState != null)
                m_WaveState.StartWave(m_Wave);
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
            if (m_PlayerWeapon != null)
                m_PlayerWeapon.Resupply(m_Config.AmmoResupplyPerWave);
            bool last = m_Wave >= m_Config.WaveCount;
            if (m_WaveState != null)
                m_WaveState.EndWave(m_Wave, last);
            if (last)
            {
                m_Phase = Phase.Finished;
                m_TimeLeft = 0f;
                Debug.Log($"[WaveDirector] All {m_Config.WaveCount} waves survived.");
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
