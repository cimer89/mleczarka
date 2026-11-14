using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>
    /// Dom gracza w trybie kariery: letnia kuchnia przy wsi z rynkiem. Miejsce wybiera się samo:
    /// wolny, płaski kawałek łąki (bez dróg, budynków i drzew) w rozsądnej odległości od rynku, najlepiej obok istniejącego domu.
    /// Mapa się nie zmienia (bez ruszania terenu); dochodzi tylko szopa ze sprzętem, rower i strefa „Dom”.
    /// </summary>
    public static class CareerHomeBuilder
    {
        const string MainScene = "Assets/_Mleczarka/Scenes/Gmina_Podlasie.unity";
        const string Art = "Assets/_Mleczarka/Art/Career/";
        const string RootName = "Dom gracza — letnia kuchnia";
        static StringBuilder log;

        [MenuItem("Mleczarka/Kariera/1. Sprawdz miejsce na dom gracza")]
        static void Check() => With(false, s => { var spot = Find(s, out var why); log.AppendLine(why); });

        [MenuItem("Mleczarka/Kariera/2. Zbuduj dom gracza (letnia kuchnia)")]
        static void Build() => With(true, s =>
        {
            foreach (var g in s.GetRootGameObjects().Where(g => g.name == RootName)) UnityEngine.Object.DestroyImmediate(g);
            string backup = "Backups/CareerHome_" + DateTime.Now.ToString("yyyy-MM-dd_HHmm"); Directory.CreateDirectory(backup);
            File.Copy(MainScene, backup + "/Gmina_Podlasie.unity", true); log.AppendLine("Kopia sceny: " + backup);
            var spot = Find(s, out var why); log.AppendLine(why);
            if (!spot.HasValue) throw new InvalidOperationException("Nie znaleziono miejsca na dom.");
            DoBuild(s, spot.Value.pos, spot.Value.rot);
            if (!EditorSceneManager.SaveScene(s)) throw new IOException("Nie zapisano sceny.");
            Shot(s, "Dom_kuchnia_przod", new Vector3(0, 2.6f, 8.5f), new Vector3(0, 1.1f, 0), 55);
            Shot(s, "Dom_kuchnia_gora", new Vector3(10, 16, 14), new Vector3(0, 0, 0), 55);
            Shot(s, "Dom_rower", new Vector3(4.5f, 1.4f, 5.6f), new Vector3(2.0f, 0.6f, 3.4f), 45);
        });

        // ---------------- wybór miejsca ----------------

        struct Spot { public Vector3 pos; public Quaternion rot; }

        static Spot? Find(Scene s, out string why)
        {
            var terrains = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>()).ToArray();
            var places = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WorldPlace>(true)).ToList();
            var market = places.FirstOrDefault(p => p.kind == PlaceKind.Rynek);
            if (!market) { why = "Brak rynku w scenie."; return null; }
            var rends = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>()).Where(r => !r.GetComponentInParent<Terrain>() && r.enabled).ToList();
            var cols = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Collider>()).Where(c => !(c is TerrainCollider) && c.enabled).ToList();
            var houses = rends.Where(r => { var n = r.name.ToLowerInvariant(); return n.Contains("house") || n.Contains("dom") || n.Contains("chata") || n.Contains("cottage"); }).Select(r => r.bounds.center).ToList();
            var trees = new List<Vector3>();
            foreach (var t in terrains) foreach (var ti in t.terrainData.treeInstances) trees.Add(Vector3.Scale(ti.position, t.terrainData.size) + t.transform.position);

            Spot? best = null; float bestScore = float.MaxValue; int tried = 0, ok = 0;
            Vector3 c0 = market.transform.position;
            for (float r = 30; r <= 140; r += 5)
                for (float a = 0; a < 360; a += 10)
                {
                    tried++;
                    var p = c0 + Quaternion.Euler(0, a, 0) * Vector3.forward * r;
                    var face = (c0 - p); face.y = 0; face.Normalize();                       // otwarta strona szopy patrzy w stronę rynku
                    var rot = Quaternion.LookRotation(face);
                    // obrys: szopa + plac przed nią (rower, wejście), z zapasem
                    var center = p + face * 1.5f;
                    var size = new Vector3(10f, 6f, 11f);
                    float hMin = float.MaxValue, hMax = float.MinValue;
                    bool bad = false;
                    for (float x = -size.x / 2; x <= size.x / 2 && !bad; x += 1.5f)
                        for (float z = -size.z / 2; z <= size.z / 2; z += 1.5f)
                        {
                            var w = center + rot * new Vector3(x, 0, z);
                            float g = Ground(terrains, w); if (float.IsNaN(g)) { bad = true; break; }
                            hMin = Mathf.Min(hMin, g); hMax = Mathf.Max(hMax, g);
                        }
                    if (bad || hMax - hMin > 0.9f) continue;
                    float y = (hMin + hMax) / 2;
                    var box = new Bounds(new Vector3(center.x, y + size.y / 2 - 0.3f, center.z), Quaternion.Inverse(rot) * size);
                    box.size = new Vector3(Mathf.Max(size.x, size.z), size.y, Mathf.Max(size.x, size.z));   // obrót: bierzemy kwadrat
                    if (rends.Any(rr => rr.bounds.Intersects(box)) || cols.Any(cc => cc.bounds.Intersects(box))) continue;
                    if (trees.Any(t => Mathf.Abs(t.x - center.x) < 7 && Mathf.Abs(t.z - center.z) < 7)) continue;
                    if (places.Any(pl => pl.kind != PlaceKind.Rynek && Vector3.Distance(Flat(pl.transform.position), Flat(center)) < pl.radius + 10)) continue;
                    ok++;
                    float house = houses.Count > 0 ? houses.Min(h => Vector3.Distance(Flat(h), Flat(center))) : 60;
                    float score = Mathf.Abs(r - 60) + (house < 9 ? 50 : house > 30 ? (house - 30) : 0) * 1.5f + (hMax - hMin) * 10;
                    if (score < bestScore) { bestScore = score; best = new Spot { pos = new Vector3(p.x, y, p.z), rot = rot }; }
                }
            why = best.HasValue ? $"Miejsce na dom: {best.Value.pos:0.0}, kierunek {best.Value.rot.eulerAngles.y:0}°. Sprawdzono {tried} miejsc, wolnych {ok}. Rynek: {c0:0.0}."
                                : $"Brak wolnego miejsca (sprawdzono {tried}).";
            return best;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        static float Ground(Terrain[] terrains, Vector3 w)
        {
            foreach (var t in terrains)
            {
                var p = t.transform.position; var s = t.terrainData.size;
                if (w.x >= p.x && w.x <= p.x + s.x && w.z >= p.z && w.z <= p.z + s.z) return t.SampleHeight(w) + p.y;
            }
            return float.NaN;
        }

        // ---------------- budowa ----------------

        static Transform root;

        static GameObject Model(string name)
        {
            var a = AssetDatabase.LoadAssetAtPath<GameObject>(Art + name + ".fbx");
            if (!a) throw new FileNotFoundException("Brak modelu " + Art + name + ".fbx");
            return (GameObject)PrefabUtility.InstantiatePrefab(a);
        }

        static Transform Find(Transform t, string suffix) => t.GetComponentsInChildren<Transform>(true).FirstOrDefault(c => c.name.EndsWith(suffix));

        /// <summary>Stawia model tak, żeby jego znacznik (np. _Stanowisko) był po stronie „face” (lokalnie w szopie).</summary>
        static GameObject Place(string model, Vector3 local, Vector3 face, string marker = "_Stanowisko")
        {
            var go = Model(model); go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            var m = Find(go.transform, marker);
            if (m)
            {
                var d = root.InverseTransformDirection(m.position - go.transform.position); d.y = 0;
                if (d.sqrMagnitude > 0.001f) go.transform.localRotation = Quaternion.Euler(0, Vector3.SignedAngle(d.normalized, face, Vector3.up), 0);
            }
            go.transform.localPosition = local;
            var b = Bounds(go);
            go.transform.position += Vector3.up * (root.TransformPoint(local).y - b.min.y);
            return go;
        }

        static Bounds Bounds(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        static void Collider(GameObject g, float k = 0.95f)
        {
            var b = Bounds(g); var c = g.AddComponent<BoxCollider>();
            c.center = g.transform.InverseTransformPoint(b.center);
            var s = g.transform.InverseTransformVector(b.size); c.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) * k;
        }

        static void DoBuild(Scene s, Vector3 pos, Quaternion rot)
        {
            root = new GameObject(RootName).transform; SceneManager.MoveGameObjectToScene(root.gameObject, s);
            root.SetPositionAndRotation(pos, rot);
            // szopa: otwarta strona (znacznik _Wejscie) do przodu (+Z)
            var shed = Place("Szopa", Vector3.zero, Vector3.forward, "_Wejscie");
            shed.name = "Letnia kuchnia (szopa)";
            var sb = Bounds(shed);
            float floor = sb.min.y + 0.21f - root.position.y;    // wierzch podłogi
            // fundament z kamieni pod podestem (teren nie jest idealnie płaski)
            var fund = GameObject.CreatePrimitive(PrimitiveType.Cube); fund.name = "Podmurówka"; fund.transform.SetParent(root, false);
            fund.transform.localPosition = new Vector3(0, -0.45f, 0); fund.transform.localScale = new Vector3(6.4f, 1.0f, 4.6f);
            fund.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Mleczarka/Art/FactoryInterior/Fugi.mat");
            // kolizje szopy: podłoga, tył, boki
            Box("Kolizja podłogi", new Vector3(0, 0.1f, 0), new Vector3(6.3f, 0.2f, 4.5f));
            Box("Kolizja tył", new Vector3(0, 1.5f, -2.1f), new Vector3(6f, 3f, 0.1f));
            Box("Kolizja lewa", new Vector3(-3f, 1.5f, 0), new Vector3(0.1f, 3f, 4.2f));
            Box("Kolizja prawa", new Vector3(3f, 1.5f, 0), new Vector3(0.1f, 3f, 4.2f));
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube); ramp.name = "Stopień"; ramp.transform.SetParent(root, false);
            ramp.transform.localPosition = new Vector3(0, 0.05f, 2.45f); ramp.transform.localScale = new Vector3(2.2f, 0.12f, 0.5f);
            ramp.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Mleczarka/Art/FactoryInterior/Drewno_palet.mat");

            Vector3 F = Vector3.forward;
            var stations = new List<CareerHome.Station>();
            CareerHome.Station St(string id, string title, GameObject go, string owned = null)
            {
                Collider(go);
                var st = new CareerHome.Station { id = id, title = title, anchor = go.transform, operatorPoint = Find(go.transform, "_Stanowisko") ?? go.transform, ownedId = owned, showWhenOwned = owned != null ? go : null };
                stations.Add(st); return st;
            }
            var lod = Place("Lodowka", new Vector3(-2.45f, floor, -1.72f), F); St("lodowka", "Lodówka", lod);
            var kuch = Place("Kuchenka", new Vector3(-1.3f, floor, -1.72f), F); St("kuchenka", "Kuchnia węglowa i garnek", kuch);
            var polka = Place("Polka", new Vector3(0.15f, floor, -1.86f), F); St("polka", "Spiżarka (półka ze słoikami)", polka);
            var masl = Place("Maselnica", new Vector3(1.35f, floor, -1.7f), F); St("maselnica", "Maselnica", masl, "maselnica");
            var wir = Place("Wirowka", new Vector3(2.35f, floor, -1.6f), F); St("wirowka", "Wirówka ręczna", wir, "wirowka_r");
            var stol = Place("Stol", new Vector3(-1.0f, floor, 0.35f), F); St("stol", "Stół kuchenny", stol);
            var prasa = Place("Prasa", new Vector3(2.5f, floor, 0.4f), Vector3.left); St("prasa", "Prasa do sera", prasa, "prasa_r");

            // rower przed szopą (opakowanie: przód roweru = +Z opakowania)
            var bikeWrap = new GameObject("Rower gracza").transform; bikeWrap.SetParent(root, false);
            bikeWrap.localPosition = new Vector3(2.2f, 0, 3.6f); bikeWrap.localRotation = Quaternion.Euler(0, 90, 0);
            var bike = Model("Rower"); bike.transform.SetParent(bikeWrap, false); bike.transform.localPosition = Vector3.zero; bike.transform.localRotation = Quaternion.identity;
            var front = Find(bike.transform, "_Przod");
            if (front) { var d = bikeWrap.InverseTransformDirection(front.position - bike.transform.position); d.y = 0; bike.transform.localRotation = Quaternion.Euler(0, Vector3.SignedAngle(d.normalized, Vector3.forward, Vector3.up), 0); }
            bikeWrap.position = new Vector3(bikeWrap.position.x, Ground(bikeWrap.position), bikeWrap.position.z);
            var bb = Bounds(bike); bike.transform.position += Vector3.up * (bikeWrap.position.y - bb.min.y);
            var rack = Find(bike.transform, "_Bagaznik"); var can = Model("Banka"); can.name = "Bańka na bagażniku";
            can.transform.SetParent(rack ? rack : bike.transform, false); can.transform.localPosition = Vector3.zero; can.transform.localRotation = Quaternion.identity; can.transform.localScale = Vector3.one * 0.85f;
            var basket = Find(bike.transform, "_Koszyk");

            var spawn = new GameObject("Start gracza").transform; spawn.SetParent(root, false);
            spawn.localPosition = new Vector3(0, 0.1f, 3.3f); spawn.localRotation = Quaternion.Euler(0, 180, 0);
            spawn.position = new Vector3(spawn.position.x, Ground(spawn.position) + 0.1f, spawn.position.z);
            var wp = root.gameObject.AddComponent<WorldPlace>(); wp.kind = PlaceKind.Dom; wp.radius = 8.5f; wp.displayName = "Dom — letnia kuchnia";
            // WorldPlace szuka „Start gracza” wśród dzieci obiektu strefy
            var light = new GameObject("Żarówka").AddComponent<Light>(); light.transform.SetParent(root, false); light.transform.localPosition = new Vector3(0, floor + 2.3f, 0);
            light.type = LightType.Point; light.range = 7; light.intensity = 1.6f; light.color = new Color(1f, 0.85f, 0.6f); light.shadows = LightShadows.None;

            // tabliczka nad wejściem
            WingDepartments.HangPlate(root, "Dom_Kuchnia", new Vector3(0, floor + 2.5f, 2.05f), Vector3.forward, 1.6f, 0.4f, floor + 2.95f);

            ClearGrass(new Vector2(7.0f, 5.6f));
            // ściany i dach chowają się, gdy zasłaniają postać przed kamerą (jak w hali)
            var shellR = shed.GetComponentsInChildren<Renderer>().Where(r => r.name.EndsWith("_Sciany") || r.name.EndsWith("_Dach")).ToArray();
            var vis = root.gameObject.AddComponent<FactoryShellVisibility>(); vis.shell = shellR; vis.localSize = new Vector3(6.4f, 6f, 4.6f);
            var home = root.gameObject.AddComponent<CareerHome>();
            home.stations = stations.ToArray();
            home.bike = bikeWrap; home.canOnBike = can.transform; home.basketPoint = basket;
            home.pantryJars = polka.transform; home.steamPoint = Find(kuch.transform, "_Para");
            home.potLid = Find(kuch.transform, "_Pokrywka"); home.churnPlunger = Find(masl.transform, "_Tlok");
            home.separatorCrank = Find(wir.transform, "_Korba_ObrotX"); home.pressPlunger = Find(prasa.transform, "_Tlok"); home.fridgeDoor = Find(lod.transform, "_Drzwi");
            home.steamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Mleczarka/Art/FactoryInterior/Para_wodna.mat");
            home.jarMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Mleczarka/Art/FactoryInterior/Mleko_w_rurze.mat");
            log.AppendLine($"Dom zbudowany: {root.position:0.0}, stanowiska: {stations.Count}.");
            Directory.CreateDirectory("Assets/_Mleczarka/Prefabs/Buildings");
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/_Mleczarka/Prefabs/Buildings/Dom_Letnia_Kuchnia.prefab");
        }

        /// <summary>Trawa (detale terenu) nie wyrasta przez podłogę szopy: zerowana tylko w obrysie budynku.</summary>
        static void ClearGrass(Vector2 size)
        {
            if (_terrains == null) _terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            int n = 0;
            foreach (var t in _terrains)
            {
                var td = t.terrainData; int res = td.detailResolution; if (res <= 0 || td.detailPrototypes.Length == 0) continue;
                Undo.RegisterCompleteObjectUndo(td, "Trawa pod letnią kuchnią");
                for (int layer = 0; layer < td.detailPrototypes.Length; layer++)
                {
                    var map = td.GetDetailLayer(0, 0, res, res, layer); bool changed = false;
                    for (float x = -size.x / 2; x <= size.x / 2; x += 0.25f)
                        for (float z = -size.y / 2; z <= size.y / 2 + 0.6f; z += 0.25f)
                        {
                            var w = root.TransformPoint(new Vector3(x, 0, z)) - t.transform.position;
                            int ix = Mathf.FloorToInt(w.x / td.size.x * res), iz = Mathf.FloorToInt(w.z / td.size.z * res);
                            if (ix < 0 || iz < 0 || ix >= res || iz >= res) continue;
                            if (map[iz, ix] != 0) { map[iz, ix] = 0; changed = true; n++; }
                        }
                    if (changed) td.SetDetailLayer(0, 0, layer, map);
                }
                EditorUtility.SetDirty(td);
            }
            AssetDatabase.SaveAssets();
            log.AppendLine($"Trawa usunięta tylko spod szopy: {n} komórek.");
        }

        static Terrain[] _terrains;
        static float Ground(Vector3 w)
        {
            if (_terrains == null) _terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            float g = Ground(_terrains, w); return float.IsNaN(g) ? w.y : g;
        }

        static void Box(string name, Vector3 p, Vector3 size)
        {
            var g = new GameObject(name); g.transform.SetParent(root, false); g.transform.localPosition = p;
            var c = g.AddComponent<BoxCollider>(); c.size = size;
        }

        // ---------------- pomocnicze ----------------

        static void With(bool save, Action<Scene> body)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[Mleczarka] Zatrzymaj tryb gry."); return; }
            log = new StringBuilder(); _terrains = null;
            var original = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(MainScene);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive);
            try
            {
                if (save && scene.isDirty) throw new InvalidOperationException("Główna scena ma niezapisane zmiany.");
                SceneManager.SetActiveScene(scene);
                body(scene);
                Debug.Log("[Mleczarka] " + log.ToString().Split('\n').Last(l => l.Trim().Length > 0));
            }
            catch (Exception e) { log.AppendLine("BŁĄD: " + e); Debug.LogException(e); }
            finally
            {
                Directory.CreateDirectory("Screenshots"); File.WriteAllText("Screenshots/dom_gracza.txt", log.ToString());
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void Shot(Scene scene, string name, Vector3 localPos, Vector3 localLook, float fov)
        {
            var cam = new GameObject("Kamera kontroli domu").AddComponent<Camera>(); SceneManager.MoveGameObjectToScene(cam.gameObject, scene);
            cam.transform.position = root.TransformPoint(localPos); cam.transform.LookAt(root.TransformPoint(localLook)); cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 2000;
            var t = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (t != null) { var d = cam.gameObject.AddComponent(t); t.GetProperty("renderPostProcessing").SetValue(d, true); }
            var rt = new RenderTexture(1600, 900, 24); cam.targetTexture = rt; cam.Render(); cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            File.WriteAllBytes("Screenshots/" + name + ".png", tex.EncodeToPNG());
            RenderTexture.active = prev; cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(cam.gameObject); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
