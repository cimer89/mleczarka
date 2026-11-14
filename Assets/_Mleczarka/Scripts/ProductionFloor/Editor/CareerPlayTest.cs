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
    /// Kariera w grze: świeża gra (bez zapisu gracza), start w domu, mleko od gospodarza, twaróg w kuchni,
    /// koszyk, rower, sprzedaż na rynku, panel rozwoju. Zrzuty ekranu gry do Screenshots/kariera_klatki.
    /// </summary>
    [InitializeOnLoad]
    public static class CareerPlayTest
    {
        const string Key = "mleczarka_career_playtest", Dir = "Screenshots/kariera_klatki/";
        static double _t0, _next; static int _step; static StringBuilder _log;

        static CareerPlayTest()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                {
                    SessionState.SetBool(Key, false);
                    _t0 = EditorApplication.timeSinceStartup; _step = 0; _next = 3; _log = new StringBuilder();
                    if (Directory.Exists(Dir)) foreach (var f in Directory.GetFiles(Dir)) File.Delete(f);
                    Directory.CreateDirectory(Dir);
                    EditorApplication.update += Tick;
                }
            };
        }

        [MenuItem("Mleczarka/Kariera/3. Test kariery w grze (Play, bez zapisu)")]
        static void Run()
        {
            if (Application.isPlaying) { Debug.LogWarning("[Mleczarka] Zatrzymaj tryb gry i uruchom test jeszcze raz."); return; }
            PlayerPrefs.SetInt("mleczarka_test_mode", (int)GameMode.Kariera); PlayerPrefs.Save();
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        static void Teleport(Vector3 p, Quaternion? r = null)
        {
            var pl = GameController.Instance.player; var cc = pl.GetComponent<CharacterController>();
            if (cc) cc.enabled = false; pl.transform.position = p; if (r.HasValue) pl.transform.rotation = r.Value; if (cc) cc.enabled = true;
        }

        static void Shot(string name) { ScreenCapture.CaptureScreenshot(Dir + name + ".png"); _log.AppendLine("zrzut " + name); }

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - _t0;
            if (t < _next) return;
            var g = GameController.Instance; if (!g) return;
            var p = g.Sim.S.players[GameController.Me]; var c = p.career;
            var home = Object.FindFirstObjectByType<CareerHome>();
            var menu = Object.FindFirstObjectByType<Mleczarka.UI.StartMenuUI>();
            Result r;
            switch (_step++)
            {
                case 0: if (menu) menu.CloseMenu(); _log.AppendLine($"tryb: {g.Sim.S.mode}, kariera: {CareerSim.Active(p)}, pieniądze {p.money}"); _next = t + 2; break;
                case 1: Shot("01_start_w_domu"); _next = t + 1; break;
                case 2:
                {
                    var farm = WorldPlace.All.Where(w => w && w.kind == PlaceKind.Gospodarstwo && w.farmId >= 0).OrderBy(w => Vector3.Distance(w.transform.position, g.player.transform.position)).First();
                    Teleport(farm.transform.position + Vector3.up * 0.5f);
                    _next = t + 1.5; break;
                }
                case 3: Shot("02_gospodarstwo"); r = g.Send(new BuyMilkCan(GameController.Me, g.PlaceOf(GameController.Me).FarmId)); _log.AppendLine("kupno mleka: " + r.Msg); _next = t + 1; break;
                case 4: { var sp = WorldPlace.All.First(w => w && w.kind == PlaceKind.Dom).transform.Find("Start gracza"); Teleport(sp.position, sp.rotation); _next = t + 1; break; }
                case 5: r = g.Send(new PourMilk(GameController.Me)); _log.AppendLine("lodówka: " + r.Msg); _next = t + 0.5; break;
                case 6:
                {
                    var st = home.stations.First(s => s.id == "kuchenka");
                    Teleport(st.operatorPoint.position + Vector3.up * 0.1f, Quaternion.LookRotation(Vector3.ProjectOnPlane(st.anchor.position - st.operatorPoint.position, Vector3.up)));
                    _next = t + 1.2; break;
                }
                case 7: Shot("03_kuchenka_panel"); r = g.Send(new StartKitchen(GameController.Me, "twarog", 2)); _log.AppendLine("start: " + r.Msg); _next = t + 0.5; break;
                case 8:
                {
                    var j = CareerSim.JobAt(p, "kuchenka");
                    if (j != null) { if (CareerSim.NeedsHands(j)) g.SendHold(new KitchenAct(GameController.Me, "kuchenka")); _step--; _next = t + 0.25; if (j.step == 1 && !File.Exists(Dir + "04_gotowanie.png")) Shot("04_gotowanie"); break; }
                    _log.AppendLine($"twaróg w spiżarce: {c.pantry[(int)Product.Twarog]}"); _next = t + 0.5; break;
                }
                case 9: r = g.Send(new LoadBasket(GameController.Me)); _log.AppendLine("koszyk: " + r.Msg); _next = t + 0.5; break;
                case 10: { var sp = WorldPlace.All.First(w => w && w.kind == PlaceKind.Dom).transform.Find("Start gracza"); Teleport(home.bike.position + home.bike.right * -0.8f); home.ToggleBike(); _next = t + 1.5; break; }
                case 11: Shot("05_na_rowerze"); _log.AppendLine("na rowerze: " + g.player.OnBike); _next = t + 0.5; break;
                case 12: { var m = WorldPlace.All.First(w => w && w.kind == PlaceKind.Rynek); home.ToggleBike(); Teleport(m.transform.position + Vector3.up * 0.5f); _next = t + 1.5; break; }
                case 13: Shot("06_rynek"); r = g.Send(new SellBasket(GameController.Me)); _log.AppendLine("rynek: " + r.Msg + $" · pieniądze {p.money}"); _next = t + 1; break;
                case 14: home.ToggleShop(true); _next = t + 1; break;
                case 15: Shot("07_rozwoj"); home.ToggleShop(false); _next = t + 1; break;
                default:
                    File.WriteAllText(Dir + "dziennik.txt", _log.ToString());
                    EditorApplication.update -= Tick;
                    Debug.Log("[Mleczarka] Test kariery w grze zakończony: " + Dir);
                    break;
            }
        }
    }
}
