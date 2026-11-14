using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.EditorTools
{
    /// <summary>Zapisuje raport o materiałach, teksturach i siatkach wybranych obiektów sceny do Screenshots/diag.txt.</summary>
    public static class Diagnostics
    {
        [MenuItem("Mleczarka/9. Diagnostyka sceny", priority = 9)]
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (var name in new[] { "Rolnik" })
            {
                var root = GameObject.Find(name);
                if (!root) { sb.AppendLine("brak " + name); continue; }
                sb.AppendLine($"== {name} rot={root.transform.rotation.eulerAngles} scale={root.transform.lossyScale}");
                foreach (var r in root.GetComponentsInChildren<Renderer>().Take(6))
                {
                    var mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                    sb.AppendLine($"  {r.name} rot={r.transform.rotation.eulerAngles} bounds={r.bounds.size} mesh={(mesh ? mesh.name : "-")} uvs={(mesh ? mesh.uv.Length : 0)} colors={(mesh ? mesh.colors.Length : 0)}");
                    foreach (var m in r.sharedMaterials)
                    {
                        if (!m) continue;
                        var t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") as Texture2D : null;
                        sb.AppendLine($"    mat={m.name} path={AssetDatabase.GetAssetPath(m)} shader={m.shader.name} q={m.renderQueue} kw=[{string.Join(",", m.shaderKeywords)}] clip={(m.HasProperty("_AlphaClip") ? m.GetFloat("_AlphaClip") : -1)} tex={(t ? t.name + " " + t.format + " " + t.width + "x" + t.height + " dim=" + t.dimension : "null")}");
                    }
                }
            }
            var ter = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).FirstOrDefault();
            if (ter)
            {
                var td = ter.terrainData;
                sb.AppendLine($"== Teren pos={ter.transform.position} size={td.size} alphaRes={td.alphamapResolution} layers={td.terrainLayers.Length} mat={(ter.materialTemplate ? ter.materialTemplate.shader.name : "null")}");
                foreach (var l in td.terrainLayers) sb.AppendLine($"  warstwa {(l ? l.name : "null")} diff={(l && l.diffuseTexture ? l.diffuseTexture.name : "null")} tile={(l ? l.tileSize.ToString() : "")}");
                foreach (var w in new[] { new Vector3(0.5f, 0, 20f), new Vector3(0.9f, 0, 6f), new Vector3(20f, 0, 20f) })
                {
                    var lp = w - ter.transform.position;
                    int ax = Mathf.Clamp((int)(lp.x / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
                    int az = Mathf.Clamp((int)(lp.z / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
                    var a = td.GetAlphamaps(ax, az, 1, 1);
                    sb.AppendLine($"  probka {w}: " + string.Join(", ", Enumerable.Range(0, a.GetLength(2)).Select(i => a[0, 0, i].ToString("0.00"))));
                }
            }
            sb.AppendLine($"ambient={RenderSettings.ambientMode} sky={(RenderSettings.skybox ? RenderSettings.skybox.shader.name : "null")}");
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "diag.txt"), sb.ToString());
            Debug.Log("[Mleczarka] Diagnostyka zapisana.");
        }
    }
}
