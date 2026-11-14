using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using Mleczarka.Player;
using Mleczarka.Vehicles;
using Mleczarka.Vehicles.Model;
using Mleczarka.Save;

namespace Mleczarka.ProductionFloor
{
    public static class ProductionBuilder
    {
        public const string ScenePath = "Assets/_Mleczarka/Scenes/Produkcja_Prototyp.unity";
        const string Request = "Temp/MleczarkaProductionBuild.request";
        static Material floor, wall, steel, blue, white, wood, grass, black;

        [InitializeOnLoadMethod]
        static void Init()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(Request);
                try { Build(); }
                catch (Exception e) { Directory.CreateDirectory("Screenshots"); File.WriteAllText("Screenshots/produkcja_budowa.txt", e.ToString()); Debug.LogException(e); }
            };
        }

        [MenuItem("Mleczarka/Produkcja/1. Przygotuj hale testowa")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Najpierw zatrzymaj grę.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Hala testowa już istnieje — nie nadpisano sceny.");
            string tests = ProductionChecks.Run();
            Scene original = SceneManager.GetActiveScene();
            const string sourcePath = "Assets/_Mleczarka/Scenes/Gmina_Podlasie.unity";
            Scene source = SceneManager.GetSceneByPath(sourcePath);
            bool opened = !source.IsValid() || !source.isLoaded;
            if (opened) source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
            if (source.isDirty) { if (opened) EditorSceneManager.CloseScene(source, true); throw new InvalidOperationException("Scena źródłowa ma niezapisane zmiany. Nie kopiowano ani nie zapisywano ich."); }
            if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new IOException("Nie utworzono kopii sceny.");
            if (opened) EditorSceneManager.CloseScene(source, true);
            Scene demo = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(demo);
            try
            {
                var roots = demo.GetRootGameObjects();
                foreach (var root in roots)
                {
                    bool keep = root.GetComponentInChildren<FarmerController>(true) || root.GetComponentInChildren<Camera>(true)
                        || root.GetComponentInChildren<CinemachineCamera>(true) || root.GetComponentInChildren<GameController>(true)
                        || root.GetComponentInChildren<Light>(true);
                    var vehicle = root.GetComponentInChildren<VehicleAssembler>(true);
                    if (vehicle) keep = vehicle.loadout.type == VehicleType.Dostawczak;
                    if (!keep) UnityEngine.Object.DestroyImmediate(root);
                }
                var gc = demo.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameController>(true)).First();
                gc.saveSlot = "produkcja_prototyp1"; gc.newGameMode = GameMode.Swobodna;
                var player = gc.player;
                player.transform.SetPositionAndRotation(new Vector3(-11, 0.3f, -2), Quaternion.identity);
                var vans = demo.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<VehicleAssembler>(true)).ToArray();
                if (vans.Length == 0) throw new InvalidOperationException("Nie znaleziono dostawczaka.");
                var van = vans[0].GetComponent<WheeledVehicle>();
                van.transform.SetPositionAndRotation(new Vector3(8, 1.2f, -24), Quaternion.Euler(0, 180, 0));
                for (int i = 1; i < vans.Length; i++) UnityEngine.Object.DestroyImmediate(vans[i].gameObject);
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("Brak URP Lit.");
                floor = Mat("Posadzka", new Color(0.55f, 0.61f, 0.65f), shader);
                wall = Mat("Sciany", new Color(0.91f, 0.94f, 0.93f), shader);
                steel = Mat("Stal", new Color(0.6f, 0.68f, 0.73f), shader); steel.SetFloat("_Metallic", 0.65f); steel.SetFloat("_Smoothness", 0.7f);
                blue = Mat("Niebieski", new Color(0.08f, 0.36f, 0.66f), shader);
                white = Mat("Opakowania", Color.white, shader);
                wood = Mat("Palety", new Color(0.52f, 0.34f, 0.16f), shader);
                grass = Mat("Trawnik", new Color(0.2f, 0.38f, 0.18f), shader);
                black = Mat("Droga", new Color(0.16f, 0.18f, 0.19f), shader);
                Box("Teren zakładu", new Vector3(0, -0.55f, -70), new Vector3(150, 1, 230), grass);
                Box("Posadzka hali", new Vector3(0, -0.1f, 0), new Vector3(46, 0.2f, 32), floor);
                Box("Droga do sklepu", new Vector3(8, -0.01f, -88), new Vector3(12, 0.12f, 148), black);
                Box("Plac załadunku", new Vector3(8, -0.01f, -22), new Vector3(30, 0.12f, 20), floor);
                Box("Ściana tylna", new Vector3(0, 2.5f, 16), new Vector3(46, 5, 0.4f), wall);
                Box("Ściana lewa", new Vector3(-23, 2.5f, 0), new Vector3(0.4f, 5, 32), wall);
                Box("Ściana prawa", new Vector3(23, 2.5f, 0), new Vector3(0.4f, 5, 32), wall);
                foreach (float x in new[] {-22f, 0, 22}) Box("Belka konstrukcyjna", new Vector3(x, 4.8f, 0), new Vector3(0.3f, 0.3f, 32), blue);
                Label("MLECZARKA · HALA PRODUKCYJNA", new Vector3(0, 3.8f, 15.7f), 0.15f);
                var stations = new List<FloorStation>();
                stations.Add(Station("1. ZBIORNIK · WYBÓR PRODUKTU", FloorAction.Intake, new Vector3(-13, 0, 0)));
                Tank(new Vector3(-16, 0, 2), 2.4f, 4.5f);
                stations.Add(Station("2. LINIA UHT", FloorAction.Uht, new Vector3(-5, 0, 1)));
                Machine(new Vector3(-5, 0, 4), 4, 2.4f);
                stations.Add(Station("PASTERYZACJA JOGURTU", FloorAction.Pasteurize, new Vector3(-13, 0, 9)));
                Machine(new Vector3(-16, 0, 10), 3, 2);
                stations.Add(Station("FERMENTACJA", FloorAction.Ferment, new Vector3(-5, 0, 10)));
                Tank(new Vector3(-5, 0, 13), 1.6f, 2.5f);
                stations.Add(Station("3. PAKOWANIE UHT · ODBIÓR SKRZYNEK", FloorAction.PackMilk, new Vector3(3, 0, 1)));
                Machine(new Vector3(3, 0, 4), 3, 2);
                Conveyor(new Vector3(7, 0, 4));
                stations.Add(Station("PAKOWANIE JOGURTU", FloorAction.PackYogurt, new Vector3(3, 0, 10)));
                Machine(new Vector3(3, 0, 13), 3, 2);
                stations.Add(Station("4. MAGAZYN · ODŁÓŻ / PODEJMIJ", FloorAction.Warehouse, new Vector3(15, 0, 0)));
                Shelf(new Vector3(18, 0, 3)); Shelf(new Vector3(18, 0, 8));
                stations.Add(Station("5. WYSYŁKA · DOSTAWCZAK NA PLACU", FloorAction.Dispatch, new Vector3(8, 0, -12)));
                var dairy = new GameObject("Strefa mleczarni").AddComponent<WorldPlace>();
                dairy.kind = PlaceKind.Mleczarnia; dairy.radius = 45;
                var sale = new GameObject("Sklep — punkt sprzedaży").AddComponent<WorldPlace>();
                sale.kind = PlaceKind.Rynek; sale.radius = 15; sale.transform.position = new Vector3(8, 0, -145);
                Box("Sklep", new Vector3(23, 2.5f, -145), new Vector3(14, 5, 11), wall);
                Box("Szyld sklepu", new Vector3(23, 4.1f, -139.3f), new Vector3(12, 1.3f, 0.2f), blue);
                Label("SKLEP · SPRZEDAŻ", new Vector3(23, 4.1f, -139), 0.12f);
                Box("Plac sklepu", new Vector3(8, 0, -145), new Vector3(22, 0.12f, 24), floor);
                var controller = new GameObject("Praca w hali").AddComponent<ProductionFloor>();
                controller.stations = stations.ToArray(); controller.van = van; controller.salePoint = sale.transform;
                EditorSceneManager.SaveScene(demo);
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory("Screenshots");
                File.WriteAllText("Screenshots/produkcja_budowa.txt", "Scena: " + ScenePath + "\nTesty logiki: " + tests + "\nOsobny zapis: produkcja_prototyp1. Mapa źródłowa bez zmian.\n");
                Debug.Log("[Mleczarka] Hala produkcyjna przygotowana. " + tests);
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(demo, true);
            }
        }

        static Material Mat(string name, Color color, Shader shader)
        {
            const string path = "Assets/_Mleczarka/Art/Materials/ProductionPrototype";
            Directory.CreateDirectory(path);
            string file = path + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(file);
            if (!mat) { mat = new Material(shader) { color = color }; AssetDatabase.CreateAsset(mat, file); }
            return mat;
        }
        static GameObject Box(string name, Vector3 at, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.position = at; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = mat; return go;
        }
        static void Tank(Vector3 at, float radius, float height)
        {
            var tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder); tank.name = "Zbiornik nierdzewny";
            tank.transform.position = at + Vector3.up * height / 2; tank.transform.localScale = new Vector3(radius, height / 2, radius);
            tank.GetComponent<Renderer>().sharedMaterial = steel;
            Box("Sterownik zbiornika", at + new Vector3(0, 1.3f, -radius / 2), new Vector3(0.8f, 0.7f, 0.3f), blue);
        }
        static void Machine(Vector3 at, float width, float height)
        {
            Box("Maszyna produkcyjna", at + Vector3.up * height / 2, new Vector3(width, height, 2), steel);
            Box("Panel obsługi", at + new Vector3(0, 1.3f, -1.1f), new Vector3(1.2f, 0.8f, 0.3f), blue);
            Box("Wyświetlacz", at + new Vector3(0, 1.4f, -1.27f), new Vector3(0.7f, 0.35f, 0.05f), black);
        }
        static void Conveyor(Vector3 at)
        {
            Box("Przenośnik", at + Vector3.up * 0.9f, new Vector3(5, 0.3f, 1.3f), blue);
            for (int i = 0; i < 5; i++) Box("Karton mleka", at + new Vector3(i * 0.7f - 1.4f, 1.4f, 0), new Vector3(0.4f, 0.7f, 0.4f), white);
        }
        static void Shelf(Vector3 at)
        {
            foreach (float x in new[] {-2f, 2f}) Box("Słup regału", at + new Vector3(x, 1.7f, 0), new Vector3(0.16f, 3.4f, 2), blue);
            foreach (float y in new[] {0.2f, 1.5f, 2.8f})
            {
                Box("Półka", at + new Vector3(0, y, 0), new Vector3(4.2f, 0.15f, 2), steel);
                Box("Paleta", at + new Vector3(0, y + 0.18f, 0), new Vector3(3.6f, 0.2f, 1.7f), wood);
            }
        }
        static FloorStation Station(string name, FloorAction action, Vector3 position)
        {
            var go = new GameObject(name); go.transform.position = position;
            var station = go.AddComponent<FloorStation>(); station.action = action; station.title = name;
            Box("Pole pracy", position + Vector3.up * 0.02f, new Vector3(2.5f, 0.04f, 2.5f), blue);
            Label(name, position + new Vector3(0, 2.7f, 0), 0.08f);
            return station;
        }
        static void Label(string value, Vector3 position, float size)
        {
            var go = new GameObject(value); go.transform.position = position;
            var text = go.AddComponent<TextMesh>(); text.text = value; text.anchor = TextAnchor.MiddleCenter;
            text.fontSize = 48; text.characterSize = size; text.color = new Color(0.05f, 0.13f, 0.2f);
        }

        [MenuItem("Mleczarka/Produkcja/2. Otworz hale testowa")]
        public static void Open()
        { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath); }

        [MenuItem("Mleczarka/Produkcja/3. Testy logistyki i zapisu")]
        public static void Test()
        {
            string checks = ProductionChecks.Run();
            var state = Simulation.NewGame(GameMode.Swobodna);
            state.players[0].manualHandling = true; state.players[0].packingRack[0] = 3; state.players[0].carried[1] = 2;
            state.players[0].carriedFromStore = true;
            var restored = SaveSystem.FromJson(SaveSystem.ToJson(state)); Simulation.Migrate(restored);
            if (restored.players[0].packingRack[0] != 3 || restored.players[0].carried[1] != 2 || !restored.players[0].carriedFromStore)
                throw new Exception("Niepoprawny zapis towaru w hali.");
            Directory.CreateDirectory("Screenshots");
            File.WriteAllText("Screenshots/produkcja_testy.txt", checks + "\nJSON round-trip regału pakowania i niesionej skrzynki: OK.");
            Debug.Log("[Mleczarka] Produkcja: " + checks + " Zapis JSON: OK.");
        }
    }
}
