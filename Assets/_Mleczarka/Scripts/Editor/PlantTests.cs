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
    /// Testy pełnego ciągu maszyn (bez sceny): trasy wszystkich 8 produktów, kroki obsługi gracza i pracowników,
    /// śmietanka z wirówki, mycie CIP, awarie i naprawy, badanie próbki, migracja starego zapisu.
    /// Wynik w konsoli i w Screenshots/testy_hali.txt.
    /// </summary>
    public static class PlantTests
    {
        sealed class AtDairy : IPlaceProvider { public Place PlaceOf(int pid) => pid == 0 ? new Place(PlaceKind.Mleczarnia) : Place.None; }

        static StringBuilder _log; static int _ok, _fail;
        static void Check(bool c, string what) { if (c) _ok++; else _fail++; _log.AppendLine((c ? "  ✔ " : "  ✘ ") + what); }

        static Simulation Fresh(bool workers = false)
        {
            var s = Simulation.NewGame(GameMode.Swobodna, 77); s.calm = true;
            var sim = new Simulation(s, new AtDairy());
            var p = s.players[0];
            PlantSim.Enable(p);
            p.manualHandling = false;            // gotowy towar od razu w magazynie (logikę skrzynek testuje osobny zestaw)
            p.money = 5000; p.tankMilk = 200; p.tankCap = 400; p.storeCap = 200;
            if (!workers) p.workers.Clear();
            return sim;
        }

        static void TimeLimits()
        {
            _log.AppendLine("Bez ograniczeń czasowych:");
            var s = Simulation.NewGame(GameMode.Misja, 5); var sim = new Simulation(s, new AtDairy());
            Check(s.noTimeLimits, "domyślnie gra bez limitów czasu");
            foreach (var o in s.orders) o.deadline = 1;
            s.mission.limit = 2;
            for (int i = 0; i < 50; i++) sim.Tick(0.2);
            Check(s.orders.All(o => o.status != OrderStatus.Przeterminowane), "zamówienia nie przepadają po terminie");
            Check(!s.mission.over, "misja nie kończy się z braku czasu");
            s.noTimeLimits = false; s.mission.over = false;
            for (int i = 0; i < 5; i++) sim.Tick(0.2);
            Check(s.mission.over && !s.mission.win, "z włączonymi limitami misja nadal może przepaść");
            s.noTimeLimits = true; Simulation.Migrate(s);
            Check(!s.mission.over && s.orders.All(o => o.status != OrderStatus.Przeterminowane), "migracja przywraca misję i zamówienia przegrane z czasu");
        }

        /// <summary>Gracz obsługuje wszystkie maszyny: jak przytrzymanie Q przy każdej, która czeka na człowieka.</summary>
        static void PlayAll(Simulation sim, double seconds, bool hands = true, bool repair = true)
        {
            var p = sim.S.players[0];
            for (double t = 0; t < seconds; t += 0.5)
            {
                if (hands) foreach (var m in p.plant.machines.ToList()) if (PlantSim.NeedsHands(p, m) || (repair && m.broken)) PlantSim.Act(sim, 0, m.id);
                for (int k = 0; k < 5; k++) sim.Tick(0.1);
            }
        }

        [MenuItem("Mleczarka/Testy/Hala: pełny ciąg maszyn", priority = 41)]
        public static void RunAll()
        {
            _log = new StringBuilder(); _ok = _fail = 0;
            try { Routes(); Cream(); Workers(); Dirt(); Breakdown(); Sample(); Migration(); Orders(); TimeLimits(); }
            catch (Exception e) { _fail++; _log.AppendLine("WYJĄTEK: " + e); }
            string summary = $"Testy hali: {_ok} OK, {_fail} błędów.";
            _log.Insert(0, summary + "\n");
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "testy_hali.txt"), _log.ToString());
            if (_fail == 0) Debug.Log("[Mleczarka] " + summary + "\n" + _log); else Debug.LogError("[Mleczarka] " + summary + "\n" + _log);
        }

        static void Routes()
        {
            _log.AppendLine("Trasy produktów (gracz obsługuje maszyny):");
            foreach (var prod in GameData.Ready)
            {
                var sim = Fresh(); var p = sim.S.players[0];
                p.plant.cream = 30;
                double money0 = p.money; int milk0 = p.tankMilk;
                var r = PlantSim.StartJob(sim, 0, prod, 2);
                Check(r.Ok, $"{GameData.P(prod).Name}: start partii ({r.Msg})");
                double hours = PlantData.Route(prod).Contains("dojrzewalnia") ? 14 : PlantData.Route(prod).Contains("fermentator") ? 7 : PlantData.Route(prod).Contains("wanna") ? 3 : 0;
                PlayAll(sim, 120 + hours * sim.SecPerHour);
                Check(p.store[(int)prod] == 2 && p.plant.jobs.Count == 0, $"{GameData.P(prod).Name}: gotowe 2 szt. po trasie {string.Join(" → ", PlantData.Route(prod))}");
                Check(p.money < money0 && p.tankMilk == milk0 - GameData.P(prod).MilkPerUnit * 2, $"{GameData.P(prod).Name}: zużyto mleko i zapłacono za dodatki");
            }
            var s2 = Fresh(); var p2 = s2.S.players[0];
            Check(!PlantSim.StartJob(s2, 0, Product.Maslo, 2).Ok, "masło bez śmietanki nie startuje");
            p2.tankMilk = 0;
            Check(!PlantSim.StartJob(s2, 0, Product.Uht, 2).Ok, "bez mleka w silosie partia nie startuje");
        }

        static void Cream()
        {
            _log.AppendLine("Śmietanka z wirówki:");
            var sim = Fresh(); var p = sim.S.players[0];
            PlantSim.StartJob(sim, 0, Product.Uht, 10);
            PlayAll(sim, 60);
            Check(Math.Abs(p.plant.cream - 10 * PlantData.CreamYield) < 1e-6, $"10 l mleka na wirówce daje {p.plant.cream:0.0} l śmietanki");
            PlantSim.StartJob(sim, 0, Product.Uht, 10); PlayAll(sim, 60);
            p.plant.cream += 2;   // uzbierane z kilku partii
            Check(PlantSim.StartJob(sim, 0, Product.Maslo, 1).Ok, "z uzbieranej śmietanki rusza masło");
        }

        static void Workers()
        {
            _log.AppendLine("Pracownicy:");
            var sim = Fresh(true); var p = sim.S.players[0];
            Check(p.workers.Count > 0, $"zespół: {p.workers.Count} os.");
            Check(p.plant.workersOff, "domyślnie wersja bez pracowników");
            PlantSim.StartJob(sim, 0, Product.Uht, 3);
            PlayAll(sim, 60, hands: false);
            Check(p.store[(int)Product.Uht] == 0 && p.plant.jobs.Count == 1 && p.plant.jobs[0].stage == 0, "bez pracowników partia czeka na gracza");
            Check(!new HireWorker(0).Execute(sim).Ok && !PlantSim.AssignWorker(sim, 0, 0, "wirowka").Ok, "bez pracowników nie da się zatrudnić ani przypisać");
            double before = p.money; sim.S.t += GameConfig.DaySeconds; sim.Tick(0.1);
            Check(p.money == before, "bez pracowników nie ma pensji");
            Check(new SetWorkersOff(0, false).Execute(sim).Ok && !p.plant.workersOff, "pracowników można włączyć");
            PlayAll(sim, 150, hands: false);
            Check(p.store[(int)Product.Uht] == 3, "pracownicy sami przeprowadzili partię UHT przez wszystkie maszyny");
            var sim2 = Fresh(false); var p2 = sim2.S.players[0];
            PlantSim.StartJob(sim2, 0, Product.Uht, 3);
            PlayAll(sim2, 60, hands: false);
            Check(p2.store[(int)Product.Uht] == 0 && p2.plant.jobs[0].step == 0, "bez ludzi i bez gracza partia stoi przy pierwszym zaworze");
            var a = PlantSim.AssignWorker(sim, 0, 0, "pasteryzator");
            Check(a.Ok && p.plant.machines.First(m => m.id == "pasteryzator").worker == 0, "przypisanie pracownika na stałe do maszyny");
        }

        static void Dirt()
        {
            _log.AppendLine("Mycie CIP:");
            var sim = Fresh(); var p = sim.S.players[0];
            var w = PlantSim.Machine(p, "wirowka");
            for (int i = 0; i < 9; i++) { PlantSim.StartJob(sim, 0, Product.Uht, 2); PlayAll(sim, 40); }
            Check(w.dirt >= PlantData.DirtBlock, $"po wielu partiach wirówka jest brudna ({w.dirt:0})");
            foreach (var x in p.plant.machines) if (x.id != "wirowka") x.dirt = 0;   // dalsze maszyny myjemy „ręcznie”, sprawdzamy tylko wirówkę
            PlantSim.StartJob(sim, 0, Product.Uht, 2); PlayAll(sim, 5);
            var j = p.plant.jobs.LastOrDefault();
            Check(j != null && j.machine == null && j.wait != null && j.wait.Contains("brudna"), "brudna maszyna nie przyjmuje partii (" + j?.wait + ")");
            double m0 = p.money;
            Check(PlantSim.StartCip(sim, 0, "wirowka").Ok && p.money < m0, "start mycia CIP (płatne środki)");
            PlayAll(sim, PlantData.CipSeconds + 2, hands: false);
            Check(w.dirt == 0 && w.cip == 0, "po myciu wirówka czysta");
            PlayAll(sim, 40);
            Check(p.plant.jobs.Count == 0, "zaległa partia przeszła po umyciu");
        }

        static void Breakdown()
        {
            _log.AppendLine("Awarie i naprawy:");
            var sim = Fresh(); var p = sim.S.players[0];
            var m = PlantSim.Machine(p, "homogenizator");
            m.condition = 0;
            bool broke = false;
            for (int i = 0; i < 12 && !broke; i++) { PlantSim.StartJob(sim, 0, Product.Uht, 1); PlayAll(sim, 30, repair: false); broke = m.broken; foreach (var x in p.plant.machines) if (x.id != "homogenizator") x.dirt = 0; }
            Check(broke, "zużyta maszyna się psuje (" + m.fault + ")");
            var job = PlantSim.JobAt(p, "homogenizator");
            PlayAll(sim, 3, hands: false);
            Check(job == null || job.Waiting, "zepsuta maszyna wstrzymuje partię");
            double money0 = p.money;
            for (int i = 0; i < 12 && m.broken; i++) PlantSim.Act(sim, 0, "homogenizator");
            Check(!m.broken && m.condition == 100 && p.money <= money0 - PlantData.RepairCost + 1e-6, "naprawa przytrzymaniem Q (płatne części)");
            m.condition = 50;
            for (int i = 0; i < 8; i++) PlantSim.Service(sim, 0, "homogenizator");
            Check(m.condition >= 90 || PlantSim.JobAt(p, "homogenizator") != null, $"przegląd podnosi stan ({m.condition:0})");
        }

        static void Sample()
        {
            _log.AppendLine("Laboratorium i przyjęcie mleka:");
            var sim = Fresh(); var p = sim.S.players[0];
            p.tankerMilk = 10; p.tankerQuality = 0.4; p.vehicle = VehicleKind.Cysterna; p.tankMilk = 0;
            Check(!sim.StartUnload(0).Ok, "rozładunek bez badania próbki zablokowany");
            for (int i = 0; i < 8; i++) PlantSim.TestSample(sim, 0);
            Check(p.plant.sampleTested && p.plant.sampleResult < 0.6, "badanie wykrywa słabe mleko: " + PlantSim.SampleText(p.plant.sampleResult));
            Check(PlantSim.RejectMilk(sim, 0).Ok && p.tankerMilk == 0, "odrzucenie słabego mleka");
            p.tankerMilk = 10; p.tankerQuality = 0.95; p.plant.sampleTested = false;
            for (int i = 0; i < 8; i++) PlantSim.TestSample(sim, 0);
            Check(sim.StartUnload(0).Ok, "po badaniu dobre mleko można rozładować");
            for (int i = 0; i < 60; i++) sim.Tick(0.1);
            Check(p.tankMilk == 10 && p.tankerMilk == 0 && Math.Abs(p.plant.rawQuality - 0.95) < 1e-6, "mleko w silosie z jakością z próbki");
            Check(!p.plant.sampleTested, "pusta cysterna = przy następnym kursie nowa próbka");
        }

        static void Migration()
        {
            _log.AppendLine("Migracja starego zapisu:");
            var s = Simulation.NewGame(GameMode.Swobodna, 5);
            var p = s.players[0];
            p.store = new[] { 3, 1 }; p.vanCargo = new[] { 2, 0 }; p.made = new[] { 5, 2 }; p.packingRack = null; p.carried = null; p.plant = null;
            s.orders[0].items = new[] { 1, 1 };
            string json = JsonUtility.ToJson(s);
            var loaded = JsonUtility.FromJson<GameState>(json);
            Simulation.Migrate(loaded);
            var lp = loaded.players[0];
            Check(lp.store.Length == GameData.ProductCount && lp.store[0] == 3 && lp.store[1] == 1, "magazyn powiększony do 8 produktów, stary towar zachowany");
            Check(lp.vanCargo.Length == GameData.ProductCount && lp.made.Length == GameData.ProductCount && lp.packingRack.Length == GameData.ProductCount, "pozostałe tablice produktów uzupełnione");
            Check(loaded.orders[0].items.Length == GameData.ProductCount && loaded.orders[0].items[0] == 1, "zamówienia ze starego zapisu działają");
            Check(lp.plant != null && !lp.plant.enabled, "stary zapis zostaje przy starej hali, dopóki nowa nie zostanie uruchomiona");
            PlantSim.Enable(lp);
            Check(lp.plant.machines.Count == PlantData.Machines.Length, $"po uruchomieniu: {lp.plant.machines.Count} maszyn");
            var json2 = JsonUtility.ToJson(loaded); var again = JsonUtility.FromJson<GameState>(json2);
            Check(again.players[0].plant.enabled && again.players[0].plant.machines.Count == PlantData.Machines.Length, "stan hali zapisuje się i wczytuje");
        }

        static void Orders()
        {
            _log.AppendLine("Zamówienia:");
            var s = Simulation.NewGame(GameMode.Swobodna, 9);
            var seen = new bool[GameData.ProductCount];
            for (int i = 0; i < 200; i++) { var o = Simulation.GenOrder(s); for (int k = 0; k < o.items.Length; k++) if (o.items[k] > 0) seen[k] = true; }
            Check(seen.All(x => x), "klienci zamawiają wszystkie 8 produktów");
        }
    }
}
