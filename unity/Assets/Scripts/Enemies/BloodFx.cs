using UnityEngine;
using ZombiCar.Core;

namespace ZombiCar.Enemies
{
    /// <summary>
    /// Blood of the zombie death bursts, shared by every zombie: one particle system of flying drops (stretched along
    /// their speed, gone when they reach the ground) and one of ground splats (flat, spread out quickly, fade after a
    /// few seconds). Both are made on first use in the active scene from the materials of the first
    /// <see cref="ZombieGibs"/>, so the hand-edited map scenes need nothing.
    /// </summary>
    public static class BloodFx
    {
        private const int c_MaxDrops = 700;
        private const int c_MaxSplats = 160;
        private const float c_SplatHeight = 0.06f;   // above the ground and its decals, under the blob shadows (0.08)
        private const float c_SplatLife = 7f;
        private static readonly Color s_Blood = new Color(1f, 0.92f, 0.92f, 1f);

        private static ParticleSystem s_Drops;
        private static ParticleSystem s_Splats;

        /// <summary>Creates the two systems in the active scene once (again after a scene change).</summary>
        public static void Ensure(Material drop, Material splat)
        {
            if (s_Drops != null)
                return;
            var root = new GameObject("BloodFx");
            s_Drops = MakeDrops(root.transform, drop);
            s_Splats = MakeSplats(root.transform, splat);
        }

        /// <summary>A burst of <paramref name="count"/> drops from <paramref name="position"/>: <paramref name="push"/> on top of a spray up and out.</summary>
        public static void Spray(Vector3 position, Vector3 push, int count, float size)
        {
            if (s_Drops == null)
                return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            Color tint = s_Blood * SceneTint.Characters;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.9f + 0.35f;
                p.position = position + Random.insideUnitSphere * 0.15f * size;
                p.velocity = dir * Random.Range(1.5f, 4.6f) * size + push * Random.Range(0.4f, 1.2f);
                p.startSize = Random.Range(0.07f, 0.17f) * size;
                p.startLifetime = Random.Range(0.6f, 1.2f);
                p.startColor = Color.Lerp(tint, tint * 0.6f, Random.value);
                s_Drops.Emit(p, 1);
            }
        }

        /// <summary>One drop (trails behind a flying piece).</summary>
        public static void Drip(Vector3 position, Vector3 velocity, float size)
        {
            if (s_Drops == null)
                return;
            var p = new ParticleSystem.EmitParams
            {
                position = position, velocity = velocity, startSize = Random.Range(0.05f, 0.1f) * size,
                startLifetime = 0.8f, startColor = s_Blood * SceneTint.Characters, applyShapeToPosition = false,
            };
            s_Drops.Emit(p, 1);
        }

        /// <summary>A splat lying on the ground at <paramref name="position"/> (y ignored), <paramref name="size"/> metres across.</summary>
        public static void Splat(Vector3 position, float size)
        {
            if (s_Splats == null)
                return;
            position.y = c_SplatHeight + Random.value * 0.004f;       // no z-fighting between overlapping splats
            var p = new ParticleSystem.EmitParams
            {
                position = position, velocity = Vector3.zero, startSize = size * Random.Range(0.85f, 1.15f),
                rotation = Random.Range(0f, 360f), startLifetime = c_SplatLife,
                startColor = s_Blood * SceneTint.Characters, applyShapeToPosition = false,
            };
            s_Splats.Emit(p, 1);
        }

        private static ParticleSystem MakeDrops(Transform parent, Material material)
        {
            ParticleSystem ps = Make("Drops", parent, material, c_MaxDrops);
            ParticleSystem.MainModule main = ps.main;
            main.gravityModifier = 1.7f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.035f;
            renderer.lengthScale = 1f;

            // Drops end on the ground (a plane at the splat height).
            var ground = new GameObject("Ground").transform;
            ground.SetParent(parent, false);
            ground.position = new Vector3(0f, c_SplatHeight, 0f);
            ParticleSystem.CollisionModule collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.SetPlane(0, ground);
            collision.lifetimeLoss = 1f;
            collision.bounce = 0f;
            collision.dampen = 1f;
            collision.enableDynamicColliders = false;
            return ps;
        }

        private static ParticleSystem MakeSplats(Transform parent, Material material)
        {
            ParticleSystem ps = Make("Splats", parent, material, c_MaxSplats);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sortingOrder = -2;                                // before the blob shadows (-1)

            // Splashes out in a blink, lies, then dries away.
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f), new Keyframe(0.02f, 1f), new Keyframe(1f, 1.05f)));
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.7f, 0.6f, 0.6f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.95f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            return ps;
        }

        private static ParticleSystem Make(string name, Transform parent, Material material, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;                                       // always playing: Emit() simulates at once
            main.playOnAwake = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.startSpeed = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }
    }
}
