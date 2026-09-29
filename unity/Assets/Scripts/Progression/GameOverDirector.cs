using System.Collections;
using System.Linq;
using UnityEngine;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Enemies;
using ZombiCar.Player;
using ZombiCar.Props;
using ZombiCar.Waves;

namespace ZombiCar.Progression
{
    /// <summary>
    /// Ends the run on a map: the car destroyed or the player down is a defeat, all waves survived is the victory.
    /// The outcome goes to <see cref="RunState"/> at once (no new waves, no more cards or workshop; the HUD's game-over
    /// screen shows it), the music fades out, and after a short moment in which the end can be seen (the car smoking,
    /// the player falling) the game pauses behind the screen.
    /// The last boss's death is the victory finale: slow motion, a huge explosion where it fell, every zombie on the
    /// map drops, more blasts around (the HUD adds "ГОРОД СПАСЁН!", the fireworks and the fanfare); the best time is
    /// saved for the menu and the run's time is sent to the Yandex "best_time" leaderboard
    /// (<see cref="YandexSdk.SubmitTimeScore"/>). A defeat can be taken back once per run (<see cref="RunState.Revive"/> after a rewarded
    /// ad): the player and the car are back up, the zombies around them are blown away and the waves go on.
    /// </summary>
    public sealed class GameOverDirector : MonoBehaviour
    {
        [SerializeField] private RunState m_Run;
        [SerializeField] private CarHealth m_Car;
        [SerializeField] private PlayerHealth m_Player;
        [SerializeField] private WaveState m_Waves;
        [SerializeField] private WaveDirector m_Director;
        [Tooltip("The finale's blasts (the barrels' comic explosion).")]
        [SerializeField, OptionalReference] private ComicExplosion m_Explosion;
        [Tooltip("Real seconds between the end and the pause behind the game-over screen.")]
        [SerializeField, Min(0f)] private float m_FreezeDelay = 1.3f;
        [Tooltip("Real seconds of the victory finale before the game pauses behind the victory screen.")]
        [SerializeField, Min(0f)] private float m_FinaleTime = 4.2f;
        [SerializeField, Range(0.05f, 1f)] private float m_FinaleSlowMotion = 0.25f;
        [Tooltip("Zombies this close to the player or the car are blown away by a revive.")]
        [SerializeField, Min(0f)] private float m_ReviveClearRadius = 7f;

        /// <summary>The victory finale is playing (tests).</summary>
        public bool FinalePlaying { get; private set; }

        private void OnEnable()
        {
            m_Car.Destroyed += OnCarDestroyed;
            m_Player.Died += OnPlayerDied;
            m_Waves.WaveEnded += OnWaveEnded;
            m_Run.Revived += OnRevived;
            if (m_Director != null)
                m_Director.FinalBossKilled += OnFinalBossKilled;
        }

        private void OnDisable()
        {
            m_Car.Destroyed -= OnCarDestroyed;
            m_Player.Died -= OnPlayerDied;
            m_Waves.WaveEnded -= OnWaveEnded;
            m_Run.Revived -= OnRevived;
            if (m_Director != null)
                m_Director.FinalBossKilled -= OnFinalBossKilled;
            GamePause.SetSlowMotion(1f);
        }

        private void OnCarDestroyed() => End(RunOutcome.CarDestroyed);

        private void OnPlayerDied() => End(RunOutcome.PlayerDied);

        private void OnWaveEnded(int wave)
        {
            if (m_Waves.Phase == WavePhase.Finished)
                End(RunOutcome.Victory);
        }

        /// <summary>Ends the run (the first outcome wins).</summary>
        public void End(RunOutcome outcome) => End(outcome, false);

        private void End(RunOutcome outcome, bool finale)
        {
            if (m_Run.IsOver)
                return;
            Debug.Log($"[GameOverDirector] Run over: {outcome}, wave {m_Run.Wave}, kills {m_Run.Kills}, scrap earned {m_Run.MoneyEarned}.");
            m_Run.End(outcome);
            if (m_Director != null)
                m_Director.enabled = false;
            AudioManager.PlayMusic(null);
            if (m_Run.Map != null)
                MetaProgress.RecordRun(m_Run.Map.name, m_Run.Wave, m_Run.Kills, m_Run.BossKills, outcome == RunOutcome.Victory,
                    !m_Run.WasRevived);
            if (outcome == RunOutcome.Victory)
            {
                // Only a victory goes to the "best time" leaderboard (a defeat sends nothing).
                YandexSdk.SubmitTimeScore(Mathf.RoundToInt(m_Run.PlayTime));
                if (m_Run.Map != null)
                {
                    bool best = MetaProgress.RecordVictory(m_Run.Map.name, m_Run.PlayTime);
                    Debug.Log($"[GameOverDirector] {m_Run.Map.name} beaten in {m_Run.PlayTime:F0} s{(best ? " (best time)" : "")}.");
                }
            }
            StopAllCoroutines();
            if (finale)
                StartCoroutine(Finale());
            else
            {
                CameraShake.Add(outcome == RunOutcome.Victory ? 0.2f : 0.5f);
                // A defeat pauses almost at once: the defeat screen is already up, nothing should go on behind it.
                StartCoroutine(Freeze(outcome == RunOutcome.Victory ? m_FreezeDelay : Mathf.Min(m_FreezeDelay, 0.25f)));
            }
        }

        private IEnumerator Freeze(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            GamePause.Set(GamePause.Reason.GameOver, true);
        }

        // ---------------------------------------------------------------- victory finale
        private Vector3 m_FinalePoint;

        private void OnFinalBossKilled(Vector3 where)
        {
            m_FinalePoint = where;
            End(RunOutcome.Victory, true);
        }

        private IEnumerator Finale()
        {
            FinalePlaying = true;
            GamePause.SetSlowMotion(m_FinaleSlowMotion);
            Blast(m_FinalePoint, 6f, 2.4f);
            CameraShake.Add(0.9f);
            // Every zombie drops, the nearest first: a wave of deaths rolling out from where the boss fell.
            ZombieController[] zombies = Object.FindObjectsByType<ZombieController>(FindObjectsInactive.Exclude)
                .Where(z => z.IsAlive)
                .OrderBy(z => (z.transform.position - m_FinalePoint).sqrMagnitude)
                .ToArray();
            float step = zombies.Length > 0 ? Mathf.Min(0.06f, 0.9f / zombies.Length) : 0f;
            foreach (ZombieController z in zombies)
            {
                if (z != null && z.IsAlive)
                    z.Kill(false);
                yield return new WaitForSecondsRealtime(step);
            }
            // More blasts around the lot, like fireworks on the ground.
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSecondsRealtime(0.35f);
                Vector2 r = Random.insideUnitCircle * 7f;
                Blast(m_FinalePoint + new Vector3(r.x, 0f, r.y), 3.5f, 1.4f);
                CameraShake.Add(0.3f);
            }
            yield return new WaitForSecondsRealtime(0.6f);
            GamePause.SetSlowMotion(1f);
            float waited = 0.9f + 3f * 0.35f + 0.6f;
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, m_FinaleTime - waited));
            FinalePlaying = false;
            GamePause.Set(GamePause.Reason.GameOver, true);
        }

        private void Blast(Vector3 at, float radius, float size)
        {
            if (m_Explosion != null)
                ComicExplosion.Spawn(m_Explosion, at, radius, size);
        }

        // ---------------------------------------------------------------- revive
        private void OnRevived()
        {
            StopAllCoroutines();
            FinalePlaying = false;
            GamePause.SetSlowMotion(1f);
            GamePause.Set(GamePause.Reason.GameOver, false);
            m_Player.Revive();
            m_Car.Revive();
            // Breathing room: the zombies right around the player and the car are blown away.
            Vector3 player = m_Player.transform.position;
            Vector3 car = m_Car.transform.position;
            float r2 = m_ReviveClearRadius * m_ReviveClearRadius;
            foreach (ZombieController z in Object.FindObjectsByType<ZombieController>(FindObjectsInactive.Exclude))
            {
                if (z.IsAlive && (z.Config == null || !z.Config.IsBoss) && ((z.transform.position - player).sqrMagnitude < r2 || (z.transform.position - car).sqrMagnitude < r2))
                    z.Kill(false);
            }
            Blast(player, m_ReviveClearRadius, 1.6f);
            CameraShake.Add(0.4f);
            if (m_Director != null)
                m_Director.enabled = true;
            MapConfig map = m_Run.Map;
            if (map != null)
                AudioManager.PlayMusic(m_Waves.HasBoss && map.BossMusic != null ? map.BossMusic : map.Music);
            Debug.Log("[GameOverDirector] Revived: the run goes on.");
        }
    }
}
