using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZombiCar.EditorTools
{
    /// <summary>Dumps bounds, children and material names of every imported model (for layout / colliders).</summary>
    public static class ArtDiagnostics
    {
        [MenuItem("Tools/Diagnostics/Dump Models")]
        public static void DumpModels()
        {
            var sb = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                    continue;
                var inst = Object.Instantiate(go);
                var renderers = inst.GetComponentsInChildren<Renderer>(true);
                var b = new Bounds(inst.transform.position, Vector3.zero);
                bool first = true;
                foreach (var r in renderers)
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                var mats = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct();
                sb.AppendLine($"{path} | size {b.size:F2} center {b.center:F2} | rootScale {inst.transform.localScale:F2} | mats [{string.Join(",", mats)}]");
                foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
                {
                    if (t == inst.transform) continue;
                    var r = t.GetComponent<Renderer>();
                    string rb = r != null ? $" bounds c{r.bounds.center:F2} s{r.bounds.size:F2} active={t.gameObject.activeSelf} enabled={r.enabled}" : "";
                    sb.AppendLine($"    - {t.name} pos{t.localPosition:F2} rot{t.localEulerAngles:F0} scl{t.localScale:F2}{rb}");
                }
                Object.DestroyImmediate(inst);
            }
            File.WriteAllText("Logs/models_dump.txt", sb.ToString());
            Debug.Log("[ArtDiagnostics] wrote Logs/models_dump.txt");
        }
    }
}
