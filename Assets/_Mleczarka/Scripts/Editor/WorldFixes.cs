using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mleczarka.EditorTools
{
    /// <summary>
    /// Poprawki świata bez przebudowy mapy (nic nie usuwa, można uruchamiać wielokrotnie):
    /// 1) kolizja na siatkach dróg — postać i koła stoją na asfalcie, a nie na terenie kilka cm pod nim;
    /// 2) słupki pod poręczami rampy wejściowej (poręcze wisiały w powietrzu);
    /// 3) więcej gatunków drzew: 5 drzew TwistedTree (Quaternius, CC0) i rozrzut wielkości — pozycje drzew bez zmian.
    /// Raport: Screenshots/poprawki_swiata.txt
    /// </summary>
    public static class WorldFixes
    {
        const string NAT = "Assets/_Mleczarka/Art/ThirdParty/Quaternius/StylizedNatureMegaKit/FBX/";
        const string TREES = "Assets/_Mleczarka/Art/Generated/Gmina/Drzewa/";
        static readonly string[] NewTrees = { "TwistedTree_1", "TwistedTree_2", "TwistedTree_3", "TwistedTree_4", "TwistedTree_5" };

        [MenuItem("Mleczarka/Testy/Popraw świat: drogi, poręcze, drzewa")]
        public static void Run()
        {
            if (Application.isPlaying) { Debug.LogWarning("[Mleczarka] Zatrzymaj tryb gry."); return; }
            var log = new StringBuilder();
            Roads(log); RampRails(log); Trees(log); SummerLeaves(log);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Screenshots"); File.WriteAllText("Screenshots/poprawki_swiata.txt", log.ToString());
            Debug.Log("[Mleczarka] Poprawki świata: Screenshots/poprawki_swiata.txt");
        }

        static void Roads(StringBuilder log)
        {
            int added = 0, total = 0;
            foreach (var drogi in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "Drogi"))
                foreach (var mf in drogi.GetComponentsInChildren<MeshFilter>(true))
                {
                    total++;
                    if (mf.GetComponent<Collider>() || !mf.sharedMesh) continue;
                    var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; added++;
                }
            log.AppendLine($"Drogi: {total} odcinków, dodana kolizja: {added}");
        }

        static void RampRails(StringBuilder log)
        {
            int posts = 0;
            foreach (var rail in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(t => t.name == "Poręcz rampy").ToArray())
            {
                var parent = rail.parent;
                foreach (Transform old in parent.Cast<Transform>().Where(c => c.name == "Słupek poręczy " + rail.GetSiblingIndex()).ToArray()) Object.DestroyImmediate(old.gameObject);
                var mr = rail.GetComponent<MeshRenderer>();
                float len = rail.lossyScale.z;
                int n = Mathf.Max(2, Mathf.CeilToInt(len / 1.4f) + 1);
                for (int i = 0; i < n; i++)
                {
                    float f = Mathf.Lerp(-0.47f, 0.47f, i / (float)(n - 1));
                    var top = rail.position + rail.forward * (f * len);
                    float bottom = top.y - 1.0f;
                    if (Physics.Raycast(top - Vector3.up * 0.1f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) bottom = hit.point.y;
                    float h = top.y - bottom; if (h < 0.2f) continue;
                    var p = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(p.GetComponent<Collider>());
                    p.name = "Słupek poręczy " + rail.GetSiblingIndex();
                    p.transform.SetParent(parent, true);
                    p.transform.position = new Vector3(top.x, bottom + h / 2, top.z);
                    p.transform.rotation = Quaternion.Euler(0, rail.eulerAngles.y, 0);
                    p.transform.localScale = new Vector3(0.07f, h, 0.07f);
                    if (mr) p.GetComponent<MeshRenderer>().sharedMaterial = mr.sharedMaterial;
                    GameObjectUtility.SetStaticEditorFlags(p, StaticEditorFlags.BatchingStatic);
                    posts++;
                }
            }
            log.AppendLine($"Poręcze rampy: dodane słupki: {posts}");
        }

        static float RawHeight(GameObject src)
        {
            var inst = (GameObject)Object.Instantiate(src); inst.transform.position = Vector3.zero;
            var rs = inst.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            Object.DestroyImmediate(inst); return b.size.y;
        }

        static GameObject TreePrefab(string n, float k)
        {
            string path = TREES + n + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing) return existing;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(NAT + n + ".fbx"); if (!src) return null;
            var inst = (GameObject)Object.Instantiate(src); inst.transform.position = Vector3.zero;
            var combine = new List<CombineInstance>(); var mats = new List<Material>();
            var scale = Matrix4x4.Scale(Vector3.one * k);
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.GetComponent<MeshRenderer>(); if (!mr || !mf.sharedMesh) continue;
                for (int sm = 0; sm < mf.sharedMesh.subMeshCount; sm++)
                {
                    combine.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = sm, transform = scale * mf.transform.localToWorldMatrix });
                    mats.Add(mr.sharedMaterials[Mathf.Min(sm, mr.sharedMaterials.Length - 1)]);
                }
            }
            Object.DestroyImmediate(inst);
            var mesh = new Mesh { name = n + "_drzewo", indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(combine.ToArray(), false, true); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, TREES + n + "_siatka.asset");
            foreach (var m in mats) if (m && !m.enableInstancing) { m.enableInstancing = true; EditorUtility.SetDirty(m); }
            var root = new GameObject(n);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
            var c = root.AddComponent<CapsuleCollider>(); c.radius = 0.35f; c.height = 5f; c.center = Vector3.up * 2.5f;
            var pf = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return pf;
        }

        /// <summary>Liście TwistedTree są w paczce jesienne (czerwone) — wersja letnia: ta sama tekstura przesunięta w zieleń.</summary>
        static void SummerLeaves(StringBuilder log)
        {
            const string tex = "Assets/_Mleczarka/Art/Textures/Drzewa/Leaves_TwistedTree_Lato_C.png";
            const string matPath = TREES + "Leaves_TwistedTree_Lato.mat";
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Mleczarka/Art/Materials/Quaternius/Leaves_TwistedTree.mat");
            var t2d = AssetDatabase.LoadAssetAtPath<Texture2D>(tex);
            if (!src || !t2d) { log.AppendLine("Liście: brak materiału lub tekstury"); return; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!m) { m = new Material(src); AssetDatabase.CreateAsset(m, matPath); }
            m.SetTexture("_BaseMap", t2d); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t2d); m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            int n = 0;
            foreach (var name in NewTrees)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(TREES + name + ".prefab"); if (!pf) continue;
                var mr = pf.GetComponent<MeshRenderer>(); var mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) if (mats[i] && mats[i].name.StartsWith("Leaves_TwistedTree")) { mats[i] = m; n++; }
                mr.sharedMaterials = mats; EditorUtility.SetDirty(pf); PrefabUtility.SavePrefabAsset(pf);
            }
            foreach (var t in Terrain.activeTerrains) t.terrainData.RefreshPrototypes();
            log.AppendLine($"Liście TwistedTree: letnia zieleń w {n} materiałach");
        }

        [MenuItem("Mleczarka/Testy/Drzewa: kolizja pni wg modelu")]
        public static void TrunkMenu()
        {
            var log = new StringBuilder(); TrunkColliders(log); AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Screenshots"); File.WriteAllText("Screenshots/kolizja_pni.txt", log.ToString());
            Debug.Log("[Mleczarka] Kolizja pni: Screenshots/kolizja_pni.txt");
        }

        /// <summary>
        /// Kolizja drzewa była stałą kapsułą 0,35 m — grube pnie (do 1,5 m promienia) dało się przejść na wylot.
        /// Promień i środek liczymy z wierzchołków kory na wysokości 0,3–2 m (80. centyl odległości od osi).
        /// </summary>
        static void TrunkColliders(StringBuilder log)
        {
            foreach (var path in Directory.GetFiles(TREES, "*.prefab"))
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                var mf = pf ? pf.GetComponent<MeshFilter>() : null; var mr = pf ? pf.GetComponent<MeshRenderer>() : null;
                if (!mf || !mf.sharedMesh || !mr) continue;
                var mesh = mf.sharedMesh; var verts = mesh.vertices; var mats = mr.sharedMaterials;
                var pts = new List<Vector3>();
                for (int sm = 0; sm < mesh.subMeshCount; sm++)
                {
                    var mat = mats[Mathf.Min(sm, mats.Length - 1)];
                    if (!mat || !(mat.name.Contains("Bark") || mat.name.Contains("Trunk") || mat.name.Contains("Wood"))) continue;
                    foreach (int i in mesh.GetTriangles(sm)) { var v = verts[i]; if (v.y > 0.3f && v.y < 2.0f) pts.Add(v); }
                }
                if (pts.Count < 10) { log.AppendLine($"{pf.name}: za mało wierzchołków kory ({pts.Count}) — bez zmian"); continue; }
                var c = new Vector3(pts.Average(p => p.x), 0, pts.Average(p => p.z));
                var d = pts.Select(p => new Vector2(p.x - c.x, p.z - c.z).magnitude).OrderBy(x => x).ToList();
                float r = Mathf.Clamp(d[(int)(d.Count * 0.8f)], 0.25f, 2.5f);
                float top = Mathf.Min(mesh.bounds.max.y * 0.6f, 8f), h = Mathf.Max(top, r * 2 + 0.5f);
                var col = pf.GetComponent<CapsuleCollider>(); if (!col) col = pf.AddComponent<CapsuleCollider>();
                col.direction = 1; col.radius = r; col.height = h; col.center = new Vector3(c.x, h / 2, c.z);
                EditorUtility.SetDirty(pf); PrefabUtility.SavePrefabAsset(pf);
                log.AppendLine($"{pf.name}: promień pnia {r:0.00} m, środek ({c.x:0.00}, {c.z:0.00}), wysokość {h:0.0} m");
            }
            foreach (var t in Terrain.activeTerrains) { t.terrainData.RefreshPrototypes(); t.Flush(); var tc = t.GetComponent<TerrainCollider>(); if (tc) { tc.enabled = false; tc.enabled = true; } }
        }

        static void Trees(StringBuilder log)
        {
            // skala jak przy istniejących drzewach liściastych
            var refPf = AssetDatabase.LoadAssetAtPath<GameObject>(TREES + "CommonTree_1.prefab");
            var refSrc = AssetDatabase.LoadAssetAtPath<GameObject>(NAT + "CommonTree_1.fbx");
            if (!refPf || !refSrc) { log.AppendLine("Drzewa: brak wzorca CommonTree_1 — pominięte"); return; }
            float k = refPf.GetComponent<MeshFilter>().sharedMesh.bounds.size.y / RawHeight(refSrc);
            var news = NewTrees.Select(n => TreePrefab(n, k)).Where(p => p).ToArray();
            log.AppendLine($"Drzewa: nowe modele {news.Length} (skala {k:0.###})");
            int changed = 0, total = 0;
            foreach (var t in Terrain.activeTerrains)
            {
                var td = t.terrainData;
                if (td.treePrototypes.Any(p => p.prefab && p.prefab.name.StartsWith("TwistedTree"))) { log.AppendLine($"  {t.name}: już zrobione"); continue; }
                var protos = td.treePrototypes.ToList(); int first = protos.Count;
                protos.AddRange(news.Select(p => new TreePrototype { prefab = p, bendFactor = 0 }));
                td.treePrototypes = protos.ToArray();
                var leafy = new HashSet<int>(Enumerable.Range(0, first).Where(i => protos[i].prefab && protos[i].prefab.name.StartsWith("CommonTree")));
                var rnd = new System.Random(t.name.GetHashCode());
                var arr = td.treeInstances;
                for (int i = 0; i < arr.Length; i++)
                {
                    total++;
                    var ti = arr[i];
                    // liściaste: ok. 45% na nowe gatunki; sosny i liściaste — większy rozrzut wielkości
                    if (leafy.Contains(ti.prototypeIndex) && rnd.NextDouble() < 0.45) { ti.prototypeIndex = first + rnd.Next(news.Length); changed++; }
                    float s = (float)(0.82 + rnd.NextDouble() * 0.36);
                    ti.widthScale *= s; ti.heightScale *= s * (float)(0.92 + rnd.NextDouble() * 0.16);
                    arr[i] = ti;
                }
                td.SetTreeInstances(arr, true);
                EditorUtility.SetDirty(td);
            }
            log.AppendLine($"Drzewa: {total} szt., zmieniony gatunek: {changed}");
        }
    }
}
