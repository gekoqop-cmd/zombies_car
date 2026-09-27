using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ZombiCar.Core;
using ZombiCar.Player;

namespace ZombiCar.EditorTools
{
    /// <summary>
    /// Tools/Diagnostics/Aim Test: puts zombies around the player in Map01 at different distances and directions,
    /// "places the cursor" on each zombie's body (screen point of its centre), and compares the shot of the old aim
    /// (cursor projected on the ground, muzzle offset to the side) with the current one (<see cref="AimSolver"/> +
    /// centred muzzle at barrel height). Logs the miss distance per case and renders Screenshots/AimTest_*.png with
    /// the gizmo geometry drawn as markers (camera renders do not include Gizmos): white = cursor on the body,
    /// yellow = camera ray, green/red/orange sphere = aim point, cyan = muzzle, red line = old shot, green = new shot.
    /// </summary>
    public static class AimTest
    {
        private const string c_Output = "Screenshots";
        private const int c_Width = 1920;
        private const int c_Height = 1080;
        private const float c_FacingLag = 40f;                           // "moving" case: body still turning
        private static readonly Vector3 s_OldMuzzleLocal = new Vector3(0.32f, 1.05f, 0.78f);
        private static readonly Vector3 s_PlayerPosition = new Vector3(-6f, 0f, -6f);

        private sealed class Case
        {
            public string Name;
            public Vector2 Offset;          // zombie position relative to the player (x east, y north)
            public float CursorSideways;    // cursor this far to the screen-right of the body (near miss)
            public bool Lag;
        }

        private static readonly Case[] s_Cases =
        {
            new Case { Name = "east 3 m", Offset = new Vector2(3f, 0f) },
            new Case { Name = "east 9 m", Offset = new Vector2(9f, 0.5f) },
            new Case { Name = "north-east 12 m", Offset = new Vector2(8.5f, 8.5f) },
            new Case { Name = "north 8 m", Offset = new Vector2(-0.5f, 8f) },
            new Case { Name = "south 5 m (to camera)", Offset = new Vector2(1f, -5f) },
            new Case { Name = "west 6 m, moving", Offset = new Vector2(-6f, 1.5f), Lag = true },
            new Case { Name = "south-west 7 m, moving", Offset = new Vector2(-5f, -5f), Lag = true },
            new Case { Name = "east 6 m, cursor 0.3 m off", Offset = new Vector2(6f, -2.5f), CursorSideways = 0.3f },
            new Case { Name = "NE 7 m, cursor 0.75 m off", Offset = new Vector2(5f, 4.5f), CursorSideways = 0.75f },
        };

        private sealed class Shot
        {
            public Vector3 Muzzle;
            public Vector3 Direction;
            public Vector3 AimPoint;
            public AimSource Source;
            public Collider Hit;
            public float Miss;              // horizontal distance from the zombie's axis to the bullet path
        }

        [MenuItem("Tools/Diagnostics/Aim Test")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Scene scene = EditorSceneManager.OpenScene(SceneBuilder.c_MapScene, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            Camera cam = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First();
            PlayerWeapon weapon = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlayerWeapon>(true)).First();
            WeaponConfig config = weapon.Config;
            Transform player = weapon.transform;
            player.position = s_PlayerPosition;
            GameObject zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("Zombie_Walker t:Prefab", new[] { EditorPaths.c_PrefabsEnemies })[0]));

            Quaternion camRotation = Quaternion.Euler(ViewConstants.c_CameraPitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(s_PlayerPosition + camRotation * new Vector3(0f, 0f, -ViewConstants.c_CameraDistance), camRotation);
            cam.fieldOfView = ViewConstants.c_CameraFov;
            cam.aspect = (float)c_Width / c_Height;

            var zombies = new List<Collider>();
            foreach (Case c in s_Cases)
            {
                var z = (GameObject)PrefabUtility.InstantiatePrefab(zombiePrefab, scene);
                z.transform.position = s_PlayerPosition + new Vector3(c.Offset.x, 0f, c.Offset.y);
                zombies.Add(z.GetComponent<Collider>());
            }
            Physics.SyncTransforms();

            var report = new StringBuilder("[AimTest] miss = horizontal distance from the zombie's axis to the bullet path " +
                                           "(zombie radius " + ((CapsuleCollider)zombies[0]).radius.ToString("F2") + " m)\n");
            report.AppendLine($"{"case",-28}{"OLD miss",10}{"OLD",6}{"NEW miss",10}{"NEW",6}  aim source");
            var markers = new List<GameObject>();
            var oldShots = new List<Shot>();
            var newShots = new List<Shot>();
            for (int i = 0; i < s_Cases.Length; i++)
            {
                Case c = s_Cases[i];
                Collider zombie = zombies[i];
                foreach (Collider other in zombies)
                    other.enabled = other == zombie;                     // one target at a time
                Physics.SyncTransforms();

                // The cursor covers the body's centre (bounds are only valid on an enabled collider).
                Vector3 body = zombie.bounds.center + cam.transform.right * c.CursorSideways;
                Ray ray = cam.ScreenPointToRay(cam.WorldToScreenPoint(body));

                Shot oldShot = OldShot(ray, player, c.Lag, config, zombie);
                Shot newShot = NewShot(ray, player, weapon, c.Lag, config, zombie);
                oldShots.Add(oldShot);
                newShots.Add(newShot);
                report.AppendLine($"{c.Name,-28}{oldShot.Miss,9:F2}m{(oldShot.Hit == zombie ? " HIT" : " miss"),6}" +
                                  $"{newShot.Miss,9:F2}m{(newShot.Hit == zombie ? " HIT" : " miss"),6}  {newShot.Source}" +
                                  $"  (aim {newShot.AimPoint:F2}, body {zombie.bounds.center:F2}, cursor {body:F2})");
                markers.Add(Marker("Cursor", body, 0.14f, Color.white));
            }
            foreach (Collider z in zombies)
                z.enabled = true;
            Debug.Log(report.ToString());

            string folder = Path.GetFullPath(c_Output);
            Directory.CreateDirectory(folder);
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            RenderShots(cam, folder, "AimTest_Old.png", oldShots, false);
            RenderShots(cam, folder, "AimTest_New.png", newShots, true);
            ShaderUtil.allowAsyncCompilation = asyncCompilation;
            EditorSceneManager.OpenScene(SceneBuilder.c_MapScene, OpenSceneMode.Single);   // discard the test objects
        }

        /// <summary>Previous behaviour: cursor projected on the ground (y = 0), muzzle 0.32 m to the side, parallel shot.</summary>
        private static Shot OldShot(Ray ray, Transform player, bool lag, WeaponConfig config, Collider zombie)
        {
            new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d);
            Vector3 ground = ray.GetPoint(d);
            Vector3 dir = ground - player.position;
            dir.y = 0f;
            dir.Normalize();
            Quaternion facing = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, lag ? c_FacingLag : 0f, 0f);
            Vector3 muzzle = player.position + facing * s_OldMuzzleLocal;
            return Trace(muzzle, dir, ground, AimSource.Plane, config, zombie);
        }

        private static Shot NewShot(Ray ray, Transform player, PlayerWeapon weapon, bool lag, WeaponConfig config, Collider zombie)
        {
            Vector3 muzzleHeight = weapon.Muzzle.position;
            AimSolution aim = AimSolver.FromRay(ray, muzzleHeight.y, config);
            Vector3 toAim = aim.Point - player.position;
            toAim.y = 0f;
            player.rotation = Quaternion.LookRotation(toAim) * Quaternion.Euler(0f, lag ? c_FacingLag : 0f, 0f);
            Physics.SyncTransforms();
            Vector3 muzzle = weapon.Muzzle.position;
            AimSolver.LevelDirection(muzzle, aim.Point, 0.25f, out Vector3 dir);
            return Trace(muzzle, dir, aim.Point, aim.Source, config, zombie);
        }

        private static Shot Trace(Vector3 muzzle, Vector3 dir, Vector3 aimPoint, AimSource source, WeaponConfig config, Collider zombie)
        {
            var shot = new Shot { Muzzle = muzzle, Direction = dir, AimPoint = aimPoint, Source = source };
            Physics.SphereCast(muzzle, config.BulletRadius, dir, out RaycastHit hit, config.Range, Layers.ProjectileBlockers,
                QueryTriggerInteraction.Ignore);
            shot.Hit = hit.collider;
            Vector3 toZombie = zombie.bounds.center - muzzle;
            toZombie.y = 0f;
            shot.Miss = Vector3.Cross(dir, toZombie).magnitude;          // |dir| = 1: distance from the path
            return shot;
        }

        private static void RenderShots(Camera cam, string folder, string file, List<Shot> shots, bool current)
        {
            var temp = new List<GameObject>();
            Color path = current ? new Color(0.1f, 1f, 0.2f) : new Color(1f, 0.15f, 0.1f);
            foreach (Shot s in shots)
            {
                Color aimColor = s.Source == AimSource.Enemy ? Color.red : s.Source == AimSource.Assist ? new Color(1f, 0.55f, 0f) :
                    current ? Color.green : Color.yellow;
                temp.Add(Marker("Aim", s.AimPoint, 0.16f, aimColor));
                temp.Add(Marker("Muzzle", s.Muzzle, 0.1f, Color.cyan));
                temp.Add(Line("CameraRay", s.AimPoint, s.AimPoint + (cam.transform.position - s.AimPoint).normalized * 4f, 0.025f, Color.yellow));
                temp.Add(Line("Shot", s.Muzzle, s.Muzzle + s.Direction * 14f, 0.05f, path));
            }
            MapScreenshots.Render(cam, c_Width, c_Height, null);
            MapScreenshots.Render(cam, c_Width, c_Height, Path.Combine(folder, file));
            foreach (GameObject go in temp)
                Object.DestroyImmediate(go);
            Debug.Log($"[AimTest] {Path.Combine(folder, file)}");
        }

        private static Material Unlit(Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", color);
            return mat;
        }

        private static GameObject Marker(string name, Vector3 position, float radius, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "AimTest_" + name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = Vector3.one * radius * 2f;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Unlit(color);
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static GameObject Line(string name, Vector3 from, Vector3 to, float width, Color color)
        {
            var go = new GameObject("AimTest_" + name);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = line.endWidth = width;
            line.sharedMaterial = Unlit(color);
            line.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
    }
}
