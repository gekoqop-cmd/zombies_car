using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ZombiCar.Characters;
using ZombiCar.Enemies;

namespace ZombiCar.EditorTools
{
    public static partial class FeatureTests
    {
        // ================================================================== 11. procedural zombie motion
        /// <summary>
        /// A walker rises, walks to the player, attacks, is shot and dies; close-ups of every phase go to
        /// Screenshots/Motion/*.png, and the squash / stretch and lean of its sprite are measured per phase.
        /// </summary>
        private static void Motion()
        {
            ZombieController zombie = null;
            Transform sprite = null;
            var stretch = new Dictionary<string, (float min, float max)>();
            var lean = new Dictionary<string, (float min, float max)>();
            int shot = 0;

            void Sample(string phase)
            {
                if (sprite == null)
                    return;
                var animator = sprite.GetComponent<SpriteAnimator>();
                float sy = Mathf.Abs(sprite.localScale.y / (animator.Scale.y * animator.ScaleMultiplier));
                float z = Mathf.DeltaAngle(0f, sprite.localEulerAngles.z);
                stretch[phase] = stretch.TryGetValue(phase, out var s) ? (Mathf.Min(s.min, sy), Mathf.Max(s.max, sy)) : (sy, sy);
                lean[phase] = lean.TryGetValue(phase, out var l) ? (Mathf.Min(l.min, z), Mathf.Max(l.max, z)) : (z, z);
            }
            void Close(string phase)
            {
                if (zombie == null)
                    return;
                Camera main = Camera.main;
                var go = new GameObject("MotionCamera");
                var cam = go.AddComponent<Camera>();
                cam.CopyFrom(main);
                go.transform.rotation = main.transform.rotation;
                go.transform.position = zombie.transform.position + Vector3.up * 1f - go.transform.forward * 6f;
                string folder = Path.GetFullPath(Path.Combine(c_Folder, "Motion"));
                Directory.CreateDirectory(folder);
                MapScreenshots.Render(cam, 560, 560, Path.Combine(folder, $"{++shot:00}_{phase}.png"));
                Object.DestroyImmediate(go);
            }

            At(1.0f, () =>
            {
                s_Director.enabled = false;
                Teleport(new Vector3(-2f, 0f, -6f));
                // Every zombie is drawn from video takes now: the frames carry the sway and the lunge, the procedural
                // motion only adds a touch on top (the thresholds below), the hit recoil and the rise in full.
                zombie = s_Spawner.SpawnAt(Zombie("Worker"), new Vector3(2f, 0f, -6f));
                sprite = zombie.Animator.transform;
            });
            for (float t = 1.05f; t < 1.9f; t += 0.03f)
                At(t, () => Sample("rise"));
            At(1.4f, () => Close("rise"));
            for (float t = 2.0f; t < 3.2f; t += 0.03f)
                At(t, () => Sample(zombie.Animator.Anim == CharacterAnim.Move ? "walk" : zombie.Animator.Anim.ToString()));
            for (int i = 0; i < 4; i++)
                At(2.2f + i * 0.13f, () => Close("walk"));
            for (float t = 3.2f; t < 6.5f; t += 0.02f)
                At(t, () =>
                {
                    if (zombie.Animator.Anim == CharacterAnim.Attack)
                        Sample("attack");
                });
            At(3.2f, () => { });
            for (int i = 0; i < 16; i++)
                At(3.3f + i * 0.2f, () =>
                {
                    if (zombie.Animator.Anim == CharacterAnim.Attack && shot < 10)
                        Close("attack");
                    s_PlayerHealth.Heal(100);
                });
            At(6.6f, () =>
            {
                Vector3 aim = zombie.transform.position - s_Player.position;
                aim.y = 0f;
                s_Bot.AimActive = true;
                s_Bot.AimDirection = aim.normalized;
                s_Bot.FireHeld = true;
            });
            // The bullets may kill it: from then on the samples are the fall.
            for (float t = 6.62f; t < 7.2f; t += 0.02f)
                At(t, () => Sample(zombie.Animator.Anim == CharacterAnim.Dead ? "dead" : "hit"));
            At(6.75f, () => Close("hit"));
            At(6.9f, () => Close("hit"));
            At(7.2f, () =>
            {
                s_Bot.FireHeld = false;
                zombie.TakeDamage(10000, Vector3.right);
            });
            for (float t = 7.22f; t < 7.8f; t += 0.02f)
                At(t, () => Sample("dead"));
            At(7.3f, () => Close("dead"));
            At(7.5f, () => Close("dead"));
            At(8.0f, () =>
            {
                foreach (KeyValuePair<string, (float min, float max)> s in stretch)
                    Debug.Log($"[FeatureTests] motion {s.Key}: height x{s.Value.min:F3}..x{s.Value.max:F3}, " +
                              $"lean {lean[s.Key].min:F1}..{lean[s.Key].max:F1} deg");
                Check(stretch.ContainsKey("walk") && stretch["walk"].max - stretch["walk"].min > 0.025f, "walking squashes and stretches a little");
                Check(lean.ContainsKey("walk") && Mathf.Max(Mathf.Abs(lean["walk"].min), Mathf.Abs(lean["walk"].max)) > 1.5f,
                    "walking leans forward a little (on top of the painted walk)");
                Check(stretch.ContainsKey("Idle") || stretch.ContainsKey("walk"), "breathing / walking sampled");
                Check(lean.ContainsKey("attack") && lean["attack"].max - lean["attack"].min > 4f, "the attack winds up and lunges (on top of the painted blow)");
                Check(lean.ContainsKey("hit") && lean["hit"].max - lean["hit"].min > 4f, "a hit makes it recoil");
                Check(stretch.ContainsKey("dead") && stretch["dead"].min < 0.95f, "the body thuds when it falls");
                Check(lean.ContainsKey("rise") && lean["rise"].max - lean["rise"].min > 4f, "rising from the ground wobbles");
            });
            At(8.2f, Finish);
        }
    }
}
