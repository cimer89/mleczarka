using System.IO;
using System.Linq;
using System.Text;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>
    /// Test w grze (bez zapisu gracza), tryb swobodny: rower przy domu — E wsiada i zsiada, jazda do przodu;
    /// skok — stan animatora w kolejnych klatkach (odbicie → powietrze → chód, bez stania w lądowaniu w biegu).
    /// Dziennik: Screenshots/rower_skok.txt.
    /// </summary>
    [InitializeOnLoad]
    public static class BikeJumpPlayTest
    {
        const string TestKey = "mleczarka_bikejump_test";
        static double _t0, _next; static int _step; static StringBuilder _log; static Vector3 _p0; static string _last; static double _phase; static int _airFrames, _landFrames, _frames;

        static BikeJumpPlayTest()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(TestKey, false))
                {
                    SessionState.SetBool(TestKey, false);
                    _t0 = EditorApplication.timeSinceStartup; _step = 0; _next = 3; _log = new StringBuilder();
                    EditorApplication.update += Tick;
                }
            };
        }

        [MenuItem("Mleczarka/Testy/Rower i skok (Play, bez zapisu)")]
        static void Run()
        {
            if (Application.isPlaying) { Debug.LogWarning("[Mleczarka] Zatrzymaj tryb gry i uruchom test jeszcze raz."); return; }
            PlayerPrefs.SetInt("mleczarka_test_mode", (int)GameMode.Swobodna); PlayerPrefs.Save();
            SessionState.SetBool(TestKey, true);
            EditorApplication.isPlaying = true;
        }

        static void Teleport(Vector3 p, Quaternion? r = null)
        {
            var pl = GameController.Instance.player; var cc = pl.GetComponent<CharacterController>();
            if (cc) cc.enabled = false; pl.transform.position = p; if (r.HasValue) pl.transform.rotation = r.Value; if (cc) cc.enabled = true;
        }

        static string AnimState()
        {
            var a = GameController.Instance.player.GetComponentInChildren<Animator>(); if (!a) return "-";
            var info = a.GetCurrentAnimatorStateInfo(0);
            var st = ((UnityEditor.Animations.AnimatorController)a.runtimeAnimatorController).layers[0].stateMachine.states.Select(x => x.state).FirstOrDefault(x => x.nameHash == info.shortNameHash);
            return (st ? st.name : info.shortNameHash.ToString()) + (a.IsInTransition(0) ? "*" : "");
        }

        static void Press(params Key[] keys)
        {
            if (Keyboard.current == null) return;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
        }

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - _t0;
            if (t < _next) return;
            var g = GameController.Instance; if (!g) return;
            var home = Object.FindFirstObjectByType<CareerHome>();
            var menu = Object.FindFirstObjectByType<Mleczarka.UI.StartMenuUI>();
            var pl = g.player;
            switch (_step++)
            {
                case 0: if (menu) menu.CloseMenu(); _log.AppendLine($"tryb: {g.Sim.S.mode}, kariera: {CareerSim.Active(g.Sim.S.players[GameController.Me])}, dom: {(bool)home}"); _next = t + 1; break;
                case 1: Teleport(home.bike.position - home.bike.right * 1.0f + Vector3.up * 0.3f); _next = t + 1; break;
                case 2: pl.EnterOrExit(); _log.AppendLine("E przy rowerze → na rowerze: " + pl.OnBike); _p0 = pl.transform.position; _next = t + 0.5; break;
                case 3: ScreenCapture.CaptureScreenshot("Screenshots/rower_tryb_swobodny.png"); _next = t + 0.5; break;
                case 4: pl.EnterOrExit(); _log.AppendLine("E na rowerze → zsiadł: " + !pl.OnBike + ", rower stoi: " + home.bike.gameObject.activeInHierarchy); _next = t + 0.5; break;
                case 5: pl.EnterOrExit(); _log.AppendLine("E jeszcze raz → na rowerze: " + pl.OnBike); pl.EnterOrExit(); _next = t + 0.5; break;
                case 6: { if (pl.OnBike) pl.EnterOrExit(); _log.AppendLine("pieszo: " + !pl.OnBike); var r = WorldPlace.All.First(w => w && w.kind == PlaceKind.Rynek); Teleport(r.transform.position + Vector3.up * 0.3f, Quaternion.identity); _next = t + 1; break; }
                // bieg po terenie 2 s: ile klatek animator jest w powietrzu / lądowaniu bez skoku
                case 7: Press(Key.W, Key.LeftShift); _phase = t; _airFrames = _landFrames = _frames = 0; _next = t; _step = 70; break;
                case 70:
                {
                    Press(Key.W, Key.LeftShift); string st = AnimState(); _frames++;
                    if (st.StartsWith("W powietrzu") || st.StartsWith("Skok")) _airFrames++; if (st.StartsWith("Lądowanie")) _landFrames++;
                    if (t - _phase < 2.5) { _step = 70; _next = t + 0.02; break; }
                    _log.AppendLine($"bieg bez skoku: {_frames} klatek, w pozie skoku {_airFrames}, w lądowaniu {_landFrames}");
                    _phase = t; _last = null; _log.Append("skok w biegu:"); _step = 71; _next = t; break;
                }
                case 71:
                {
                    bool space = t - _phase < 0.1; if (space) Press(Key.W, Key.LeftShift, Key.Space); else Press(Key.W, Key.LeftShift);
                    string st = AnimState(); if (st != _last) { _log.Append($" {t - _phase:0.00}s {st} (y {pl.transform.position.y:0.00});"); _last = st; }
                    if (t - _phase < 2.0) { _step = 71; _next = t + 0.02; break; }
                    _log.AppendLine(); Press(); _phase = t; _last = null; _log.Append("skok w miejscu:"); _step = 72; _next = t + 0.8; break;
                }
                case 72:
                {
                    bool space = t - _phase > 0.8 && t - _phase < 0.9; if (space) Press(Key.Space); else Press();
                    string st = AnimState(); if (st != _last) { _log.Append($" {t - _phase:0.00}s {st};"); _last = st; }
                    if (t - _phase < 2.8) { _step = 72; _next = t + 0.02; break; }
                    _log.AppendLine(); Press(); _step = 99; _next = t; break;
                }
                default:
                    _log.AppendLine("stan animatora na końcu: " + AnimState());
                    File.WriteAllText("Screenshots/rower_skok.txt", _log.ToString());
                    EditorApplication.update -= Tick;
                    Debug.Log("[Mleczarka] Test roweru i skoku zakończony: Screenshots/rower_skok.txt");
                    break;
            }
        }
    }
}
