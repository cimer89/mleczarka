using System;
using System.IO;
using System.Linq;
using System.Text;
using Mleczarka.Core;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.EditorTools
{
    /// <summary>
    /// Testy rdzenia symulacji (bez sceny): przejście misji 1 od odbioru mleka do dostawy, umowy, pensje,
    /// rywal, zapis i odczyt (determinizm). Wynik w konsoli i w Screenshots/testy_symulacji.txt.
    /// </summary>
    public static class SimulationTests
    {
        sealed class StubPlaces : IPlaceProvider
        {
            public Place Current = Place.None;
            public Place PlaceOf(int pid) => pid == 0 ? Current : Place.None;
        }

        static StringBuilder _log; static int _ok, _fail;
        static void Check(bool cond, string what) { if (cond) _ok++; else _fail++; _log.AppendLine((cond ? "  ✔ " : "  ✘ ") + what); }
        static void Run(Simulation sim, double seconds) { for (double t = 0; t < seconds; t += 0.1) sim.Tick(0.1); }

        [MenuItem("Mleczarka/Testy/Symulacja mleczarni", priority = 40)]
        public static void RunAll()
        {
            _log = new StringBuilder(); _ok = _fail = 0;
            try { MissionFlow(); Contracts(); WagesAndRival(); SaveRoundTrip(); }
            catch (Exception e) { _fail++; _log.AppendLine("WYJĄTEK: " + e); }
            string summary = $"Testy symulacji: {_ok} OK, {_fail} błędów.";
            _log.Insert(0, summary + "\n");
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "testy_symulacji.txt"), _log.ToString());
            if (_fail == 0) Debug.Log("[Mleczarka] " + summary + "\n" + _log); else Debug.LogError("[Mleczarka] " + summary + "\n" + _log);
        }

        static void MissionFlow()
        {
            _log.AppendLine("Misja 1: odbiór, produkcja, dostawa");
            var places = new StubPlaces();
            var sim = new Simulation(Simulation.NewGame(GameMode.Misja, 7), places);
            var bus = new LocalCommandBus(sim);
            var p = sim.S.players[0];

            Check(!bus.Send(new CollectMilk(0, 0)).Ok, "nie można pompować poza gospodarstwem");
            places.Current = new Place(PlaceKind.Gospodarstwo, 0);
            Check(bus.Send(new CollectMilk(0, 0)).Ok, "start pompowania u Kowalskich");
            Run(sim, 6);
            Check(p.tankerMilk == 14, $"cysterna ma 14 l (ma {p.tankerMilk})");
            Check(Math.Abs(p.money - 63.2) < 0.001, $"zapłacono 14 × 1,20 zł (stan {p.money})");
            Check(!sim.S.pump.Active, "pompa skończyła, gdy zabrakło mleka");

            places.Current = new Place(PlaceKind.Mleczarnia);
            Check(bus.Send(new UnloadMilk(0)).Ok, "rozładunek w mleczarni");
            Run(sim, 5);
            Check(p.tankMilk == 14 && p.tankerMilk == 0, $"zbiornik 14 l (ma {p.tankMilk})");

            Check(bus.Send(new StartBatch(0, Product.Uht, 7)).Ok, "partia UHT 7 szt.");
            Check(bus.Send(new StartBatch(0, Product.Jogurt, 2)).Ok, "partia jogurtu 2 szt.");
            Check(p.tankMilk == 3, $"zużyto 7 + 4 l mleka (zostało {p.tankMilk})");
            Run(sim, sim.SecPerHour * 8);
            Check(p.store[(int)Product.Uht] == 7, $"magazyn: 7 UHT (jest {p.store[(int)Product.Uht]})");
            Check(p.store[(int)Product.Jogurt] == 2, $"magazyn: 2 jogurty po fermentacji (jest {p.store[(int)Product.Jogurt]})");

            Check(!bus.Send(new LoadVan(0)).Ok, "cysterną nie załaduje się towaru");
            bus.Send(new SetVehicle(0, VehicleKind.Dostawczak));
            Check(bus.Send(new LoadVan(0)).Ok, "załadunek dostawczaka");
            Check(p.vanCargo.Sum() == 9, $"w dostawczaku 9 szt. (jest {p.vanCargo.Sum()})");

            places.Current = new Place(PlaceKind.Rynek);
            double before = p.money;
            var orders = sim.S.orders.Where(o => o.mission).ToList();
            foreach (var o in orders.Take(2)) Check(bus.Send(new Deliver(0, o.id)).Ok, $"dostawa: {o.client}");
            Check(Math.Abs(p.money - before - 85) < 0.001, $"nagrody 40 + 45 zł (przyrost {p.money - before})");
            var third = orders[2];
            Check(!bus.Send(new Deliver(0, third.id)).Ok, "trzecie zamówienie: brakuje towaru");
            Check(!sim.S.mission.over, "misja trwa, dopóki nie ma trzeciej dostawy");
        }

        static void Contracts()
        {
            _log.AppendLine("Umowy z gospodarstwami");
            var places = new StubPlaces();
            var sim = new Simulation(Simulation.NewGame(GameMode.Swobodna, 3), places);
            var p = sim.S.players[0];
            Check(!sim.Bid(0, 2, 1.4).Ok, "oferta tylko na miejscu");
            places.Current = new Place(PlaceKind.Gospodarstwo, 2);
            Check(!sim.Bid(0, 2, 1.0).Ok, "za niska cena odrzucona");
            Check(sim.Bid(0, 2, 1.4).Ok, "umowa z Wiśniewskimi za 1,40 zł/l");
            Check(sim.S.farms[2].owner == 0 && Math.Abs(p.money - 65) < 0.001, $"opłata 15 zł (stan {p.money})");
            places.Current = new Place(PlaceKind.Gospodarstwo, 5);
            var info = sim.OfferInfo(0, 5);
            Check(!info.free && Math.Abs(info.fee - 30) < 0.001, "przejęcie umowy rywala kosztuje 30 zł");
            Check(!sim.Bid(0, 5, 1.2).Ok, "przebicie musi być o min. 0,20 zł");
        }

        static void WagesAndRival()
        {
            _log.AppendLine("Pensje, zamówienia i rywal w trybie swobodnym");
            var sim = new Simulation(Simulation.NewGame(GameMode.Swobodna, 11), new StubPlaces());
            var p = sim.S.players[0];
            double before = p.money;
            Run(sim, GameConfig.DaySeconds + 10);
            Check(sim.Clock().day == 2, $"po jednym dniu gry drugi dzień (dzień {sim.Clock().day})");
            Check(Math.Abs(before - p.money - 30) < 0.001, $"pensje 3 × 10 zł (różnica {before - p.money})");
            Check(sim.S.farms.Count(f => f.owner == 1) >= 2, $"rywal podpisał nową umowę (ma {sim.S.farms.Count(f => f.owner == 1)})");
            Check(sim.S.orders.Count >= 3, $"są zamówienia ({sim.S.orders.Count})");
            Check(sim.S.farms.All(f => f.stock <= f.max), "zapasy w gospodarstwach nie przekraczają maksimum");
        }

        static void SaveRoundTrip()
        {
            _log.AppendLine("Zapis i odczyt (JSON) oraz determinizm");
            var a = new Simulation(Simulation.NewGame(GameMode.Swobodna, 99), new StubPlaces());
            Run(a, 123);
            string json = JsonUtility.ToJson(a.S);
            var b = new Simulation(JsonUtility.FromJson<GameState>(json), new StubPlaces());
            Check(b.S.t == a.S.t && b.S.rng == a.S.rng && b.S.orders.Count == a.S.orders.Count, "stan po odczycie identyczny");
            Run(a, 400); Run(b, 400);
            Check(Math.Abs(a.S.players[0].money - b.S.players[0].money) < 1e-9 && a.S.rng == b.S.rng && a.S.orders.Count == b.S.orders.Count,
                  "po kolejnych 400 s obie kopie dają ten sam wynik");
            Check(json.Length > 500, $"JSON ma treść ({json.Length} znaków)");
        }
    }
}
