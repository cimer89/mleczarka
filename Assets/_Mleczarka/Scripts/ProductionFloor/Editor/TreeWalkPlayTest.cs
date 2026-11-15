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
    /// <summary>Test w grze (bez zapisu): postać idzie prosto na kilka grubych drzew — ile minimalnie dzieli ją od osi pnia. Dziennik: Screenshots/drzewa_test.txt</summary>
    [InitializeOnLoad]
    public static class TreeWalkPlayTest
    {
        const string TestKey = "mleczarka_tree_walk";
        static double _t0, _next, _phase; static int _step, _tree; static StringBuilder _log; static float _minD; static Vector3 _axis; static float _r;
        static (Vector3 pos, float r, string name)[] _targets;

        static TreeWalkPlayTest()
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

        [MenuItem("Mleczarka/Testy/Drzewa: czy da się przejść przez pień (Play)")]
        static void Run()
        {
            if (Application.isPlaying) return;
            PlayerPrefs.SetInt("mleczarka_test_mode", (int)GameMode.Swobodna); PlayerPrefs.Save();
            SessionState.SetBool(TestKey, true);
            EditorApplication.isPlaying = true;
        }

        static void Press(params Key[] k) { if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k)); }

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - _t0; if (t < _next) return;
            var g = GameController.Instance; if (!g) return; var pl = g.player;
            try { Step(t, g, pl); }
            catch (System.Exception e) { _log.AppendLine("WYJĄTEK krok " + _step + ": " + e); _step = 9; _next = t; Step(t, g, pl); }
        }

        static void Step(double t, GameController g, Mleczarka.Player.FarmerController pl)
        {
            switch (_step)
            {
                case 0:
                {
                    var m = Object.FindFirstObjectByType<Mleczarka.UI.StartMenuUI>(); if (m) m.CloseMenu();
                    // największe pnie każdego rodzaju w terenie z dużą liczbą drzew
                    var tr = Terrain.activeTerrains.OrderByDescending(x => x.terrainData.treeInstances.Length).First(); var td = tr.terrainData;
                    _targets = td.treeInstances.GroupBy(i => i.prototypeIndex).Select(gr =>
                    {
                        var ti = gr.OrderByDescending(i => i.widthScale).Skip(gr.Count() / 2).First();
                        var pf = td.treePrototypes[ti.prototypeIndex].prefab; var col = pf.GetComponent<CapsuleCollider>();
                        var pos = Vector3.Scale(ti.position, td.size) + tr.transform.position;
                        return (pos, col ? col.radius * ti.widthScale : 0f, pf.name);
                    }).ToArray();
                    _tree = 0; _step = 1; _next = t + 1; break;
                }
                case 1:
                {
                    if (_tree >= _targets.Length) { _step = 9; _next = t; break; }
                    var (pos, r, name) = _targets[_tree];
                    var dir = Quaternion.Euler(0, _tree * 47f, 0) * Vector3.forward;
                    var start = pos - dir * (r + 4f); var trn = Terrain.activeTerrains.FirstOrDefault(x => { var o = x.transform.position; var s = x.terrainData.size; return start.x >= o.x && start.z >= o.z && start.x <= o.x + s.x && start.z <= o.z + s.z; });
                    start.y = (trn ? trn.SampleHeight(start) + trn.transform.position.y : pos.y) + 0.2f;
                    var cc = pl.GetComponent<CharacterController>(); cc.enabled = false;
                    pl.transform.SetPositionAndRotation(start, Quaternion.LookRotation(dir)); cc.enabled = true;
                    if (pl.orbital) pl.orbital.HorizontalAxis.Value = Quaternion.LookRotation(dir).eulerAngles.y;
                    _minD = 999; _axis = pos; _r = r; _phase = t; _step = 2; _next = t + 0.3; break;
                }
                case 2:
                {
                    Press(Key.W);
                    var p = pl.transform.position; _minD = Mathf.Min(_minD, new Vector2(p.x - _axis.x, p.z - _axis.z).magnitude);
                    if (t - _phase < 3.0) { _next = t + 0.02; break; }
                    Press();
                    var (pos, r, name) = _targets[_tree];
                    bool through = _minD < 0.3f;
                    _log.AppendLine($"{name}: promień kolizji {r:0.00} m, najbliżej osi pnia {_minD:0.00} m → {(through ? "PRZESZEDŁ PRZEZ PIEŃ" : "zatrzymany")}");
                    if (_tree == 0) ScreenCapture.CaptureScreenshot("Screenshots/drzewo_test.png");
                    _tree++; _step = 1; _next = t + 0.3; break;
                }
                default:
                    File.WriteAllText("Screenshots/drzewa_test.txt", _log.ToString());
                    EditorApplication.update -= Tick; Debug.Log("[Mleczarka] Test drzew: Screenshots/drzewa_test.txt");
                    break;
            }
        }
    }
}
