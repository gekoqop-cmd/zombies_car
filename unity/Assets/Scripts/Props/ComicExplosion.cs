using System.Collections.Generic;
using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Props
{
    /// <summary>
    /// The comic-book explosion: a white-hot flash, a jagged yellow-orange burst that pops and shrinks, a "БАХ!" /
    /// "БУМ!" / "БАБАХ!" lettering that jumps out and floats up, stars and cartoon smoke puffs flying out, a shockwave
    /// ring and a scorch mark on the ground, a short orange light, the boom and a screen shake that fades with the
    /// distance. Pooled: <see cref="Spawn"/> reuses a finished one.
    /// </summary>
    public sealed class ComicExplosion : MonoBehaviour
    {
        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly List<ComicExplosion> s_Pool = new List<ComicExplosion>();

        [SerializeField] private Renderer m_Flash;
        [SerializeField] private Renderer m_Burst;
        [SerializeField] private Renderer m_Text;
        [Tooltip("Lettering variants, one picked per explosion.")]
        [SerializeField] private Material[] m_TextMaterials = System.Array.Empty<Material>();
        [SerializeField] private Renderer m_Ring;
        [SerializeField] private Renderer m_Scorch;
        [SerializeField] private ParticleSystem m_Stars;
        [SerializeField] private ParticleSystem m_Clouds;
        [SerializeField] private Light m_Light;
        [SerializeField, Min(0.1f)] private float m_ScorchTime = 8f;
        [SerializeField, Min(0f)] private float m_LightIntensity = 7f;
        [SerializeField, Min(0f)] private float m_Shake = 0.55f;

        private MaterialPropertyBlock m_Block;
        private float m_Start = float.NegativeInfinity;
        private float m_Radius = 4f;
        private float m_Size = 1f;
        private float m_TextTilt;
        private Vector3 m_FlashScale, m_BurstScale, m_TextScale, m_TextPosition;

        public bool IsPlaying => gameObject.activeSelf;

        /// <summary>Plays an explosion of <paramref name="radius"/> metres at <paramref name="position"/> (scaled by <paramref name="size"/>).</summary>
        public static ComicExplosion Spawn(ComicExplosion prefab, Vector3 position, float radius, float size = 1f)
        {
            ComicExplosion fx = null;
            s_Pool.RemoveAll(e => e == null);
            foreach (ComicExplosion e in s_Pool)
            {
                if (e.m_Flash != null && !e.gameObject.activeSelf && e.name == prefab.name)
                {
                    fx = e;
                    break;
                }
            }
            if (fx == null)
            {
                fx = Instantiate(prefab);
                fx.name = prefab.name;
                s_Pool.Add(fx);
            }
            fx.Play(position, radius, size);
            return fx;
        }

        private void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            m_FlashScale = m_Flash.transform.localScale;
            m_BurstScale = m_Burst.transform.localScale;
            m_TextScale = m_Text.transform.localScale;
            m_TextPosition = m_Text.transform.localPosition;
        }

        private void Play(Vector3 position, float radius, float size)
        {
            transform.position = new Vector3(position.x, 0f, position.z);
            m_Radius = radius;
            m_Size = size;
            m_Start = Time.time;
            gameObject.SetActive(true);
            if (m_TextMaterials.Length > 0)
                m_Text.sharedMaterial = m_TextMaterials[Random.Range(0, m_TextMaterials.Length)];
            m_TextTilt = Random.Range(-10f, 10f);
            m_Stars.Emit(Mathf.RoundToInt(14 * size));
            m_Clouds.Emit(Mathf.RoundToInt(7 * size));
            AudioManager.PlaySfx(Sound.Explosion, 1f, Random.Range(0.9f, 1.05f));
            CameraShake.AddAt(position, m_Shake * size);
            GameFeel.Blast(size);
            Animate();
        }

        private void Update() => Animate();

        private void Animate()
        {
            float t = Time.time - m_Start;
            float s = m_Size;

            // Flash: a white-hot glow for a moment.
            float flash = 1f - Mathf.Clamp01(t / 0.18f);
            m_Flash.enabled = flash > 0f;
            m_Flash.transform.localScale = m_FlashScale * s * (1f + t * 4f);
            SetAlpha(m_Flash, flash);

            // Burst: pops with an overshoot, holds, shrinks away.
            float pop = Mathf.Clamp01(t / 0.12f);
            float burstScale = t < 0.12f ? Mathf.Lerp(0.2f, 1.25f, pop) : Mathf.Lerp(1.25f, 0.9f, Mathf.Clamp01((t - 0.12f) / 0.1f));
            float burstFade = 1f - Mathf.Clamp01((t - 0.3f) / 0.25f);
            m_Burst.enabled = burstFade > 0f;
            m_Burst.transform.localScale = m_BurstScale * s * burstScale * Mathf.Lerp(0.6f, 1f, burstFade);
            m_Burst.transform.localRotation = Quaternion.Euler(0f, 0f, t * 40f);
            SetAlpha(m_Burst, burstFade);

            // Lettering: jumps out after the burst, wobbles, floats up and fades.
            float tt = t - 0.05f;
            float textPop = Mathf.Clamp01(tt / 0.18f);
            float back = 1f + 2.4f * Mathf.Pow(textPop - 1f, 3f) + 1.4f * Mathf.Pow(textPop - 1f, 2f);
            float textFade = 1f - Mathf.Clamp01((tt - 0.75f) / 0.3f);
            m_Text.enabled = tt > 0f && textFade > 0f;
            m_Text.transform.localScale = m_TextScale * s * Mathf.Max(0f, back);
            m_Text.transform.localPosition = m_TextPosition + Vector3.up * (0.7f * Mathf.Clamp01(tt / 1f)) * s;
            m_Text.transform.localRotation = Quaternion.Euler(0f, 0f, m_TextTilt + Mathf.Sin(tt * 18f) * 4f * (1f - textPop * 0.8f));
            SetAlpha(m_Text, textFade);

            // Shockwave ring on the ground.
            float ring = Mathf.Clamp01(t / 0.32f);
            m_Ring.enabled = ring < 1f;
            float ringSize = m_Radius * 2.2f * Mathf.Lerp(0.2f, 1f, 1f - (1f - ring) * (1f - ring));
            m_Ring.transform.localScale = new Vector3(ringSize, 1f, ringSize);
            SetAlpha(m_Ring, 1f - ring);

            // Scorch mark: stays a while, then fades.
            float scorch = Mathf.Clamp01(t / 0.1f) * (1f - Mathf.Clamp01((t - (m_ScorchTime - 2f)) / 2f));
            m_Scorch.transform.localScale = new Vector3(m_Radius * 1.1f, 1f, m_Radius * 1.1f);
            SetAlpha(m_Scorch, scorch);

            if (m_Light != null)
            {
                float light = 1f - Mathf.Clamp01(t / 0.3f);
                m_Light.enabled = light > 0f;
                m_Light.intensity = m_LightIntensity * light * light;
                m_Light.range = m_Radius * 2.5f;
            }

            if (t >= m_ScorchTime)
                gameObject.SetActive(false);
        }

        private void SetAlpha(Renderer r, float alpha)
        {
            Color c = r.sharedMaterial.GetColor(s_BaseColor);
            c.a *= Mathf.Clamp01(alpha);
            m_Block.SetColor(s_BaseColor, c);
            r.SetPropertyBlock(m_Block);
        }
    }
}
