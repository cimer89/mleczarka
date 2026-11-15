using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using Mleczarka.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>Test w grze (kariera, bez zapisu): panel kuchni z podpowiedzią, mieszanie łyżką, lodówka z wnętrzem, dojenie z przytrzymaniem F. Zrzuty: Screenshots/czynnosci/</summary>
    [InitializeOnLoad]
    public static class ActionPlayTest
    {
        const string TestKey = "mleczarka_action_test", Dir = "Screenshots/czynnosci/";
        static double _t0, _next; static int _step; static StringBuilder _log;

        static ActionPlayTest()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(TestKey, false))
                {
                    SessionState.SetBool(TestKey, false);
                    _t0 = EditorApplication.timeSinceStartup; _step = 0; _next = 3; _log = new StringBuilder();
                    Directory.CreateDirectory(Dir);
                    EditorApplication.update += Tick;
                }
                if (s == PlayModeStateChange.ExitingPlayMode) EditorApplication.update -= Tick;
            };
        }

        [MenuItem("Mleczarka/Kariera/10. Test czynności, lodówki i dojenia (Play)")]
        static void Run()
        {
            if (Application.isPlaying) return;
            PlayerPrefs.SetInt("mleczarka_test_mode", (int)GameMode.Kariera); PlayerPrefs.Save();
            SessionState.SetBool(TestKey, true);
            EditorApplication.isPlaying = true;
        }

        static void Teleport(Vector3 p, float yaw, float pitch = 14f, float radius = -1f)
        {
            var pl = GameController.Instance.player; var cc = pl.GetComponent<CharacterController>();
            if (cc) cc.enabled = false; pl.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0)); if (cc) cc.enabled = true;
            if (pl.orbital) { pl.orbital.HorizontalAxis.Value = yaw; pl.orbital.VerticalAxis.Value = pitch; if (radius > 0) pl.orbital.Radius = radius; }
        }
        static float Yaw(Vector3 from, Vector3 to) { var d = to - from; d.y = 0; return Quaternion.LookRotation(d).eulerAngles.y; }
        static void Keys(params Key[] k) { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k)); }

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - _t0; if (t < _next) return;
            var g = GameController.Instance; if (!g) return;
            try { Step(t, g); }
            catch (System.Exception e) { _log.AppendLine("WYJĄTEK krok " + _step + ": " + e); _step = 99; _next = t; }
        }

        static CareerHome.Station St(CareerHome h, string id) => h.stations.First(s => s.id == id);
        static void Primary(CareerHome h, bool pressed) => typeof(CareerHome).GetMethod("Primary", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(h, new object[] { pressed });
        static string Act(GameController g) { var a = g.player.GetComponentInChildren<Animator>(); return a ? $"Act={a.GetInteger("Act")}, stan={(a.GetCurrentAnimatorClipInfo(0).Length > 0 ? a.GetCurrentAnimatorClipInfo(0)[0].clip.name : "-")}" : "brak animatora"; }

        static void Step(double t, GameController g)
        {
            var p = g.Sim.S.players[GameController.Me]; var c = p.career; var home = Object.FindFirstObjectByType<CareerHome>();
            switch (_step++)
            {
                case 0:
                {
                    var m = Object.FindFirstObjectByType<Mleczarka.UI.StartMenuUI>(); if (m) m.CloseMenu();
                    c.fridge = 2.97; c.cream = 0.5;
                    var s = St(home, "kuchenka"); var op = s.operatorPoint.position;
                    Teleport(op + Vector3.up * 0.1f, Yaw(op, s.anchor.position), 22f, 2.6f);
                    _log.AppendLine($"rekwizyty: {(ActionProps.I ? $"wiadro {(bool)ActionProps.I.bucket}, łyżka {(bool)ActionProps.I.spoon}" : "BRAK obiektu")}");
                    _next = t + 1.5; break;
                }
                case 1: ScreenCapture.CaptureScreenshot(Dir + "01_panel_kuchni.png"); _log.AppendLine("podpowiedź: " + CareerSim.StartBlock(p, CareerData.R("twarog"), 1) + "| lodówka " + CareerSim.L(c.fridge)); _next = t + 0.5; break;
                case 2: Primary(home, true); _log.AppendLine($"start twarogu: zadań {c.jobs.Count}, lodówka {c.fridge:0.00} l, {Act(g)}"); _next = t + 0.8; break;
                case 3: case 4: case 5: Primary(home, false); _next = t + 0.5; break;
                case 6:
                {
                    Primary(home, false);
                    var a = ActionAnim.For(g.player);
                    _log.AppendLine($"mieszanie: IK {a.IKActive}, łyżka widoczna {GameObject.Find("Lyzka (czynność)") != null}, {Act(g)}, krok {c.jobs.FirstOrDefault()?.step}");
                    ScreenCapture.CaptureScreenshot(Dir + "02_mieszanie.png"); _next = t + 0.5; break;
                }
                case 7:
                {
                    var s = St(home, "lodowka"); var op = s.operatorPoint.position;
                    c.fridge = 14; c.cream = 1.2;
                    var side = Vector3.Cross(Vector3.up, (s.anchor.position - op).normalized);
                    Teleport(op + Vector3.up * 0.1f - side * 0.35f, Yaw(op, s.anchor.position) - 25f, 12f, 2.2f);
                    _next = t + 2.2; break;
                }
                case 8: ScreenCapture.CaptureScreenshot(Dir + "03_lodowka.png"); _log.AppendLine($"lodówka: {Act(g)}"); _next = t + 0.5; break;
                case 9:
                {
                    var own = Object.FindObjectsByType<Cow>(FindObjectsSortMode.None).First(x => x.own);
                    c.fridge = 0; c.canister = 0; c.cowMilk = 20;
                    var pos = own.transform.position + own.transform.right * 1.6f; pos.y += 0.3f;
                    Teleport(pos, Yaw(pos, own.transform.position), 15f, 3.2f);
                    _next = t + 1; break;
                }
                case 10: _log.AppendLine("podpowiedź przy krowie: " + g.ContextHint()); g.DoContextAction(); Keys(Key.F); _next = t + 1.0; break;
                case 11: Keys(Key.F); ScreenCapture.CaptureScreenshot(Dir + "04_wiadro.png"); _next = t + 2.0; break;
                case 12:
                {
                    Keys(Key.F);
                    var own = Object.FindObjectsByType<Cow>(FindObjectsSortMode.None).First(x => x.own);
                    _log.AppendLine($"dojenie trwa: {own.Milking}, w wiadrze {own.SessionLiters} l, lodówka {c.fridge} l, {Act(g)}, ruch zablokowany {g.player.InputBlocked}");
                    _log.AppendLine("podpowiedź: " + g.ContextHint());
                    ScreenCapture.CaptureScreenshot(Dir + "05_dojenie.png"); _next = t + 1.5; break;
                }
                case 13: Keys(); _next = t + 1.2; break;
                case 14:
                {
                    var own = Object.FindObjectsByType<Cow>(FindObjectsSortMode.None).First(x => x.own);
                    _log.AppendLine($"po puszczeniu F: dojenie {own.Milking}, lodówka {c.fridge} l, krowie zostało {c.cowMilk:0.0} l, ruch zablokowany {g.player.InputBlocked}, {Act(g)}");
                    _next = t + 0.5; break;
                }
                default:
                    File.WriteAllText("Screenshots/czynnosci_test.txt", _log.ToString());
                    EditorApplication.update -= Tick; Debug.Log("[Mleczarka] Test czynności: Screenshots/czynnosci_test.txt");
                    break;
            }
        }
    }
}
