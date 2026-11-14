using System;
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
using UnityEngine.SceneManagement;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>
    /// Skrzydło maszyn dobudowane od zachodu do hali Codexa (24 × 26 m, ta sama posadzka).
    /// Stawia 17 własnych modeli z Blendera (Art/Machines), rurociągi między maszynami, taśmę zbiorczą,
    /// paletę odbioru, chłodnię, oświetlenie i przejście w zachodniej ścianie hali.
    /// Nie zmienia terenu, dróg ani reszty gminy. Przed zmianą kopiuje scenę do Backups/.
    /// </summary>
    public static class PlantWingBuilder
    {
        const string MainScene = "Assets/_Mleczarka/Scenes/Gmina_Podlasie.unity";
        const string Machines = "Assets/_Mleczarka/Art/Machines/";
        const string Gen = "Assets/_Mleczarka/Art/FactoryInterior/";
        const string WingName = "Mleczarnia — skrzydło maszyn";
        const string FloorName = "Produkcja — skrzydło maszyn";
        const string HallName = "Mleczarnia — hala produkcyjna";
        const string Tag = "Skrzydło: ";
        const float HalfX = 12, HalfZ = 13, DoorZ0 = -4.5f, DoorZ1 = -0.5f, AisleZ = -2.5f;

        static Transform root;
        static List<Renderer> shell;
        static Material cream, navy, teal, steel, floor, grout, glass, glow, rubber, yellow, wood;
        static StringBuilder log;

        class Place { public string model, id; public PlantMachineView.Kind kind; public float x, z; public Vector3 face; public string title; }

        // układ w lokalnych współrzędnych skrzydła: +X na wschód (do hali), +Z na północ; „face” = kierunek, w którym stoi operator
        static readonly Place[] Layout =
        {
            // rząd północny — mleko płynne
            new Place { model = "Przyjecie", kind = PlantMachineView.Kind.Intake, title = "Przyjęcie mleka", x = -10.6f, z = 2.5f, face = Vector3.back },
            new Place { model = "Wirowka", id = "wirowka", x = -8.4f, z = 2.5f, face = Vector3.back },
            new Place { model = "Homogenizator", id = "homogenizator", x = -6.0f, z = 2.5f, face = Vector3.back },
            new Place { model = "Pasteryzator", id = "pasteryzator", x = -3.1f, z = 2.5f, face = Vector3.back },
            new Place { model = "UHT", id = "uht", x = 0.1f, z = 2.5f, face = Vector3.back },
            new Place { model = "Fermentator", id = "fermentator1", x = 3.1f, z = 2.5f, face = Vector3.back },
            new Place { model = "Fermentator", id = "fermentator2", x = 5.5f, z = 2.5f, face = Vector3.back },
            // zaplecze rzędu północnego
            new Place { model = "ZbiornikSmietanki", kind = PlantMachineView.Kind.CreamTank, title = "Zbiornik śmietanki", x = -8.4f, z = 6.5f, face = Vector3.back },
            new Place { model = "CIP", kind = PlantMachineView.Kind.Cip, title = "Stacja mycia CIP", x = -4.5f, z = 6.8f, face = Vector3.back },
            new Place { model = "Chlodnia", kind = PlantMachineView.Kind.ColdRoom, title = "Chłodnia", x = 9.3f, z = 8.0f, face = Vector3.back },
            new Place { model = "Laboratorium", kind = PlantMachineView.Kind.Lab, title = "Laboratorium", x = -11.2f, z = AisleZ, face = Vector3.right },
            // rząd południowy — masło, sery, rozlew, pakowanie
            new Place { model = "Maslnica", id = "maslnica", x = -9.3f, z = -6.5f, face = Vector3.forward },
            new Place { model = "Wanna", id = "wanna", x = -6.0f, z = -6.5f, face = Vector3.forward },
            new Place { model = "Prasa", id = "prasa", x = -2.9f, z = -6.5f, face = Vector3.forward },
            new Place { model = "Dojrzewalnia", id = "dojrzewalnia", x = 0.2f, z = -6.5f, face = Vector3.forward },
            new Place { model = "RozlewKubki", id = "rozlewKubki", x = 3.2f, z = -6.5f, face = Vector3.forward },
            new Place { model = "RozlewKarton", id = "rozlewKarton", x = 6.4f, z = -6.5f, face = Vector3.forward },
            new Place { model = "Pakowarka", id = "pakowarka", x = 9.2f, z = -6.5f, face = Vector3.forward },
        };

        static readonly (string from, string to, bool curd)[] Pipes =
        {
            ("silos", "wirowka", false), ("wirowka", "homogenizator", false), ("wirowka", "smietanka", false), ("wirowka", "pasteryzator", false),
            ("homogenizator", "uht", false), ("homogenizator", "pasteryzator", false), ("uht", "rozlewKarton", false), ("pasteryzator", "rozlewKarton", false),
            ("pasteryzator", "fermentator1", false), ("pasteryzator", "fermentator2", false), ("fermentator1", "rozlewKubki", false), ("fermentator2", "rozlewKubki", false),
            ("smietanka", "pasteryzator", false), ("pasteryzator", "maslnica", false), ("pasteryzator", "wanna", false), ("wanna", "prasa", true),
        };

        // ---------------- menu ----------------

        [MenuItem("Mleczarka/Produkcja/9. Sprawdz miejsce na skrzydlo maszyn")]
        public static void Check() => WithScene(false, scene =>
        {
            var hall = Hall(scene);
            var terrains = InScene<Terrain>(scene).ToArray();
            var c = hall.TransformPoint(new Vector3(-26, 0, 0));
            float min = float.MaxValue, max = float.MinValue;
            for (float x = -HalfX; x <= HalfX; x += 1) for (float z = -HalfZ; z <= HalfZ; z += 1)
            { float g = Ground(terrains, c + hall.rotation * new Vector3(x, 0, z)); if (float.IsNaN(g)) continue; min = Mathf.Min(min, g); max = Mathf.Max(max, g); }
            log.AppendLine($"Posadzka hali: {hall.position.y:0.00} m. Teren pod skrzydłem: {min:0.00}…{max:0.00} m.");
            if (max > hall.position.y - 0.05f) log.AppendLine("UWAGA: teren wystaje ponad posadzkę — trzeba by obniżyć teren lub podnieść skrzydło.");
            var bounds = new Bounds(c + Vector3.up * 3, new Vector3(HalfX * 2 + 0.4f, 8, HalfZ * 2 + 0.4f));
            foreach (var r in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>()))
            {
                if (r.transform.IsChildOf(hall) || r.GetComponentInParent<Terrain>()) continue;
                if (r.bounds.Intersects(bounds)) log.AppendLine($"Obiekt w obrysie: {PathOf(r.transform)} ({r.bounds.center:0.0})");
            }
            foreach (var t in terrains)
                foreach (var ti in t.terrainData.treeInstances)
                {
                    var p = Vector3.Scale(ti.position, t.terrainData.size) + t.transform.position;
                    if (bounds.Contains(new Vector3(p.x, bounds.center.y, p.z))) log.AppendLine($"Drzewo terenu w obrysie: {p:0.0}");
                }
            Write("skrzydlo_miejsce.txt");
        });

        [MenuItem("Mleczarka/Produkcja/10. Dobuduj skrzydlo maszyn")]
        public static void Build() => WithScene(true, scene => DoBuild(scene, false));

        [MenuItem("Mleczarka/Produkcja/11. Przebuduj skrzydlo maszyn (tylko wygenerowane obiekty)")]
        public static void Rebuild() => WithScene(true, scene => DoBuild(scene, true));

        // ---------------- budowa ----------------

        static void DoBuild(Scene scene, bool rebuild)
        {
            var hall = Hall(scene);
            var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == WingName);
            if (existing && !rebuild) throw new InvalidOperationException("Skrzydło już istnieje. Użyj „Przebuduj skrzydło”.");
            string backup = "Backups/PlantWing_" + DateTime.Now.ToString("yyyy-MM-dd_HHmm");
            Directory.CreateDirectory(backup);
            File.Copy(MainScene, backup + "/Gmina_Podlasie.unity", true);
            log.AppendLine("Kopia sceny: " + backup);
            if (existing)
            {
                UnityEngine.Object.DestroyImmediate(existing);
                foreach (var g in scene.GetRootGameObjects().Where(g => g.name == FloorName)) UnityEngine.Object.DestroyImmediate(g);
                foreach (var t in hall.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(Tag)).ToList()) if (t) UnityEngine.Object.DestroyImmediate(t.gameObject);
            }

            var terrains = InScene<Terrain>(scene).ToArray();
            CreateMaterials();
            root = new GameObject(WingName).transform;
            root.SetPositionAndRotation(hall.TransformPoint(new Vector3(-26, 0, 0)), hall.rotation);
            shell = new List<Renderer>();

            float minGround = float.MaxValue;
            for (float x = -HalfX; x <= HalfX; x += 1) for (float z = -HalfZ; z <= HalfZ; z += 1)
            { float g = Ground(terrains, root.TransformPoint(new Vector3(x, 0, z))); if (!float.IsNaN(g)) minGround = Mathf.Min(minGround, g); }
            if (minGround == float.MaxValue) minGround = root.position.y - 3;
            float depth = Mathf.Max(3, root.position.y - minGround + 0.6f);
            BuildShell(depth);
            OpenHallWall(hall);

            var views = new List<PlantMachineView>();
            foreach (var p in Layout) views.Add(PlaceMachine(p));
            var silos = PlaceSilos();
            var pipes = BuildPipes(views, silos);
            Transform beltA, beltB; BuildBelt(out beltA, out beltB);
            var pallet = Pallet(new Vector3(11.1f, 0, -6.5f));
            var cold = views.First(v => v.kind == PlantMachineView.Kind.ColdRoom);
            var coldInside = cold.interior ? cold.interior : cold.transform;

            // działy produkcyjne, tabliczki maszyn, szyldy
            var doors = WingDepartments.Build(root, shell, cream, navy, teal, glass, floor, steel);
            for (int i = 0; i < Layout.Length; i++)
            {
                var p = Layout[i]; var v = views[i];
                string sign = "Maszyna_" + (p.id ?? (p.kind == PlantMachineView.Kind.Intake ? "przyjecie" : p.kind == PlantMachineView.Kind.Lab ? "lab" : p.kind == PlantMachineView.Kind.Cip ? "cip" : p.kind == PlantMachineView.Kind.CreamTank ? "smietanka" : "chlodnia"));
                var b = Bounds(v.gameObject);
                var ext = root.InverseTransformVector(b.extents); float half = Mathf.Abs(Vector3.Dot(new Vector3(Mathf.Abs(ext.x), 0, Mathf.Abs(ext.z)), new Vector3(Mathf.Abs(p.face.x), 0, Mathf.Abs(p.face.z))));
                WingDepartments.MachinePlate(root, sign, new Vector3(p.x, 0, p.z), p.face, half + 0.25f);
            }
            WingDepartments.MachinePlate(root, "Maszyna_silos", new Vector3(-10.2f, 0, 9.6f), Vector3.back, 1.6f);
            WingDepartments.MachinePlate(root, "Maszyna_paleta", new Vector3(11.1f, 0, -6.5f), Vector3.forward, 0.7f, 2.4f);
            WingDepartments.WingFacade(root, HalfZ);
            WingDepartments.OldHall(hall, log);
            var vis = root.gameObject.AddComponent<FactoryShellVisibility>(); vis.shell = shell.ToArray(); vis.localSize = new Vector3(HalfX * 2, 10, HalfZ * 2);

            var oldFloor = InScene<ProductionFloor>(scene).FirstOrDefault();
            var go = new GameObject(FloorName); SceneManager.MoveGameObjectToScene(go, scene);
            var pf = go.AddComponent<PlantFloor>();
            pf.wingRoot = root; pf.views = views.ToArray(); pf.pipes = pipes.ToArray(); pf.beltA = beltA; pf.beltB = beltB;
            pf.pallet = pallet; pf.coldInterior = coldInside; pf.aisleZ = AisleZ; pf.doors = doors; pf.doorSpans = WingDepartments.Depts.Select(d => new Vector2(d.x0, d.x1)).ToArray();
            if (oldFloor) { pf.cartonPrefab = oldFloor.milkPrefab; pf.cupPrefab = oldFloor.yogurtPrefab; pf.cratePrefab = oldFloor.cratePrefab; }
            pf.milkMat = Mat("Mleko_w_rurze", new Color(0.97f, 0.97f, 0.94f), 0, 0.7f, new Color(0.35f, 0.35f, 0.34f));
            pf.butterMat = Mat("Maslo_kostka", new Color(0.98f, 0.88f, 0.45f), 0, 0.4f);
            pf.cheeseMat = Mat("Ser_krazek", new Color(0.96f, 0.76f, 0.28f), 0, 0.35f);
            pf.twarogMat = Mat("Twarog_kostka", new Color(0.97f, 0.96f, 0.92f), 0, 0.2f);
            pf.steamMat = SteamMat();
            pf.workerPrefab = WorkerPrefab(scene);

            // strefa mleczarni obejmuje skrzydło
            var zone = InScene<WorldPlace>(scene).First(w => w.kind == PlaceKind.Mleczarnia);
            float need = views.Max(v => Vector3.Distance(Flat(v.station.position), Flat(zone.transform.position))) + 3;
            if (zone.radius < need) { log.AppendLine($"Strefa mleczarni: promień {zone.radius:0} → {need:0} m."); zone.radius = need; }

            Directory.CreateDirectory("Assets/_Mleczarka/Prefabs/Buildings");
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/_Mleczarka/Prefabs/Buildings/Mleczarnia_Skrzydlo.prefab");
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Nie zapisano sceny.");
            log.AppendLine($"Skrzydło: {root.position}, fundament {depth:0.0} m, maszyny i stanowiska: {views.Count}, rurociągi: {pipes.Count}.");
            Capture(scene, "Skrzydlo_wnetrze", new Vector3(13, 9, -14), new Vector3(-1, 0.5f, -1), true);
            Capture(scene, "Skrzydlo_gora", new Vector3(0.1f, 30, -6), new Vector3(0, 0, 0), true);
            Capture(scene, "Skrzydlo_rzad_pn", new Vector3(-2, 2.6f, -2.5f), new Vector3(-2, 1.2f, 2.5f), true, 75);
            Capture(scene, "Skrzydlo_rzad_pd", new Vector3(1, 2.6f, -1.5f), new Vector3(1, 1.0f, -6.5f), true, 75);
            Capture(scene, "Skrzydlo_zewnatrz", new Vector3(-30, 18, -30), new Vector3(8, 2, 0), false);
            Write("skrzydlo_budowa.txt");
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        static void BuildShell(float depth)
        {
            Box("Fundament", new Vector3(0, -depth / 2 - 0.05f, 0), new Vector3(HalfX * 2 + 0.8f, depth, HalfZ * 2 + 0.8f), navy);
            Box("Posadzka", new Vector3(0, -0.1f, 0), new Vector3(HalfX * 2, 0.2f, HalfZ * 2), floor);
            for (int x = -10; x <= 10; x += 2) Box("Spoina posadzki", new Vector3(x, 0.006f, 0), new Vector3(0.018f, 0.01f, HalfZ * 2), grout, false, false);
            for (int z = -12; z <= 12; z += 2) Box("Spoina posadzki", new Vector3(0, 0.006f, z), new Vector3(HalfX * 2, 0.01f, 0.018f), grout, false, false);
            // ściany: zachodnia, północna, południowa (wschodnia to ściana hali z przejściem)
            Box("Cokół zachodni", new Vector3(-HalfX, 0.5f, 0), new Vector3(0.35f, 1, HalfZ * 2), teal);
            Box("Cokół północny", new Vector3(0, 0.5f, HalfZ), new Vector3(HalfX * 2, 1, 0.35f), teal);
            Box("Cokół południowy", new Vector3(0, 0.5f, -HalfZ), new Vector3(HalfX * 2, 1, 0.35f), teal);
            Box("Ściana zachodnia", new Vector3(-HalfX, 3.2f, 0), new Vector3(0.3f, 4.4f, HalfZ * 2), cream, true);
            Box("Ściana północna", new Vector3(0, 3.2f, HalfZ), new Vector3(HalfX * 2, 4.4f, 0.3f), cream, true);
            Box("Ściana południowa", new Vector3(0, 3.2f, -HalfZ), new Vector3(HalfX * 2, 4.4f, 0.3f), cream, true);
            for (int x = -9; x <= 9; x += 6)
                foreach (float side in new[] { -1f, 1f })
                {
                    float z = side * (HalfZ + 0.16f);
                    Box("Rama okienna", new Vector3(x, 3.4f, z), new Vector3(3.2f, 1.6f, 0.12f), navy, true);
                    Box("Szklenie", new Vector3(x, 3.4f, z + side * 0.04f), new Vector3(2.9f, 1.3f, 0.08f), glass, true);
                }
            for (int z = -9; z <= 9; z += 6)
            {
                Box("Rama okienna", new Vector3(-HalfX - 0.16f, 3.4f, z), new Vector3(0.12f, 1.6f, 3.2f), navy, true);
                Box("Szklenie", new Vector3(-HalfX - 0.2f, 3.4f, z), new Vector3(0.08f, 1.3f, 2.9f), glass, true);
            }
            for (int z = -12; z <= 12; z += 6)
            {
                Box("Słup konstrukcji", new Vector3(-HalfX, 2.75f, z), new Vector3(0.28f, 5.5f, 0.3f), navy);
                Box("Dźwigar dachowy", new Vector3(0, 5.35f, z), new Vector3(HalfX * 2, 0.25f, 0.25f), navy, true);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                var roof = Box("Dach stalowy", new Vector3(side * 6.15f, 6.0f, 0), new Vector3(12.6f, 0.22f, HalfZ * 2 + 1.6f), navy, true);
                roof.transform.localRotation = Quaternion.Euler(0, 0, side * -6f);
            }
            Box("Kalenica", new Vector3(0, 6.66f, 0), new Vector3(0.6f, 0.2f, HalfZ * 2 + 1.7f), teal, true);
            for (float x = -11.4f; x <= 11.4f; x += 1.2f)
                Box("Rąbek pokrycia", new Vector3(x, 6.8f - Mathf.Abs(x) * 0.105f, 0), new Vector3(0.05f, 0.055f, HalfZ * 2 + 1.6f), teal, true, false);
            for (int z = -8; z <= 8; z += 8) foreach (int x in new[] { -6, 6 })
            {
                Box("Oprawa LED", new Vector3(x, 4.95f, z), new Vector3(4, 0.1f, 0.3f), glow, false, false);
                var light = new GameObject("Światło skrzydła").AddComponent<Light>(); light.transform.SetParent(root, false);
                light.transform.localPosition = new Vector3(x, 4.3f, z); light.type = LightType.Point; light.range = 11; light.intensity = 2.4f;
                light.color = new Color(0.9f, 0.96f, 1); light.shadows = LightShadows.None;
            }
            // oznaczenia: główny korytarz od przejścia z hali do laboratorium, strefy obsługi
            for (float x = -9.5f; x <= 11; x += 2) Box("Pas komunikacyjny", new Vector3(x, 0.015f, AisleZ), new Vector3(1.3f, 0.02f, 0.1f), yellow, false, false);
        }

        /// <summary>Przejście 4 m w zachodniej ścianie hali: oryginalne bryły zostają (wyłączone), w ich miejsce segmenty z otworem.</summary>
        static void OpenHallWall(Transform hall)
        {
            var wall = hall.Find("Ściana zachodnia"); var plinth = hall.Find("Cokół zachodni");
            var vis = hall.GetComponent<FactoryShellVisibility>();
            var add = new List<Renderer>();
            Transform keep = root; root = hall;
            try
            {
                if (wall) wall.gameObject.SetActive(false);
                if (plinth) plinth.gameObject.SetActive(false);
                float z0 = DoorZ0, z1 = DoorZ1;
                Seg("Ściana zachodnia (płd.)", 3.2f, 4.4f, -13, z0, cream, add, true);
                Seg("Ściana zachodnia (pn.)", 3.2f, 4.4f, z1, 13, cream, add, true);
                Seg("Cokół zachodni (płd.)", 0.5f, 1, -13, z0, teal, add, false);
                Seg("Cokół zachodni (pn.)", 0.5f, 1, z1, 13, teal, add, false);
                var lintel = Box(Tag + "Nadproże przejścia", new Vector3(-14, 4.6f, (z0 + z1) / 2), new Vector3(0.4f, 1.6f, z1 - z0), navy);
                add.Add(lintel.GetComponent<Renderer>());
                Box(Tag + "Ościeżnica", new Vector3(-14, 1.9f, z0 - 0.1f), new Vector3(0.45f, 3.8f, 0.2f), teal);
                Box(Tag + "Ościeżnica", new Vector3(-14, 1.9f, z1 + 0.1f), new Vector3(0.45f, 3.8f, 0.2f), teal);
                Box(Tag + "Próg", new Vector3(-14, 0.005f, (z0 + z1) / 2), new Vector3(0.6f, 0.02f, z1 - z0), yellow, false, false);
                for (float x = -13; x <= -11; x += 1) Box(Tag + "Pas do skrzydła", new Vector3(x, 0.015f, (z0 + z1) / 2), new Vector3(0.6f, 0.02f, 0.1f), yellow, false, false);
            }
            finally { root = keep; }
            if (vis) vis.shell = vis.shell.Where(r => r).Concat(add).ToArray();
        }

        static void Seg(string name, float y, float h, float z0, float z1, Material m, List<Renderer> add, bool cut)
        {
            var g = Box(Tag + name, new Vector3(-14, y, (z0 + z1) / 2), new Vector3(cut ? 0.3f : 0.35f, h, z1 - z0), m);
            if (cut) add.Add(g.GetComponent<Renderer>());
        }

        // ---------------- maszyny ----------------

        static PlantMachineView PlaceMachine(Place p)
        {
            string path = Machines + p.model + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset) throw new FileNotFoundException("Brak modelu maszyny: " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            go.name = (p.id ?? p.model) + " — " + (p.id != null ? PlantData.M(p.id).Name : p.title);
            var mk = Markers(go.transform);
            // obrót: operator (znacznik _Stanowisko) ma stać od strony korytarza
            if (mk.TryGetValue("Stanowisko", out var st))
            {
                var d = root.InverseTransformDirection(st.position - go.transform.position); d.y = 0;
                if (d.sqrMagnitude > 0.01f) go.transform.localRotation = Quaternion.Euler(0, Vector3.SignedAngle(d.normalized, p.face, Vector3.up), 0);
            }
            go.transform.localPosition = new Vector3(p.x, 0, p.z);
            // stopa maszyny na posadzce
            // lampka stanu leży w modelu w środku podstawy (poniżej posadzki) — liczymy stopę bez niej,
            // a lampkę stawiamy na górze korpusu; wcześniej zaniżała dół i maszyny wisiały 16 cm nad posadzką
            var b = Bounds(go); go.transform.position += Vector3.up * (root.position.y - b.min.y);
            b = Bounds(go);
            foreach (var lr in go.GetComponentsInChildren<Renderer>().Where(r => r.name.EndsWith("_Lampka")))
            {
                var lb = lr.bounds; var top = new Vector3(b.center.x, b.max.y, b.center.z);
                lr.transform.position += top - new Vector3(lb.center.x, lb.min.y, lb.center.z) + Vector3.up * 0.01f;
            }
            var col = go.AddComponent<BoxCollider>();
            col.center = go.transform.InverseTransformPoint(b.center);
            var ls = go.transform.InverseTransformVector(b.size); col.size = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z)) * 0.92f;

            var v = go.AddComponent<PlantMachineView>();
            v.kind = p.id != null ? PlantMachineView.Kind.Machine : p.kind; v.machineId = p.id; v.title = p.title;
            v.station = mk.TryGetValue("Stanowisko", out st) ? st : Marker(go.transform, "Stanowisko (zastępczy)", root.TransformPoint(new Vector3(p.x, 0, p.z) + p.face * 1.4f));
            v.inPort = mk.TryGetValue("Wejscie", out var a) ? a : Marker(go.transform, "Wejście (zastępcze)", b.center + Vector3.up * b.extents.y * 0.4f - root.right * b.extents.x * 0.8f);
            v.outPort = mk.TryGetValue("Wyjscie", out var o) ? o : Marker(go.transform, "Wyjście (zastępcze)", b.center - Vector3.up * b.extents.y * 0.2f + root.right * b.extents.x * 0.8f);
            if (mk.TryGetValue("Para", out var pa)) v.steamPoint = pa;
            if (mk.TryGetValue("Babelki", out var bb)) v.bubblePoint = bb;
            if (mk.TryGetValue("Wnetrze", out var wn)) v.interior = wn;
            // strefa obsługi na posadzce
            var sp = root.InverseTransformPoint(v.station.position);
            Box("Pole obsługi", new Vector3(sp.x, 0.012f, sp.z), new Vector3(1.2f, 0.02f, 1.2f), teal, false, false).transform.localRotation = Quaternion.LookRotation(p.face);
            log.AppendLine($"{go.name}: wymiary {b.size.x:0.0}×{b.size.y:0.0}×{b.size.z:0.0} m, znaczniki: {string.Join(", ", mk.Keys)}");
            return v;
        }

        static Dictionary<string, Transform> Markers(Transform t)
        {
            var d = new Dictionary<string, Transform>();
            foreach (var c in t.GetComponentsInChildren<Transform>(true))
                foreach (var k in new[] { "Stanowisko", "Wejscie", "Wyjscie", "Para", "Babelki", "Wnetrze" })
                    if (c.name.EndsWith("_" + k) && !d.ContainsKey(k)) d[k] = c;
            return d;
        }

        static Transform Marker(Transform parent, string name, Vector3 world)
        { var g = new GameObject(name).transform; g.SetParent(parent, true); g.position = world; return g; }

        static Transform PlaceSilos()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Mleczarka/Art/World/Silos_Mleka.fbx");
            var go = asset ? (GameObject)PrefabUtility.InstantiatePrefab(asset) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Silos mleka surowego"; go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            var b = Bounds(go); go.transform.localScale *= 4.6f / Mathf.Max(0.01f, b.size.y);
            b = Bounds(go); go.transform.position += root.TransformPoint(new Vector3(-10.2f, 0, 9.6f)) - new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterials = r.sharedMaterials.Select(m => steel).ToArray();
            b = Bounds(go);
            var col = go.AddComponent<BoxCollider>(); col.center = go.transform.InverseTransformPoint(b.center);
            var ls = go.transform.InverseTransformVector(b.size); col.size = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
            return Marker(go.transform, "Wylot silosu", new Vector3(b.center.x, b.min.y + 0.6f, b.center.z) - root.forward * b.extents.z);
        }

        // ---------------- rurociągi ----------------

        static List<PlantFloor.PipeRoute> BuildPipes(List<PlantMachineView> views, Transform silos)
        {
            Transform Port(string id, bool output)
            {
                if (id == "silos") return silos;
                var v = id == "smietanka" ? views.First(x => x.kind == PlantMachineView.Kind.CreamTank) : views.First(x => x.machineId == id);
                return output ? v.outPort : v.inPort;
            }
            var list = new List<PlantFloor.PipeRoute>();
            var holder = new GameObject("Rurociągi").transform; holder.SetParent(root, false);
            int k = 0;
            foreach (var (from, to, curd) in Pipes)
            {
                Vector3 a = Port(from, true).position, b = Port(to, false).position;
                if (from == "smietanka") a = Port(from, false).position;   // zbiornik ma jeden króciec u góry
                float h = root.position.y + 3.15f + 0.16f * (k % 8);
                var pts = new[] { a, new Vector3(a.x, h, a.z), Corner(a, b, h), new Vector3(b.x, h, b.z), b };
                for (int i = 1; i < pts.Length; i++) Tube(holder, pts[i - 1], pts[i], curd ? 0.09f : 0.07f);
                for (int i = 1; i < pts.Length - 1; i++) { var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); s.name = "Kolano"; s.transform.SetParent(holder, true); s.transform.position = pts[i]; s.transform.localScale = Vector3.one * 0.17f; s.GetComponent<Renderer>().sharedMaterial = steel; UnityEngine.Object.DestroyImmediate(s.GetComponent<Collider>()); }
                // wieszak do dźwigara
                var mid = Corner(a, b, h);
                var hang = GameObject.CreatePrimitive(PrimitiveType.Cylinder); hang.name = "Wieszak rury"; hang.transform.SetParent(holder, true);
                hang.transform.position = new Vector3(mid.x, (h + root.position.y + 5.3f) / 2, mid.z); hang.transform.localScale = new Vector3(0.025f, (root.position.y + 5.3f - h) / 2, 0.025f);
                hang.GetComponent<Renderer>().sharedMaterial = navy; UnityEngine.Object.DestroyImmediate(hang.GetComponent<Collider>());
                list.Add(new PlantFloor.PipeRoute { from = from, to = to, points = pts, curd = curd });
                k++;
            }
            return list;
        }

        static Vector3 Corner(Vector3 a, Vector3 b, float h)
        {
            // najpierw wzdłuż X skrzydła, potem wzdłuż Z
            var la = root.InverseTransformPoint(a); var lb = root.InverseTransformPoint(b);
            var c = root.TransformPoint(new Vector3(lb.x, 0, la.z)); c.y = h; return c;
        }

        static void Tube(Transform parent, Vector3 a, Vector3 b, float r)
        {
            float len = Vector3.Distance(a, b); if (len < 0.01f) return;
            var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder); t.name = "Rura"; t.transform.SetParent(parent, true);
            t.transform.position = (a + b) / 2; t.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            t.transform.localScale = new Vector3(r * 2, len / 2, r * 2);
            t.GetComponent<Renderer>().sharedMaterial = steel; UnityEngine.Object.DestroyImmediate(t.GetComponent<Collider>());
        }

        // ---------------- taśma i paleta ----------------

        static void BuildBelt(out Transform a, out Transform b)
        {
            float z = -8.6f, x0 = -10.6f, x1 = 9.6f, top = 0.8f;
            var holder = new GameObject("Taśma zbiorcza").transform; holder.SetParent(root, false);
            Transform keep = root; root = holder;
            try
            {
                Box("Rama taśmy", new Vector3((x0 + x1) / 2, top - 0.12f, z), new Vector3(x1 - x0, 0.16f, 0.62f), steel);
                Box("Pas taśmy", new Vector3((x0 + x1) / 2, top - 0.03f, z), new Vector3(x1 - x0, 0.03f, 0.5f), rubber, false, false);
                foreach (float side in new[] { -1f, 1f }) Box("Burta", new Vector3((x0 + x1) / 2, top + 0.05f, z + side * 0.3f), new Vector3(x1 - x0, 0.1f, 0.03f), steel, false, false);
                for (float x = x0 + 0.4f; x <= x1; x += 2) foreach (float side in new[] { -1f, 1f })
                    Box("Noga taśmy", new Vector3(x, (top - 0.2f) / 2, z + side * 0.25f), new Vector3(0.06f, top - 0.2f, 0.06f), navy, false, false);
            }
            finally { root = keep; }
            a = Marker(root, "Taśma — początek", root.TransformPoint(new Vector3(x0, top, z)));
            b = Marker(root, "Taśma — koniec", root.TransformPoint(new Vector3(x1, top, z)));
        }

        static Transform Pallet(Vector3 at)
        {
            var holder = new GameObject("Paleta odbioru (pakowarka)").transform; holder.SetParent(root, false); holder.localPosition = at;
            Transform keep = root; root = holder;
            try
            {
                for (int i = 0; i < 5; i++) Box("Deska palety", new Vector3(0, 0.13f, i * 0.2f - 0.4f), new Vector3(1.2f, 0.03f, 0.14f), wood);
                foreach (float x in new[] { -0.5f, 0, 0.5f }) Box("Klocek palety", new Vector3(x, 0.06f, 0), new Vector3(0.14f, 0.11f, 1.0f), wood, false, false);
            }
            finally { root = keep; }
            return Marker(holder, "Skrzynki na palecie", holder.position + Vector3.up * 0.15f);
        }

        // ---------------- pracownik ----------------

        static GameObject WorkerPrefab(Scene scene)
        {
            var farmer = InScene<Mleczarka.Player.FarmerController>(scene).FirstOrDefault();
            var anim = farmer ? farmer.GetComponentInChildren<Animator>() : null;
            if (!anim) { log.AppendLine("Brak modelu rolnika — pracownicy bez postaci."); return null; }
            var work = WorkClip();
            // gracz też dostaje stan „Praca” (te same animacje UAL)
            if (anim.runtimeAnimatorController is AnimatorController pc) AddWork(pc, work);
            string ctrlPath = Gen + "Pracownik.controller";
            if (anim.runtimeAnimatorController) { AssetDatabase.DeleteAsset(ctrlPath); AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(anim.runtimeAnimatorController), ctrlPath); }
            var wc = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (wc) AddWork(wc, work);
            var copy = UnityEngine.Object.Instantiate(anim.gameObject);
            var holder = new GameObject("Pracownik");
            copy.name = "Model"; copy.transform.SetParent(holder.transform, false);
            copy.transform.localPosition = anim.transform.localPosition; copy.transform.localRotation = anim.transform.localRotation; copy.transform.localScale = anim.transform.localScale;
            foreach (var c in copy.GetComponentsInChildren<Component>(true))
                if (!(c is Transform) && !(c is Renderer) && !(c is Animator) && !(c is MeshFilter)) UnityEngine.Object.DestroyImmediate(c);
            var wa = copy.GetComponent<Animator>(); if (wa) { wa.runtimeAnimatorController = wc; wa.applyRootMotion = false; wa.cullingMode = AnimatorCullingMode.CullUpdateTransforms; }
            string path = "Assets/_Mleczarka/Prefabs/Production/Pracownik.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var prefab = PrefabUtility.SaveAsPrefabAsset(holder, path);
            UnityEngine.Object.DestroyImmediate(holder);
            log.AppendLine($"Pracownik: model rolnika, animacja pracy: {(work ? work.name : "brak")}.");
            return prefab;
        }

        static AnimationClip WorkClip()
        {
            var clips = new List<AnimationClip>();
            foreach (var f in new[] { "UniversalAnimationLibrary/UAL1_Standard.fbx", "UniversalAnimationLibrary2/UAL2_Standard.fbx" })
                clips.AddRange(AssetDatabase.LoadAllAssetsAtPath("Assets/_Mleczarka/Art/ThirdParty/Quaternius/" + f).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")));
            log.AppendLine("Klipy UAL: " + string.Join(", ", clips.Select(c => c.name)));
            foreach (var want in new[] { "Interact", "Fixing_Kneeling", "PickUp_Table", "Push_Loop", "Farm_Harvest", "Idle_Talking_Loop" })
            { var c = clips.FirstOrDefault(x => x.name == want) ?? clips.FirstOrDefault(x => x.name.StartsWith(want)); if (c) return c; }
            return clips.FirstOrDefault(c => c.name.Contains("Idle"));
        }

        static void AddWork(AnimatorController ctrl, AnimationClip clip)
        {
            if (!clip || ctrl.parameters.Any(p => p.name == "Work")) return;
            ctrl.AddParameter("Work", AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine; var loco = sm.defaultState;
            var work = sm.AddState("Praca"); work.motion = clip;
            var t1 = loco.AddTransition(work); t1.AddCondition(AnimatorConditionMode.If, 0, "Work"); t1.AddCondition(AnimatorConditionMode.Less, 0.5f, "Speed"); t1.hasExitTime = false; t1.duration = 0.2f;
            var t2 = work.AddTransition(loco); t2.AddCondition(AnimatorConditionMode.IfNot, 0, "Work"); t2.hasExitTime = false; t2.duration = 0.25f;
            var t3 = work.AddTransition(loco); t3.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); t3.hasExitTime = false; t3.duration = 0.15f;
            if (!clip.isLooping) { var t4 = work.AddTransition(work); t4.hasExitTime = true; t4.exitTime = 0.92f; t4.duration = 0.12f; t4.AddCondition(AnimatorConditionMode.If, 0, "Work"); }
            EditorUtility.SetDirty(ctrl);
        }

        // ---------------- pomocnicze ----------------

        static void WithScene(bool save, Action<Scene> body)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[Mleczarka] Zatrzymaj tryb gry."); return; }
            log = new StringBuilder();
            Scene original = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(MainScene);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive);
            try
            {
                if (save && scene.isDirty) throw new InvalidOperationException("Główna scena ma niezapisane zmiany. Zapisz ją albo odrzuć zmiany i spróbuj ponownie.");
                SceneManager.SetActiveScene(scene);
                body(scene);
                Debug.Log("[Mleczarka] " + log.ToString().Split('\n').Last(l => l.Trim().Length > 0));
            }
            catch (Exception e) { log.AppendLine("BŁĄD: " + e); Write("skrzydlo_blad.txt"); Debug.LogException(e); }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void Write(string file) { Directory.CreateDirectory("Screenshots"); File.WriteAllText("Screenshots/" + file, log.ToString()); }

        static Transform Hall(Scene scene)
        {
            var h = scene.GetRootGameObjects().FirstOrDefault(g => g.name == HallName);
            if (!h) throw new InvalidOperationException("Brak hali produkcyjnej w głównej scenie.");
            return h.transform;
        }

        static IEnumerable<T> InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

        static string PathOf(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }

        static float Ground(Terrain[] terrains, Vector3 w)
        {
            foreach (var t in terrains)
            {
                var p = t.transform.position; var s = t.terrainData.size;
                if (w.x >= p.x && w.x <= p.x + s.x && w.z >= p.z && w.z <= p.z + s.z) return t.SampleHeight(w) + p.y;
            }
            return float.NaN;
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>().Where(r => !r.name.EndsWith("_Lampka")).ToArray();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds; foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds); return b;
        }

        static GameObject Box(string name, Vector3 p, Vector3 size, Material m, bool cut = false, bool collide = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(root, false);
            g.transform.localPosition = p; g.transform.localScale = size;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m;
            if (!collide) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            if (cut) shell.Add(r);
            return g;
        }

        static void Label(string text, Vector3 at, float size, Color c, Quaternion rot)
        {
            var g = new GameObject(text); g.transform.SetParent(root, false); g.transform.localPosition = at; g.transform.localRotation = rot;
            var tm = g.AddComponent<TextMesh>(); tm.text = text; tm.fontSize = 64; tm.characterSize = size; tm.anchor = TextAnchor.MiddleCenter; tm.color = c;
        }

        static void CreateMaterials()
        {
            Directory.CreateDirectory(Gen);
            cream = Mat("Tynk_kremowy", new Color(0.89f, 0.9f, 0.83f));
            navy = Mat("Stal_granatowa", new Color(0.035f, 0.13f, 0.21f), 0.3f, 0.4f);
            teal = Mat("Akcent_mietowy", new Color(0.08f, 0.47f, 0.5f), 0.2f, 0.35f);
            steel = Mat("Stal_nierdzewna", new Color(0.64f, 0.73f, 0.76f), 0.75f, 0.65f);
            floor = Mat("Posadzka_jasna", new Color(0.72f, 0.78f, 0.77f), 0.05f, 0.4f);
            grout = Mat("Fugi", new Color(0.5f, 0.59f, 0.58f));
            glass = Mat("Szklo_niebieskie", new Color(0.24f, 0.48f, 0.6f), 0.4f, 0.9f);
            yellow = Mat("Oznaczenia_bhp", new Color(0.98f, 0.7f, 0.15f));
            wood = Mat("Drewno_palet", new Color(0.45f, 0.28f, 0.13f));
            rubber = Mat("Guma_tasmy", new Color(0.09f, 0.09f, 0.1f), 0, 0.25f);
            glow = Mat("Lampy", new Color(0.9f, 0.97f, 1), 0, 0.2f, new Color(0.8f, 0.95f, 1) * 2);
        }

        static Material Mat(string name, Color c, float metallic = 0, float smooth = 0.2f, Color? emit = null)
        {
            string path = Gen + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.color = c; m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smooth);
            if (emit.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emit.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            EditorUtility.SetDirty(m); return m;
        }

        static Material SteamMat()
        {
            var m = Mat("Para_wodna", new Color(1, 1, 1, 0.4f), 0, 0.1f);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m); return m;
        }

        static void Capture(Scene scene, string name, Vector3 localPos, Vector3 localLook, bool hideShell, float fov = 62)
        {
            var cam = new GameObject("Kamera kontroli skrzydła").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(cam.gameObject, scene);
            cam.transform.position = root.TransformPoint(localPos); cam.transform.LookAt(root.TransformPoint(localLook));
            cam.fieldOfView = fov; cam.farClipPlane = 3000; cam.nearClipPlane = 0.05f;
            var t = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (t != null) { var d = cam.gameObject.AddComponent(t); t.GetProperty("renderPostProcessing").SetValue(d, true); }
            var hall = Hall(scene).GetComponent<FactoryShellVisibility>();
            var all = shell.Concat(hall ? hall.shell : new Renderer[0]).Where(r => r).ToList();
            var saved = all.Select(r => r.enabled).ToList();
            if (hideShell) foreach (var r in all) r.enabled = false;
            var prev = RenderTexture.active; var rt = new RenderTexture(1600, 900, 24); cam.targetTexture = rt;
            try
            {
                cam.Render(); cam.Render();   // GPU Resident Drawer liczy zasłanianie z poprzedniej klatki
                RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
                Directory.CreateDirectory("Screenshots"); File.WriteAllBytes("Screenshots/" + name + ".png", tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                for (int i = 0; i < all.Count; i++) all[i].enabled = saved[i];
                RenderTexture.active = prev; cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(cam.gameObject); UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }
}
