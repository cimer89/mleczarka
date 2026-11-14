using System.IO;
using System.Linq;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>
    /// Podgląd animacji procesu w trybie gry: partia UHT i jogurtu „obsługiwana” jak przez gracza (komendy co 0,4 s),
    /// zrzuty z kamery wewnątrz hali w kilku chwilach. Zapis gry jest wstrzymany — prawdziwa gra gracza się nie zmienia.
    /// </summary>
    public static class ProcessPreview
    {
        static double _t0, _next; static int _shot; static Camera _cam; static float _savedMilk;
        static readonly (float at, string name, Vector3 pos, Vector3 look)[] Shots =
        {
            (0.0f, "proces_0_menu", default, default),
            (21.0f, "proces_1_uht_i_pasteryzacja", new Vector3(-6f, 4.2f, -11.5f), new Vector3(-6f, 1.5f, 0f)),
            (22.0f, "proces_2_pakowanie_uht", new Vector3(2f, 3.2f, -9f), new Vector3(6.5f, 0.8f, -3f)),
            (23.0f, "proces_3_fermentacja", new Vector3(-6f, 3.8f, 1.5f), new Vector3(-3f, 1.6f, 9.5f)),
            (24.0f, "proces_4_pakowanie_jogurtu", new Vector3(2f, 3.2f, 3f), new Vector3(6.5f, 0.8f, 9f)),
        };

        [MenuItem("Mleczarka/Produkcja/8. Podglad animacji procesu (Play)")]
        static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Mleczarka] Najpierw uruchom tryb gry w Gmina_Podlasie."); return; }
            var g = GameController.Instance; var floor = Object.FindFirstObjectByType<ProductionFloor>();
            if (!g || !floor || !floor.facilityRoot) { Debug.LogWarning("[Mleczarka] Brak hali produkcyjnej w scenie."); return; }
            g.SuspendSaving = true;
            Directory.CreateDirectory("Screenshots");
            ScreenCapture.CaptureScreenshot("Screenshots/proces_0_menu.png");      // menu startowe, jeśli jest otwarte
            _t0 = EditorApplication.timeSinceStartup; _next = 0; _shot = 1; _frame = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static bool _started;
        static void Begin()
        {
            var g = GameController.Instance; var floor = Object.FindFirstObjectByType<ProductionFloor>();
            var menu = Object.FindFirstObjectByType<Mleczarka.UI.StartMenuUI>(); if (menu) menu.CloseMenu();
            var p = g.Sim.S.players[GameController.Me];
            _savedMilk = p.tankMilk;
            p.tankMilk = Mathf.Max(p.tankMilk, Mathf.Min(p.tankCap, 30));
            if (g.player.CurrentVehicle) g.player.EnterOrExit();
            var intake = floor.stations.First(s => s.action == FloorAction.Intake);
            g.player.transform.position = intake.transform.position + Vector3.up * 0.1f;
            g.Send(new StartBatch(0, Product.Uht, 4));
            g.Send(new StartBatch(0, Product.Jogurt, 2));
            _cam = new GameObject("Kamera podglądu procesu").AddComponent<Camera>();
            _cam.enabled = false; _cam.fieldOfView = 55; _cam.nearClipPlane = 0.05f; _cam.farClipPlane = 300;
            _started = true;
        }

        const int Frames = 64; static int _frame;
        static void Grab(string name, int w, int h)
        {
            Directory.CreateDirectory(Path.GetDirectoryName("Screenshots/" + name + ".png"));
            var rt = new RenderTexture(w, h, 24); _cam.targetTexture = rt; _cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            File.WriteAllBytes("Screenshots/" + name + ".png", tex.EncodeToPNG());
            RenderTexture.active = null; _cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        }

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; _started = false; return; }
            var g = GameController.Instance; if (!g) return;
            double el = EditorApplication.timeSinceStartup - _t0;
            if (!_started) { if (el > 1.0) Begin(); return; }
            var p = g.Sim.S.players[GameController.Me];
            // „gracz” obsługuje maszyny: jak przytrzymanie Q
            if (el >= _next)
            {
                _next = el + 0.4;
                foreach (var b in p.batches.ToList())
                {
                    var sg = Simulation.StageOf(b);
                    if (sg.Kind == StationKind.Praca && (b.product == Product.Uht) == (_frame < Frames / 2)) g.Send(new HelpStation(0, sg.Station));
                    else if (sg.Station == "ferm" && _frame >= Frames / 2 && b.progress < g.Sim.StageNeed(b) - 3) b.progress = g.Sim.StageNeed(b) - 3;   // skrót: fermentacja trwa 4 minuty
                    
                }
            }
            var root = Object.FindFirstObjectByType<ProductionFloor>().facilityRoot;
            if (_frame < Frames && el >= 1.2 + _frame * 0.3)
            {
                bool yog = _frame >= Frames / 2;
                // pierwsza połowa: linia UHT (zbiornik → UHT → pakowarka → taśma); druga: jogurt (pasteryzacja → fermentacja → pakowanie)
                _cam.transform.position = root.TransformPoint(yog ? new Vector3(-1f, 6.5f, -1.5f) : new Vector3(-1f, 6.5f, -13f));
                _cam.transform.LookAt(root.TransformPoint(yog ? new Vector3(-1f, 1f, 9f) : new Vector3(-1f, 1f, -3f)));
                Grab($"proces_klatki/k{_frame:00}", 800, 450);
                _frame++;
            }
            while (_shot < Shots.Length && el >= Shots[_shot].at + 1.0)
            {
                var s = Shots[_shot++];
                _cam.transform.position = root.TransformPoint(s.pos); _cam.transform.LookAt(root.TransformPoint(s.look));
                var rt = new RenderTexture(1600, 900, 24); _cam.targetTexture = rt; _cam.Render(); _cam.Render();
                RenderTexture.active = rt; var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
                File.WriteAllBytes("Screenshots/" + s.name + ".png", tex.EncodeToPNG());
                RenderTexture.active = null; _cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
                Debug.Log($"[Mleczarka] Podgląd procesu: {s.name} (partie: {string.Join(", ", p.batches.Select(b => GameData.P(b.product).Name + " " + Simulation.StageOf(b).Label + " " + (100 * b.progress / g.Sim.StageNeed(b)).ToString("0") + "%"))}; przy pakowaniu: {p.packingRack.Sum()})");
            }
            if (_shot >= Shots.Length && _frame >= Frames)
            {
                EditorApplication.update -= Tick; _started = false;
                Debug.Log("[Mleczarka] Podgląd procesu zakończony. Zapis gry wstrzymany do końca tej sesji trybu gry — wyjdź z trybu gry.");
            }
        }
    }
}
