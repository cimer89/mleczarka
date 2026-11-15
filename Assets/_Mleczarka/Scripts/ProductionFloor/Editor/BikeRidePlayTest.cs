using System.IO;
using System.Text;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>Test jazdy rowerem w grze (bez zapisu): rozpędzanie, zakręt z przechyłem, hamowanie; zrzuty z boku. Dziennik: Screenshots/rower_jazda.txt</summary>
    [InitializeOnLoad]
    public static class BikeRidePlayTest
    {
        const string TestKey = "mleczarka_bike_ride", Dir = "Screenshots/rower_jazda/";
        static double _t0, _next, _phase; static int _step; static StringBuilder _log;

        static BikeRidePlayTest()
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
            };
        }

        [MenuItem("Mleczarka/Testy/Rower: jazda, zakręt, hamowanie (Play)")]
        static void Run()
        {
            if (Application.isPlaying) return;
            PlayerPrefs.SetInt("mleczarka_test_mode", (int)GameMode.Swobodna); PlayerPrefs.Save();
            SessionState.SetBool(TestKey, true);
            EditorApplication.isPlaying = true;
        }

        static void Press(params Key[] k) { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k)); }
        static void Side(float off) { var pl = GameController.Instance.player; if (pl.orbital) { pl.orbital.HorizontalAxis.Value = pl.transform.eulerAngles.y + off; pl.orbital.VerticalAxis.Value = 8f; } }
        static string State() { var pl = GameController.Instance.player; return $"v {pl.CurrentSpeed:0.0} m/s, kierownica {pl.BikeSteer:0}°, przechył {pl.BikeLean:0}°"; }

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - _t0; if (t < _next) return;
            var g = GameController.Instance; if (!g) return; var pl = g.player;
            var home = Object.FindFirstObjectByType<CareerHome>();
            switch (_step)
            {
                case 0: { var m = Object.FindFirstObjectByType<Mleczarka.UI.StartMenuUI>(); if (m) m.CloseMenu();
                    var cc = pl.GetComponent<CharacterController>(); cc.enabled = false; pl.transform.position = home.bike.position - home.bike.right * 1.0f + Vector3.up * 0.2f; cc.enabled = true;
                    _step = 1; _next = t + 1; break; }
                case 1: pl.EnterOrExit(); _log.AppendLine("wsiadł: " + pl.OnBike); Side(0); _phase = t; _step = 2; _next = t + 0.5; break;
                case 2: Press(Key.W); if (t - _phase < 3) { _next = t + 0.02; break; } _log.AppendLine("po 3 s pedałowania: " + State()); { var ik = pl.GetComponentInChildren<Mleczarka.Player.BikeRiderIK>(); _log.AppendLine(ik ? $"IK: wywołań {ik.ikCalls}, {ik.ikStatus}; korekta siodełka {pl.bikeSeatFix}; części: {ik.Names()}" : "IK: brak komponentu"); } Side(75); _step = 3; _phase = t; _next = t; break;
                case 3: Press(Key.W); if (t - _phase < 0.6) { _next = t + 0.02; break; } ScreenCapture.CaptureScreenshot(Dir + "01_jazda_z_boku.png"); _step = 4; _phase = t; _next = t + 0.05; break;
                case 4: Press(Key.W, Key.D); if (t - _phase < 1.5) { _next = t + 0.02; break; } _log.AppendLine("zakręt w prawo: " + State()); Side(180); _step = 5; _phase = t; _next = t; break;
                case 5: Press(Key.W, Key.D); if (t - _phase < 0.5) { _next = t + 0.02; break; } ScreenCapture.CaptureScreenshot(Dir + "02_zakret_przechyl.png"); _step = 6; _phase = t; _next = t + 0.05; break;
                case 6: Press(Key.S); if (t - _phase < 1.5) { _next = t + 0.02; break; } _log.AppendLine("po 1,5 s hamowania: " + State()); Press(); _step = 7; _next = t + 0.3; break;
                case 7: Side(90); _step = 8; _next = t + 0.6; break;
                case 8: ScreenCapture.CaptureScreenshot(Dir + "03_postoj.png"); _step = 9; _next = t + 0.3; break;
                case 9: pl.EnterOrExit(); _log.AppendLine("zsiadł: " + !pl.OnBike); _step = 10; _next = t + 0.3; break;
                default:
                    File.WriteAllText("Screenshots/rower_jazda.txt", _log.ToString());
                    EditorApplication.update -= Tick; Debug.Log("[Mleczarka] Test jazdy rowerem: Screenshots/rower_jazda.txt");
                    break;
            }
        }
    }
}
