using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mleczarka.Vehicles;

namespace Mleczarka.Racing
{
    public static class RaceDemoBuilder
    {
        public const string ScenePath = "Assets/_Mleczarka/Scenes/Wyscig_Prototyp.unity";
        const string Request = "Temp/MleczarkaRaceBuild.request";

        // Jednorazowe, jawne zlecenie budowy osobnej sceny. Nie nadpisuje istniejącej sceny.
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(Request);
                try { Build(); }
                catch (Exception e) { Directory.CreateDirectory("Screenshots"); File.WriteAllText("Screenshots/wyscig_budowa.txt", e.ToString()); Debug.LogException(e); }
            };
        }

        [MenuItem("Mleczarka/Wyscigi/1. Przygotuj osobna scene testowa")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Zatrzymaj tryb gry przed budową sceny testowej.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Scena testowa już istnieje. Otwórz ją; nie została nadpisana.");
            var source = UnityEngine.Object.FindObjectsByType<WheeledVehicle>(FindObjectsSortMode.None)
                .Where(v => v.gameObject.scene.IsValid() && v.GetComponent<VehicleAssembler>() != null)
                .OrderByDescending(v => v.name.IndexOf("cysterna", StringComparison.OrdinalIgnoreCase) >= 0).FirstOrDefault();
            if (!source) throw new InvalidOperationException("Otwórz istniejącą scenę Gmina_Podlasie z pojazdem. Nic nie zmieniono.");
            string checks = RaceChecks.Run();
            Scene original = SceneManager.GetActiveScene();
            Scene demo = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(demo);
            bool saved = false;
            try
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (!shader) throw new InvalidOperationException("Brak shadera do nawierzchni toru.");
                var road = MaterialAsset("Tor_Asfalt", new Color(0.22f, 0.23f, 0.24f), shader);
                var grass = MaterialAsset("Tor_Trawa", new Color(0.22f, 0.43f, 0.16f), shader);
                var white = MaterialAsset("Tor_Bialy", Color.white, shader);
                var red = MaterialAsset("Tor_Czerwony", new Color(0.8f, 0.15f, 0.1f), shader);
                Box("Podłoże", new Vector3(0, -0.6f, 0), new Vector3(250, 1, 190), grass);
                var route = new Vector3[40];
                for (int i = 0; i < route.Length; i++)
                {
                    float angle = i * Mathf.PI * 2 / route.Length;
                    route[i] = new Vector3(Mathf.Cos(angle) * 90, 1.2f, Mathf.Sin(angle) * 55);
                }
                for (int i = 0; i < route.Length; i++)
                {
                    var a = route[i]; var b = route[(i + 1) % route.Length];
                    var direction = (b - a).normalized; var right = Vector3.Cross(Vector3.up, direction);
                    var mid = (a + b) / 2; mid.y = 0;
                    var tile = Box("Tor " + i, mid, new Vector3(17, 0.2f, Vector3.Distance(a, b) + 1), road);
                    tile.transform.rotation = Quaternion.LookRotation(direction);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var barrier = Box("Bariera " + i, mid + right * side * 10 + Vector3.up * 0.5f,
                            new Vector3(0.6f, 1, Vector3.Distance(a, b) + 0.5f), i % 2 == 0 ? white : red);
                        barrier.transform.rotation = Quaternion.LookRotation(direction);
                    }
                }
                var start = route[route.Length - 1];
                Vector3 forward = (route[0] - start).normalized; forward.y = 0;
                var player = CopyVehicle(source, "Cysterna gracza — wyścig", start - Vector3.Cross(Vector3.up, forward) * 3, forward);
                var rival = CopyVehicle(source, "Cysterna rywala — wyścig", start + Vector3.Cross(Vector3.up, forward) * 3, forward);
                var assembler = rival.GetComponent<VehicleAssembler>();
                assembler.loadout.paintPrimary = new Vehicles.Model.Rgb(0.85f, 0.18f, 0.1f);
                assembler.ApplyPaint();
                var sun = new GameObject("Słońce").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.5f; sun.transform.rotation = Quaternion.Euler(45, -30, 0);
                var camera = new GameObject("Kamera wyścigu").AddComponent<Camera>();
                camera.tag = "MainCamera"; camera.fieldOfView = 65; camera.farClipPlane = 600;
                camera.transform.position = start - forward * 13 + Vector3.up * 7;
                camera.gameObject.AddComponent<AudioListener>();
                var controller = new GameObject("Wyścig — dwa okrążenia").AddComponent<RaceDemo>();
                controller.playerVehicle = player; controller.rivalVehicle = rival; controller.raceCamera = camera;
                controller.route = route;
                if (!EditorSceneManager.SaveScene(demo, ScenePath)) throw new IOException("Nie zapisano sceny testowej.");
                saved = true;
                Directory.CreateDirectory("Screenshots");
                File.WriteAllText("Screenshots/wyscig_budowa.txt", "Scena zapisana: " + ScenePath + "\nReguły: " + checks + "\nŹródło modeli: " + source.name + "\nMapa źródłowa bez zmian. Test jazdy i strojenie AI pozostają do wykonania.");
                Debug.Log("[Mleczarka] Scena wyścigu przygotowana. Reguły: " + checks);
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(demo, true);
                if (!saved) Debug.LogError("[Mleczarka] Nie udało się przygotować sceny wyścigu. Scena źródłowa nie została zmieniona.");
            }
        }

        static Material MaterialAsset(string name, Color color, Shader shader)
        {
            const string folder = "Assets/_Mleczarka/Art/Materials/RacingPrototype";
            Directory.CreateDirectory(folder);
            string path = folder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            var material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path); return material;
        }

        static GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }

        static WheeledVehicle CopyVehicle(WheeledVehicle source, string name, Vector3 position, Vector3 forward)
        {
            var clone = UnityEngine.Object.Instantiate(source.gameObject);
            clone.name = name; clone.transform.SetParent(null);
            clone.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            var assembler = clone.GetComponent<VehicleAssembler>();
            // Kopia danych, żeby kolory rywala nie zmieniały oryginalnego pojazdu.
            assembler.loadout = JsonUtility.FromJson<Vehicles.Model.VehicleLoadout>(JsonUtility.ToJson(assembler.loadout));
            var rb = clone.GetComponent<Rigidbody>(); rb.isKinematic = false;
            clone.SetActive(true); return clone.GetComponent<WheeledVehicle>();
        }

        [MenuItem("Mleczarka/Wyscigi/2. Sprawdz reguly")]
        public static void Test() { Debug.Log("[Mleczarka] Wyścig: " + RaceChecks.Run()); }

        [MenuItem("Mleczarka/Wyscigi/3. Otworz scene testowa")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
