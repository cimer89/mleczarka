using System;
using System.Linq;
using Mleczarka.Core;

namespace Mleczarka.ProductionFloor
{
    public static class ProductionChecks
    {
        sealed class Places : IPlaceProvider
        {
            public Place here = new Place(PlaceKind.Mleczarnia);
            public Place PlaceOf(int id) { return here; }
        }
        static int count;
        static void Check(bool value, string name)
        { if (!value) throw new Exception("FAIL: " + name); count++; }
        public static string Run()
        {
            count = 0;
            var places = new Places();
            var sim = new Simulation(Simulation.NewGame(GameMode.Swobodna), places);
            var p = sim.S.players[0];
            p.manualHandling = true; p.tankMilk = 12;
            foreach (var w in p.workers) { w.station = "tank"; w.auto = false; }
            Check(sim.StartBatch(0, Product.Uht, 4).Ok && p.tankMilk == 8, "partia zużywa mleko raz");
            for (int i = 0; i < 100; i++) sim.Tick(0.1);
            Check(p.batches.Count == 1 && p.batches[0].progress == 0, "brak automatycznej pracy");
            for (int i = 0; i < 30 && p.batches.Any(b => Simulation.StageOf(b).Station == "uht"); i++) sim.Help(0, "uht");
            Check(Simulation.StageOf(p.batches[0]).Station == "packC", "praca przy UHT");
            for (int i = 0; i < 30 && p.batches.Count > 0; i++) sim.Help(0, "packC");
            Check(p.batches.Count == 0 && p.packingRack[0] == 4 && p.store[0] == 0, "pakowanie tworzy towar do odbioru");
            Check(ProductionLogistics.Pick(p, Product.Uht, true).Ok && p.carried[0] == 4 && p.packingRack[0] == 0, "podniesienie skrzynki");
            Check(!ProductionLogistics.Pick(p, Product.Uht, true).Ok && p.carried[0] == 4, "brak duplikacji przy podnoszeniu");
            Check(!ProductionLogistics.PutInVan(p).Ok && p.carried[0] == 4, "wymagane przejście przez magazyn");
            Check(ProductionLogistics.PutInStore(p).Ok && p.store[0] == 4 && p.carried.Sum() == 0, "odłożenie do magazynu");
            Check(!ProductionLogistics.PutInStore(p).Ok && p.store[0] == 4, "brak podwójnego odłożenia");
            Check(!sim.LoadVan(0).Ok && p.store[0] == 4, "zakaz automatycznego załadunku");
            Check(ProductionLogistics.Pick(p, Product.Uht, false).Ok && p.store[0] == 0, "pobranie z magazynu");
            p.vanCap = 3;
            Check(!ProductionLogistics.PutInVan(p).Ok && p.carried[0] == 4 && p.vanCargo[0] == 0, "pełny van nie gubi towaru");
            p.vanCap = 12;
            Check(ProductionLogistics.PutInVan(p).Ok && p.carried.Sum() == 0 && p.vanCargo[0] == 4, "załadunek");
            Check(!ProductionLogistics.PutInVan(p).Ok && p.vanCargo[0] == 4, "brak podwójnego załadunku");
            p.vehicle = VehicleKind.Dostawczak; places.here = new Place(PlaceKind.Rynek);
            // Zamówienia otwarte nie rezerwują towaru; sprzedaż nadwyżek używa istniejącej ekonomii.
            double before = p.money;
            Check(sim.SellRest(0).Ok && p.money > before && p.vanCargo[0] == 0, "sprzedaż po dostawie");
            double after = p.money;
            Check(!sim.SellRest(0).Ok && p.money == after, "brak podwójnej zapłaty");
            p.packingRack[0] = 2;
            Check(!new MoveCrate(0, CrateAction.PickPacked).Execute(sim).Ok && p.packingRack[0] == 2, "przenoszenie tylko w zakładzie");
            Check(!ProductionLogistics.Pick(p, (Product)99, true).Ok, "nieprawidłowy produkt");
            p.packingRack = null; p.carried = null; Simulation.Migrate(sim.S);
            Check(p.packingRack != null && p.carried != null, "migracja brakujących pól");
            var old = new Simulation(Simulation.NewGame(GameMode.Swobodna), new Places());
            old.S.players[0].tankMilk = 4;
            old.StartBatch(0, Product.Uht, 4);
            for (int i = 0; i < 30; i++) { old.Help(0, "uht"); old.Help(0, "packC"); }
            Check(old.S.players[0].store[0] == 4 && old.S.players[0].packingRack[0] == 0, "dotychczasowy tryb bez zmian");
            var assisted = new Simulation(Simulation.NewGame(GameMode.Swobodna), new Places());
            var staff = assisted.S.players[0]; staff.manualHandling = true; staff.tankMilk = 4;
            assisted.StartBatch(0, Product.Uht, 4);
            for (int i = 0; i < 10; i++) assisted.Tick(0.1);
            Check(staff.batches[0].progress == 0, "załoga nie pomija osobistej pracy");
            staff.productionStaffAssist = true;
            for (int i = 0; i < 10; i++) assisted.Tick(0.1);
            Check(staff.batches[0].progress > 0, "opcjonalna pomoc zatrudnionej załogi");
            var returns = new Simulation(Simulation.NewGame(GameMode.Swobodna), new Places());
            var depot = returns.S.players[0];
            depot.manualHandling = true;
            depot.vehicle = VehicleKind.Dostawczak;
            depot.storeCap = 8;
            depot.packingRack[0] = 4;
            depot.carried[0] = 2;
            depot.vanCargo[0] = 1;
            depot.vanCargo[1] = 3;
            Check(returns.UnloadVan(0).Ok && depot.store.Sum() == 2 && depot.vanCargo.Sum() == 2,
                "zwrot mieszanego ładunku zajmuje tylko wolne miejsca");
            Check(!returns.UnloadVan(0).Ok && depot.vanCargo.Sum() == 2 && depot.store.Sum() == 2,
                "powtórny rozładunek pełnego zakładu zachowuje towar");
            depot.storeCap = 1;
            Check(Simulation.FreeStorage(depot) == 0 && !returns.UnloadVan(0).Ok && depot.store.Sum() == 2,
                "przepełniony magazyn nie przenosi towaru z powrotem do auta");
            return count + " OK, 0 błędów";
        }
    }
}
