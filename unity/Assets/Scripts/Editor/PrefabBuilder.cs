using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using ZombiCar.Car;
using ZombiCar.Core;
using ZombiCar.Player;
using Object = UnityEngine.Object;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Tools/Build Prefabs: a prefab (variant) for every FBX with simple Box/Capsule colliders (no mesh colliders),
    /// static flags on buildings/fences/signs/nature, plus generated environment prefabs (ground, roads, decals),
    /// the bullet, the 2D characters (zombies, see <see cref="CharacterBuilder"/>) and the player. The map is assembled
    /// only from these prefabs.
    /// </summary>
    public static class PrefabBuilder
    {
        private static readonly string[] s_EnvironmentGroups = { "Workshop", "Fences", "Signs", "Nature" };

        // Generated ground: 1 asphalt tile covers 6 x 6 m (large slabs, little visible repetition); roads tile every 7 m.
        private const float c_AsphaltTile = 6f;
        private const float c_RoadWidth = 7f;
        private const float c_SidewalkWidth = 1.7f;
        private const float c_SidewalkTile = 1.6f;
        private const float c_DecalHeight = 0.014f;
        private const float c_StoneDecalLift = 0.012f;
        private const float c_FenceMinThickness = 0.35f;

        // Car_Player: the roof cargo lost its transforms in the FBX export -> placed back on the roof rack.
        private const float c_CargoFloorHeight = 2.242f;
        private static readonly string[] s_CarDamageParts = { "DMG1_Hood_Dented", "DMG2_Bumper_Bent", "DMG3_Cargo_Fallen" };
        private static readonly (string name, float x, float z)[] s_CarCargo =
        {
            ("Cargo_Jerrycan_Green", 0.42f, 0.62f), ("Cargo_Jerrycan_Red", -0.05f, 0.62f),
            ("Cargo_Crate", 0.30f, -0.25f), ("Cargo_SpareTire", -0.30f, -0.95f),
        };

        public static readonly Vector3 CarPlayerColliderCenter = new Vector3(0f, 1.1f, 0.12f);
        public static readonly Vector3 CarPlayerColliderSize = new Vector3(2.3f, 2.2f, 5.1f);

        // Roads (placed by SceneBuilder.BuildGround): the north road crosses the whole ground, the east road runs from
        // the north sidewalk down to the south edge of the ground.
        public const float c_NorthSidewalkEdge = 15.8f;

        public static float EastRoadLength(MapConfig map) =>
            c_NorthSidewalkEdge - (map.GroundCenter.z - map.GroundSize.y * 0.5f);

        /// <summary>
        /// Concept objects without a model: stand-in cubes of the real size (listed in the build log / report).
        /// Parts: prefab name, material, centre, size; the collider covers only the parts marked solid.
        /// </summary>
        private static readonly (string prefab, string material, Vector3 center, Vector3 size, bool solid)[] s_StubParts =
        {
            ("Stub_Bicycle", "M_Stub_Bicycle", new Vector3(0f, 0.5f, 0f), new Vector3(1.75f, 1f, 0.45f), true),
            ("Stub_Hydrant", "M_Stub_Hydrant", new Vector3(0f, 0.45f, 0f), new Vector3(0.4f, 0.9f, 0.4f), true),
        };

        [MenuItem("Tools/Build Prefabs")]
        public static void BuildAll()
        {
            EditorPaths.EnsureFolder(EditorPaths.c_PrefabsEnvironment);
            EditorPaths.EnsureFolder(EditorPaths.c_PrefabsProps);
            EditorPaths.EnsureFolder(EditorPaths.c_PrefabsCharacters);
            EditorPaths.EnsureFolder(EditorPaths.c_PrefabsUi);
            EditorPaths.EnsureFolder(EditorPaths.c_GeneratedMeshes);

            int models = BuildModelPrefabs();
            BuildGeneratedEnvironment();
            BuildStubs();
            TreeBuilder.BuildAll();
            Bullet bullet = BuildBullet();
            CharacterBuilder.BuildAll();
            PlayerBuilder.BuildAssets();
            BuildPlayer(bullet);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PrefabBuilder] {models} model prefabs + generated environment, bullet, zombies, player.");
        }

        // ------------------------------------------------------------------ FBX -> prefab variants
        private static int BuildModelPrefabs()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { EditorPaths.c_Models }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                string group = Path.GetDirectoryName(path)?.Replace('\\', '/').Substring(EditorPaths.c_Models.Length + 1);
                if (string.IsNullOrEmpty(group))
                    continue;
                string topGroup = group.Split('/')[0];
                bool environment = s_EnvironmentGroups.Contains(topGroup);
                string folder = $"{(environment ? EditorPaths.c_PrefabsEnvironment : EditorPaths.c_PrefabsProps)}/{group}";
                EditorPaths.EnsureFolder(folder);

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                go.name = name;
                Configure(go, name, topGroup, environment);
                SavePrefab(go, $"{folder}/{name}.prefab");
                count++;
            }
            return count;
        }

        private static void Configure(GameObject go, string name, string group, bool environment)
        {
            bool foliage = group == "Nature" && (name.StartsWith("Bush") || name.StartsWith("Grass"));
            bool decal = name.StartsWith("Decal_");
            SetLayerRecursively(go, foliage || decal ? 0 : Layers.c_ObstacleIndex);

            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                bool smallOrFlat = decal || name.StartsWith("Grass") || r.name.EndsWith("_Grass");
                r.shadowCastingMode = smallOrFlat ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }

            switch (group)
            {
                case "Nature":
                    if (name.StartsWith("Rock"))
                        AddBox(go, BodyBounds(go));
                    if (decal)
                        foreach (Transform child in go.transform)
                            child.localPosition += Vector3.up * c_StoneDecalLift;   // above the asphalt, no z-fighting
                    break;
                case "Fences":
                    if (name == "Fence_Corner")
                    {
                        Bounds b = BodyBounds(go);
                        AddBox(go, new Bounds(new Vector3(b.center.x, b.center.y, 0f), new Vector3(b.size.x, b.size.y, c_FenceMinThickness)));
                        AddBox(go, new Bounds(new Vector3(0f, b.center.y, b.center.z), new Vector3(c_FenceMinThickness, b.size.y, b.size.z)));
                    }
                    else
                    {
                        AddBox(go, BodyBounds(go), c_FenceMinThickness);
                    }
                    break;
                case "Signs":
                    AddSignColliders(go, name);
                    break;
                case "Street":
                    if (name.StartsWith("Barrel") || name == "Tires_Stack" || name == "Cone")
                        AddCapsule(go, BodyBounds(go));
                    else
                        AddBox(go, BodyBounds(go));
                    break;
                case "Cargo":
                case "Furniture":
                    AddBox(go, BodyBounds(go));
                    break;
                case "Props":
                    if (name == "Workshop_Vegetation")
                        SetLayerRecursively(go, 0);
                    else if (name.StartsWith("Prop_Barrel") || name.StartsWith("Prop_TireStack"))
                        AddCapsule(go, BodyBounds(go));
                    else
                        AddBox(go, BodyBounds(go));
                    break;
                case "Car":
                    ConfigureCarPlayer(go);
                    break;
                case "Workshop":
                    ConfigureWorkshop(go);
                    break;
                case "Vehicles":
                    // Parked wrecks: one box around the body (the wheels stay inside it), never moved.
                    AddBox(go, BodyBounds(go));
                    SetStaticRecursively(go, StaticEditorFlags.BatchingStatic);
                    break;
            }

            if (environment)
                SetStaticRecursively(go, StaticEditorFlags.BatchingStatic);
        }

        private static void AddSignColliders(GameObject go, string name)
        {
            switch (name)
            {
                case "Sign_Gas":
                    const float postOffset = 0.95f, postRadius = 0.26f, postHeight = 3f;
                    AddCapsule(go, new Vector3(-postOffset, postHeight * 0.5f, 0f), postRadius, postHeight);
                    AddCapsule(go, new Vector3(postOffset, postHeight * 0.5f, 0f), postRadius, postHeight);
                    break;
                case "Sign_Road":
                    AddCapsule(go, new Vector3(0f, 1.15f, 0f), 0.24f, 2.3f);
                    break;
                case "Lamp_Street":
                    AddCapsule(go, new Vector3(0f, 1.95f, 0f), 0.3f, 3.9f);
                    break;
                case "Pole_Power":
                    AddCapsule(go, new Vector3(0f, 3f, 0f), 0.34f, 6f);
                    break;
                default:
                    AddBox(go, BodyBounds(go));
                    break;
            }
        }

        private static void ConfigureCarPlayer(GameObject go)
        {
            foreach (string part in s_CarDamageParts)
            {
                Transform t = FindDeep(go.transform, part);
                if (t != null)
                    t.gameObject.SetActive(false);   // damage stages are switched on by CarHealth later
            }
            foreach ((string name, float x, float z) in s_CarCargo)
            {
                Transform t = FindDeep(go.transform, name);
                if (t == null || !t.TryGetComponent(out Renderer r))
                    continue;
                t.rotation = Quaternion.identity;
                Bounds b = r.bounds;
                t.position += new Vector3(x - b.center.x, c_CargoFloorHeight - b.min.y, z - b.center.z);
                Debug.Log($"[PrefabBuilder] Car cargo {name}: bounds {r.bounds.size:F2} at {r.bounds.center:F2}");
            }
            var box = go.AddComponent<BoxCollider>();
            box.center = CarPlayerColliderCenter;
            box.size = CarPlayerColliderSize;
            // Its own layer (zombies target it rather than treat it as scenery): bullets stop at it with sparks and
            // no damage (Layers.ProjectileBlockers), it blocks the player's CharacterController (default layer
            // collision) and carves the zombie NavMesh below.
            go.layer = Layers.c_CarObstacleIndex;
            var navObstacle = go.AddComponent<NavMeshObstacle>();
            navObstacle.shape = NavMeshObstacleShape.Box;
            navObstacle.center = CarPlayerColliderCenter;
            navObstacle.size = CarPlayerColliderSize;
            navObstacle.carving = true;
            var stages = new GameObject[s_CarDamageParts.Length];
            for (int i = 0; i < stages.Length; i++)
                stages[i] = FindDeep(go.transform, s_CarDamageParts[i])?.gameObject;
            CharacterBuilder.SetProps(go.AddComponent<CarHealth>(), ("m_DamageStageObjects", stages), ("m_Body", box));
        }

        private static void ConfigureWorkshop(GameObject go)
        {
            // Building walls (footprint 6 x 4.4 m); the garage interior is not walkable.
            AddBox(go, new Bounds(new Vector3(-0.1f, 1.5f, 0.05f), new Vector3(6.2f, 3f, 4.6f)));
            const float halfWidth = 3f, halfDepth = 2.2f;
            foreach (Transform child in go.transform)
            {
                if (!child.name.StartsWith("Prop_") || !child.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null)
                    continue;
                Vector3 p = child.localPosition;
                if (Mathf.Abs(p.x) <= halfWidth && Mathf.Abs(p.z) <= halfDepth)
                    continue;   // inside the building: covered by the wall box
                var box = child.gameObject.AddComponent<BoxCollider>();
                box.center = mf.sharedMesh.bounds.center;
                box.size = mf.sharedMesh.bounds.size;
            }
            Transform vegetation = go.transform.Find("Workshop_Vegetation");
            if (vegetation != null)
                vegetation.gameObject.layer = 0;
        }

        // ------------------------------------------------------------------ generated environment
        private static void BuildGeneratedEnvironment()
        {
            MapConfig map = DataAssetsBuilder.Load<MapConfig>(DataAssetsBuilder.c_MapConfig);
            string folder = EditorPaths.c_PrefabsEnvironment + "/Generated";
            EditorPaths.EnsureFolder(folder);

            Mesh ground = SaveMesh(GridMesh(map.GroundSize.x, map.GroundSize.y, 10, 8, c_AsphaltTile, c_AsphaltTile), "SM_Ground");
            GameObject groundGo = MeshObject("Env_Ground", ground, MaterialLibrary.Get("M_Asphalt"), false);
            var groundCollider = groundGo.AddComponent<BoxCollider>();
            groundCollider.center = new Vector3(0f, -0.1f, 0f);
            groundCollider.size = new Vector3(map.GroundSize.x, 0.2f, map.GroundSize.y);
            groundGo.layer = Layers.c_GroundIndex;
            SetStaticRecursively(groundGo, StaticEditorFlags.BatchingStatic);
            SavePrefab(groundGo, $"{folder}/Env_Ground.prefab");

            foreach ((string suffix, float length) in new[] { ("North", map.GroundSize.x), ("East", EastRoadLength(map)) })
            {
                Mesh road = SaveMesh(StripMesh(length, c_RoadWidth, c_RoadWidth, true), $"SM_Road_{suffix}");
                GameObject roadGo = MeshObject($"Env_Road_{suffix}", road, MaterialLibrary.Get("M_Road"), false);
                SetStaticRecursively(roadGo, StaticEditorFlags.BatchingStatic);
                SavePrefab(roadGo, $"{folder}/Env_Road_{suffix}.prefab");

                Mesh walk = SaveMesh(StripMesh(length, c_SidewalkWidth, c_SidewalkTile, false), $"SM_Sidewalk_{suffix}");
                GameObject walkGo = MeshObject($"Env_Sidewalk_{suffix}", walk, MaterialLibrary.Get("M_Sidewalk"), false);
                SetStaticRecursively(walkGo, StaticEditorFlags.BatchingStatic);
                SavePrefab(walkGo, $"{folder}/Env_Sidewalk_{suffix}.prefab");
            }

            Mesh quad = SaveMesh(QuadXZ(), "SM_QuadXZ");
            (string name, float size)[] decals =
            {
                ("Decal_Crack1", 3.2f), ("Decal_Crack2", 3.6f), ("Decal_Pothole1", 3.4f), ("Decal_Pothole2", 3f), ("Decal_Manhole", 1.1f),
            };
            foreach ((string name, float size) in decals)
            {
                var root = new GameObject(name);
                GameObject visual = MeshObject("Visual", quad, MaterialLibrary.Get("M_" + name), false);
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.up * c_DecalHeight;
                visual.transform.localScale = new Vector3(size, 1f, size);
                SetStaticRecursively(root, StaticEditorFlags.BatchingStatic);
                SavePrefab(root, $"{folder}/{name}.prefab");
            }
        }

        // ------------------------------------------------------------------ stubs for missing models
        private static void BuildStubs()
        {
            string folder = EditorPaths.c_PrefabsEnvironment + "/Stubs";
            EditorPaths.EnsureFolder(folder);
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            foreach (IGrouping<string, (string prefab, string material, Vector3 center, Vector3 size, bool solid)> stub in
                     s_StubParts.GroupBy(p => p.prefab))
            {
                var root = new GameObject(stub.Key);
                bool hasSolid = false;
                var solid = new Bounds();
                var all = new Bounds(stub.First().center, stub.First().size);
                foreach (var part in stub)
                {
                    all.Encapsulate(new Bounds(part.center, part.size));
                    GameObject go = MeshObject(part.material.Replace("M_Stub_", ""), cube, MaterialLibrary.Get(part.material), true);
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = part.center;
                    go.transform.localScale = part.size;
                    if (!part.solid)
                        continue;
                    if (!hasSolid) { solid = new Bounds(part.center, part.size); hasSolid = true; }
                    else solid.Encapsulate(new Bounds(part.center, part.size));
                }
                if (hasSolid)
                    AddBox(root, solid);
                SetLayerRecursively(root, Layers.c_ObstacleIndex);
                SetStaticRecursively(root, StaticEditorFlags.BatchingStatic);
                SavePrefab(root, $"{folder}/{stub.Key}.prefab");
                Debug.Log($"[PrefabBuilder] STUB {stub.Key}: no model in models/, cube stand-in {all.size:F2} m");
            }
        }

        // ------------------------------------------------------------------ bullet + player
        private static Bullet BuildBullet()
        {
            var go = new GameObject("Bullet") { layer = Layers.c_ProjectileIndex };
            GameObject visual = MeshObject("Visual", Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), MaterialLibrary.Get("M_Bullet"), false);
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(0.12f, 0.12f, 0.55f);
            visual.layer = Layers.c_ProjectileIndex;
            visual.GetComponent<MeshRenderer>().receiveShadows = false;
            go.AddComponent<Bullet>();
            GameObject saved = SavePrefab(go, $"{EditorPaths.c_PrefabsCharacters}/Bullet.prefab");
            return saved.GetComponent<Bullet>();
        }

        private static void BuildPlayer(Bullet bulletPrefab)
        {
            var root = new GameObject("Player") { layer = Layers.c_PlayerIndex, tag = "Player" };
            var cc = root.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.skinWidth = 0.04f;
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 45f;

            // The body is the 2D sprite built by PlayerBuilder; bullets leave from its animated barrel (View/Body/Muzzle).
            var weaponConfig = DataAssetsBuilder.Load<WeaponConfig>(DataAssetsBuilder.c_WeaponConfig);

            var desktop = root.AddComponent<DesktopPlayerInput>();
            var mobile = root.AddComponent<MobilePlayerInput>();
            var provider = root.AddComponent<PlayerInputProvider>();
            var controller = root.AddComponent<PlayerController>();
            var pool = root.AddComponent<BulletPool>();
            var weapon = root.AddComponent<PlayerWeapon>();
            var aimGizmos = root.AddComponent<AimGizmos>();

            Wire(desktop, ("m_AimOrigin", root.transform));
            Wire(aimGizmos, ("m_Weapon", weapon));
            Wire(provider, ("m_Desktop", desktop), ("m_Mobile", mobile));
            Wire(controller, ("m_Config", DataAssetsBuilder.Load<PlayerConfig>(DataAssetsBuilder.c_PlayerConfig)),
                ("m_InputProvider", provider));
            Wire(pool, ("m_Prefab", bulletPrefab));
            Wire(weapon, ("m_Config", weaponConfig),
                ("m_InputProvider", provider), ("m_Pool", pool),
                ("m_Ammo", DataAssetsBuilder.Load<AmmoState>(DataAssetsBuilder.c_AmmoState)));
            PlayerBuilder.BuildView(root, provider, weapon, desktop);
            SetLayerRecursively(root, Layers.c_PlayerIndex);

            SavePrefab(root, $"{EditorPaths.c_PrefabsCharacters}/Player.prefab");
        }

        // ------------------------------------------------------------------ helpers
        public static void Wire(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach ((string field, Object value) in refs)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null)
                    throw new InvalidOperationException($"{target.GetType().Name} has no serialized field {field}");
                p.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject SavePrefab(GameObject go, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        private static GameObject MeshObject(string name, Mesh mesh, Material material, bool castShadows)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        private static Bounds BodyBounds(GameObject root)
        {
            bool any = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.EndsWith("_Grass"))
                    continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        private static void AddBox(GameObject go, Bounds local, float minThickness = 0f)
        {
            var box = go.AddComponent<BoxCollider>();
            Vector3 size = local.size;
            size.x = Mathf.Max(size.x, minThickness);
            size.z = Mathf.Max(size.z, minThickness);
            box.center = local.center;
            box.size = size;
        }

        private static void AddCapsule(GameObject go, Bounds local) =>
            AddCapsule(go, local.center, Mathf.Max(local.size.x, local.size.z) * 0.5f, local.size.y);

        private static void AddCapsule(GameObject go, Vector3 center, float radius, float height)
        {
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.center = center;
            capsule.radius = radius;
            capsule.height = Mathf.Max(height, radius * 2f);
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        private static void SetStaticRecursively(GameObject go, StaticEditorFlags flags)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }
            return null;
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{EditorPaths.c_GeneratedMeshes}/{name}.asset";
            mesh.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);   // keep the GUID so prefab references survive rebuilds
            existing.name = name;
            return existing;
        }

        private static Mesh GridMesh(float sizeX, float sizeZ, int cellsX, int cellsZ, float tileX, float tileZ)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int j = 0; j <= cellsZ; j++)
            for (int i = 0; i <= cellsX; i++)
            {
                float x = -sizeX * 0.5f + sizeX * i / cellsX;
                float z = -sizeZ * 0.5f + sizeZ * j / cellsZ;
                vertices.Add(new Vector3(x, 0f, z));
                uvs.Add(new Vector2(x / tileX, z / tileZ));
            }
            int row = cellsX + 1;
            for (int j = 0; j < cellsZ; j++)
            for (int i = 0; i < cellsX; i++)
            {
                int v00 = j * row + i, v10 = v00 + 1, v01 = v00 + row, v11 = v01 + 1;
                triangles.AddRange(new[] { v00, v01, v11, v00, v11, v10 });
            }
            return Finish(vertices, uvs, triangles);
        }

        /// <summary>Strip along X (length) of the given width (Z). Road: u along the length, v across; otherwise world tiling.</summary>
        private static Mesh StripMesh(float length, float width, float tile, bool roadMapping)
        {
            int segments = Mathf.Max(1, Mathf.CeilToInt(length / 10f));
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float x = -length * 0.5f + length * i / segments;
                vertices.Add(new Vector3(x, 0f, -width * 0.5f));
                vertices.Add(new Vector3(x, 0f, width * 0.5f));
                if (roadMapping)
                {
                    uvs.Add(new Vector2(x / tile, 0f));
                    uvs.Add(new Vector2(x / tile, 1f));
                }
                else
                {
                    uvs.Add(new Vector2(x / tile, -width * 0.5f / tile));
                    uvs.Add(new Vector2(x / tile, width * 0.5f / tile));
                }
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;   // a,c on -z ; b,d on +z
                triangles.AddRange(new[] { a, b, d, a, d, c });
            }
            return Finish(vertices, uvs, triangles);
        }

        private static Mesh QuadXZ()
        {
            var vertices = new List<Vector3> { new(-0.5f, 0f, -0.5f), new(0.5f, 0f, -0.5f), new(-0.5f, 0f, 0.5f), new(0.5f, 0f, 0.5f) };
            var uvs = new List<Vector2> { new(0f, 0f), new(1f, 0f), new(0f, 1f), new(1f, 1f) };
            var triangles = new List<int> { 0, 2, 3, 0, 3, 1 };
            return Finish(vertices, uvs, triangles);
        }

        private static Mesh Finish(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.SetNormals(Enumerable.Repeat(Vector3.up, vertices.Count).ToList());
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
