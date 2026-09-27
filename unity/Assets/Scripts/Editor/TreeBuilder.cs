using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ZombiCar.Core;
using Random = System.Random;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Low-poly background trees generated in code (the art set has no tree model): a tapered six-sided trunk and a
    /// crown of a few jittered, flat-shaded icospheres in two greens. Meshes go to Models/Generated, prefabs to
    /// Prefabs/Environment/Nature (Tree_LowPoly1..). Only the trunk is solid. Called by <see cref="PrefabBuilder"/>.
    /// </summary>
    public static class TreeBuilder
    {
        public const string c_Prefix = "Tree_LowPoly";
        private const string c_Folder = EditorPaths.c_PrefabsEnvironment + "/Nature";
        private const int c_TrunkSides = 6;

        private sealed class TreeSpec
        {
            public int Seed;
            public float TrunkHeight;
            public float TrunkRadius;
            public (Vector3 center, float radius, bool light)[] Blobs;
        }

        // Crowns ~4.5 m wide, tops at ~6.5 m: the size of the stand-ins they replace.
        private static readonly TreeSpec[] s_Trees =
        {
            new TreeSpec
            {
                Seed = 11, TrunkHeight = 3.2f, TrunkRadius = 0.3f,
                Blobs = new[]
                {
                    (new Vector3(0f, 4.3f, 0f), 1.9f, false), (new Vector3(-1.1f, 3.8f, 0.5f), 1.3f, true),
                    (new Vector3(1.1f, 3.9f, -0.4f), 1.35f, false), (new Vector3(0.2f, 5.5f, 0.1f), 1.25f, true),
                },
            },
            new TreeSpec
            {
                Seed = 23, TrunkHeight = 2.8f, TrunkRadius = 0.26f,
                Blobs = new[]
                {
                    (new Vector3(0f, 4f, 0f), 1.7f, true), (new Vector3(0.9f, 3.6f, 0.8f), 1.2f, false),
                    (new Vector3(-0.9f, 4.4f, -0.5f), 1.3f, false), (new Vector3(0.1f, 5.2f, -0.2f), 1.1f, true),
                    (new Vector3(1.0f, 4.8f, -0.8f), 0.9f, false),
                },
            },
        };

        public static int Count => s_Trees.Length;

        public static void BuildAll()
        {
            EditorPaths.EnsureFolder(c_Folder);
            EditorPaths.EnsureFolder(EditorPaths.c_GeneratedMeshes);
            for (int i = 0; i < s_Trees.Length; i++)
                Build($"{c_Prefix}{i + 1}", s_Trees[i]);
        }

        private static void Build(string name, TreeSpec spec)
        {
            var rnd = new Random(spec.Seed);
            Mesh trunk = SaveMesh(Trunk(spec.TrunkHeight, spec.TrunkRadius), $"SM_{name}_Trunk");
            Mesh crown = SaveMesh(Crown(spec.Blobs, rnd), $"SM_{name}_Crown");

            var root = new GameObject(name);
            AddPart(root, "Trunk", trunk, new[] { MaterialLibrary.Get("M_Tree_Bark") });
            AddPart(root, "Crown", crown, new[] { MaterialLibrary.Get("M_Tree_Crown"), MaterialLibrary.Get("M_Tree_CrownLight") });
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, spec.TrunkHeight * 0.5f, 0f);
            box.size = new Vector3(spec.TrunkRadius * 2f, spec.TrunkHeight, spec.TrunkRadius * 2f);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = Layers.c_ObstacleIndex;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
            PrefabUtility.SaveAsPrefabAsset(root, $"{c_Folder}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        private static void AddPart(GameObject root, string name, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = materials;
            mr.shadowCastingMode = ShadowCastingMode.On;
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{EditorPaths.c_GeneratedMeshes}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.name = name;
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);   // keeps the asset's GUID for the prefabs
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // Tapered six-sided trunk, flat-shaded, sunk slightly into the ground and into the crown.
        private static Mesh Trunk(float height, float radius)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            const float bottom = -0.1f;
            float topRadius = radius * 0.55f;
            for (int i = 0; i < c_TrunkSides; i++)
            {
                float a0 = i * Mathf.PI * 2f / c_TrunkSides, a1 = (i + 1) * Mathf.PI * 2f / c_TrunkSides;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * radius, bottom, Mathf.Sin(a0) * radius);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * radius, bottom, Mathf.Sin(a1) * radius);
                Vector3 t0 = new Vector3(Mathf.Cos(a0) * topRadius, height, Mathf.Sin(a0) * topRadius);
                Vector3 t1 = new Vector3(Mathf.Cos(a1) * topRadius, height, Mathf.Sin(a1) * topRadius);
                AddTriangle(vertices, triangles, b0, t1, b1);
                AddTriangle(vertices, triangles, b0, t0, t1);
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Crown: jittered icospheres (one subdivision, 80 faces each), dark and light ones on two sub-meshes.
        private static Mesh Crown((Vector3 center, float radius, bool light)[] blobs, Random rnd)
        {
            var vertices = new List<Vector3>();
            var dark = new List<int>();
            var light = new List<int>();
            (Vector3[] sphere, int[] faces) = Icosphere();
            foreach ((Vector3 center, float radius, bool isLight) in blobs)
            {
                var jittered = new Vector3[sphere.Length];
                for (int i = 0; i < sphere.Length; i++)
                {
                    float r = radius * (0.86f + 0.26f * (float)rnd.NextDouble());
                    Vector3 v = sphere[i] * r;
                    v.y *= 0.82f;                                     // slightly flattened, reads as foliage
                    jittered[i] = center + v;
                }
                List<int> target = isLight ? light : dark;
                for (int f = 0; f < faces.Length; f += 3)
                    AddTriangle(vertices, target, jittered[faces[f]], jittered[faces[f + 1]], jittered[faces[f + 2]]);
            }
            var mesh = new Mesh { subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(dark, 0);
            mesh.SetTriangles(light, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Unshared vertices per face: flat shading after RecalculateNormals.
        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(i);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
        }

        private static (Vector3[] vertices, int[] faces) Icosphere()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            // One subdivision: every face into four, midpoints shared.
            var midpoints = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (midpoints.TryGetValue(key, out int index))
                    return index;
                v.Add((v[a] + v[b]) * 0.5f);
                midpoints[key] = v.Count - 1;
                return v.Count - 1;
            }
            var subdivided = new List<int>();
            for (int f = 0; f < faces.Length; f += 3)
            {
                int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                subdivided.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            for (int i = 0; i < v.Count; i++)
                v[i] = v[i].normalized;
            return (v.ToArray(), subdivided.ToArray());
        }
    }
}
