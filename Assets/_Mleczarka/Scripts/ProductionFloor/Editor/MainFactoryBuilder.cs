using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using Mleczarka.Save;
using Mleczarka.Vehicles;
using Mleczarka.Vehicles.Model;

namespace Mleczarka.ProductionFloor
{
    public static class MainFactoryBuilder
    {
        const string MainScene = "Assets/_Mleczarka/Scenes/Gmina_Podlasie.unity";
        const string Factory = "Assets/_Mleczarka/Art/ThirdParty/Kenney/FactoryKit/Models/FBX format/";
        const string Food = "Assets/_Mleczarka/Art/ThirdParty/Kenney/FoodKit/Models/FBX format/";
        const string Generated = "Assets/_Mleczarka/Art/FactoryInterior/";
        const string Request = "Temp/MleczarkaMainFactory.request";
        static Transform root;
        static Material cream, navy, teal, steel, floor, grout, glass, wood, glow, yellow, machineMat, foodMat;
        static List<Renderer> shell;
        static List<FloorStation> stations;

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(Request);
                try { Integrate(); }
                catch (Exception e) { Directory.CreateDirectory("Screenshots"); File.WriteAllText("Screenshots/mleczarnia_integracja.txt", e.ToString()); Debug.LogException(e); }
            };
            EditorApplication.delayCall += () =>
            {
                const string refine = "Temp/MleczarkaMainFactoryRefine.request";
                if (!File.Exists(refine) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(refine);
                try { Refine(); } catch (Exception e) { Debug.LogException(e); }
            };
        }

        static IEnumerable<T> InScene<T>(Scene scene) where T : Component
        { return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)); }

        [MenuItem("Mleczarka/Produkcja/5. Przenies hale do glownej gry")]
        public static void Integrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Zatrzymaj tryb gry.");
            Scene original = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(MainScene);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Główna scena ma niezapisane zmiany. Integracja przerwana bez nadpisywania.");
            if (scene.GetRootGameObjects().Any(g => g.name == "Mleczarnia — hala produkcyjna"))
                throw new InvalidOperationException("Hala już istnieje. Nie nadpisano jej.");
            string backupFolder = "Backups/MainFactory_2026-11-11";
            Directory.CreateDirectory(backupFolder);
            if (!File.Exists(backupFolder + "/Gmina_Podlasie.unity")) File.Copy(MainScene, backupFolder + "/Gmina_Podlasie.unity");
            var gc = InScene<GameController>(scene).Single();
            string save = SaveSystem.PathFor(gc.saveSlot);
            if (File.Exists(save) && !File.Exists(backupFolder + "/mleczarka_" + gc.saveSlot + ".json"))
                File.Copy(save, backupFolder + "/mleczarka_" + gc.saveSlot + ".json");
            var zone = InScene<WorldPlace>(scene).First(w => w.kind == PlaceKind.Mleczarnia);
            var market = InScene<WorldPlace>(scene).Where(w => w.kind == PlaceKind.Rynek).OrderBy(w => Vector3.Distance(w.transform.position, zone.transform.position)).First();
            var van = InScene<VehicleAssembler>(scene).First(v => v.loadout.type == VehicleType.Dostawczak).GetComponent<WheeledVehicle>();
            var oldHall = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "Hala" && t.parent && t.parent.name == "Mleczarnia");
            SceneManager.SetActiveScene(scene);
            try
            {
                CreateMaterials();
                root = new GameObject("Mleczarnia — hala produkcyjna").transform;
                // Bryła zastępuje wyłącznie dawną makietę hali; drogi, teren i pozostała gmina bez zmian.
                float baseY = float.MinValue;
                var terrains = InScene<Terrain>(scene).ToArray();
                for (int x = -40; x <= -12; x += 4) for (int z = 3; z <= 29; z += 4)
                    baseY = Mathf.Max(baseY, Ground(terrains, x, z));
                baseY += 0.2f;
                root.position = new Vector3(-26, baseY, 16);
                shell = new List<Renderer>(); stations = new List<FloorStation>();
                BuildShell(terrains);
                BuildInterior();
                var visibility = root.gameObject.AddComponent<FactoryShellVisibility>(); visibility.shell = shell.ToArray();
                var system = new GameObject("Produkcja w głównej grze").AddComponent<ProductionFloor>();
                system.stations = stations.ToArray(); system.van = van; system.salePoint = market.transform;
                system.initializeDemoMilk = false; system.showFullHud = false; system.facilityRoot = root;
                system.milkPrefab = ItemPrefab("carton", Food, 0.55f, foodMat, "Karton_UHT");
                system.yogurtPrefab = ItemPrefab("cup", Food, 0.4f, foodMat, "Kubek_Jogurtu");
                system.cratePrefab = ItemPrefab("box-small", Factory, 0.4f, machineMat, "Skrzynka_Transportowa");
                zone.radius = Mathf.Max(zone.radius, 58);
                gc.usePhysicalProduction = true;
                if (oldHall) oldHall.gameObject.SetActive(false);
                Directory.CreateDirectory("Assets/_Mleczarka/Prefabs/Buildings");
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/_Mleczarka/Prefabs/Buildings/Mleczarnia_Produkcja.prefab");
                CheckScene(scene, system, zone);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Nie zapisano głównej sceny.");
                var report = new StringBuilder();
                report.AppendLine("Produkcja w głównej scenie: " + MainScene);
                report.AppendLine("Hala: " + root.position + "; podniesienie fundamentu: " + baseY.ToString("0.00") + " m.");
                report.AppendLine("Stanowiska: " + stations.Count + ". Dostawczak: " + van.name + ". Sprzedaż: " + market.name);
                report.AppendLine("Zapis gry: " + gc.saveSlot + ". Nie dodano mleka, pieniędzy ani produktów. Pracownicy zachowani.");
                report.AppendLine("Kopia sceny i istniejącego zapisu: " + backupFolder);
                report.AppendLine("Logika: " + ProductionChecks.Run());
                Directory.CreateDirectory("Screenshots"); File.WriteAllText("Screenshots/mleczarnia_integracja.txt", report.ToString());
                Capture(scene, false); Capture(scene, true);
                Debug.Log("[Mleczarka] Hala przeniesiona do głównej gry. " + ProductionChecks.Run());
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static float Ground(Terrain[] terrains, float x, float z)
        {
            foreach (var t in terrains)
            {
                var p = t.transform.position; var s = t.terrainData.size;
                if (x >= p.x && x <= p.x + s.x && z >= p.z && z <= p.z + s.z) return t.SampleHeight(new Vector3(x, 0, z)) + p.y;
            }
            throw new InvalidOperationException("Brak terenu pod halą: " + x + ", " + z);
        }

        static void CreateMaterials()
        {
            Directory.CreateDirectory(Generated);
            cream = Mat("Tynk_kremowy", new Color(0.89f, 0.9f, 0.83f));
            navy = Mat("Stal_granatowa", new Color(0.035f, 0.13f, 0.21f), 0.3f, 0.4f);
            teal = Mat("Akcent_mietowy", new Color(0.08f, 0.47f, 0.5f), 0.2f, 0.35f);
            steel = Mat("Stal_nierdzewna", new Color(0.64f, 0.73f, 0.76f), 0.75f, 0.65f);
            floor = Mat("Posadzka_jasna", new Color(0.72f, 0.78f, 0.77f), 0.05f, 0.4f);
            grout = Mat("Fugi", new Color(0.5f, 0.59f, 0.58f));
            glass = Mat("Szklo_niebieskie", new Color(0.24f, 0.48f, 0.6f), 0.4f, 0.9f);
            wood = Mat("Drewno_palet", new Color(0.45f, 0.28f, 0.13f));
            yellow = Mat("Oznaczenia_bhp", new Color(0.98f, 0.7f, 0.15f));
            glow = Mat("Lampy", new Color(0.9f, 0.97f, 1)); glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(0.8f, 0.95f, 1) * 2);
            machineMat = Mat("Kenney_maszyny_URP", Color.white, 0.3f, 0.4f);
            machineMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Factory + "Textures/colormap.png"));
            foodMat = Mat("Kenney_produkty_URP", Color.white, 0, 0.2f);
            foodMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Food + "Textures/colormap.png"));
        }
        static Material Mat(string name, Color color, float metallic = 0, float smoothness = 0.2f)
        {
            string path = Generated + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.color = color; mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Smoothness", smoothness); EditorUtility.SetDirty(mat); return mat;
        }
        static GameObject Box(string name, Vector3 p, Vector3 size, Material material, bool cutaway = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(root, false);
            go.transform.localPosition = p; go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            if (cutaway) shell.Add(renderer); return go;
        }
        static void Label(string value, Vector3 at, float size, Color color, Transform parent = null)
        {
            var go = new GameObject(value); go.transform.SetParent(root, false); go.transform.localPosition = at;
            var tm = go.AddComponent<TextMesh>(); tm.text = value; tm.fontSize = 64; tm.characterSize = size; tm.anchor = TextAnchor.MiddleCenter; tm.color = color;
            if (parent) go.transform.SetParent(parent, true);
        }

        static void BuildShell(Terrain[] terrains)
        {
            Box("Fundament", new Vector3(0, -1.55f, 0), new Vector3(28.8f, 3, 26.8f), navy);
            Box("Posadzka", new Vector3(0, -0.1f, 0), new Vector3(28, 0.2f, 26), floor);
            for (int x = -12; x <= 12; x += 2) Box("Spoina posadzki", new Vector3(x, 0.006f, 0), new Vector3(0.018f, 0.01f, 26), grout);
            for (int z = -12; z <= 12; z += 2) Box("Spoina posadzki", new Vector3(0, 0.006f, z), new Vector3(28, 0.01f, 0.018f), grout);
            Box("Cokół zachodni", new Vector3(-14, 0.5f, 0), new Vector3(0.35f, 1, 26), teal);
            Box("Cokół wschodni", new Vector3(14, 0.5f, 0), new Vector3(0.35f, 1, 26), teal);
            Box("Cokół północny", new Vector3(0, 0.5f, 13), new Vector3(28, 1, 0.35f), teal);
            Box("Ściana zachodnia", new Vector3(-14, 3.2f, 0), new Vector3(0.3f, 4.4f, 26), cream, true);
            Box("Ściana wschodnia", new Vector3(14, 3.2f, 0), new Vector3(0.3f, 4.4f, 26), cream, true);
            Box("Ściana północna", new Vector3(0, 3.2f, 13), new Vector3(28, 4.4f, 0.3f), cream, true);
            Box("Fasada", new Vector3(-4, 2.7f, -13), new Vector3(20, 5.4f, 0.35f), cream, true);
            Box("Fasada magazynu", new Vector3(12, 2.7f, -13), new Vector3(4, 5.4f, 0.35f), cream, true);
            Box("Nadproże wejścia", new Vector3(8, 4.4f, -13), new Vector3(4, 2, 0.4f), navy, true);
            Box("Ościeżnica lewa", new Vector3(5.8f, 1.7f, -13), new Vector3(0.35f, 3.4f, 0.6f), teal);
            Box("Ościeżnica prawa", new Vector3(10.2f, 1.7f, -13), new Vector3(0.35f, 3.4f, 0.6f), teal);
            Box("Daszek wejścia", new Vector3(8, 3.6f, -14), new Vector3(6, 0.15f, 2.5f), navy, true);
            Box("Szyld fasadowy", new Vector3(-3, 4.2f, -13.28f), new Vector3(17, 1.3f, 0.18f), navy, true);
            Label("MLECZARNIA", new Vector3(-3, 4.2f, -13.4f), 0.13f, Color.white);
            for (int x = -11; x <= 1; x += 4)
            {
                Box("Rama okienna", new Vector3(x, 2.25f, -13.24f), new Vector3(3, 1.7f, 0.2f), navy, true);
                Box("Szklenie", new Vector3(x, 2.25f, -13.36f), new Vector3(2.7f, 1.4f, 0.1f), glass, true);
                Box("Szpros", new Vector3(x, 2.25f, -13.44f), new Vector3(0.09f, 1.4f, 0.08f), cream, true);
            }
            for (int z = -10; z <= 10; z += 5)
            {
                foreach (int x in new[] {-14, 14}) Box("Słup konstrukcji", new Vector3(x, 2.75f, z), new Vector3(0.28f, 5.5f, 0.3f), navy);
                Box("Dźwigar dachowy", new Vector3(0, 5.35f, z), new Vector3(28, 0.25f, 0.25f), navy, true);
            }
            foreach (float side in new[] {-1f, 1f})
            {
                var roof = Box("Dach stalowy", new Vector3(side * 7.2f, 6.05f, 0), new Vector3(14.8f, 0.22f, 27.6f), navy, true);
                roof.transform.localRotation = Quaternion.Euler(0, 0, side * -6f);
            }
            Box("Kalenica", new Vector3(0, 6.9f, 0), new Vector3(0.6f, 0.2f, 27.7f), teal, true);
            float groundY = Ground(terrains, root.position.x + 8, root.position.z - 20);
            Vector3 a = new Vector3(8, 0.05f, -13.4f), b = new Vector3(8, groundY - root.position.y + 0.08f, -20);
            Vector3 direction = b - a;
            var ramp = Box("Rampa wejściowa", (a + b) / 2, new Vector3(4.4f, 0.16f, direction.magnitude), floor);
            ramp.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);
            foreach (float x in new[] {5.65f, 10.35f})
            {
                var rail = Box("Poręcz rampy", (a + b) / 2 + new Vector3(x - 8, 1, 0), new Vector3(0.08f, 0.08f, direction.magnitude), steel);
                rail.transform.localRotation = ramp.transform.localRotation;
            }
            for (int z = -8; z <= 8; z += 8) foreach (int x in new[] {-7, 7})
            {
                Box("Oprawa LED", new Vector3(x, 4.95f, z), new Vector3(4, 0.1f, 0.3f), glow);
                var light = new GameObject("Światło hali").AddComponent<Light>(); light.transform.SetParent(root, false);
                light.transform.localPosition = new Vector3(x, 4.3f, z); light.type = LightType.Point; light.range = 10; light.intensity = 2.2f;
                light.color = new Color(0.87f, 0.95f, 1); light.shadows = LightShadows.None;
            }
        }

        static void BuildInterior()
        {
            Station("PRZYJĘCIE / START PARTII", FloorAction.Intake, new Vector3(-9, 0, -7));
            Model("Assets/_Mleczarka/Art/World/Silos_Mleka.fbx", new Vector3(-10, 0, -2.5f), 3.7f, steel);
            Model(Factory + "screen-panel-small.fbx", new Vector3(-9, 0.9f, -4.1f), 0.8f, machineMat);
            Station("LINIA UHT", FloorAction.Uht, new Vector3(-2, 0, -7));
            Model(Factory + "machine-window.fbx", new Vector3(-3, 0, -3), 2.5f, machineMat);
            Model(Factory + "machine-connection-pipe.fbx", new Vector3(-0.5f, 0, -3), 2.5f, machineMat);
            var pack = Station("PAKOWANIE MLEKA UHT", FloorAction.PackMilk, new Vector3(5, 0, -7));
            Model(Factory + "machine.fbx", new Vector3(4, 0, -3), 2.6f, machineMat);
            Model(Factory + "conveyor-long-sides.fbx", new Vector3(7, 0, -3), 1.1f, machineMat, 90);
            pack.stockAnchor = Anchor("Odbiór UHT", new Vector3(8.5f, 1.2f, -3));
            Station("PASTERYZACJA JOGURTU", FloorAction.Pasteurize, new Vector3(-9, 0, 5));
            Model(Factory + "machine-fortified.fbx", new Vector3(-10, 0, 9), 2.7f, machineMat);
            Station("FERMENTACJA", FloorAction.Ferment, new Vector3(-3, 0, 5));
            Model("Assets/_Mleczarka/Art/World/Silos_Mleka.fbx", new Vector3(-4, 0, 9.5f), 3.3f, steel);
            Model("Assets/_Mleczarka/Art/World/Silos_Mleka.fbx", new Vector3(-1.5f, 0, 9.5f), 3.3f, steel);
            var yogurt = Station("PAKOWANIE JOGURTU", FloorAction.PackYogurt, new Vector3(4, 0, 5));
            Model(Factory + "machine-window-bar.fbx", new Vector3(4, 0, 9), 2.4f, machineMat);
            Model(Factory + "conveyor-long-sides.fbx", new Vector3(7, 0, 9), 1.1f, machineMat, 90);
            yogurt.stockAnchor = Anchor("Odbiór jogurtu", new Vector3(8, 1.2f, 9));
            var store = Station("MAGAZYN WYROBÓW GOTOWYCH", FloorAction.Warehouse, new Vector3(10, 0, 1));
            Shelf(new Vector3(11, 0, 5)); Shelf(new Vector3(11, 0, 10));
            store.stockAnchor = Anchor("UHT na regale", new Vector3(11, 0.7f, 5));
            store.secondStockAnchor = Anchor("Jogurt na regale", new Vector3(11, 0.7f, 10));
            Station("WYSYŁKA / WYJŚCIE NA PLAC", FloorAction.Dispatch, new Vector3(8, 0, -11));
            Pipe(new Vector3(-10, 3.2f, -2.5f), new Vector3(-2, 3.2f, -2.5f));
            Pipe(new Vector3(-2, 3.2f, -2.5f), new Vector3(4, 3.2f, -2.5f));
            Pipe(new Vector3(-10, 3, 9), new Vector3(-3, 3, 9));
            Pipe(new Vector3(-3, 3, 9), new Vector3(4, 3, 9));
            // Czytelny korytarz pomiędzy liniami: oznaczenia nie są interfejsem unoszącym się nad sceną.
            for (int x = -11; x <= 9; x += 2) Box("Pas komunikacyjny", new Vector3(x, 0.015f, -0.8f), new Vector3(1.3f, 0.02f, 0.1f), yellow);
            Box("Strefa magazynu", new Vector3(10.5f, 0.012f, 7), new Vector3(6, 0.02f, 11), cream);
        }
        static FloorStation Station(string name, FloorAction action, Vector3 at)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false); go.transform.localPosition = at;
            var station = go.AddComponent<FloorStation>(); station.action = action; station.title = name;
            foreach (float side in new[] {-1f, 1f}) Box("Znacznik stanowiska", at + new Vector3(side * 1.05f, 0.02f, 0), new Vector3(0.07f, 0.025f, 1.6f), teal);
            Box("Znacznik stanowiska", at + new Vector3(0, 0.02f, -0.8f), new Vector3(2.1f, 0.025f, 0.07f), teal);
            Label(name, at + new Vector3(0, 2.4f, 0), 0.065f, new Color(0.04f, 0.2f, 0.24f), go.transform);
            stations.Add(station); return station;
        }
        static Transform Anchor(string name, Vector3 at)
        { var go = new GameObject(name); go.transform.SetParent(root, false); go.transform.localPosition = at; return go.transform; }
        static void Pipe(Vector3 a, Vector3 b)
        {
            var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pipe.name = "Rurociąg mleka"; pipe.transform.SetParent(root, false);
            pipe.transform.localPosition = (a + b) / 2; pipe.transform.localScale = new Vector3(0.16f, Vector3.Distance(a, b) / 2, 0.16f);
            pipe.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a); pipe.GetComponent<Renderer>().sharedMaterial = steel;
        }
        static void Shelf(Vector3 at)
        {
            foreach (float x in new[] {-1.5f, 1.5f}) Box("Słup regału", at + new Vector3(x, 1.8f, 0), new Vector3(0.12f, 3.6f, 1.6f), navy);
            foreach (float y in new[] {0.3f, 1.45f, 2.6f})
            {
                Box("Belka regału", at + new Vector3(0, y, 0), new Vector3(3.2f, 0.15f, 1.6f), teal);
                for (int i = 0; i < 6; i++) Box("Deska palety", at + new Vector3(0, y + 0.13f, i * 0.23f - 0.58f), new Vector3(2.9f, 0.12f, 0.18f), wood);
            }
        }
        static GameObject Model(string path, Vector3 at, float height, Material material, float yaw = 0)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset) throw new FileNotFoundException("Brak modelu: " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset); go.transform.SetParent(root, false);
            Normalize(go, height); go.transform.localRotation = Quaternion.Euler(0, yaw, 0); go.transform.localPosition += at;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(m => material).ToArray();
            var bounds = Bounds(go);
            var collider = go.AddComponent<BoxCollider>(); collider.center = go.transform.InverseTransformPoint(bounds.center);
            var local = new Bounds(go.transform.InverseTransformPoint(bounds.min), Vector3.zero);
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
                local.Encapsulate(go.transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x == 0 ? -1 : 1, y == 0 ? -1 : 1, z == 0 ? -1 : 1))));
            collider.center = local.center; collider.size = local.size;
            return go;
        }
        static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Model bez geometrii: " + go.name);
            var b = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) b.Encapsulate(renderer.bounds); return b;
        }
        static void Normalize(GameObject go, float height)
        {
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            var b = Bounds(go); go.transform.localScale *= height / Mathf.Max(0.001f, b.size.y);
            b = Bounds(go); go.transform.position += new Vector3(-b.center.x + root.position.x, -b.min.y + root.position.y, -b.center.z + root.position.z);
        }
        static GameObject ItemPrefab(string name, string path, float height, Material material, string outputName)
        {
            string folder = "Assets/_Mleczarka/Prefabs/Production"; Directory.CreateDirectory(folder);
            var holder = new GameObject(outputName); holder.transform.SetParent(root, false);
            var child = Model(path + name + ".fbx", Vector3.zero, height, material);
            child.transform.SetParent(holder.transform, true);
            foreach (var collider in child.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            var prefab = PrefabUtility.SaveAsPrefabAsset(holder, folder + "/" + outputName + ".prefab");
            UnityEngine.Object.DestroyImmediate(holder); return prefab;
        }
        static void CheckScene(Scene scene, ProductionFloor system, WorldPlace zone)
        {
            if (system.stations.Length != 8 || !system.van || !system.salePoint || system.initializeDemoMilk || !system.milkPrefab || !system.cratePrefab)
                throw new InvalidOperationException("Niepełna konfiguracja produkcji.");
            foreach (var station in system.stations)
                if (!zone.Contains(station.transform.position)) throw new InvalidOperationException("Stanowisko poza mleczarnią: " + station.name);
            if (!InScene<GameController>(scene).Single().usePhysicalProduction) throw new InvalidOperationException("Nie włączono fizycznej produkcji.");
        }
        static void Capture(Scene scene, bool interior)
        {
            var camera = new GameObject("Kamera kontroli hali").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.transform.position = root.position + (interior ? new Vector3(12, 8, -11) : new Vector3(33, 19, -33));
            camera.transform.LookAt(root.position + (interior ? new Vector3(-2, 1.2f, 2) : new Vector3(0, 2, 0)));
            camera.fieldOfView = 65; camera.farClipPlane = 6000;
            // Ten sam postprocessing co w głównej kamerze: poprawna ekspozycja i kolory.
            var cameraDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (cameraDataType != null)
            {
                var cameraData = camera.gameObject.AddComponent(cameraDataType);
                cameraDataType.GetProperty("renderPostProcessing").SetValue(cameraData, true);
            }
            var previous = RenderTexture.active;
            var rt = new RenderTexture(1280, 720, 24); camera.targetTexture = rt;
            var saved = shell.Select(r => r.enabled).ToArray();
            if (interior) foreach (var r in shell) r.enabled = false;
            try
            {
                camera.Render(); RenderTexture.active = rt;
                var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                File.WriteAllBytes("Screenshots/Mleczarnia_Main_" + (interior ? "Interior" : "Exterior") + ".png", texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally
            {
                for (int i = 0; i < shell.Count; i++) shell[i].enabled = saved[i];
                RenderTexture.active = previous; camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(rt);
            }
        }
        [MenuItem("Mleczarka/Produkcja/6. Otworz glowna gre")]
        public static void OpenMain()
        { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(MainScene); }

        [MenuItem("Mleczarka/Produkcja/7. Kontrola wygladu glownej hali")]
        public static void Refine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Zatrzymaj grę.");
            Scene original = SceneManager.GetActiveScene(); var scene = SceneManager.GetSceneByPath(MainScene);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Główna scena ma niezapisane zmiany.");
            SceneManager.SetActiveScene(scene);
            try
            {
                root = scene.GetRootGameObjects().Single(g => g.name == "Mleczarnia — hala produkcyjna").transform;
                shell = root.GetComponent<FactoryShellVisibility>().shell.ToList(); CreateMaterials();
                foreach (var station in root.GetComponentsInChildren<FloorStation>())
                    foreach (var text in station.GetComponentsInChildren<TextMesh>()) text.GetComponent<Renderer>().enabled = false;
                if (!root.Find("Rąbek pokrycia"))
                {
                    for (float x = -13.2f; x <= 13.2f; x += 1.2f)
                        Box("Rąbek pokrycia", new Vector3(x, 6.86f - Mathf.Abs(x) * 0.105f, 0), new Vector3(0.05f, 0.055f, 27.6f), teal, true);
                }
                root.GetComponent<FactoryShellVisibility>().shell = shell.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/_Mleczarka/Prefabs/Buildings/Mleczarnia_Produkcja.prefab");
                EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); Capture(scene, false); Capture(scene, true);
                File.WriteAllText("Screenshots/mleczarnia_kontrola.txt", ProductionChecks.Run() + "\nMateriały URP, dach, oznaczenia i zdjęcia kontrolne: OK.");
            }
            finally
            { if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original); if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
