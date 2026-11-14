using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mleczarka.Characters;
using Mleczarka.Player;
using Mleczarka.Vehicles;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mleczarka.EditorTools
{
    /// <summary>
    /// Buduje scenę demonstracyjną „Gospodarstwo”: zagroda z domem z modułów Medieval Village MegaKit,
    /// las i łąka ze Stylized Nature MegaKit, rolnik (UBC + strój Peasant + animacje UAL),
    /// ciągnik zastępczy (Kenney) z fizyką kół, kamera Cinemachine, światło i postprocess URP.
    /// Scenę da się w każdej chwili zbudować od nowa — nie ma w niej ręcznych zmian.
    /// </summary>
    public static partial class DemoFarmBuilder
    {
        const string Q = QuaterniusMaterials.Art + "ThirdParty/Quaternius/";
        const string NAT = Q + "StylizedNatureMegaKit/FBX/";
        const string VIL = Q + "MedievalVillageMegaKit/FBX/";
        const string UBC = Q + "UniversalBaseCharacters/";
        const string OUT = Q + "ModularOutfitsFantasy/";
        const string UAL1 = Q + "UniversalAnimationLibrary/UAL1_Standard.fbx";
        const string KEN = QuaterniusMaterials.Art + "ThirdParty/Kenney/CarKit/";
        const string GEN = QuaterniusMaterials.Art + "Generated/";
        const string ScenePath = "Assets/_Mleczarka/Scenes/Demo_Gospodarstwo.unity";

        static float _kVillage = 1f, _kNature = 1f;
        static Quaternion _villageFix = Quaternion.identity;
        static System.Random _rng;

        [MenuItem("Mleczarka/2. Zbuduj scenę demo: Gospodarstwo", priority = 2)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            QuaterniusMaterials.SetupAll();
            Directory.CreateDirectory("Assets/_Mleczarka/Scenes");
            Directory.CreateDirectory(GEN + "Characters");
            Directory.CreateDirectory(GEN + "Settings");
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _rng = new System.Random(7);
            MeasureKits();

            var env = new GameObject("Otoczenie");
            LightingSetup.Apply(LightingSetup.Pora.Dzien);
            new GameObject("Odświeżanie otoczenia").AddComponent<Mleczarka.EnvironmentRefresh>();
            BuildTerrain(env.transform);
            BuildGround(env.transform);
            BuildHouse(env.transform, new Vector3(0, 0, 0), 0f);
            BuildYard(env.transform);
            BuildNature(env.transform);
            VehicleFactory.BuildInScene(Mleczarka.Vehicles.Model.VehicleType.Dostawczak, "Dostawczak mleczarni", new Vector3(6.5f, 0.05f, 6.5f), -20f);
            VehicleFactory.BuildInScene(Mleczarka.Vehicles.Model.VehicleType.Dostawczak, "Dostawczak z bańkami", new Vector3(-5.5f, 0.05f, -3.0f), 0f,
                new[] { "Skrzynia_Otwarta_Banki" },
                new[] { new Mleczarka.Vehicles.Model.Rgb(0.85f, 0.25f, 0.20f), new Mleczarka.Vehicles.Model.Rgb(0.95f, 0.92f, 0.80f), new Mleczarka.Vehicles.Model.Rgb(0.20f, 0.45f, 0.75f) });
            VehicleFactory.BuildInScene(Mleczarka.Vehicles.Model.VehicleType.Dostawczak, "Dostawczak wyścigowy", new Vector3(13.0f, 0.05f, 16f), 210f,
                new[] { "Kabina_Dostawcza_Sportowa", "Skrzynia_Wyscigowa", "Kola_Sportowe", "Zderzak_Dostawczy_Sportowy_Przod", "Wydech_Dostawczy_Sportowy" },
                new[] { new Mleczarka.Vehicles.Model.Rgb(0.10f, 0.35f, 0.90f), new Mleczarka.Vehicles.Model.Rgb(0.95f, 0.95f, 0.95f), new Mleczarka.Vehicles.Model.Rgb(0.98f, 0.55f, 0.05f) });
            CisternFactory.BuildInScene(new Vector3(0.5f, 0.05f, 17.5f), 180f); // na polnej drodze, przodem do zagrody
            // pokaz personalizacji: wersja tuningowa i wyścigowa obok drogi
            CisternFactory.BuildInScene("Cysterna tuning", new Vector3(7.0f, 0.05f, 20f), 200f,
                new[] { "Zbiornik_Duzy", "Kola_Terenowe", "Dach_Belka_Swietlna", "Wydech_Podwojny" },
                new[] { new Mleczarka.Vehicles.Model.Rgb(0.20f, 0.55f, 0.25f), new Mleczarka.Vehicles.Model.Rgb(0.95f, 0.95f, 0.92f), new Mleczarka.Vehicles.Model.Rgb(0.95f, 0.70f, 0.10f) });
            CisternFactory.BuildInScene("Cysterna wyścigowa", new Vector3(-6.0f, 0.05f, 20f), 160f,
                new[] { "Kabina_Sportowa", "Zbiornik_Wyscigowy", "Kola_Sportowe", "Spoiler_Wyscigowy", "Zderzak_Sportowy_Przod", "Zderzak_Sportowy_Tyl", "Wydech_Podwojny" },
                new[] { new Mleczarka.Vehicles.Model.Rgb(0.98f, 0.76f, 0.08f), new Mleczarka.Vehicles.Model.Rgb(0.08f, 0.08f, 0.10f), new Mleczarka.Vehicles.Model.Rgb(0.85f, 0.12f, 0.10f) });
            var farmer = BuildFarmer(new Vector3(-1.5f, 0f, 6.5f), 160f);
            BuildCamera(farmer);
            BuildGarageUI(farmer);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"[Mleczarka] Scena zbudowana: {ScenePath}. Skala wsi {_kVillage:0.###}, natury {_kNature:0.###}.");
        }

        // ---------- pomocnicze ----------

        static GameObject Load(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!go) Debug.LogWarning("[Mleczarka] Brak modelu: " + path);
            return go;
        }

        static GameObject Place(string path, Transform parent, Vector3 pos, float yaw, float scale = 1f)
        {
            var src = Load(path);
            if (!src) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0) * (path.StartsWith(VIL) ? _villageFix : Quaternion.identity);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void SaveAsset(Object o, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(o, path);
        }

        static float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        static void MakeStatic(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
        }

        static void MeasureKits()
        {
            // Moduły wsi (FBX z Blendera) bywają „położone”: wysokość ściany wychodzi wzdłuż Z. Dobieramy obrót tak, by stała pionowo.
            _villageFix = Quaternion.identity;
            var wall = Place(VIL + "Wall_Plaster_Straight.fbx", null, Vector3.zero, 0);
            if (wall)
            {
                var b = WorldBounds(wall);
                if (b.size.z > b.size.y * 2f) _villageFix = Quaternion.Euler(b.center.z > 0 ? -90f : 90f, 0, 0);
                wall.transform.rotation = _villageFix;
                b = WorldBounds(wall);
                _kVillage = 2f / Mathf.Max(0.0001f, b.size.x);
                Debug.Log($"[Mleczarka] Ściana testowa po korekcie: {b.size}, środek {b.center}, korekta {_villageFix.eulerAngles}");
                Object.DestroyImmediate(wall);
            }
            var tree = Place(NAT + "CommonTree_1.fbx", null, Vector3.zero, 0);
            if (tree) { _kNature = 7.26f / Mathf.Max(0.0001f, WorldBounds(tree).size.y); Object.DestroyImmediate(tree); }
        }

        // ---------- światło, niebo, postprocess ----------

        static void BuildLightAndSky()
        {
            var sunGo = new GameObject("Słońce");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.80f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sunGo.transform.rotation = Quaternion.Euler(38f, -140f, 0f);
            sunGo.AddComponent<UniversalAdditionalLightData>();

            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetColor("_SkyTint", new Color(0.45f, 0.62f, 0.95f));
            sky.SetColor("_GroundColor", new Color(0.42f, 0.46f, 0.38f));
            sky.SetFloat("_Exposure", 1.25f);
            SaveAsset(sky, GEN + "Settings/Niebo_Dzien.mat");
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.63f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.30f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.92f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 160f;

            string profilePath = GEN + "Settings/Postprocess_Gospodarstwo.asset";
            AssetDatabase.DeleteAsset(profilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.35f); bloom.threshold.Override(1.05f); bloom.scatter.Override(0.6f);
            var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.Override(0.25f); ca.contrast.Override(10f); ca.saturation.Override(6f);
            var wb = profile.Add<WhiteBalance>(true); wb.temperature.Override(6f);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.18f); vig.smoothness.Override(0.5f);
            foreach (var c in profile.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);

            var volGo = new GameObject("Postprocess");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
        }

        // ---------- teren ----------

        static void BuildGround(Transform parent)
        {
            // kamienie ścieżki od drzwi do drogi
            var path = new GameObject("Ścieżka").transform; path.SetParent(parent);
            for (int i = 0; i < 4; i++)
            {
                string[] stones = { "RockPath_Round_Small_1", "RockPath_Round_Small_2", "RockPath_Round_Small_3", "RockPath_Round_Wide" };
                Place(NAT + stones[i % stones.Length] + ".fbx", path, new Vector3(0.9f + R(-0.25f, 0.25f), 0.0f, 4.2f + i * 1.25f), R(0, 360), _kNature * 0.6f);
            }
            MakeStatic(path.gameObject);
        }

        // ---------- teren (Unity Terrain, stylizowane tekstury) ----------

        static Terrain _terrain;
        static float GroundY(Vector3 p) => _terrain ? _terrain.SampleHeight(p) + _terrain.transform.position.y : 0f;
        static Vector3 G(Vector3 p) => new Vector3(p.x, GroundY(p), p.z);

        static TerrainLayer Layer(string name, string tex, float tile)
        {
            string path = GEN + "Settings/Warstwa_" + name + ".terrainlayer";
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (!l) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
            const string T = "Assets/_Mleczarka/Art/Textures/Teren/";
            l.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(T + tex + "_BaseColor.png");
            l.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(T + tex + "_Normal.png");
            l.normalScale = 0.6f;
            l.tileSize = new Vector2(tile, tile);
            l.smoothness = 0.05f; l.metallic = 0f;
            l.diffuseRemapMax = new Vector4(0.86f, 0.86f, 0.86f, 1f);   // trochę ciemniej i mniej jaskrawo pod słońcem HDRI
            EditorUtility.SetDirty(l);
            return l;
        }

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        static void BuildTerrain(Transform parent)
        {
            const float S = 240f, H = 24f; const int HR = 257, AR = 512;
            // najpierw zapis jako asset — mapy warstw (podtekstury) muszą trafić do pliku
            string tdPath = GEN + "Settings/Teren_Gospodarstwo.asset";
            AssetDatabase.DeleteAsset(tdPath);
            var td = new TerrainData();
            AssetDatabase.CreateAsset(td, tdPath);
            td.heightmapResolution = HR;
            td.alphamapResolution = AR;
            td.size = new Vector3(S, H, S);
            Vector3 origin = new Vector3(-S / 2, 0, -S / 2);

            // płasko w zagrodzie i przy drodze, łagodne pagórki dalej
            var hts = new float[HR, HR];
            for (int y = 0; y < HR; y++)
                for (int x = 0; x < HR; x++)
                {
                    float wx = origin.x + x / (float)(HR - 1) * S, wz = origin.z + y / (float)(HR - 1) * S;
                    float d = new Vector2(wx, wz - 4f).magnitude;
                    float hill = Mathf.PerlinNoise(wx * 0.018f + 11f, wz * 0.018f + 7f) * 7f + Mathf.PerlinNoise(wx * 0.06f, wz * 0.06f) * 1.2f;
                    float w = Smooth(40f, 75f, d);
                    if (wz > 5f) w *= Smooth(5f, 14f, Mathf.Abs(wx - 0.5f));   // droga
                    hts[y, x] = hill * w / H;
                }
            td.SetHeights(0, 0, hts);

            td.terrainLayers = new[] { Layer("Trawa", "T_Trawa_Stylizowana", 7f), Layer("Laka", "T_Trawa_Laka", 9f), Layer("Ziemia", "T_Ziemia_Droga", 4f) };
            var map = new float[AR, AR, 3];
            for (int y = 0; y < AR; y++)
                for (int x = 0; x < AR; x++)
                {
                    float wx = origin.x + (x + 0.5f) / AR * S, wz = origin.z + (y + 0.5f) / AR * S;
                    float edgeNoise = (Mathf.PerlinNoise(wx * 0.4f, wz * 0.4f) - 0.5f) * 0.6f;
                    float road = wz > 9.5f ? 1f - Smooth(1.3f, 2.3f, Mathf.Abs(wx - 0.5f) + edgeNoise) : 0f;
                    float path = (wz > 2.8f && wz < 11f) ? 1f - Smooth(0.5f, 1.1f, Mathf.Abs(wx - 0.9f) + edgeNoise * 0.6f) : 0f;
                    float yard = 1f - Smooth(1.5f, 3.5f, new Vector2(wx - 6.5f, wz - 6.5f).magnitude + edgeNoise * 2f);   // plac przy dostawczaku
                    float dirt = Mathf.Clamp01(Mathf.Max(road, Mathf.Max(path, yard * 0.85f)));
                    float meadow = Smooth(0.45f, 0.72f, Mathf.PerlinNoise(wx * 0.035f + 3f, wz * 0.035f + 9f)) * (1f - dirt);
                    map[y, x, 2] = dirt;
                    map[y, x, 1] = meadow;
                    map[y, x, 0] = 1f - dirt - meadow;
                }
            td.SetAlphamaps(0, 0, map);
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();
            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Teren";
            go.transform.SetParent(parent);
            go.transform.position = origin;
            _terrain = go.GetComponent<Terrain>();
            var tm = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            SaveAsset(tm, GEN + "Settings/M_Teren.mat");
            _terrain.materialTemplate = tm;
            _terrain.drawInstanced = true;
            _terrain.basemapDistance = 400f;
            _terrain.heightmapPixelError = 3f;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        // ---------- dom z modułów (sprawdzony wcześniej w podglądzie) ----------

        static void BuildHouse(Transform parent, Vector3 pos, float yaw)
        {
            var house = new GameObject("Dom rolnika").transform;
            house.SetParent(parent);
            house.localPosition = pos;
            house.localRotation = Quaternion.Euler(0, yaw, 0);
            house.localScale = Vector3.one * _kVillage;

            const float W = 4f, L = 6f;
            void P(string m, float x, float y, float z, float r) => Place(VIL + m + ".fbx", house, new Vector3(x, y, z), r);

            // Szczyty (krótsze ściany) na z = ±3; współrzędna X lustrzana względem glTF.
            foreach (int s in new[] { 1, -1 })
                foreach (int x in new[] { -1, 1 })
                {
                    float r = s == 1 ? 0 : 180;
                    bool door = s == 1 && x == 1;
                    P(door ? "Wall_Plaster_Door_Flat" : "Wall_Plaster_Window_Wide_Flat", x, 0, s * L / 2, r);
                    if (door) P("Door_1_Flat", x + 0.5f, 0, s * L / 2, 0);
                    else { P("Window_Wide_Flat1", x, 0, s * L / 2, r); P("WindowShutters_Wide_Flat_Open", x, 0, s * L / 2, r); }
                }
            // Ściany boczne na x = ±2
            foreach (int s in new[] { 1, -1 })
                foreach (int z in new[] { -2, 0, 2 })
                {
                    float r = -s * 90;
                    P(z == 0 ? "Wall_Plaster_WoodGrid" : "Wall_Plaster_Window_Wide_Flat", s * W / 2, 0, z, r);
                    if (z != 0) { P("Window_Wide_Flat1", s * W / 2, 0, z, r); P("WindowShutters_Wide_Flat_Open", s * W / 2, 0, z, r); }
                }
            foreach (int sx in new[] { 1, -1 })
                foreach (int sz in new[] { 1, -1 })
                    P("Corner_Exterior_Wood", sx * W / 2, 0, sz * L / 2, 0);
            P("Floor_WoodDark", -1, 0.02f, -2, 0); P("Floor_WoodDark", 1, 0.02f, -2, 0);
            P("Floor_WoodDark", -1, 0.02f, 0, 0); P("Floor_WoodDark", 1, 0.02f, 0, 0);
            P("Floor_WoodDark", -1, 0.02f, 2, 0); P("Floor_WoodDark", 1, 0.02f, 2, 0);
            P("Roof_RoundTiles_4x6", 0, 3, 0, 0);
            P("Roof_Front_Brick4", 0, 3, L / 2, 0);
            P("Roof_Front_Brick4", 0, 3, -L / 2, 180);
            P("Prop_Chimney", -1, 4.2f, -1.5f, 0);

            foreach (var mf in house.GetComponentsInChildren<MeshFilter>())
                if (!mf.GetComponent<Collider>()) mf.gameObject.AddComponent<MeshCollider>();
            MakeStatic(house.gameObject);
        }

        // ---------- zagroda: płot, rekwizyty ----------

        static void BuildYard(Transform parent)
        {
            var yard = new GameObject("Zagroda").transform; yard.SetParent(parent);
            float k = _kVillage;
            // prostokąt płotu x ∈ [-10, 10], z ∈ [-9, 11], brama na drodze (x≈0, z = 11)
            for (float x = -9; x <= 9; x += 2)
            {
                if (x < -2f || x > 2f) Fence(yard, new Vector3(x, 0, 11), 0, k); // brama 4 m na drogę
                Fence(yard, new Vector3(x, 0, -9), 0, k);
            }
            for (float z = -8; z <= 10; z += 2)
            {
                Fence(yard, new Vector3(-10, 0, z), 90, k);
                Fence(yard, new Vector3(10, 0, z), 90, k);
            }
            Place(VIL + "Prop_Wagon.fbx", yard, new Vector3(-6.2f, 0, 3.5f), 70, k);
            Place(VIL + "Prop_Crate.fbx", yard, new Vector3(3.4f, 0, 2.2f), 15, k * 0.8f);
            Place(VIL + "Prop_Crate.fbx", yard, new Vector3(3.6f, 0, 3.3f), -10, k * 0.7f);
            Place(VIL + "Prop_Crate.fbx", yard, new Vector3(3.5f, 0.85f, 2.7f), 40, k * 0.6f);
            foreach (var r in yard.GetComponentsInChildren<MeshRenderer>())
                if (!r.GetComponent<Collider>()) r.gameObject.AddComponent<BoxCollider>();
            MakeStatic(yard.gameObject);
        }

        static void Fence(Transform parent, Vector3 pos, float yaw, float k) =>
            Place(VIL + (_rng.NextDouble() < 0.5 ? "Prop_WoodenFence_Single" : "Prop_WoodenFence_Extension1") + ".fbx", parent, pos, yaw, k);

        // ---------- przyroda ----------

        static void BuildNature(Transform parent)
        {
            var root = new GameObject("Przyroda").transform; root.SetParent(parent);
            var trees = new GameObject("Drzewa").transform; trees.SetParent(root);
            var bushes = new GameObject("Krzewy i kwiaty").transform; bushes.SetParent(root);
            var grass = new GameObject("Trawy").transform; grass.SetParent(root);
            var rocks = new GameObject("Kamienie").transform; rocks.SetParent(root);
            float k = _kNature;

            bool Free(Vector3 p) =>
                !(Mathf.Abs(p.x) < 12.5f && p.z > -11.5f && p.z < 13.5f) &&   // zagroda
                !(Mathf.Abs(p.x - 0.5f) < 4f && p.z > 10f) &&                  // droga
                !(p.x > -11f && p.x < 17f && p.z > 11f && p.z < 27f);           // plac z pojazdami

            // pierścień lasu: liściaste bliżej, świerki dalej
            int placed = 0;
            for (int i = 0; i < 400 && placed < 70; i++)
            {
                float a = R(0, Mathf.PI * 2), d = R(18f, 58f);
                var p = new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d - 6f);
                if (!Free(p)) continue;
                bool pine = d > 34f ? _rng.NextDouble() < 0.7 : _rng.NextDouble() < 0.2;
                string m = pine ? $"Pine_{_rng.Next(1, 6)}" : $"CommonTree_{_rng.Next(1, 6)}";
                var t = Place(NAT + m + ".fbx", trees, G(p), R(0, 360), k * R(0.85f, 1.25f));
                if (t)
                {
                    var c = t.AddComponent<CapsuleCollider>();
                    c.radius = 0.35f / t.transform.localScale.x; c.height = 4f / t.transform.localScale.x; c.center = Vector3.up * c.height * 0.5f;
                }
                placed++;
            }
            // pojedyncze drzewa w zagrodzie (jak lipa przy chałupie)
            Place(NAT + "CommonTree_3.fbx", trees, G(new Vector3(-7.5f, 0, -5.5f)), 30, k * 1.1f);
            Place(NAT + "CommonTree_1.fbx", trees, G(new Vector3(7.8f, 0, -6.5f)), 200, k);

            // krzewy i kwiaty przy domu i płocie
            var nearHouse = new[] { new Vector3(-2.9f, 0, 3.8f), new Vector3(2.9f, 0, -3.6f), new Vector3(-3f, 0, -2.5f), new Vector3(3.1f, 0, 1.0f) };
            foreach (var p in nearHouse) Place(NAT + (_rng.NextDouble() < 0.5 ? "Bush_Common_Flowers" : "Bush_Common") + ".fbx", bushes, G(p), R(0, 360), k * R(0.55f, 0.75f));
            for (int i = 0; i < 60; i++)
            {
                var p = new Vector3(R(-26, 26), 0, R(-24, 30));
                if (Mathf.Abs(p.x) < 4.2f && Mathf.Abs(p.z) < 5.2f) continue; // dom
                if (Mathf.Abs(p.x - 0.5f) < 2.5f && p.z > 3f) continue;         // ścieżka i droga
                string[] f = { "Flower_3_Group", "Flower_4_Group", "Flower_3_Single", "Flower_4_Single", "Bush_Common_Flowers", "Clover_1", "Clover_2", "Plant_1", "Fern_1" };
                string m = f[_rng.Next(f.Length)];
                float s = m.StartsWith("Bush") ? R(0.5f, 0.8f) : m.StartsWith("Fern") ? R(0.18f, 0.25f) : m.StartsWith("Plant") ? R(0.35f, 0.5f) : R(0.28f, 0.42f);
                Place(NAT + m + ".fbx", bushes, G(p), R(0, 360), k * s);
            }
            for (int i = 0; i < 260; i++)
            {
                var p = new Vector3(R(-45, 45), 0, R(-40, 50));
                if (Mathf.Abs(p.x) < 4.2f && Mathf.Abs(p.z) < 5.2f) continue;
                if (Mathf.Abs(p.x - 0.5f) < 2.2f && p.z > 3f) continue;
                string[] g = { "Grass_Common_Short", "Grass_Common_Tall", "Grass_Wispy_Short", "Grass_Wispy_Tall" };
                Place(NAT + g[_rng.Next(g.Length)] + ".fbx", grass, G(p), R(0, 360), k * R(0.3f, 0.45f));
            }
            for (int i = 0; i < 14; i++)
            {
                var p = new Vector3(R(-40, 40), 0, R(-40, 45));
                if (!Free(p)) continue;
                var r = Place(NAT + $"Rock_Medium_{_rng.Next(1, 4)}.fbx", rocks, G(p) - Vector3.up * 0.1f, R(0, 360), k * R(0.3f, 0.6f));
                if (r) foreach (var mf in r.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>();
            }
            Place(NAT + "Mushroom_Common.fbx", rocks, G(new Vector3(-8.2f, 0, -4.6f)), 20, k * 0.8f);
            Place(NAT + "Mushroom_Laetiporus.fbx", rocks, G(new Vector3(-6.8f, 0, -6.3f)), 80, k * 0.8f);

            foreach (var r in root.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = r.transform.IsChildOf(grass) ? ShadowCastingMode.Off : ShadowCastingMode.On;
            MakeStatic(root.gameObject);
        }

        // ---------- dostawczak (zastępczy model Kenney, fizyka WheelCollider) ----------

        public static GameObject BuildVehicle(string modelPath, string objectName, float length, float massKg, Vector3 pos, float yaw)
        {
            var root = new GameObject(objectName);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var model = Place(modelPath, root.transform, Vector3.zero, 0);
            if (!model) return root;
            model.name = "Model";

            Transform Find(string n) => model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == n);
            var fl = Find("wheel-front-left"); var fr = Find("wheel-front-right");
            var bl = Find("wheel-back-left"); var br = Find("wheel-back-right");

            // skala do zadanej długości i obrót tak, by przód był na +Z
            var b0 = WorldBounds(model);
            if (length > 0f) model.transform.localScale = Vector3.one * (length / Mathf.Max(b0.size.x, b0.size.z));
            Vector3 fwd = root.transform.InverseTransformPoint((fl.position + fr.position) * 0.5f) - root.transform.InverseTransformPoint((bl.position + br.position) * 0.5f);
            fwd.y = 0;
            float ang = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            model.transform.localRotation = Quaternion.Euler(0, -ang, 0);
            // posadź na ziemi
            var b1 = WorldBounds(model);
            model.transform.position += Vector3.up * (root.transform.position.y - b1.min.y);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = massKg;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var body = Find("body");
            var bb = WorldBounds(body.gameObject);
            var box = root.AddComponent<BoxCollider>();
            Vector3 c = root.transform.InverseTransformPoint(bb.center);
            Vector3 size = root.transform.InverseTransformVector(bb.size);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            float lift = size.y * 0.3f;
            box.center = c + Vector3.up * lift * 0.5f;
            box.size = new Vector3(size.x, size.y - lift, size.z);

            var veh = root.AddComponent<WheeledVehicle>();
            var wheels = new List<WheeledVehicle.Wheel>();
            var colRoot = new GameObject("Koła (fizyka)").transform; colRoot.SetParent(root.transform, false);
            foreach (var (t, front) in new[] { (fl, true), (fr, true), (bl, false), (br, false) })
            {
                float radius = WorldBounds(t.gameObject).extents.y;
                var wgo = new GameObject("WC_" + t.name);
                wgo.transform.SetParent(colRoot, false);
                const float travel = 0.22f;
                wgo.transform.position = t.position + Vector3.up * travel * 0.5f;
                var wc = wgo.AddComponent<WheelCollider>();
                wc.radius = radius;
                wc.mass = Mathf.Max(30f, massKg / 60f);
                wc.suspensionDistance = travel;
                wc.forceAppPointDistance = 0.1f;
                // sprężyna dobrana do masy: w spoczynku koło ugina się do połowy skoku
                float load = massKg * 9.81f / 4f;
                var spring = wc.suspensionSpring; spring.spring = load / (travel * 0.5f); spring.damper = 2f * Mathf.Sqrt(spring.spring * massKg / 4f) * 0.35f; spring.targetPosition = 0.5f; wc.suspensionSpring = spring;
                var fwdF = wc.forwardFriction; fwdF.stiffness = 1.4f; wc.forwardFriction = fwdF;
                var sideF = wc.sidewaysFriction; sideF.stiffness = 1.6f; wc.sidewaysFriction = sideF;
                wheels.Add(new WheeledVehicle.Wheel { collider = wc, visual = t, steer = front, drive = !front });
            }
            veh.wheels = wheels.ToArray();
            var camT = new GameObject("CameraTarget").transform; camT.SetParent(root.transform, false); camT.localPosition = new Vector3(0, 2.4f, 0);
            veh.cameraTarget = camT;
            var exit = new GameObject("ExitPoint").transform; exit.SetParent(root.transform, false); exit.localPosition = new Vector3(-2.2f, 0.1f, 0);
            veh.exitPoint = exit;
            return root;
        }

        // ---------- rolnik ----------

        static GameObject BuildFarmer(Vector3 pos, float yaw)
        {
            var body = Load(UBC + "Bodies/Superhero_Male_FullBody.fbx");
            if (!body) return new GameObject("Rolnik (brak modelu)");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(body);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // Model Quaternius patrzy w -Z, a sterowanie zakłada przód w +Z: model siedzi w obiekcie gracza obrócony o 180°.
            var player = new GameObject("Rolnik");
            player.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            root.name = "Model";
            root.transform.SetParent(player.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            var bones = SkinnedPartBinder.MapBones(root.transform);

            // z ciała zostaje sama głowa i szyja; resztę zakrywa strój (ciało „Superhero” prześwitywałoby przez ubranie)
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (smr.sharedMaterials.Any(m => m && m.name.StartsWith("MI_Superhero")))
                    smr.sharedMesh = HeadOnly(smr, "Superhero_Male_Head");

            foreach (var part in new[] { OUT + "Outfits/Male_Peasant.fbx", UBC + "Hair/Hair_SimpleParted.fbx", UBC + "Hair/Hair_Beard.fbx" })
            {
                var src = Load(part);
                if (!src) continue;
                var inst = (GameObject)Object.Instantiate(src);
                int missing = 0;
                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToList())
                    missing += SkinnedPartBinder.Bind(smr, root.transform, bones);
                if (missing > 0) Debug.LogWarning($"[Mleczarka] {Path.GetFileName(part)}: {missing} kości bez odpowiednika w szkielecie ciała.");
                Object.DestroyImmediate(inst);
            }
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                smr.updateWhenOffscreen = false;

            var anim = root.GetComponent<Animator>();
            if (!anim) anim = root.AddComponent<Animator>();
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            anim.runtimeAnimatorController = BuildAnimator();

            var cc = player.AddComponent<CharacterController>();
            var h = WorldBounds(root).size.y;
            cc.height = h; cc.radius = 0.3f; cc.center = new Vector3(0, h * 0.5f + 0.02f, 0); cc.stepOffset = 0.35f; cc.skinWidth = 0.03f;

            var camT = new GameObject("CameraTarget").transform; camT.SetParent(player.transform, false); camT.localPosition = new Vector3(0, h * 0.85f, 0);
            var ctrl = player.AddComponent<FarmerController>();
            ctrl.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            ctrl.cameraTarget = camT;
            Debug.Log($"[Mleczarka] Rolnik: wysokość {h:0.00} m.");
            return player;
        }

        static Mesh HeadOnly(SkinnedMeshRenderer smr, string assetName)
        {
            string path = GEN + "Characters/" + assetName + ".asset";
            var src = smr.sharedMesh;
            var keep = new HashSet<int>();
            for (int i = 0; i < smr.bones.Length; i++)
                if (smr.bones[i] && (smr.bones[i].name == "Head" || smr.bones[i].name == "neck_01")) keep.Add(i);

            var weights = src.boneWeights;
            bool Ok(int v)
            {
                var w = weights[v]; float s = 0;
                if (keep.Contains(w.boneIndex0)) s += w.weight0;
                if (keep.Contains(w.boneIndex1)) s += w.weight1;
                if (keep.Contains(w.boneIndex2)) s += w.weight2;
                if (keep.Contains(w.boneIndex3)) s += w.weight3;
                return s > 0.5f;
            }

            var mesh = Object.Instantiate(src);
            mesh.name = assetName;
            for (int sm = 0; sm < src.subMeshCount; sm++)
            {
                var tris = src.GetTriangles(sm);
                var kept = new List<int>(tris.Length / 3);
                for (int t = 0; t < tris.Length; t += 3)
                    if (Ok(tris[t]) && Ok(tris[t + 1]) && Ok(tris[t + 2])) { kept.Add(tris[t]); kept.Add(tris[t + 1]); kept.Add(tris[t + 2]); }
                mesh.SetTriangles(kept, sm);
            }
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static RuntimeAnimatorController BuildAnimator()
        {
            string path = GEN + "Characters/Rolnik.controller";
            AssetDatabase.DeleteAsset(path);
            var clips = AssetDatabase.LoadAllAssetsAtPath(UAL1).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
            AnimationClip C(string n) { if (clips.TryGetValue(n, out var c)) return c; Debug.LogWarning("[Mleczarka] Brak klipu " + n); return null; }

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;

            var loco = ctrl.CreateBlendTreeInController("Ruch", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(C("Idle_Loop"), 0f);
            tree.AddChild(C("Walk_Loop"), 2.0f);
            tree.AddChild(C("Jog_Fwd_Loop"), 4.2f);
            tree.AddChild(C("Sprint_Loop"), 6.5f);
            sm.defaultState = loco;

            var jump = sm.AddState("Skok"); jump.motion = C("Jump_Start");
            var air = sm.AddState("W powietrzu"); air.motion = C("Jump_Loop");
            var land = sm.AddState("Lądowanie"); land.motion = C("Jump_Land");

            var t1 = loco.AddTransition(jump); t1.AddCondition(AnimatorConditionMode.If, 0, "Jump"); t1.hasExitTime = false; t1.duration = 0.08f;
            var t2 = jump.AddTransition(air); t2.hasExitTime = true; t2.exitTime = 0.9f; t2.duration = 0.1f;
            var t3 = air.AddTransition(land); t3.AddCondition(AnimatorConditionMode.If, 0, "Grounded"); t3.hasExitTime = false; t3.duration = 0.05f;
            var t3b = jump.AddTransition(land); t3b.AddCondition(AnimatorConditionMode.If, 0, "Grounded"); t3b.hasExitTime = true; t3b.exitTime = 0.6f; t3b.duration = 0.05f;
            var t4 = land.AddTransition(loco); t4.hasExitTime = true; t4.exitTime = 0.5f; t4.duration = 0.15f;
            var t5 = loco.AddTransition(air); t5.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); t5.hasExitTime = false; t5.duration = 0.25f;
            return ctrl;
        }

        // ---------- garaż (UI Toolkit) ----------

        static void BuildGarageUI(GameObject farmer)
        {
            const string psPath = "Assets/_Mleczarka/UI/PanelSettings_Gra.asset";
            var ps = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(psPath);
            if (!ps)
            {
                ps = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
                AssetDatabase.CreateAsset(ps, psPath);
            }
            ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.ThemeStyleSheet>("Assets/_Mleczarka/UI/MleczarkaTheme.tss");
            ps.scaleMode = UnityEngine.UIElements.PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.match = 0.5f;
            EditorUtility.SetDirty(ps);
            var go = new GameObject("Garaż (UI)");
            var doc = go.AddComponent<UnityEngine.UIElements.UIDocument>();
            doc.panelSettings = ps;
            var ui = go.AddComponent<Mleczarka.UI.GarageUI>();
            ui.player = farmer.GetComponent<FarmerController>();
        }

        // ---------- kamera ----------

        static void BuildCamera(GameObject farmer)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.AddComponent<CinemachineBrain>();

            var vcamGo = new GameObject("Kamera gracza (Cinemachine)");
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Lens.FieldOfView = 50f;
            var ctrl = farmer.GetComponent<FarmerController>();
            var target = ctrl ? ctrl.cameraTarget : farmer.transform;
            vcam.Follow = target;
            vcam.LookAt = target;
            var orbital = vcamGo.AddComponent<CinemachineOrbitalFollow>();
            orbital.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbital.Radius = 5.5f;
            orbital.HorizontalAxis.Value = farmer.transform.eulerAngles.y;
            orbital.VerticalAxis.Value = 14f;
            vcamGo.AddComponent<CinemachineRotationComposer>();
            if (ctrl) { ctrl.cinemachineCamera = vcam; ctrl.orbital = orbital; }
            camGo.transform.position = target.position - farmer.transform.forward * 5.5f + Vector3.up * 1.5f;
            camGo.transform.LookAt(target);
        }
    }
}
