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
        const string FenceFbx = "Assets/_Mleczarka/Art/ThirdParty/Quaternius/MedievalVillageMegaKit/FBX/Prop_WoodenFence_Single.fbx";
        const string GateFbx = "Assets/_Mleczarka/Art/World/Furtka_Pastwisko.fbx", GatePrefab = "Assets/_Mleczarka/Prefabs/World/Furtka_Pastwisko.prefab";
        const float FenceHeight = 1.5f;   // płot z pakietu ma 0,81 m — na krowy podnosimy do ok. 1,2 m
        const float PostWidth = 0.14f;

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
            var fence = AssetDatabase.LoadAssetAtPath<GameObject>(FenceFbx);
            var gatePf = SetupGatePrefab(fence, log);
            var pastures = new List<(Vector3 c, Quaternion q, float w, float d)>();
            var barns = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Stodola") && (t.parent == null || !t.parent.name.StartsWith("Stodola"))).ToList();
            foreach (var z in farmZones.OrderBy(z => z.farmId))
            {
                int cows = CareerSim.Cows(z.farmId);
                var house = houses.OrderBy(h => Flat(Center(h) - z.transform.position)).FirstOrDefault();
                var center = house && Flat(Center(house) - z.transform.position) < 30f ? Center(house) : z.transform.position;
                var g = new GameObject($"Krowy: {z.displayName} ({cows})").transform; g.SetParent(root, false);
                var barn = barns.Where(b => Flat(Center(b) - center) < 30f).OrderBy(b => Flat(Center(b) - center)).FirstOrDefault();
                var road = RoadNear(center, 40f) ?? z.transform.position;
                if (fence && gatePf && TryPasture(center, road, barn, cows + 5, cows + 4, pastures, out var pc, out var pq, out var pw, out var pd))
                {
                    pastures.Add((pc, pq, pw, pd));
                    BuildPasture(g, fence, gatePf, pc, pq, pw, pd, center, log); Physics.SyncTransforms();
                    for (int i = 0; i < cows; i++)
                    {
                        var p = GroundT(pc + pq * new Vector3((i - (cows - 1) * 0.5f) * pw / (cows + 1), 0, (i % 2 == 0 ? -1f : 1f) * pd * 0.12f));
                        var cow = Spawn(prefab, g, p, center, $"Krowa {i + 1}").GetComponent<Cow>();
                        SetArea(cow, pc, pq, pw, pd); taken.Add(p); total++;
                    }
                    log.AppendLine($"{z.displayName}: pastwisko {pw:0} × {pd:0} m, {Flat(pc - center):0} m za domem{(barn ? " (za stodołą)" : "")}, krowy {cows} ({cows * CareerData.LitersPerCow:0} l na dobę)");
                }
                else
                {
                    var spots = FindSpots(center, z.transform.position, cows, 8f, 26f, taken);
                    for (int i = 0; i < spots.Count; i++) { Spawn(prefab, g, spots[i], center, $"Krowa {i + 1}"); taken.Add(spots[i]); total++; }
                    log.AppendLine($"UWAGA {z.displayName}: brak miejsca na ogrodzone pastwisko — krowy luzem przy domu ({spots.Count}/{cows})");
                }
            }

            // ---- własna krowa: małe ogrodzone pastwisko za letnią kuchnią ----
            var dom = zones.FirstOrDefault(z => z.kind == PlaceKind.Dom);
            if (dom)
            {
                var hc = dom.transform.position;
                var road = RoadNear(hc, 60f) ?? hc + Vector3.forward * 20f;
                if (fence && gatePf && TryPasture(hc, road, null, 6, 5, pastures, out var pc, out var pq, out var pw, out var pd))
                {
                    pastures.Add((pc, pq, pw, pd));
                    var g = new GameObject("Własne pastwisko").transform; g.SetParent(root, false);
                    BuildPasture(g, fence, gatePf, pc, pq, pw, pd, hc, log); Physics.SyncTransforms();
                    var own = Spawn(prefab, g, GroundT(pc), hc, "Własna krowa (Krasula)");
                    var cow = own.GetComponent<Cow>(); cow.own = true; SetArea(cow, pc, pq, pw, pd);
                    log.AppendLine($"Własna krowa: pastwisko {pw:0} × {pd:0} m, {Flat(pc - hc):0} m od letniej kuchni");
                }
                else log.AppendLine("UWAGA: brak miejsca na własne pastwisko przy domu.");
            }
            log.AppendLine($"Razem krów: {total} + własna");

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
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
            // kontroler: wczytany i przebudowany w miejscu (usunięcie i ponowne utworzenie gubi referencję w prefabie)
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Ctrl) ?? AnimatorController.CreateAnimatorControllerAtPath(Ctrl);
            foreach (var prm in ctrl.parameters.ToArray()) ctrl.RemoveParameter(prm);
            ctrl.AddParameter("Walk", AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var st in sm.states.ToArray()) sm.RemoveState(st.state);
            var sIdle = sm.AddState("Stoi"); sIdle.motion = idle;
            var sWalk = sm.AddState("Idzie"); sWalk.motion = walk;
            sm.defaultState = sIdle;
            var a = sIdle.AddTransition(sWalk); a.AddCondition(AnimatorConditionMode.If, 0, "Walk"); a.hasExitTime = false; a.duration = 0.3f;
            var b = sWalk.AddTransition(sIdle); b.AddCondition(AnimatorConditionMode.IfNot, 0, "Walk"); b.hasExitTime = false; b.duration = 0.3f;
            EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets();

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
            rootGo.AddComponent<Cow>().controller = ctrl;
            log.AppendLine($"Krowa: wymiary {bb.size.x:0.00} × {bb.size.y:0.00} × {bb.size.z:0.00} m");
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            try
            {
                var pf = PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
                var pa = pf.GetComponentInChildren<Animator>();
                var pcow = pf.GetComponent<Cow>();
                log.AppendLine($"Prefab krowy: animator {(pa ? "jest" : "BRAK")}, kontroler w modelu {(pa && pa.runtimeAnimatorController ? pa.runtimeAnimatorController.name : "brak")}, w skrypcie krowy {(pcow && pcow.controller ? pcow.controller.name : "BRAK")}, ruch: {(walk ? walk.name : "BRAK")}, stanie: {(idle ? idle.name : "BRAK")}");
                return pf;
            }
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

        // ---------- pastwisko: ogrodzenie z furtką ----------
        static float _pitch, _fenceScale = 1f, _gateScale = 1f; static bool _longX, _gateLongX;

        static void Measure(GameObject src, out float len, out bool longX)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            go.transform.position = Vector3.zero;   // obrót z modelu zostaje (pakiet Quaternius ma -90° w X)
            var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            longX = b.size.x >= b.size.z; len = longX ? b.size.x : b.size.z;
            Object.DestroyImmediate(go);
        }

        static GameObject SetupGatePrefab(GameObject fence, StringBuilder log)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(GateFbx);
            if (!fence || !model) { log.AppendLine("UWAGA: brak płotu albo furtki: " + FenceFbx + " / " + GateFbx); return null; }
            // pakiet Quaternius bywa w centymetrach — skala do prawdziwego segmentu 2,06 m (słupek co 1,92 m)
            Measure(fence, out var len, out _longX);
            _fenceScale = 2.064f / len; _pitch = 2.064f - PostWidth;
            Measure(model, out var glen, out _gateLongX);
            _gateScale = 1.92f / glen;
            var fmat = fence.GetComponentInChildren<Renderer>().sharedMaterial;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = fmat;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<BoxCollider>();
            var gate = go.AddComponent<PastureGate>();
            gate.leaf = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Furtka_Skrzydlo");
            Directory.CreateDirectory(Path.GetDirectoryName(GatePrefab));
            try { return PrefabUtility.SaveAsPrefabAsset(go, GatePrefab); }
            finally { Object.DestroyImmediate(go); log.AppendLine($"Płot: segment {_pitch:0.00} m (oś {(_longX ? "X" : "Z")}, skala {_fenceScale:0.####}), furtka (oś {(_gateLongX ? "X" : "Z")}, skala {_gateScale:0.####})"); }
        }

        /// <summary>Prostokąt pastwiska za domem (i stodołą), od strony przeciwnej do drogi: sam teren, bez budynków, drzew, płotów i dróg.</summary>
        static bool TryPasture(Vector3 house, Vector3 road, Transform barn, int nW, int nD, List<(Vector3 c, Quaternion q, float w, float d)> used,
                               out Vector3 c, out Quaternion q, out float w, out float d)
        {
            var away = house - road; away.y = 0; if (away.sqrMagnitude < 0.01f) away = Vector3.forward; away.Normalize();
            float start = 8f;
            if (barn)
            {
                var rs = barn.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, 0, (i & 2) == 0 ? b.min.z : b.max.z);
                    start = Mathf.Max(start, Vector3.Dot(corner - new Vector3(house.x, 0, house.z), away) + 3f);
                }
            }
            for (int shrink = 0; shrink < 3; shrink++)
            {
                int sw = Mathf.Max(4, nW - shrink * 2), sd = Mathf.Max(3, nD - shrink);
                w = sw * _pitch; d = sd * _pitch;
                foreach (float dOff in new[] { 0f, 3f, 6f, 10f, 14f })
                    foreach (float lat in new[] { 0f, 5f, -5f, 10f, -10f, 15f, -15f })
                    {
                        // lekki obrót, żeby ogrodzenie było równoległe do domu / ulicy
                        q = Quaternion.LookRotation(away);
                        var side = q * Vector3.right;
                        var cc = house + away * (start + dOff + d / 2) + side * lat; cc.y = GroundT(cc).y; float ww = w;
                        if (used.Any(u => Flat(u.c - cc) < (u.w + ww) * 0.5f + 4f)) continue;
                        if (Free(cc, q, w, d)) { c = cc; return true; }
                    }
            }
            c = default; q = default; w = d = 0; return false;
        }

        static bool Free(Vector3 c, Quaternion q, float w, float d)
        {
            float lo = 1e9f, hi = -1e9f;
            for (float x = -w / 2 - 1f; x <= w / 2 + 1f; x += 2f)
                for (float z = -d / 2 - 1f; z <= d / 2 + 1f; z += 2f)
                {
                    var p = c + q * new Vector3(x, 0, z);
                    if (!Physics.Raycast(p + Vector3.up * 80f, Vector3.down, out var h, 200f, ~0, QueryTriggerInteraction.Ignore)) return false;
                    if (!(h.collider is TerrainCollider)) return false;     // budynek, drzewo, płot, droga
                    lo = Mathf.Min(lo, h.point.y); hi = Mathf.Max(hi, h.point.y);
                }
            if (hi - lo > 2.5f) return false;
            var mid = new Vector3(c.x, (lo + hi) * 0.5f + 1.6f, c.z);
            var ext = new Vector3(w / 2 + 0.8f, 1.3f + (hi - lo) * 0.5f, d / 2 + 0.8f);
            return !Physics.OverlapBox(mid, ext, q, ~0, QueryTriggerInteraction.Ignore).Any(col => !(col is TerrainCollider));
        }

        static void BuildPasture(Transform parent, GameObject fence, GameObject gatePrefab, Vector3 c, Quaternion q, float w, float d, Vector3 house, StringBuilder log)
        {
            var g = new GameObject("Pastwisko (ogrodzenie)").transform; g.SetParent(parent, false);
            var cs = new[] { new Vector3(-w / 2, 0, -d / 2), new Vector3(w / 2, 0, -d / 2), new Vector3(w / 2, 0, d / 2), new Vector3(-w / 2, 0, d / 2) };
            int nW = Mathf.RoundToInt(w / _pitch);
            for (int s = 0; s < 4; s++)
            {
                Vector3 a = c + q * cs[s], b = c + q * cs[(s + 1) % 4];
                int n = Mathf.RoundToInt(Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, b.z)) / _pitch);
                for (int i = 0; i < n; i++)
                {
                    var p0 = GroundT(Vector3.Lerp(a, b, (float)i / n)); var p1 = GroundT(Vector3.Lerp(a, b, (float)(i + 1) / n));
                    var dir = p1 - p0; var mid = (p0 + p1) * 0.5f + Vector3.down * 0.03f;
                    bool isGate = s == 0 && i == nW / 2;   // furtka w środku boku od strony domu
                    var src = isGate ? gatePrefab : fence; bool lx = isGate ? _gateLongX : _longX;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(src, g);
                    go.name = isGate ? "Furtka" : "Płot " + (s + 1) + "." + (i + 1);
                    var flat = new Vector3(dir.x, 0, dir.z).normalized;
                    var rot = Quaternion.LookRotation(dir.normalized, Vector3.up) * (lx ? Quaternion.Euler(0, -90f, 0) : Quaternion.identity);
                    if (isGate) rot = Quaternion.LookRotation(flat, Vector3.up) * (lx ? Quaternion.Euler(0, -90f, 0) : Quaternion.identity);
                    var baseRot = src.transform.rotation;
                    go.transform.SetPositionAndRotation(mid, rot * baseRot);
                    float sc = isGate ? _gateScale : _fenceScale;
                    // wysokość: ta oś modelu, która po obrocie patrzy w górę
                    var upL = Quaternion.Inverse(baseRot) * Vector3.up;
                    var hs = new Vector3(sc, sc, sc);
                    if (Mathf.Abs(upL.x) > 0.7f) hs.x *= FenceHeight; else if (Mathf.Abs(upL.y) > 0.7f) hs.y *= FenceHeight; else hs.z *= FenceHeight;
                    go.transform.localScale = Vector3.Scale(go.transform.localScale, hs);
                    if (!isGate) foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<BoxCollider>();
                    else
                    {
                        // skrzydło otwiera się do środka pastwiska
                        var gt = go.GetComponent<PastureGate>();
                        if (gt && gt.leaf)
                        {
                            var leafMid = gt.leaf.GetComponent<Renderer>() ? gt.leaf.GetComponent<Renderer>().bounds.center : gt.leaf.position;
                            var arm = leafMid - gt.leaf.position; arm.y = 0;
                            var plus = gt.leaf.position + Quaternion.Euler(0, 90f, 0) * arm;
                            gt.openAngle = Flat(plus - c) < Flat(gt.leaf.position + Quaternion.Euler(0, -90f, 0) * arm - c) ? 95f : -95f;
                        }
                    }
                }
            }
            PaintGrass(c, q, w, d, house + (c - house) * 0.25f);
        }

        static void SetArea(Cow cow, Vector3 c, Quaternion q, float w, float d)
        {
            cow.hasArea = true; cow.areaCenter = c; cow.areaYaw = q.eulerAngles.y;
            cow.areaHalf = new Vector2(Mathf.Max(0.5f, w / 2 - 1.6f), Mathf.Max(0.5f, d / 2 - 1.6f));
        }

        /// <summary>Pod pastwiskiem trawa jak na podwórzu (zamiast uprawy z pola).</summary>
        static void PaintGrass(Vector3 c, Quaternion q, float w, float d, Vector3 yard)
        {
            var ty = Terrain.activeTerrains.FirstOrDefault(t => Inside(t, yard)); if (!ty) return;
            var yd = ty.terrainData; var yl = yd.terrainLayers;
            int ax = Mathf.Clamp((int)((yard.x - ty.transform.position.x) / yd.size.x * yd.alphamapWidth), 0, yd.alphamapWidth - 1);
            int az = Mathf.Clamp((int)((yard.z - ty.transform.position.z) / yd.size.z * yd.alphamapHeight), 0, yd.alphamapHeight - 1);
            var yw = yd.GetAlphamaps(ax, az, 1, 1);
            int dx = Mathf.Clamp((int)((yard.x - ty.transform.position.x) / yd.size.x * yd.detailWidth), 0, yd.detailWidth - 1);
            int dz = Mathf.Clamp((int)((yard.z - ty.transform.position.z) / yd.size.z * yd.detailHeight), 0, yd.detailHeight - 1);
            var ydet = new int[yd.detailPrototypes.Length];
            for (int k = 0; k < ydet.Length; k++) ydet[k] = yd.GetDetailLayer(dx, dz, 1, 1, k)[0, 0];
            var inv = Quaternion.Inverse(q);
            bool In(Vector3 p) { var l = inv * (p - c); return Mathf.Abs(l.x) <= w / 2 + 0.3f && Mathf.Abs(l.z) <= d / 2 + 0.3f; }
            float r = Mathf.Sqrt(w * w + d * d) / 2 + 1f;
            foreach (var t in Terrain.activeTerrains)
            {
                var td = t.terrainData; var tp = t.transform.position;
                if (c.x + r < tp.x || c.x - r > tp.x + td.size.x || c.z + r < tp.z || c.z - r > tp.z + td.size.z) continue;
                // tekstura
                int x0 = Mathf.Clamp((int)((c.x - r - tp.x) / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1), x1 = Mathf.Clamp((int)((c.x + r - tp.x) / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
                int z0 = Mathf.Clamp((int)((c.z - r - tp.z) / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1), z1 = Mathf.Clamp((int)((c.z + r - tp.z) / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
                var a = td.GetAlphamaps(x0, z0, x1 - x0 + 1, z1 - z0 + 1); var tl = td.terrainLayers;
                for (int iz = 0; iz <= z1 - z0; iz++)
                    for (int ix = 0; ix <= x1 - x0; ix++)
                    {
                        var p = new Vector3(tp.x + (x0 + ix + 0.5f) / td.alphamapWidth * td.size.x, 0, tp.z + (z0 + iz + 0.5f) / td.alphamapHeight * td.size.z);
                        if (!In(p)) continue;
                        for (int k = 0; k < tl.Length; k++) { int yk = System.Array.IndexOf(yl, tl[k]); a[iz, ix, k] = yk >= 0 ? yw[0, 0, yk] : 0f; }
                    }
                td.SetAlphamaps(x0, z0, a);
                // kępy roślin (bez kłosów z pola)
                int dx0 = Mathf.Clamp((int)((c.x - r - tp.x) / td.size.x * td.detailWidth), 0, td.detailWidth - 1), dx1 = Mathf.Clamp((int)((c.x + r - tp.x) / td.size.x * td.detailWidth), 0, td.detailWidth - 1);
                int dz0 = Mathf.Clamp((int)((c.z - r - tp.z) / td.size.z * td.detailHeight), 0, td.detailHeight - 1), dz1 = Mathf.Clamp((int)((c.z + r - tp.z) / td.size.z * td.detailHeight), 0, td.detailHeight - 1);
                for (int k = 0; k < td.detailPrototypes.Length; k++)
                {
                    var dl = td.GetDetailLayer(dx0, dz0, dx1 - dx0 + 1, dz1 - dz0 + 1, k);
                    for (int iz = 0; iz <= dz1 - dz0; iz++)
                        for (int ix = 0; ix <= dx1 - dx0; ix++)
                        {
                            var p = new Vector3(tp.x + (dx0 + ix + 0.5f) / td.detailWidth * td.size.x, 0, tp.z + (dz0 + iz + 0.5f) / td.detailHeight * td.size.z);
                            if (In(p)) dl[iz, ix] = k < ydet.Length ? ydet[k] : 0;
                        }
                    td.SetDetailLayer(dx0, dz0, k, dl);
                }
                EditorUtility.SetDirty(td);
            }
        }

        static bool Inside(Terrain t, Vector3 p) { var tp = t.transform.position; var s = t.terrainData.size; return p.x >= tp.x && p.x <= tp.x + s.x && p.z >= tp.z && p.z <= tp.z + s.z; }

        /// <summary>Punkt na terenie (nie na płocie, dachu czy drzewie).</summary>
        static Vector3 GroundT(Vector3 p)
        {
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 80f, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (h.collider is TerrainCollider) return h.point;
            return Ground(p);
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
