using System.Linq;
using System.Text;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using Mleczarka.Vehicles;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.EditorTools
{
    /// <summary>
    /// Test całej pętli w trybie gry, przez te same ścieżki co gracz (DoContextAction = klawisz F, Send = przyciski paneli).
    /// Pojazdy są przestawiane między miejscami zamiast jazdy, a czas symulacji przewijany.
    /// </summary>
    public static class PlayLoopTest
    {
        static StringBuilder _log;
        static void Log(string s) { _log.AppendLine(s); }

        [MenuItem("Mleczarka/Testy/Pętla gry (tryb gry, świat Gmina)", priority = 52)]
        public static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Mleczarka] Uruchom tryb gry w scenie Swiat_Gmina."); return; }
            var gc = GameController.Instance; if (!gc) { Debug.LogWarning("[Mleczarka] Brak GameController."); return; }
            _log = new StringBuilder("[Mleczarka] Test pętli gry:\n");
            gc.NewGame(GameMode.Swobodna);
            var sim = gc.Sim; const int me = GameController.Me; var p = sim.S.players[me];
            gc.Message += (m, e) => Log((e ? "  ✗ " : "  · ") + m);
            var tanker = GameObject.Find("Cysterna gracza").GetComponent<WheeledVehicle>();
            var van = GameObject.Find("Dostawczak gracza").GetComponent<WheeledVehicle>();
            var zones = Object.FindObjectsByType<WorldPlace>(FindObjectsSortMode.None);
            WorldPlace Z(PlaceKind k, int farm = -1) => zones.First(z => z.kind == k && (farm < 0 || z.farmId == farm));
            void Move(WheeledVehicle v, Vector3 pos)
            {
                var rb = v.GetComponent<Rigidbody>();
                v.transform.position = pos + Vector3.up * 0.3f; if (rb) { rb.position = v.transform.position; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            }
            void Tick(double seconds) { for (double t = 0; t < seconds; t += 0.5) { sim.Tick(0.5); sim.Events.Clear(); } }
            void Enter(WheeledVehicle v)
            {
                if (gc.player.CurrentVehicle) gc.player.EnterOrExit();
                gc.player.transform.position = v.transform.position + v.transform.right * -2f;
                gc.player.EnterOrExit();
                Log($"  wsiadam: {(gc.player.CurrentVehicle ? gc.player.CurrentVehicle.name : "NIE UDAŁO SIĘ")}");
            }
            int ok = 0, all = 0;
            void Check(string what, bool cond) { all++; if (cond) ok++; Log($"{(cond ? "OK " : "BŁĄD")} {what}"); }

            // 1. cysterna → gospodarstwo z umową → zbieranie
            Enter(tanker);
            int farm = sim.S.farms.First(f => f.owner == me).id;
            Move(tanker, Z(PlaceKind.Gospodarstwo, farm).transform.position);
            Log($"  miejsce: {gc.PlaceOf(me).Kind} {gc.PlaceOf(me).FarmId}; podpowiedź: {gc.ContextHint()}");
            gc.DoContextAction(); Tick(120);
            Check($"zebrano mleko do cysterny ({p.tankerMilk}/{p.tankerCap} l)", p.tankerMilk > 0);

            // 2. podpisanie umowy z wolnym gospodarstwem
            var free = sim.S.farms.FirstOrDefault(f => f.owner < 0);
            if (free != null)
            {
                Move(tanker, Z(PlaceKind.Gospodarstwo, free.id).transform.position);
                gc.DoContextAction();
                Check($"umowa z gospodarstwem {free.name}", sim.S.farms[free.id].owner == me);
            }

            // 3. mleczarnia: rozładunek
            Move(tanker, Z(PlaceKind.Mleczarnia).transform.position);
            int before = p.tankMilk; gc.DoContextAction(); Tick(120);
            Check($"rozładowano do zbiornika ({before} → {p.tankMilk} l)", p.tankMilk > before);

            // 4. przyjęcie zamówienia i produkcja (te same komendy co przyciski paneli)
            var order = sim.S.orders.FirstOrDefault(o => o.status == OrderStatus.Otwarte);
            if (order != null) { gc.Send(new ClaimOrder(me, order.id)); Check($"przyjęto zamówienie: {order.client}", order.status == OrderStatus.Przyjete); }
            foreach (var k in GameData.Ready)
            {
                int q = Mathf.Max(order != null ? order.items[(int)k] : 0, 2);
                gc.Send(new StartBatch(me, k, Mathf.Min(q, GameData.P(k).MaxBatch)));
            }
            if (order != null) Log("  zamówienie: " + string.Join(", ", Enumerable.Range(0, order.items.Length).Where(i => order.items[i] > 0).Select(i => $"{order.items[i]}× {GameData.Products[i].Name}")));
            // jak gracz w panelu: pracownik idzie tam, gdzie partia czeka na ręce (przycisk „Zmień stanowisko”)
            for (double t = 0; t < sim.SecPerHour * 14 && p.batches.Count > 0; t += 5)
            {
                foreach (var b in p.batches)
                {
                    var sg = Simulation.StageOf(b);
                    if (sg.Kind != StationKind.Praca || Simulation.WorkerAt(p, sg.Station) != null || p.workers.Any(w => w.Target == sg.Station)) continue;
                    var idle = p.workers.FirstOrDefault(w => !sim.BatchesAt(p, w.Target).Any(x => Simulation.StageOf(x).Kind == StationKind.Praca));
                    if (idle != null) gc.Send(new MoveWorker(me, idle.id, sg.Station));
                }
                Tick(5);
            }
            Check($"produkcja: magazyn UHT {p.store[(int)Product.Uht]}, jogurt {p.store[(int)Product.Jogurt]}", p.store.Sum() > 0);

            // 5. dostawczak: załadunek i dostawa na rynek
            Enter(van); Move(van, Z(PlaceKind.Mleczarnia).transform.position);
            gc.Send(new SetVehicle(me, VehicleKind.Dostawczak));   // w grze robi to GameController co klatkę
            gc.DoContextAction();
            Check($"załadowano dostawczak ({p.vanCargo.Sum()} szt.)", p.vanCargo.Sum() > 0);
            double money = p.money;
            Move(van, Z(PlaceKind.Rynek).transform.position);
            gc.DoContextAction();
            Check($"dostawa na rynku: pieniądze {Money.Zl(money)} → {Money.Zl(p.money)} zł", p.money > money);
            if (order != null) Check($"zamówienie {order.client}: {order.status}", order.status == OrderStatus.Dostarczone);

            // 6. zapis i odczyt
            gc.SaveGame();
            var save = Mleczarka.Save.SaveSystem.Load(gc.saveSlot);
            Check($"zapis gry: dzień {sim.Clock().day}, pieniądze w zapisie {Money.Zl(save.game.players[me].money)} zł, pojazdy {save.vehicles.Count}", save != null && System.Math.Abs(save.game.players[me].money - p.money) < 0.01);

            // zostaw scenę w czytelnym stanie do zrzutu: dostawczak przy mleczarni
            Move(van, Z(PlaceKind.Mleczarnia).transform.position + new Vector3(4, 0, 6));
            _log.Insert(0, $"[Mleczarka] Pętla gry: {ok}/{all} OK\n");
            Debug.Log(_log.ToString());
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "Screenshots", "test_petli.txt"), _log.ToString());
        }
    }
}
