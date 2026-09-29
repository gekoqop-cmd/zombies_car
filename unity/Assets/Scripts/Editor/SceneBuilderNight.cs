using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ZombiCar.Car;
using ZombiCar.Core;
using Random = System.Random;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// What makes Map02 the night version of the lot: night copies of the glowing materials (stronger emission, lit
    /// workshop windows) swapped in on this scene only, a rearranged layout (props, bushes, fence sections), the car's
    /// headlights and cabin light, more street lamps with static pools of light under them, and a night
    /// post-processing profile (stronger bloom, split toning, darker vignette).
    /// </summary>
    public static partial class SceneBuilder
    {
        private const string c_NightMaterialFolder = EditorPaths.c_Materials + "/Night";
        private const string c_NightVolumeProfile = EditorPaths.c_Settings + "/Map02_VolumeProfile.asset";
        private static readonly Color s_WindowGlow = new Color(1f, 0.72f, 0.38f) * 1.8f;

        // Regular material → how its night copy glows (emission multiplier; the glass gets a warm glow of its own).
        private static readonly (string name, float emission)[] s_NightGlow =
        {
            ("M_LampGlow", 2.2f), ("M_Signs_Emissive", 1.9f), ("M_Car_Emissive", 2f), ("M_Glass", 0f),
        };

        /// <summary>Puts the night copies of the glowing materials on every renderer of the open scene.</summary>
        private static void SwapNightMaterials()
        {
            EditorPaths.EnsureFolder(c_NightMaterialFolder);
            var map = new Dictionary<Material, Material>();
            foreach ((string name, float emission) in s_NightGlow)
            {
                Material regular = MaterialLibrary.Get(name);
                if (regular == null)
                    continue;
                string path = $"{c_NightMaterialFolder}/{name}_Night.mat";
                var night = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (night == null)
                {
                    night = new Material(regular);
                    AssetDatabase.CreateAsset(night, path);
                }
                night.CopyPropertiesFromMaterial(regular);
                night.shader = regular.shader;
                night.EnableKeyword("_EMISSION");
                if (emission > 0f)
                {
                    night.SetColor("_EmissionColor", regular.GetColor("_EmissionColor") * emission);
                }
                else
                {
                    // Window glass: lit from inside with its own texture as the glow pattern.
                    night.SetTexture("_EmissionMap", regular.GetTexture("_BaseMap"));
                    night.SetColor("_EmissionColor", s_WindowGlow);
                }
                night.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(night);
                map[regular] = night;
            }
            int swapped = 0;
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && map.TryGetValue(materials[i], out Material night))
                    {
                        materials[i] = night;
                        changed = true;
                        swapped++;
                    }
                }
                if (changed)
                    renderer.sharedMaterials = materials;
            }
            Debug.Log($"[SceneBuilder] Night materials: {swapped} slots use the glowing night copies.");
        }

        // ------------------------------------------------------------------ layout variation
        private const float c_PropMoveChance = 0.55f;
        private const float c_BushMoveChance = 0.45f;

        /// <summary>
        /// Rearranges part of the lot so the night map is not a copy: about half of the props move 1.5–3.5 m and turn,
        /// some bushes shift. Spots near the car, the player spawn, the workshop and the fences are left alone.
        /// </summary>
        private static void VaryLayout(Transform props, Transform nature, Transform environment, MapConfig map, int seed)
        {
            var rnd = new Random(seed);
            int moved = 0;
            // Every prop, building, parked car and the extra night lamps keep their footprint clear: a moved prop never
            // lands inside another object (a barrel inside the cargo cart at the north fence was the bug).
            // (The ground, roads and the workshop are left out: they are too big to count as a footprint — the workshop
            // yard has its own exclusion in IsFreeForProp.)
            var occupied = props.Cast<Transform>()
                .Concat(environment.Cast<Transform>().Where(t => FootprintRadius(t) < c_MaxOccupiedRadius)).ToList();
            foreach (Transform t in props.Cast<Transform>().ToArray())
            {
                if (rnd.NextDouble() > c_PropMoveChance)
                    continue;
                if (TryMove(t, rnd, 1.5f, 3.5f, map, occupied))
                    moved++;
            }
            foreach (Transform t in nature.Cast<Transform>().Where(t => t.name.StartsWith("Bush")).ToArray())
            {
                if (rnd.NextDouble() > c_BushMoveChance)
                    continue;
                TryMove(t, rnd, 0.8f, 2f, map, null);
            }
            Debug.Log($"[SceneBuilder] Night layout: {moved} props moved.");
        }

        private const float c_PropClearance = 0.6f;          // metres between the footprints of two props
        private const float c_MaxOccupiedRadius = 4f;

        private static bool TryMove(Transform t, Random rnd, float min, float max, MapConfig map, List<Transform> occupied)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                double angle = rnd.NextDouble() * System.Math.PI * 2.0;
                float distance = Mathf.Lerp(min, max, (float)rnd.NextDouble());
                Vector3 p = t.position + new Vector3((float)System.Math.Cos(angle), 0f, (float)System.Math.Sin(angle)) * distance;
                if (!IsFreeForProp(p, map) || (occupied != null && Overlaps(t, p, occupied)))
                    continue;
                t.position = p;
                t.rotation = Quaternion.Euler(0f, t.eulerAngles.y + Mathf.Lerp(-60f, 60f, (float)rnd.NextDouble()), 0f);
                return true;
            }
            return false;
        }

        // Footprint radius of a prop: half the larger side of its renderers' bounds.
        private static float FootprintRadius(Transform t)
        {
            Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return 0.5f;
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers)
                b.Encapsulate(r.bounds);
            return Mathf.Max(b.extents.x, b.extents.z);
        }

        private static bool Overlaps(Transform moving, Vector3 p, List<Transform> occupied)
        {
            float radius = FootprintRadius(moving);
            foreach (Transform other in occupied)
            {
                if (other == moving)
                    continue;
                if (Near(p.x, p.z, other.position.x, other.position.z, radius + FootprintRadius(other) + c_PropClearance))
                    return true;
            }
            foreach ((float x, float z, float _) in s_NightLotLamps)
            {
                if (Near(p.x, p.z, x, z, radius + 1f))
                    return true;
            }
            foreach ((float x, float z, float _) in s_LotExplosives)                 // placed after the variation
            {
                if (Near(p.x, p.z, x, z, radius + 1.2f))
                    return true;
            }
            return false;
        }

        private static bool IsFreeForProp(Vector3 p, MapConfig map)
        {
            Vector3 c = map.PlayAreaCenter;
            float hx = map.PlayAreaSize.x * 0.5f - 2f, hz = map.PlayAreaSize.y * 0.5f - 2f;
            if (Mathf.Abs(p.x - c.x) > hx || Mathf.Abs(p.z - c.z) > hz)
                return false;                                                     // keep off the fences
            if (p.x * p.x + p.z * p.z < 36f)
                return false;                                                     // the car
            if (Near(p.x, p.z, PlayerSpawn.x, PlayerSpawn.z, 3f))
                return false;
            return !(p.x > -16.5f && p.x < -1.5f && p.z > 3.8f);                  // workshop and its yard
        }

        // ------------------------------------------------------------------ street lamps
        // Extra lamps inside the lot at night (three) (x, z, yaw): along the edges, clear of the car, the spawn and the workshop.
        private static readonly (float x, float z, float yaw)[] s_NightLotLamps =
        {
            (-14f, -7f, 90f), (13.5f, -9f, 270f), (-6.5f, -11.5f, 0f),   // the one at (7, 6.5) removed on request
        };
        private static readonly Vector3 s_LampHeadLocal = new Vector3(-0.9f, 0f, 0f);   // the lantern arm of Lamp_Street
        private const float c_LightPoolSize = 8f;
        private const float c_LightPoolHeight = 0.035f;                                 // above the ground decals
        private static readonly Color s_LightPoolColor = new Color(1f, 0.62f, 0.3f, 0.55f);
        private const string c_LightPoolTexture = EditorPaths.c_Vfx + "/T_LightPool.png";
        private const string c_LightPoolMaterial = c_NightMaterialFolder + "/M_FX_LightPool.mat";
        // Point lights of the street lamps (base and Ultra): replaced by the pools, which never switch off.
        private static readonly string[] s_StreetLampLights =
            { "Light_StreetLamp", "Light_StreetLamp2", "UltraLight_StreetLamp2", "UltraLight_RoadLampW", "UltraLight_RoadLampE" };

        /// <summary>
        /// Night street lamps: four more inside the lot, and under every lamp a static warm pool of light on the ground
        /// (an additive quad, always on, no light count limits) instead of the lamps' dynamic point lights, which the
        /// per-object light limit switched on and off as the camera moved.
        /// </summary>
        /// <summary>The extra lot lamps (before the NavMesh bake, so the zombies walk around them).</summary>
        private static void AddNightLotLamps(Transform props)
        {
            foreach ((float x, float z, float yaw) in s_NightLotLamps)
                Place("Lamp_Street", props, x, z, yaw);
        }

        private static void NightStreetLamps(Transform lighting)
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (System.Array.IndexOf(s_StreetLampLights, light.name) >= 0)
                    Object.DestroyImmediate(light.gameObject);
            }

            Material pool = LightPoolMaterial();
            Mesh quad = AssetDatabase.LoadAssetAtPath<Mesh>(CharacterBuilder.c_QuadMesh);
            var root = new GameObject("LampLightPools").transform;
            root.SetParent(lighting, false);
            int count = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t.name != "Lamp_Street" || !PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                    continue;
                Vector3 head = t.TransformPoint(s_LampHeadLocal);
                var go = new GameObject($"LightPool_{++count}");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(head.x, c_LightPoolHeight, head.z);
                go.transform.localScale = new Vector3(c_LightPoolSize, 1f, c_LightPoolSize);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pool;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            Debug.Log($"[SceneBuilder] Night lamps: {s_NightLotLamps.Length} added in the lot, {count} static light pools.");
        }

        private static Material LightPoolMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(c_LightPoolMaterial);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, c_LightPoolMaterial);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(c_LightPoolTexture));
            material.SetColor("_BaseColor", s_LightPoolColor);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);                         // additive: lights up whatever is below
            UnityEditor.BaseShaderGUI.SetMaterialKeywords(material, UnityEditor.Rendering.Universal.ShaderGUI.ParticleGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------ car lights
        private static readonly Color s_HeadlightColor = new Color(1f, 0.86f, 0.62f);
        private static readonly Color s_CabinColor = new Color(1f, 0.72f, 0.42f);

        /// <summary>Low-beam headlights (two spots at the front lamps, aimed a little down) and a warm cabin light.</summary>
        private static void AddCarLights(GameObject car)
        {
            Material emissive = null;
            var fronts = new List<Vector3>();
            Transform bumper = FindChild(car.transform, "Bumper_Front");
            float front = bumper != null && bumper.TryGetComponent(out Renderer bumperRenderer)
                ? Mathf.Sign(car.transform.InverseTransformPoint(bumperRenderer.bounds.center).z)
                : 1f;
            foreach (MeshRenderer r in car.GetComponentsInChildren<MeshRenderer>(true))
            {
                int slot = System.Array.FindIndex(r.sharedMaterials, m => m != null && m.name.StartsWith("M_Car_Emissive"));
                if (slot < 0 || !r.gameObject.activeInHierarchy)
                    continue;
                emissive = r.sharedMaterials[slot];
                Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] vertices = mesh.vertices;
                foreach (int i in mesh.GetTriangles(slot).Distinct())
                    fronts.Add(car.transform.InverseTransformPoint(r.transform.TransformPoint(vertices[i])));
            }
            if (fronts.Count == 0)
            {
                Debug.LogError("[SceneBuilder] Car_Player has no emissive headlights: no car lights added.");
                return;
            }
            // Headlights: the glowing parts at the very front and below the roof (the roof lamps are ignored).
            float frontMost = fronts.Max(v => v.z * front);
            List<Vector3> lamps = fronts.Where(v => v.z * front > frontMost - 0.25f && v.y < 1.4f).ToList();
            var headlights = new List<Light>();
            foreach (IGrouping<bool, Vector3> side in lamps.GroupBy(v => v.x < 0f))
            {
                Vector3 local = side.Aggregate(Vector3.zero, (s, v) => s + v) / side.Count();
                var go = new GameObject(side.Key ? "Headlight_L" : "Headlight_R");
                go.transform.SetParent(car.transform, false);
                go.transform.localPosition = local + new Vector3(0f, 0f, 0.15f * front);
                go.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.22f, front));
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = s_HeadlightColor;
                light.intensity = 9f;
                light.range = 10f;
                light.spotAngle = 62f;
                light.innerSpotAngle = 30f;
                light.shadows = LightShadows.None;
                light.shadowStrength = 0.7f;
                light.lightmapBakeType = LightmapBakeType.Realtime;
                headlights.Add(light);
            }
            var cabinGo = new GameObject("CabinLight");
            cabinGo.transform.SetParent(car.transform, false);
            cabinGo.transform.localPosition = PrefabBuilder.CarPlayerColliderCenter + new Vector3(0f, 0.55f, -0.4f * front);
            var cabin = cabinGo.AddComponent<Light>();
            cabin.type = LightType.Point;
            cabin.color = s_CabinColor;
            cabin.intensity = 1.2f;
            cabin.range = 3.2f;
            cabin.shadows = LightShadows.None;
            cabin.lightmapBakeType = LightmapBakeType.Realtime;

            var tier = car.AddComponent<CarLightsTier>();
            var so = new SerializedObject(tier);
            SerializedProperty array = so.FindProperty("m_Headlights");
            array.arraySize = headlights.Count;
            for (int i = 0; i < headlights.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = headlights[i];
            so.FindProperty("m_Cabin").objectReferenceValue = cabin;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[SceneBuilder] Car lights: {headlights.Count} headlights + cabin light ({emissive?.name} glass).");
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }
            return null;
        }

        // ------------------------------------------------------------------ post-processing
        private static VolumeProfile BuildNightVolumeProfile()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(c_NightVolumeProfile) != null)
                AssetDatabase.DeleteAsset(c_NightVolumeProfile);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, c_NightVolumeProfile);

            var bloom = AddOverride<Bloom>(profile);
            bloom.threshold.Override(0.7f);                 // lamps, signs and windows bleed light into the dark
            bloom.intensity.Override(1.35f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.82f, 0.62f));
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            bloom.maxIterations.Override(6);

            var vignette = AddOverride<Vignette>(profile);
            vignette.intensity.Override(0.4f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.01f, 0.015f, 0.05f));

            var split = AddOverride<SplitToning>(profile);
            split.shadows.Override(new Color(0.45f, 0.52f, 0.72f));   // bluish shadows
            split.highlights.Override(new Color(1f, 0.76f, 0.5f));    // warm lamp light
            split.balance.Override(-10f);

            var color = AddOverride<ColorAdjustments>(profile);
            color.postExposure.Override(0.3f);              // keeps the lot readable for play
            color.contrast.Override(14f);
            color.saturation.Override(-10f);

            var dof = AddOverride<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(38f);
            dof.gaussianEnd.Override(75f);
            dof.gaussianMaxRadius.Override(1.1f);
            dof.highQualitySampling.Override(false);
            dof.active = false;   // enabled on the High quality tier by QualityPostProcessApplier

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
