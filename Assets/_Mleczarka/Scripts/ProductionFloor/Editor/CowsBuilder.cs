using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>
    /// Krowy (model CC0 „Farm Animals” Quaternius): przy każdym gospodarstwie tyle krów, ile ma w danych (krowa = 20 l na dobę),
    /// nowe miejsca skupu mleka w istniejących domach wsi bez gospodarstw i własna krowa na działce przy domu gracza (mleko za darmo).
    /// Nie stawia nowych budynków. Można uruchamiać wielokrotnie — przebudowuje tylko swoje obiekty. Raport: Screenshots/krowy.txt
    /// </summary>
    public static class CowsBuilder
    {
        const string RootName = "Krowy i nowe miejsca skupu";
        const string Dir = "Assets/_Mleczarka/Art/ThirdParty/Quaternius/FarmAnimals/";
        const string Fbx = Dir + "Cow.fbx", Ctrl = Dir + "Krowa.controller", PrefabPath = "Assets/_Mleczarka/Prefabs/World/Krowa.prefab";
        const float CowLength = 2.3f;

        [MenuItem("Mleczarka/Kariera/6. Krowy, nowe miejsca skupu i własna krowa")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("[Mleczarka] Zatrzymaj tryb gry."); return; }
            var log = new StringBuilder();
            var prefab = SetupPrefab(log);
            if (!prefab) { Debug.LogError("[Mleczarka] Krowa: brak modelu " + Fbx); return; }

            var old = GameObject.Find(RootName); if (old) Object.DestroyImmediate(old);
            var root = new GameObject(RootName).transform;
            Physics.SyncTransforms();

            var zones = Object.FindObjectsByType<WorldPlace>(FindObjectsSortMode.None).ToList();
            var houses = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Chata_Podlaska") && (t.parent == null || !t.parent.name.StartsWith("Chata_Podlaska"))).ToList();
            log.AppendLine($"Domów wiejskich: {houses.Count}, stref: {zones.Count}");

            // ---- nowe miejsca skupu: gospodarstwa z danych, które nie mają jeszcze strefy w świecie ----
            var farmZones = zones.Where(z => z.kind == PlaceKind.Gospodarstwo && z.farmId >= 0).ToList();
            var missing = Enumerable.Range(0, GameData.Farms.Length).Where(i => !farmZones.Any(z => z.farmId == i)).ToList();
            var cands = new List<(Transform house, Vector3 center, Vector3 road, string village)>();
            foreach (var h in houses)
            {
                var c = Center(h);
                if (zones.Any(z => Flat(z.transform.position - c) < z.radius + 14f)) continue;
                var road = RoadNear(c, 35f);
                if (road == null) continue;
                cands.Add((h, c, road.Value, h.parent ? h.parent.name : "-"));
            }
            log.AppendLine($"Domy wolne przy drodze: {cands.Count} w {cands.Select(x => x.village).Distinct().Count()} miejscowościach");
            var farmPos = farmZones.Select(z => z.transform.position).ToList();
            var villagesWithFarm = farmZones.Select(z => z.transform.parent ? z.transform.parent.name : "-").GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            foreach (var fid in missing)
            {
                if (cands.Count == 0) { log.AppendLine($"UWAGA: brak domu na gospodarstwo {GameData.Farms[fid].Name}"); continue; }
                // rozrzut po gminie: najdalej od innych gospodarstw; wieś, która ma już gospodarstwo, liczy się słabiej
                var pick = cands.OrderByDescending(x =>
                {
                    float d = farmPos.Count == 0 ? 999f : farmPos.Min(f => Flat(f - x.center));
                    int n = villagesWithFarm.TryGetValue(x.village, out var k) ? k : 0;
                    return d / (1 + n * 0.8f);
                }).First();
                cands.RemoveAll(x => Flat(x.center - pick.center) < 60f);
                var toRoad = pick.road - pick.center; toRoad.y = 0;
                var gate = Ground(pick.center + toRoad.normalized * Mathf.Min(toRoad.magnitude * 0.7f, 14f));
                var z = new GameObject($"Strefa: {GameData.Farms[fid].Name} ({pick.village})").AddComponent<WorldPlace>();
                z.transform.SetParent(root, true); z.transform.position = gate;
                z.kind = PlaceKind.Gospodarstwo; z.radius = 13f; z.farmId = fid; z.displayName = GameData.Farms[fid].Name;
                farmZones.Add(z); farmPos.Add(gate); zones.Add(z);
                villagesWithFarm[pick.village] = (villagesWithFarm.TryGetValue(pick.village, out var cnt) ? cnt : 0) + 1;
                log.AppendLine($"Nowe miejsce skupu: {GameData.Farms[fid].Name} — {pick.village}, dom „{pick.house.name}” {pick.center:F0}, wjazd {gate:F0}");
            }

            // ---- krowy przy gospodarstwach ----
            var taken = new List<Vector3>();
            int total = 0;
            foreach (var z in farmZones.OrderBy(z => z.farmId))
            {
                int cows = CareerSim.Cows(z.farmId);
                var house = houses.OrderBy(h => Flat(Center(h) - z.transform.position)).FirstOrDefault();
                var center = house && Flat(Center(house) - z.transform.position) < 30f ? Center(house) : z.transform.position;
                var spots = FindSpots(center, z.transform.position, cows, 8f, 26f, taken);
                var g = new GameObject($"Krowy: {z.displayName} ({cows})").transform; g.SetParent(root, false);
                for (int i = 0; i < spots.Count; i++) { Spawn(prefab, g, spots[i], center, $"Krowa {i + 1}"); taken.Add(spots[i]); total++; }
                log.AppendLine($"{z.displayName}: krowy {spots.Count}/{cows} ({cows * CareerData.LitersPerCow:0} l na dobę){(spots.Count < cows ? " — UWAGA: za mało miejsca" : "")}");
            }

            // ---- własna krowa na działce przy domu ----
            var dom = zones.FirstOrDefault(z => z.kind == PlaceKind.Dom);
            if (dom)
            {
                float maxR = Mathf.Max(5f, dom.radius - 2f);
                var spot = FindSpots(dom.transform.position, dom.transform.position + Vector3.forward * 100f, 1, 4f, maxR, taken, 2f);
                if (spot.Count > 0)
                {
                    var own = Spawn(prefab, root, spot[0], dom.transform.position, "Własna krowa (Krasula)");
                    var cow = own.GetComponent<Cow>(); cow.own = true; cow.wanderRadius = 1.5f;
                    log.AppendLine($"Własna krowa: {spot[0]:F0}, {Flat(spot[0] - dom.transform.position):0.0} m od środka domu (promień domu {dom.radius:0})");
                }
                else log.AppendLine("UWAGA: brak miejsca na własną krowę przy domu.");
            }
            log.AppendLine($"Razem krów: {total} + własna");

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("Screenshots"); File.WriteAllText("Screenshots/krowy.txt", log.ToString());
            Debug.Log("[Mleczarka] Krowy: Screenshots/krowy.txt");
        }

        // ---------- model, materiały, animacje, prefab ----------
        static GameObject SetupPrefab(StringBuilder log)
        {
            var imp = AssetImporter.GetAtPath(Fbx) as ModelImporter; if (!imp) return null;
            bool dirty = false;
            if (imp.animationType != ModelImporterAnimationType.Generic) { imp.animationType = ModelImporterAnimationType.Generic; dirty = true; }
            if (imp.importCameras || imp.importLights) { imp.importCameras = false; imp.importLights = false; dirty = true; }
            if (!imp.importAnimation) { imp.importAnimation = true; dirty = true; }
            if (dirty) { imp.SaveAndReimport(); }
            var clips = imp.defaultClipAnimations;
            log.AppendLine($"Import: animacje {(imp.importAnimation ? "tak" : "nie")}, ujęcia w pliku: {string.Join(", ", clips.Select(c => c.name))}, ustawione: {imp.clipAnimations.Length}");
            if (clips.Length > 0 && imp.clipAnimations.Length != clips.Length)
            {
                foreach (var c in clips) { string n = c.name.ToLower(); c.loopTime = n.Contains("idle") || n.Contains("walk") || n.Contains("run"); }
                imp.clipAnimations = clips; dirty = true;
            }
            // materiały URP z kolorami z modelu (biały, czarny, różowy)
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var m in AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Material>().ToArray())
            {
                string path = Dir + "Krowa_" + m.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat)
                {
                    mat = new Material(shader) { name = "Krowa_" + m.name };
                    var col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.color : Color.white;
                    string mn = m.name.ToLower();
                    if (mn.Contains("white")) col = new Color(0.92f, 0.9f, 0.86f);
                    else if (mn.Contains("black")) col = new Color(0.07f, 0.065f, 0.06f);
                    else if (mn.Contains("pink")) col = new Color(0.86f, 0.58f, 0.56f);
                    mat.SetColor("_BaseColor", col); mat.SetFloat("_Smoothness", 0.15f);
                    AssetDatabase.CreateAsset(mat, path);
                }
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), mat); dirty = true;
            }
            if (dirty) imp.SaveAndReimport();

            var all = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            AnimationClip Clip(string n) => all.FirstOrDefault(c => c.name == n || c.name.EndsWith("|" + n));
            var idle = Clip("Idle"); var walk = Clip("WalkSlow") ?? Clip("Walk");
            log.AppendLine("Animacje krowy: " + string.Join(", ", all.Select(c => c.name)));
            if (File.Exists(Ctrl)) AssetDatabase.DeleteAsset(Ctrl);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(Ctrl);
            ctrl.AddParameter("Walk", AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine;
            var sIdle = sm.AddState("Stoi"); sIdle.motion = idle;
            var sWalk = sm.AddState("Idzie"); sWalk.motion = walk;
            sm.defaultState = sIdle;
            var a = sIdle.AddTransition(sWalk); a.AddCondition(AnimatorConditionMode.If, 0, "Walk"); a.hasExitTime = false; a.duration = 0.3f;
            var b = sWalk.AddTransition(sIdle); b.AddCondition(AnimatorConditionMode.IfNot, 0, "Walk"); b.hasExitTime = false; b.duration = 0.3f;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            foreach (var leftover in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name == "Krowa")) Object.DestroyImmediate(leftover);
            var rootGo = new GameObject("Krowa");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model); inst.name = "Model"; inst.transform.SetParent(rootGo.transform, false);
            // skala: długość ok. 2,3 m; kierunek: głowa w stronę +Z korzenia
            var rs = inst.GetComponentsInChildren<Renderer>(); var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
            float len = Mathf.Max(bb.size.x, bb.size.z);
            inst.transform.localScale *= CowLength / Mathf.Max(0.01f, len);
            var head = inst.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Head");
            if (head)
            {
                var d = head.position - rootGo.transform.position; d.y = 0;
                if (d.sqrMagnitude > 1e-4f) inst.transform.rotation = Quaternion.FromToRotation(d.normalized, Vector3.forward) * inst.transform.rotation;
            }
            rs = inst.GetComponentsInChildren<Renderer>(); bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
            inst.transform.position += new Vector3(-bb.center.x, -bb.min.y, -bb.center.z);   // kopyta na ziemi, środek w korzeniu
            var anim = inst.GetComponent<Animator>(); if (!anim) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.CullCompletely;
            foreach (var sk in inst.GetComponentsInChildren<SkinnedMeshRenderer>()) sk.updateWhenOffscreen = false;
            rs = inst.GetComponentsInChildren<Renderer>(); bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
            var box = rootGo.AddComponent<BoxCollider>();
            box.center = new Vector3(0, bb.size.y * 0.55f, 0); box.size = new Vector3(bb.size.x * 0.75f, bb.size.y * 0.7f, bb.size.z * 0.85f);
            rootGo.layer = 2;   // Ignore Raycast: kamera i promienie ziemi przechodzą, gracz się zatrzymuje
            rootGo.AddComponent<Cow>();
            log.AppendLine($"Krowa: wymiary {bb.size.x:0.00} × {bb.size.y:0.00} × {bb.size.z:0.00} m");
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            try { return PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath); }
            finally { Object.DestroyImmediate(rootGo); }
        }

        static GameObject Spawn(GameObject prefab, Transform parent, Vector3 p, Vector3 lookFrom, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name; go.transform.position = p;
            // bokiem do domu, trochę losowo
            var d = p - lookFrom; d.y = 0;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 90f + Random.Range(-40f, 40f);
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        /// <summary>Miejsca na trawie wokół domu: sam teren (bez drogi, płotu, drzew, budynków), równo, z wolnym kołem na spacer krowy.</summary>
        static List<Vector3> FindSpots(Vector3 center, Vector3 gate, int count, float rMin, float rMax, List<Vector3> taken, float clear = 2.6f)
        {
            float baseY = Ground(center).y;
            var cand = new List<(Vector3 p, float score)>();
            for (float r = rMin; r <= rMax; r += 2f)
                for (int a = 0; a < 360; a += 12)
                {
                    var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                    var top = new Vector3(center.x, 0, center.z) + dir * r;
                    if (!Physics.Raycast(top + Vector3.up * (baseY + 60f), Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!(hit.collider is TerrainCollider)) continue;            // droga, dach, drzewo, płot
                    if (Mathf.Abs(hit.point.y - baseY) > 1.5f || hit.normal.y < 0.94f) continue;
                    var p = hit.point;
                    if (taken.Any(t => Flat(t - p) < 3.5f)) continue;
                    bool blocked = Physics.OverlapSphere(p + Vector3.up * (clear * 0.5f + 0.4f), clear, ~0, QueryTriggerInteraction.Ignore).Any(c => !(c is TerrainCollider));
                    if (blocked) continue;
                    // za domem (dalej od ulicy niż dom) i blisko domu
                    float score = Mathf.Abs(r - (rMin + 3f)) + (Flat(p - gate) < Flat(center - gate) ? 6f : 0f);
                    cand.Add((p, score));
                }
            var res = new List<Vector3>();
            foreach (var c in cand.OrderBy(c => c.score))
            {
                if (res.Any(x => Flat(x - c.p) < 3.5f)) continue;
                res.Add(c.p); if (res.Count >= count) break;
            }
            return res;
        }

        /// <summary>Najbliższy punkt drogi: promienie w dół po okręgach wokół domu (kolizje dróg są wklęsłe, ClosestPoint na nich nie działa).</summary>
        static Vector3? RoadNear(Vector3 c, float max)
        {
            for (float r = 6f; r <= max; r += 3f)
            {
                Vector3? best = null; float bd = 1e9f;
                for (int a = 0; a < 360; a += 10)
                {
                    var p = c + Quaternion.Euler(0, a, 0) * Vector3.forward * r;
                    foreach (var h in Physics.RaycastAll(p + Vector3.up * 60f, Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
                        if (h.collider.name.StartsWith("odcinek")) { float d = Flat(h.point - c); if (d < bd) { bd = d; best = h.point; } }
                }
                if (best != null) return best;
            }
            return null;
        }

        static Vector3 Center(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return t.position;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return new Vector3(b.center.x, t.position.y, b.center.z);
        }
        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
        static Vector3 Ground(Vector3 p)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * 40f, Vector3.down, 90f, ~0, QueryTriggerInteraction.Ignore);
            return hits.Length > 0 ? hits.OrderBy(h => h.point.y).First().point : p;
        }
    }
}
