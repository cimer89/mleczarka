using System.IO;
using System.Linq;
using System.Text;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>Test w grze (kariera, bez zapisu): klienci na rynku, towar z koszyka do ręki, podanie klientowi; placówki z szyldem i odbiorcą. Zrzuty: Screenshots/rynek/</summary>
    [InitializeOnLoad]
    public static class MarketPlayTest
    {
        const string TestKey = "mleczarka_market_test", Dir = "Screenshots/rynek/";
        static double _t0, _next; static int _step; static StringBuilder _log;

        static MarketPlayTest()
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

        [MenuItem("Mleczarka/Kariera/5. Test sprzedaży z ręki i placówek (Play)")]
        static void Run()
        {
            if (Application.isPlaying) return;
            PlayerPrefs.SetInt("mleczarka_test_mode", (int)GameMode.Kariera); PlayerPrefs.Save();
            SessionState.SetBool(TestKey, true);
            EditorApplication.isPlaying = true;
        }

        static void Teleport(Vector3 p, float yaw)
        {
            var pl = GameController.Instance.player; var cc = pl.GetComponent<CharacterController>();
            if (cc) cc.enabled = false; pl.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0)); if (cc) cc.enabled = true;
            if (pl.orbital) { pl.orbital.HorizontalAxis.Value = yaw; pl.orbital.VerticalAxis.Value = 14f; }
        }

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - _t0; if (t < _next) return;
            var g = GameController.Instance; if (!g) return;
            try { Step(t, g); }
            catch (System.Exception e) { _log.AppendLine("WYJĄTEK krok " + _step + ": " + e.Message); _step = 99; _next = t; }
        }

        static void Step(double t, GameController g)
        {
            var p = g.Sim.S.players[GameController.Me]; var c = p.career;
            var hs = Object.FindFirstObjectByType<HandSales>(); var home = Object.FindFirstObjectByType<CareerHome>();
            switch (_step++)
            {
                case 0:
                {
                    var m = Object.FindFirstObjectByType<Mleczarka.UI.StartMenuUI>(); if (m) m.CloseMenu();
                    c.basket[(int)Product.Twarog] = 2; c.basket[(int)Product.MlekoPast] = 2; c.basket[(int)Product.Smietana] = 2;
                    var s = hs.marketSpots[0];
                    // rower i gracz przy rynku, kilka metrów od klientów
                    home.bike.position = s.position + s.forward * 5f + s.right * 2.5f; home.bike.rotation = Quaternion.LookRotation(s.right);
                    Teleport(home.bike.position - s.right * 1.2f + Vector3.up * 0.2f, Quaternion.LookRotation(s.position - home.bike.position).eulerAngles.y);
                    _log.AppendLine($"klientów miejsc: {hs.marketSpots.Length}, odbiorców: {hs.receivers.Length}");
                    _next = t + 9; break;   // klienci podchodzą
                }
                case 1: ScreenCapture.CaptureScreenshot(Dir + "01_klienci.png"); _log.AppendLine("podpowiedź przy rowerze: " + g.ContextHint()); _next = t + 0.5; break;
                case 2: g.DoContextAction(); _log.AppendLine($"w ręku: {(c.hand >= 0 ? GameData.P((Product)c.hand).Name : "nic")}, koszyk {c.basket.Sum()}"); _next = t + 0.5; break;
                case 3:
                {
                    // podejdź do klienta od strony placu: start 6 m przed nim, potem krokami z kolizjami (jak chód) aż do lady
                    var cust = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(a => a.name == "Klient na rynku" && hs.marketSpots.Any(sp => Vector3.Distance(sp.position, a.position) < 1.0f)).OrderBy(x => Vector3.Distance(x.position, g.player.transform.position)).ToList();
                    _log.AppendLine($"klientów przy straganach: {cust.Count}, zasięg podania {hs.customerReach:0.0} m");
                    var cc = g.player.GetComponent<CharacterController>();
                    foreach (var x in cust)
                    {
                        var start = x.position + x.forward * 6f + Vector3.up * 0.3f;
                        Teleport(start, Quaternion.LookRotation(-x.forward).eulerAngles.y);
                        Physics.SyncTransforms();
                        float walked = 0; string hint = "";
                        for (int i = 0; i < 120; i++)
                        {
                            var to = x.position - g.player.transform.position; to.y = 0;
                            var before = g.player.transform.position;
                            cc.Move(to.normalized * 0.05f + Vector3.down * 0.02f); walked += 0.05f;
                            hint = g.ContextHint() ?? "";
                            if (hint.StartsWith("F — podaj") || (g.player.transform.position - before).sqrMagnitude < 0.0001f) break;
                        }
                        var d = x.position - g.player.transform.position; d.y = 0;
                        _log.AppendLine($"podejście do klienta: odległość {d.magnitude:0.00} m, przed klientem: {Vector3.Dot(-d, x.forward) > 0}, podpowiedź: {hint}");
                        if (hint.StartsWith("F — podaj")) break;
                    }
                    _next = t + 1; break;
                }
                case 4: _log.AppendLine("podpowiedź przy kliencie: " + g.ContextHint()); ScreenCapture.CaptureScreenshot(Dir + "02_podanie.png"); _next = t + 0.5; break;
                case 5: { double m = p.money; g.DoContextAction(); _log.AppendLine($"sprzedaż: pieniądze {m} → {p.money}, w ręku {c.hand}"); _next = t + 2; break; }
                case 6: ScreenCapture.CaptureScreenshot(Dir + "03_po_sprzedazy.png"); _next = t + 0.5; break;
                case 7:
                {
                    var r = hs.receivers[0]; var pos = r.point.position + r.point.forward * 3.0f + r.point.right * 3.5f;
                    Teleport(pos + Vector3.up * 0.2f, Quaternion.LookRotation(r.point.position - pos).eulerAngles.y);
                    _next = t + 1.5; break;
                }
                case 8: ScreenCapture.CaptureScreenshot(Dir + "04_placowka.png"); _log.AppendLine($"placówka: {hs.receivers[0].name}, miejsce gry: {g.PlaceOf(GameController.Me).Kind}"); _next = t + 0.5; break;
                default:
                    File.WriteAllText("Screenshots/rynek_test.txt", _log.ToString());
                    EditorApplication.update -= Tick; Debug.Log("[Mleczarka] Test rynku: Screenshots/rynek_test.txt");
                    break;
            }
        }
    }
}
