using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using Mleczarka.Player;
using Mleczarka.Vehicles.Model;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Mleczarka.EditorTools
{
    /// <summary>
    /// Świat gry „Gmina”: mapa z wersji HTML (węzły dróg, 6 gospodarstw, mleczarnia, rynek, Mleczpol) w skali ×2,
    /// zbudowana z modeli Quaternius i własnych z Blendera, z działającymi strefami symulacji i interfejsem.
    /// </summary>
    public static partial class DemoFarmBuilder
    {
        const string WorldScenePath = "Assets/_Mleczarka/Scenes/Swiat_Gmina.unity";
        const string WORLD = QuaterniusMaterials.Art + "World/";
        const float K = 2f;                          // skala mapy HTML → metry
        const float RoadHalf = 3.4f;

        static readonly Dictionary<string, Vector2> Nodes = new Dictionary<string, Vector2>
        {
            ["A"] = new Vector2(-60, -45), ["g1"] = new Vector2(-30, -45), ["B"] = new Vector2(0, -45), ["g6"] = new Vector2(30, -45), ["C"] = new Vector2(60, -45),
            ["g2"] = new Vector2(-60, -10), ["H"] = new Vector2(-60, 8), ["g3"] = new Vector2(-60, 40), ["G"] = new Vector2(-60, 60),
            ["X"] = new Vector2(0, 8), ["Dd"] = new Vector2(60, 8), ["g5"] = new Vector2(60, 30), ["E"] = new Vector2(60, 60),
            ["g4"] = new Vector2(30, 60), ["F"] = new Vector2(0, 60), ["M"] = new Vector2(0, 76),
        };
        static readonly string[,] Edges =
        {
            { "A", "g1" }, { "g1", "B" }, { "B", "g6" }, { "g6", "C" }, { "C", "Dd" }, { "Dd", "g5" }, { "g5", "E" },
            { "E", "g4" }, { "g4", "F" }, { "F", "G" }, { "G", "g3" }, { "g3", "H" }, { "H", "g2" }, { "g2", "A" },
            { "B", "X" }, { "X", "F" }, { "H", "X" }, { "X", "Dd" }, { "F", "M" },
        };
        static readonly (string gate, Vector2 dir)[] FarmGates =
        {
            ("g1", new Vector2(0, -1)), ("g2", new Vector2(-1, 0)), ("g3", new Vector2(-1, 0)),
            ("g4", new Vector2(0, 1)), ("g5", new Vector2(1, 0)), ("g6", new Vector2(0, -1)),
        };

        static Vector3 N(string n) => new Vector3(Nodes[n].x * K, 0, Nodes[n].y * K);
        static Vector3 W(float x, float z) => new Vector3(x * K, 0, z * K);   // współrzędne z HTML
        static readonly List<(Vector2 a, Vector2 b)> _segments = new List<(Vector2, Vector2)>();
        static readonly List<(Vector2 c, float r)> _flat = new List<(Vector2, float)>();     // miejsca wyrównane pod budynki
        static readonly List<(Vector2 c, Vector2 half)> _blocked = new List<(Vector2, Vector2)>();  // bez drzew

        static float DistRoad(float x, float z)
        {
            var p = new Vector2(x, z); float best = 1e9f;
            foreach (var (a, b) in _segments)
            {
                var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return best;
        }

        static float Yaw(Vector3 forward) => Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        [MenuItem("Mleczarka/4. Zbuduj świat: Gmina (gra)", priority = 4)]
        public static void BuildWorld()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            QuaterniusMaterials.SetupAll();
            Directory.CreateDirectory("Assets/_Mleczarka/Scenes");
            Directory.CreateDirectory(GEN + "Settings");
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _rng = new System.Random(11);
            MeasureKits();
            _segments.Clear(); _flat.Clear(); _blocked.Clear();
            for (int i = 0; i < Edges.GetLength(0); i++) _segments.Add((Nodes[Edges[i, 0]] * K, Nodes[Edges[i, 1]] * K));

            // miejsca do wyrównania terenu
            var dairyHall = W(-17, -17); var dairyZone = W(-5, -7);
            var marketZone = W(0, 82);
            _flat.Add((new Vector2(dairyHall.x, dairyHall.z), 30f)); _flat.Add((new Vector2(dairyZone.x, dairyZone.z), 20f));
            _flat.Add((new Vector2(0, 176), 46f));
            _flat.Add((new Vector2(160, -124), 22f));
            foreach (var (gate, dir) in FarmGates) { var c = Nodes[gate] * K + dir * 16f; _flat.Add((c, 26f)); }

            var env = new GameObject("Otoczenie");
            LightingSetup.Apply(LightingSetup.Pora.Dzien);
            new GameObject("Odświeżanie otoczenia").AddComponent<Mleczarka.EnvironmentRefresh>();
            BuildWorldTerrain(env.transform);

            var places = new GameObject("Miejsca").transform;
            for (int i = 0; i < FarmGates.Length; i++) BuildFarm(places, i);
            BuildDairy(places, dairyHall, dairyZone);
            BuildMarket(places, marketZone);
            BuildRival(places);
            BuildWorldNature(env.transform);

            // pojazdy gracza zaparkowane przy mleczarni
            var tanker = CisternFactory.BuildInScene("Cysterna gracza", G(new Vector3(-8, 0, -4)) + Vector3.up * 0.05f, 90f, null, null);
            var van = VehicleFactory.BuildInScene(VehicleType.Dostawczak, "Dostawczak gracza", G(new Vector3(-8, 0, -22)) + Vector3.up * 0.05f, 90f);
            var farmer = BuildFarmer(G(new Vector3(-2, 0, -12)), 0f);
            BuildCamera(farmer);
            BuildGarageUI(farmer);
            var garageDoc = Object.FindFirstObjectByType<Mleczarka.UI.GarageUI>().GetComponent<UnityEngine.UIElements.UIDocument>();
            garageDoc.sortingOrder = 10;

            var gc = new GameObject("Gra (symulacja)").AddComponent<GameController>();
            gc.player = farmer.GetComponent<FarmerController>();
            var hud = new GameObject("HUD (UI)");
            var hudDoc = hud.AddComponent<UnityEngine.UIElements.UIDocument>();
            hudDoc.panelSettings = garageDoc.panelSettings; hudDoc.sortingOrder = 0;
            hud.AddComponent<Mleczarka.UI.HudUI>();

            EditorSceneManager.SaveScene(scene, WorldScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != WorldScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(WorldScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"[Mleczarka] Świat zbudowany: {WorldScenePath}");
        }

        // ---------- teren ----------

        static void BuildWorldTerrain(Transform parent)
        {
            const float S = 380f, H = 30f; const int HR = 513, AR = 1024;
            var origin = new Vector3(-S / 2, 0, -140f);
            string tdPath = GEN + "Settings/Teren_Gmina.asset";
            AssetDatabase.DeleteAsset(tdPath);
            var td = new TerrainData();
            AssetDatabase.CreateAsset(td, tdPath);
            td.heightmapResolution = HR; td.alphamapResolution = AR; td.size = new Vector3(S, H, S);

            float FlatW(float x, float z)
            {
                float w = Smooth(RoadHalf + 3f, 40f, DistRoad(x, z));
                foreach (var (c, r) in _flat) w = Mathf.Min(w, Smooth(r * 0.75f, r * 2.4f, (new Vector2(x, z) - c).magnitude));
                return w;
            }

            var hts = new float[HR, HR];
            for (int y = 0; y < HR; y++)
                for (int x = 0; x < HR; x++)
                {
                    float wx = origin.x + x / (float)(HR - 1) * S, wz = origin.z + y / (float)(HR - 1) * S;
                    float hill = Mathf.PerlinNoise(wx * 0.012f + 11f, wz * 0.012f + 7f) * 8f + Mathf.PerlinNoise(wx * 0.05f, wz * 0.05f) * 1.2f;
                    float edge = Mathf.Max(Mathf.Abs(wx) - 160f, Mathf.Max(origin.z + 20f - wz, wz - (origin.z + S - 20f)));
                    float rim = Smooth(0f, 30f, edge) * 16f;                    // wzgórza zamykające mapę
                    hts[y, x] = (hill + rim) * FlatW(wx, wz) / H;
                }
            td.SetHeights(0, 0, hts);

            td.terrainLayers = new[] { Layer("Trawa", "T_Trawa_Stylizowana", 7f), Layer("Laka", "T_Trawa_Laka", 9f), Layer("Ziemia", "T_Ziemia_Droga", 4f) };
            var fields = new[] { (W(-42, -26), W(9, 7)), (W(30, -22), W(11, 8)), (W(-30, 34), W(11, 8)), (W(30, 34), W(11, 8)), (W(-84, -60), W(9, 7)), (W(84, 84), W(8, 7)) };
            foreach (var (c, h) in fields) _blocked.Add((new Vector2(c.x, c.z), new Vector2(h.x + 3, h.z + 3)));
            var map = new float[AR, AR, 3];
            for (int y = 0; y < AR; y++)
                for (int x = 0; x < AR; x++)
                {
                    float wx = origin.x + (x + 0.5f) / AR * S, wz = origin.z + (y + 0.5f) / AR * S;
                    float noise = (Mathf.PerlinNoise(wx * 0.4f, wz * 0.4f) - 0.5f) * 0.8f;
                    float road = 1f - Smooth(RoadHalf - 0.6f, RoadHalf + 0.6f, DistRoad(wx, wz) + noise);
                    float plaza = 0f;
                    foreach (var (c, r) in _flat) plaza = Mathf.Max(plaza, 1f - Smooth(r * 0.35f, r * 0.5f, (new Vector2(wx, wz) - c).magnitude + noise * 3f));
                    float field = 0f;
                    foreach (var (c, h) in fields)
                        if (Mathf.Abs(wx - c.x) < h.x && Mathf.Abs(wz - c.z) < h.z) field = 0.85f;
                    float dirt = Mathf.Clamp01(Mathf.Max(road, plaza * 0.8f));
                    float meadow = Mathf.Max(field, Smooth(0.45f, 0.72f, Mathf.PerlinNoise(wx * 0.03f + 3f, wz * 0.03f + 9f))) * (1f - dirt);
                    map[y, x, 2] = dirt; map[y, x, 1] = meadow; map[y, x, 0] = 1f - dirt - meadow;
                }
            td.SetAlphamaps(0, 0, map);
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();
            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Teren"; go.transform.SetParent(parent); go.transform.position = origin;
            _terrain = go.GetComponent<Terrain>();
            var tm = AssetDatabase.LoadAssetAtPath<Material>(GEN + "Settings/M_Teren.mat");
            if (!tm) { tm = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")); SaveAsset(tm, GEN + "Settings/M_Teren.mat"); }
            _terrain.materialTemplate = tm; _terrain.drawInstanced = true; _terrain.basemapDistance = 500f; _terrain.heightmapPixelError = 4f;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        // ---------- miejsca ----------

        static WorldPlace Zone(Transform parent, string name, PlaceKind kind, Vector3 pos, float r, int farm = -1)
        {
            var z = new GameObject("Strefa: " + name).AddComponent<WorldPlace>();
            z.transform.SetParent(parent); z.transform.position = G(pos);
            z.kind = kind; z.radius = r; z.farmId = farm; z.displayName = name;
            return z;
        }

        static readonly string[] FarmNames = { "Kowalscy", "Nowakowie", "Wiśniewscy", "Zielińscy", "Wójcikowie", "Kamińscy" };

        static void BuildFarm(Transform parent, int i)
        {
            var (gateName, d) = FarmGates[i];
            var gate = N(gateName); var dir = new Vector3(d.x, 0, d.y); var side = new Vector3(d.y, 0, -d.x);
            var root = new GameObject("Gospodarstwo " + FarmNames[i]).transform; root.SetParent(parent);
            Zone(root, FarmNames[i], PlaceKind.Gospodarstwo, gate + dir * 9f, 12f, i);

            var housePos = gate + dir * 18f + side * 6f;
            var hp = G(housePos); BuildHouse(root, hp, Yaw(-dir));
            _blocked.Add((new Vector2(housePos.x, housePos.z), new Vector2(9, 9)));
            // podwórze: wóz, skrzynie, bańki (silos w mniejszej skali jako zbiornik na mleko)
            float k = _kVillage;
            var tankPos = gate + dir * 9f - side * 7f;
            var silo = Place(WORLD + "Silos_Mleka.fbx", root, G(tankPos), Yaw(side), 0.45f);
            if (silo) AddMeshColliders(silo);
            Place(VIL + "Prop_Wagon.fbx", root, G(gate + dir * 16f - side * 6f), Yaw(side) + 20, k);
            for (int c = 0; c < 3; c++) Place(VIL + "Prop_Crate.fbx", root, G(gate + dir * (12f + c * 1.2f) + side * 2.6f), R(0, 360), k * 0.75f);
            // pastwisko z płotem
            var pasture = gate + dir * 18f - side * 16f;
            _blocked.Add((new Vector2(pasture.x, pasture.z), new Vector2(9, 9)));
            var fence = new GameObject("Pastwisko").transform; fence.SetParent(root);
            for (float t = -7; t <= 7; t += 2)
            {
                Fence(fence, G(pasture + side * t + dir * 8f), Yaw(side) + 90, k);
                Fence(fence, G(pasture + side * t - dir * 8f), Yaw(side) + 90, k);
                Fence(fence, G(pasture + dir * t + side * 8f), Yaw(side), k);
                if (Mathf.Abs(t) > 2) Fence(fence, G(pasture + dir * t - side * 8f), Yaw(side), k);
            }
            foreach (var r in fence.GetComponentsInChildren<MeshRenderer>()) if (!r.GetComponent<Collider>()) r.gameObject.AddComponent<BoxCollider>();
            // drzewo przy domu
            Place(NAT + $"CommonTree_{1 + i % 5}.fbx", root, G(housePos + side * 8f + dir * 4f), R(0, 360), _kNature * R(0.95f, 1.2f));
            MakeStatic(root.gameObject);
        }

        static void AddMeshColliders(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) if (!mf.GetComponent<Collider>()) mf.gameObject.AddComponent<MeshCollider>();
        }

        static void BuildDairy(Transform parent, Vector3 hall, Vector3 zone)
        {
            var root = new GameObject("Mleczarnia").transform; root.SetParent(parent);
            Zone(root, "Mleczarnia", PlaceKind.Mleczarnia, zone, 15f);
            // hala: dom z modułów w większej skali, drzwi w stronę placu
            var hallRoot = new GameObject("Hala").transform; hallRoot.SetParent(root);
            BuildHouse(hallRoot, G(hall), Yaw(zone - hall));
            hallRoot.localScale = Vector3.one * 1.0f;
            var h = hallRoot.GetChild(0); h.localScale = Vector3.one * _kVillage * 2.1f;
            _blocked.Add((new Vector2(hall.x, hall.z), new Vector2(16, 16)));
            var silo = Place(WORLD + "Silos_Mleka.fbx", root, G(hall + new Vector3(-12, 0, 12)), 30f, 1f);
            if (silo) AddMeshColliders(silo);
            var sign = Place(WORLD + "Szyld_Mleczarnia.fbx", root, G(new Vector3(6, 0, -2)), 90f + 180f, 1f);
            if (sign) AddMeshColliders(sign);
            for (int c = 0; c < 4; c++) Place(VIL + "Prop_Crate.fbx", root, G(hall + new Vector3(10 + c * 1.1f, 0, -9)), R(0, 360), _kVillage * 0.8f);
            MakeStatic(root.gameObject);
        }

        static void BuildMarket(Transform parent, Vector3 zone)
        {
            var root = new GameObject("Rynek").transform; root.SetParent(parent);
            Zone(root, "Rynek", PlaceKind.Rynek, zone, 17f);
            var stalls = new[] { new Vector3(-13, 0, 174), new Vector3(0, 0, 180), new Vector3(13, 0, 174) };
            foreach (var s in stalls)
            {
                var st = Place(WORLD + "Stragan_Rynek.fbx", root, G(s), Yaw(zone - s), 1f);
                if (st) AddMeshColliders(st);
            }
            var houses = new[] { W(-30, 92), W(30, 92), W(-20, 104), W(20, 104), W(-44, 84), W(44, 84) };
            foreach (var hp in houses) { BuildHouse(root, G(hp), Yaw(zone - hp)); _blocked.Add((new Vector2(hp.x, hp.z), new Vector2(9, 9))); }
            _blocked.Add((new Vector2(0, 176), new Vector2(22, 12)));
            foreach (var c in new[] { new Vector3(-6, 0, 170), new Vector3(7, 0, 171) }) Place(VIL + "Prop_Crate.fbx", root, G(c), R(0, 360), _kVillage * 0.8f);
            MakeStatic(root.gameObject);
        }

        static void BuildRival(Transform parent)
        {
            var root = new GameObject("Mleczpol (rywal)").transform; root.SetParent(parent);
            var p = new Vector3(160, 0, -124);
            var hallRoot = new GameObject("Hala Mleczpolu").transform; hallRoot.SetParent(root);
            BuildHouse(hallRoot, G(p), Yaw(N("C") - p));
            hallRoot.GetChild(0).localScale = Vector3.one * _kVillage * 1.8f;
            var silo = Place(WORLD + "Silos_Mleka.fbx", root, G(p + new Vector3(10, 0, 8)), 0, 0.8f);
            if (silo) AddMeshColliders(silo);
            _blocked.Add((new Vector2(p.x, p.z), new Vector2(16, 16)));
            MakeStatic(root.gameObject);
        }

        // ---------- przyroda ----------

        static bool Blocked(Vector3 p, float pad = 0f)
        {
            foreach (var (c, h) in _blocked) if (Mathf.Abs(p.x - c.x) < h.x + pad && Mathf.Abs(p.z - c.y) < h.y + pad) return true;
            foreach (var z in _zones) if (z && (new Vector2(p.x, p.z) - new Vector2(z.transform.position.x, z.transform.position.z)).magnitude < z.radius + 4f + pad) return true;
            return false;
        }

        static WorldPlace[] _zones = new WorldPlace[0];

        static void BuildWorldNature(Transform parent)
        {
            _zones = Object.FindObjectsByType<WorldPlace>(FindObjectsSortMode.None);
            var root = new GameObject("Przyroda").transform; root.SetParent(parent);
            var trees = new GameObject("Drzewa").transform; trees.SetParent(root);
            var bushes = new GameObject("Krzewy i kwiaty").transform; bushes.SetParent(root);
            var grass = new GameObject("Trawy").transform; grass.SetParent(root);
            var rocks = new GameObject("Kamienie").transform; rocks.SetParent(root);
            float k = _kNature;

            int placed = 0;
            for (int i = 0; i < 4000 && placed < 340; i++)
            {
                var p = new Vector3(R(-185, 185), 0, R(-138, 238));
                if (DistRoad(p.x, p.z) < 9f || Blocked(p, 3f)) continue;
                bool outer = Mathf.Abs(p.x) > 140 || p.z < -110 || p.z > 190;
                // kępy: gęściej przy krawędziach mapy, pojedynczo w środku
                if (!outer && Mathf.PerlinNoise(p.x * 0.02f + 5f, p.z * 0.02f + 1f) < 0.5f) continue;
                bool pine = outer ? _rng.NextDouble() < 0.65 : _rng.NextDouble() < 0.2;
                string m = pine ? $"Pine_{_rng.Next(1, 6)}" : $"CommonTree_{_rng.Next(1, 6)}";
                var t = Place(NAT + m + ".fbx", trees, G(p), R(0, 360), k * R(0.9f, 1.4f));
                if (t)
                {
                    var c = t.AddComponent<CapsuleCollider>();
                    c.radius = 0.35f / t.transform.localScale.x; c.height = 4f / t.transform.localScale.x; c.center = Vector3.up * c.height * 0.5f;
                }
                placed++;
            }
            string[] f = { "Flower_3_Group", "Flower_4_Group", "Bush_Common_Flowers", "Bush_Common", "Clover_1", "Plant_1", "Fern_1" };
            for (int i = 0, n = 0; i < 2000 && n < 260; i++)
            {
                var p = new Vector3(R(-150, 150), 0, R(-120, 200));
                if (DistRoad(p.x, p.z) < RoadHalf + 1.5f || Blocked(p)) continue;
                string m = f[_rng.Next(f.Length)];
                float s = m.StartsWith("Bush") ? R(0.55f, 0.85f) : m.StartsWith("Fern") ? R(0.18f, 0.25f) : m.StartsWith("Plant") ? R(0.35f, 0.5f) : R(0.3f, 0.45f);
                Place(NAT + m + ".fbx", bushes, G(p), R(0, 360), k * s); n++;
            }
            string[] g = { "Grass_Common_Short", "Grass_Common_Tall", "Grass_Wispy_Short", "Grass_Wispy_Tall" };
            for (int i = 0, n = 0; i < 4000 && n < 900; i++)
            {
                var p = new Vector3(R(-150, 150), 0, R(-120, 200));
                if (DistRoad(p.x, p.z) < RoadHalf + 0.8f || Blocked(p, -3f)) continue;
                Place(NAT + g[_rng.Next(g.Length)] + ".fbx", grass, G(p), R(0, 360), k * R(0.32f, 0.5f)); n++;
            }
            for (int i = 0, n = 0; i < 400 && n < 40; i++)
            {
                var p = new Vector3(R(-160, 160), 0, R(-130, 210));
                if (DistRoad(p.x, p.z) < 8f || Blocked(p, 2f)) continue;
                var r = Place(NAT + $"Rock_Medium_{_rng.Next(1, 4)}.fbx", rocks, G(p) - Vector3.up * 0.1f, R(0, 360), k * R(0.35f, 0.8f));
                if (r) AddMeshColliders(r);
                n++;
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = r.transform.IsChildOf(grass) ? ShadowCastingMode.Off : ShadowCastingMode.On;
            MakeStatic(root.gameObject);
        }
    }
}
