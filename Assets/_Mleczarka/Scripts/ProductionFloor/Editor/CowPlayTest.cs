using System.IO;
using System.Linq;
using System.Text;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>Test w grze (kariera, bez zapisu): dojenie własnej krowy przy domu, krowy przy gospodarstwie, zakup w nowym miejscu skupu. Zrzuty: Screenshots/krowy/</summary>
    [InitializeOnLoad]
    public static class CowPlayTest
    {
        const string TestKey = "mleczarka_cow_test", Dir = "Screenshots/krowy/";
        static double _t0, _next; static int _step; static StringBuilder _log;

        static CowPlayTest()
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

        [MenuItem("Mleczarka/Kariera/7. Test krów i miejsc skupu (Play)")]
        static void Run()
        {
            if (Application.isPlaying) return;
            PlayerPrefs.SetInt("mleczarka_test_mode", (int)GameMode.Kariera); PlayerPrefs.Save();
            SessionState.SetBool(TestKey, true);
            EditorApplication.isPlaying = true;
        }

        static void Teleport(Vector3 p, float yaw, float pitch = 14f)
        {
            var pl = GameController.Instance.player; var cc = pl.GetComponent<CharacterController>();
            if (cc) cc.enabled = false; pl.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0)); if (cc) cc.enabled = true;
            if (pl.orbital) { pl.orbital.HorizontalAxis.Value = yaw; pl.orbital.VerticalAxis.Value = pitch; }
        }
        static float Yaw(Vector3 from, Vector3 to) { var d = to - from; d.y = 0; return Quaternion.LookRotation(d).eulerAngles.y; }
        static Vector3 Ground(Vector3 p) => Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var h, 60f, ~(1 << 2), QueryTriggerInteraction.Ignore) ? h.point : p;

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
            var cows = Object.FindObjectsByType<Cow>(FindObjectsSortMode.None);
            var own = cows.FirstOrDefault(x => x.own);
            switch (_step++)
            {
                case 0:
                {
                    var m = Object.FindFirstObjectByType<Mleczarka.UI.StartMenuUI>(); if (m) m.CloseMenu();
                    _log.AppendLine($"krów w świecie: {cows.Length}, własna: {(own ? own.name : "BRAK")}, gospodarstw: {g.Sim.S.farms.Count}");
                    var pos = Ground(own.transform.position + own.transform.right * 2.2f + own.transform.forward * 0.4f);
                    Teleport(pos + Vector3.up * 0.2f, Yaw(pos, own.transform.position));
                    _next = t + 2; break;
                }
                case 1: _log.AppendLine("podpowiedź przy krowie: " + g.ContextHint()); ScreenCapture.CaptureScreenshot(Dir + "01_wlasna_krowa.png"); _next = t + 0.5; break;
                case 2:
                {
                    double money = p.money, fr = c.fridge;
                    g.DoContextAction();
                    _log.AppendLine($"dojenie: lodówka {fr:0} → {c.fridge:0} l, bańka {c.canister:0} l, pieniądze {money} → {p.money}, w krowie zostało {c.cowMilk:0.0} l");
                    _log.AppendLine("podpowiedź po wydojeniu: " + g.ContextHint());
                    _next = t + 1; break;
                }
                case 3:
                {
                    // gospodarstwo z 3 krowami (Wiśniewscy): widok od furtki na ogrodzone pastwisko
                    var grp = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(x => x.name.StartsWith("Krowy: Wiśniewscy"));
                    var cw = grp.GetComponentsInChildren<Cow>(); var mid = cw[0].hasArea ? cw[0].areaCenter : cw.Aggregate(Vector3.zero, (a, x) => a + x.transform.position) / cw.Length;
                    var gate = grp.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == "Furtka");
                    var from = gate ? gate.position + (gate.position - mid).normalized * 7f + Vector3.Cross(Vector3.up, (gate.position - mid).normalized) * 3f : mid + new Vector3(7f, 0, -7f);
                    var pos = Ground(from);
                    Teleport(pos + Vector3.up * 0.2f, Yaw(pos, mid), 12f);
                    _log.AppendLine($"Wiśniewscy: furtka {(gate ? "jest" : "BRAK")}, segmentów płotu {grp.GetComponentsInChildren<Transform>().Count(x => x.name.StartsWith("Płot "))}");
                    _next = t + 2.5; break;
                }
                case 4:
                {
                    ScreenCapture.CaptureScreenshot(Dir + "02_gospodarstwo_3_krowy.png");
                    // gracz 1,8 m przed furtką, od strony podwórza — furtka powinna się otworzyć
                    var grp = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(x => x.name.StartsWith("Krowy: Wiśniewscy"));
                    var gate = grp.GetComponentInChildren<PastureGate>(); var cow = grp.GetComponentInChildren<Cow>();
                    if (gate && cow)
                    {
                        var outDir = gate.transform.position - cow.areaCenter; outDir.y = 0; outDir.Normalize();
                        var p2 = Ground(gate.transform.position + outDir * 1.8f);
                        Teleport(p2 + Vector3.up * 0.2f, Yaw(p2, cow.areaCenter), 18f);
                    }
                    _next = t + 1.5; break;
                }
                case 5:
                {
                    var gate = Object.FindObjectsByType<PastureGate>(FindObjectsSortMode.None).OrderBy(x => Vector3.Distance(x.transform.position, g.player.transform.position)).FirstOrDefault();
                    if (gate && gate.leaf) _log.AppendLine($"furtka przy graczu ({Vector3.Distance(gate.transform.position, g.player.transform.position):0.0} m): skrzydło obrócone o {Quaternion.Angle(gate.leaf.localRotation, Quaternion.identity):0}°");
                    ScreenCapture.CaptureScreenshot(Dir + "02b_furtka_otwarta.png");
                    _step = 50; _next = t + 0.5; break;
                }
                case 50:
                {
                    // nowe miejsce skupu: Bielscy — wjazd do strefy, zakup do bańki
                    var z = WorldPlace.All.First(w => w && w.kind == PlaceKind.Gospodarstwo && w.displayName == "Bielscy");
                    var grp = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(x => x.name.StartsWith("Krowy: Bielscy"));
                    var cowPos = grp && grp.childCount > 0 ? grp.GetChild(0).position : z.transform.position;
                    var pos = Ground(z.transform.position);
                    Teleport(pos + Vector3.up * 0.2f, Yaw(pos, cowPos), 10f);
                    c.canister = 0; p.money = 60;
                    _next = t + 2; break;
                }
                case 51:
                {
                    _log.AppendLine($"miejsce: {g.PlaceOf(GameController.Me).Kind} {g.PlaceOf(GameController.Me).FarmId}, podpowiedź: {g.ContextHint()}");
                    ScreenCapture.CaptureScreenshot(Dir + "03_nowe_miejsce_skupu.png");
                    _next = t + 0.5; break;
                }
                case 52: { double m = p.money; g.DoContextAction(); _log.AppendLine($"zakup: bańka {c.canister:0} l, pieniądze {m} → {p.money}"); _next = t + 6; break; }
                case 53:
                {
                    var a = own.GetComponentInChildren<Animator>();
                    var clip = a && a.runtimeAnimatorController && a.GetCurrentAnimatorClipInfo(0).Length > 0 ? a.GetCurrentAnimatorClipInfo(0)[0].clip.name : "-";
                    int walking = cows.Count(x => x && x.GetComponentInChildren<Animator>() && x.GetComponentInChildren<Animator>().runtimeAnimatorController && x.GetComponentInChildren<Animator>().GetBool("Walk"));
                    _log.AppendLine($"animator własnej krowy: {(a && a.runtimeAnimatorController ? a.runtimeAnimatorController.name : "brak")}, klip: {clip}; krów w ruchu teraz: {walking}/{cows.Length}");
                    _next = t + 0.5; break;
                }
                default:
                    File.WriteAllText("Screenshots/krowy_test.txt", _log.ToString());
                    EditorApplication.update -= Tick; Debug.Log("[Mleczarka] Test krów: Screenshots/krowy_test.txt");
                    break;
            }
        }
    }
}
