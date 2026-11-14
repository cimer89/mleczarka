using System.IO;
using System.Linq;
using System.Text;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>
    /// Podgląd skrzydła maszyn w trybie gry: kilka partii (UHT, jogurt, masło, twaróg) obsługiwanych przez pracowników,
    /// zrzuty z kamer co kilka sekund i dziennik etapów. Zapis gry wstrzymany — gra gracza się nie zmienia.
    /// </summary>
    public static class WingPreview
    {
        static double _t0; static int _n; static Camera _cam; static StringBuilder _log; static int _phase;
        const string Dir = "Screenshots/skrzydlo_klatki/";

        [MenuItem("Mleczarka/Produkcja/12. Podglad skrzydla maszyn (Play)")]
        static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Mleczarka] Najpierw uruchom tryb gry (Play) w Gmina_Podlasie."); return; }
            var pf = Object.FindFirstObjectByType<PlantFloor>();
            if (!pf) { Debug.LogWarning("[Mleczarka] Brak skrzydła maszyn w scenie."); return; }
            GameController.Instance.SuspendSaving = true;
            if (Directory.Exists(Dir)) foreach (var f in Directory.GetFiles(Dir)) File.Delete(f);
            Directory.CreateDirectory(Dir);
            _log = new StringBuilder(); _t0 = EditorApplication.timeSinceStartup; _n = 0; _phase = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static PlantFloor PF => Object.FindFirstObjectByType<PlantFloor>();

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - _t0;
            var g = GameController.Instance; var p = g.Sim.S.players[GameController.Me]; var pf = PF;
            if (_phase == 0)
            {
                var menu = Object.FindFirstObjectByType<Mleczarka.UI.StartMenuUI>(); if (menu) menu.CloseMenu();
                if (g.player.CurrentVehicle) g.player.EnterOrExit();
                p.tankMilk = Mathf.Max(p.tankMilk, Mathf.Min(p.tankCap, 80)); p.money = System.Math.Max(p.money, 400);
                p.plant.cream = Mathf.Max((float)p.plant.cream, 12);
                var intake = pf.views.First(v => v.kind == PlantMachineView.Kind.Intake);
                g.player.transform.position = intake.station.position + Vector3.up * 0.05f;
                foreach (var (prod, q) in new[] { (Product.Uht, 4), (Product.Jogurt, 2), (Product.Maslo, 2), (Product.Twarog, 2) })
                    _log.AppendLine($"start {prod}: {g.Send(new StartJob(GameController.Me, prod, q))}");
                Time.timeScale = 2f;
                _cam = new GameObject("Kamera podglądu skrzydła").AddComponent<Camera>(); _cam.enabled = false; _cam.nearClipPlane = 0.05f; _cam.farClipPlane = 400;
                var t2 = System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                if (t2 != null) { var d = _cam.gameObject.AddComponent(t2); t2.GetProperty("renderPostProcessing").SetValue(d, true); }
                _phase = 1; return;
            }
            if (_phase == 1 && t > 2) { ScreenCapture.CaptureScreenshot(Dir + "00_panel_przyjecia.png"); _phase = 2; return; }
            if (_phase == 2 && t > 3.5)
            {
                // gracz w korytarzu skrzydła (ściany i dach chowają się, gdy jest w środku)
                g.player.transform.position = pf.wingRoot.TransformPoint(new Vector3(-3, 0.05f, pf.aisleZ));
                var past = pf.views.First(v => v.machineId == "pasteryzator");
                g.player.transform.position = past.station.position + Vector3.up * 0.05f;
                _phase = 3; return;
            }
            if (_phase == 3 && t > 5) { ScreenCapture.CaptureScreenshot(Dir + "01_panel_maszyny.png"); _phase = 4; return; }
            if (_phase == 4 && t > 6)
            {
                g.player.transform.position = pf.wingRoot.TransformPoint(new Vector3(-1, 0.05f, pf.aisleZ));
                _phase = 5; _next = t + 1; return;
            }
            // wersja bez pracowników: „ręce gracza” — co 0,5 s czynność przy każdej maszynie, która czeka na człowieka
            if (_phase >= 3 && PlantSim.WorkersOff(p) && t >= _hands)
            {
                _hands = t + 0.25;
                foreach (var m in p.plant.machines.ToList())
                    if (PlantSim.NeedsHands(p, m)) { g.Send(new MachineAct(GameController.Me, m.id)); var v = pf.views.FirstOrDefault(x => x.machineId == m.id); if (v) v.LastHandsTime = Time.time; }
            }
            if (_phase == 5 && t >= _next)
            {
                _next = t + 7;
                var w = pf.wingRoot;
                Shot(_n + "_a_calosc", w.TransformPoint(new Vector3(11, 8.5f, -12)), w.TransformPoint(new Vector3(-2, 0.5f, 0)), 60);
                Shot(_n + "_b_rzad_pn", w.TransformPoint(new Vector3(-4, 3.2f, -1.5f)), w.TransformPoint(new Vector3(-3, 1.2f, 2.5f)), 70);
                Shot(_n + "_c_rzad_pd", w.TransformPoint(new Vector3(2, 3.2f, -1.5f)), w.TransformPoint(new Vector3(2, 0.8f, -7)), 70);
                var wk = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(x => x.name.StartsWith("Pracownik: "));
                if (wk) Shot(_n + "_d_pracownik", wk.position + wk.forward * 2.2f + Vector3.up * 1.6f + wk.right * 1.2f, wk.position + Vector3.up * 1f, 55);
                _log.AppendLine($"--- klatka {_n}, czas gry {g.Sim.S.t:0}s, mleko {p.tankMilk}, śmietanka {p.plant.cream:0.0}, pakowanie {p.packingRack.Sum()}");
                foreach (var j in p.plant.jobs) _log.AppendLine($"  {j.product}×{j.qty}: etap {j.stage}, maszyna {j.machine}, krok {j.step}, {j.wait}");
                foreach (var ww in p.workers) _log.AppendLine($"  pracownik {ww.name}: {ww.station} → {ww.walkTo}");
                _n++;
                if (_n >= 9)
                {
                    Time.timeScale = 1;
                    File.WriteAllText(Dir + "dziennik.txt", _log.ToString());
                    EditorApplication.update -= Tick; Object.Destroy(_cam.gameObject);
                    Debug.Log("[Mleczarka] Podgląd skrzydła gotowy: " + Dir);
                }
            }
        }
        static double _next, _hands;

        static void Shot(string name, Vector3 pos, Vector3 look, float fov)
        {
            _cam.transform.position = pos; _cam.transform.LookAt(look); _cam.fieldOfView = fov;
            var rt = new RenderTexture(1280, 720, 24); _cam.targetTexture = rt;
            // podgląd z góry: ściany i dach na chwilę schowane (w grze chowają się tylko zasłaniające gracza)
            var shell = Object.FindObjectsByType<FactoryShellVisibility>(FindObjectsSortMode.None).SelectMany(v => v.shell).Where(r => r).ToList();
            var was = shell.Select(r => r.enabled).ToList(); foreach (var r in shell) r.enabled = false;
            _cam.Render(); _cam.Render();
            for (int i = 0; i < shell.Count; i++) shell[i].enabled = was[i];
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            File.WriteAllBytes(Dir + name + ".jpg", tex.EncodeToJPG(85));
            RenderTexture.active = prev; _cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(tex);
        }
    }
}
