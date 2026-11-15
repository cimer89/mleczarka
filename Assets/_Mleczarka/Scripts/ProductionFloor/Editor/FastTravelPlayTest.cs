using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>Test w grze (kariera, bez zapisu): szybki transport z mapy pieszo i rowerem, przeskok czasu zależny od środka transportu. Zrzuty: Screenshots/transport/</summary>
    [InitializeOnLoad]
    public static class FastTravelPlayTest
    {
        const string TestKey = "mleczarka_fasttravel_test", Dir = "Screenshots/transport/";
        static double _t0, _next; static int _step; static StringBuilder _log; static double _clock;

        static FastTravelPlayTest()
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

        [MenuItem("Mleczarka/Kariera/8. Test szybkiego transportu (Play)")]
        static void Run()
        {
            if (Application.isPlaying) return;
            PlayerPrefs.SetInt("mleczarka_test_mode", (int)GameMode.Kariera); PlayerPrefs.Save();
            SessionState.SetBool(TestKey, true);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - _t0; if (t < _next) return;
            var g = GameController.Instance; if (!g) return;
            try { Step(t, g); }
            catch (System.Exception e) { _log.AppendLine("WYJĄTEK krok " + _step + ": " + e); _step = 99; _next = t; }
        }

        static object Map() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(m => m.GetType().Name == "MapUI");
        static void Call(object o, string name, params object[] a) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(o, a);
        static string Clock(double t) { double m = t * 1440.0 / GameConfig.DaySeconds; int day = (int)(m / 1440) + 1; m %= 1440; return $"dzień {day} {(int)(m / 60):00}:{(int)(m % 60):00}"; }

        static void Step(double t, GameController g)
        {
            var S = g.Sim.S; var home = Object.FindFirstObjectByType<CareerHome>();
            WorldPlace Place(string name) => WorldPlace.All.First(w => w && w.displayName == name);
            switch (_step++)
            {
                case 0:
                {
                    var m = Object.FindFirstObjectByType<Mleczarka.UI.StartMenuUI>(); if (m) m.CloseMenu();
                    var rynek = WorldPlace.All.First(w => w && w.kind == PlaceKind.Rynek);
                    // porównanie przeskoku dla tej samej trasy (dom → rynek)
                    float d = FastTravel.Distance(g, rynek) * FastTravel.RoadFactor;
                    var veh = Object.FindFirstObjectByType<Mleczarka.Vehicles.WheeledVehicle>();
                    float vs = veh ? Mathf.Max(8f, veh.maxSpeedKmh / 3.6f * 0.7f) : 0;
                    _log.AppendLine($"Trasa dom → rynek: {FastTravel.Distance(g, rynek):0} m w linii prostej (po drogach ok. {d:0} m)");
                    _log.AppendLine($"  pieszo: {FastTravel.Duration(d / g.player.runSpeed)}, rowerem: {FastTravel.Duration(d / g.player.bikeSpeed)}, pojazdem ({(veh ? veh.name : "-")}, {(veh ? veh.maxSpeedKmh : 0):0} km/h): {(veh ? FastTravel.Duration(d / vs) : "-")}");
                    // panel na mapie
                    var map = Map(); Call(map, "SetBig", true); Call(map, "SelectTravel", rynek);
                    _next = t + 1; break;
                }
                case 1: ScreenCapture.CaptureScreenshot(Dir + "01_mapa_panel.png"); _next = t + 0.5; break;
                case 2:
                {
                    _clock = S.t; var rynek = WorldPlace.All.First(w => w && w.kind == PlaceKind.Rynek);
                    Call(Map(), "DoTravel");
                    var d = rynek.transform.position - g.PlayerPosition; d.y = 0;
                    _log.AppendLine($"Pieszo → rynek: czas {Clock(_clock)} → {Clock(S.t)} (+{FastTravel.Duration(S.t - _clock)}), odległość od celu {d.magnitude:0.0} m, mapa zamknięta: {!Mleczarka.UI.MapUI.BigMapOpen}, ruch odblokowany: {!g.player.InputBlocked}");
                    _next = t + 2; break;
                }
                case 3: ScreenCapture.CaptureScreenshot(Dir + "02_pieszo_na_rynku.png"); _next = t + 0.5; break;
                case 4:
                {
                    // rower podstawiony obok i wsiadamy
                    var pl = g.player.transform;
                    home.bike.position = pl.position + pl.right * 1.0f; home.bike.rotation = pl.rotation;
                    home.ToggleBike();
                    _log.AppendLine($"na rowerze: {g.player.OnBike}");
                    _next = t + 1; break;
                }
                case 5:
                {
                    _clock = S.t; var farm = Place("Wiśniewscy");
                    var r = FastTravel.Go(g, farm);
                    var d = farm.transform.position - g.PlayerPosition; d.y = 0;
                    _log.AppendLine($"Rowerem → Wiśniewscy: {r.Msg} | czas +{FastTravel.Duration(S.t - _clock)}, od celu {d.magnitude:0.0} m, rower pod graczem: {(home.bike && home.bike.IsChildOf(g.player.transform))}");
                    _next = t + 2; break;
                }
                case 6: ScreenCapture.CaptureScreenshot(Dir + "03_rowerem_u_gospodarzy.png"); _next = t + 0.5; break;
                case 7:
                {
                    var r = FastTravel.Go(g, Place("Wiśniewscy"));
                    _log.AppendLine($"Ponownie do tego samego miejsca: {(r.Ok ? "BŁĄD — pozwolił" : "odmowa: " + r.Msg)}");
                    _next = t + 0.5; break;
                }
                default:
                    File.WriteAllText("Screenshots/transport_test.txt", _log.ToString());
                    EditorApplication.update -= Tick; Debug.Log("[Mleczarka] Test transportu: Screenshots/transport_test.txt");
                    break;
            }
        }
    }
}
