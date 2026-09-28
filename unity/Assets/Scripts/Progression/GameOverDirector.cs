using System.Collections;
using UnityEngine;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Player;
using ZombiCar.Waves;

namespace ZombiCar.Progression
{
    /// <summary>
    /// Ends the run on a map: the car destroyed or the player down is a defeat, all waves survived is the victory.
    /// The outcome goes to <see cref="RunState"/> at once (no new waves, no more cards or workshop; the HUD's game-over
    /// screen shows it), the music fades out, and after a short moment in which the end can be seen (the car smoking,
    /// the player falling) the game pauses behind the screen.
    /// </summary>
    public sealed class GameOverDirector : MonoBehaviour
    {
        [SerializeField] private RunState m_Run;
        [SerializeField] private CarHealth m_Car;
        [SerializeField] private PlayerHealth m_Player;
        [SerializeField] private WaveState m_Waves;
        [SerializeField] private WaveDirector m_Director;
        [Tooltip("Real seconds between the end and the pause behind the game-over screen.")]
        [SerializeField, Min(0f)] private float m_FreezeDelay = 1.3f;

        private void OnEnable()
        {
            m_Car.Destroyed += OnCarDestroyed;
            m_Player.Died += OnPlayerDied;
            m_Waves.WaveEnded += OnWaveEnded;
        }

        private void OnDisable()
        {
            m_Car.Destroyed -= OnCarDestroyed;
            m_Player.Died -= OnPlayerDied;
            m_Waves.WaveEnded -= OnWaveEnded;
        }

        private void OnCarDestroyed() => End(RunOutcome.CarDestroyed);

        private void OnPlayerDied() => End(RunOutcome.PlayerDied);

        private void OnWaveEnded(int wave)
        {
            if (m_Waves.Phase == WavePhase.Finished)
                End(RunOutcome.Victory);
        }

        /// <summary>Ends the run (the first outcome wins).</summary>
        public void End(RunOutcome outcome)
        {
            if (m_Run.IsOver)
                return;
            Debug.Log($"[GameOverDirector] Run over: {outcome}, wave {m_Run.Wave}, kills {m_Run.Kills}, scrap earned {m_Run.MoneyEarned}.");
            m_Run.End(outcome);
            if (m_Director != null)
                m_Director.enabled = false;
            AudioManager.PlayMusic(null);
            CameraShake.Add(outcome == RunOutcome.Victory ? 0.2f : 0.5f);
            StartCoroutine(Freeze());
        }

        private IEnumerator Freeze()
        {
            yield return new WaitForSecondsRealtime(m_FreezeDelay);
            GamePause.Set(GamePause.Reason.GameOver, true);
        }
    }
}
