using System.IO;
using System.Linq;
using System.Text;
using Mleczarka.Core;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.EditorTools
{
    /// <summary>Testy trybu kariery: od bańki mleka do pierwszej sprzedaży i zakupów; blokady pojazdów i hali; migracja zapisu.</summary>
    public static class CareerTests
    {
        sealed class World : IPlaceProvider { public Place place = Place.None; public Place PlaceOf(int id) => place; }
        static StringBuilder _log; static int _ok, _bad;
        static void Check(bool c, string what) { if (c) _ok++; else _bad++; _log.AppendLine((c ? "OK   " : "BŁĄD ") + what); }

        [MenuItem("Mleczarka/Testy/Kariera: od kuchni")]
        public static void Run()
        {
            _log = new StringBuilder(); _ok = _bad = 0;
            try { Loop(); Locks(); Migration(); HandSale(); Institutions(); Cows(); Rounding(); }
            catch (System.Exception e) { _bad++; _log.AppendLine("WYJĄTEK " + e); }
            Directory.CreateDirectory("Screenshots"); File.WriteAllText("Screenshots/testy_kariery.txt", _log.ToString());
            Debug.Log($"[Mleczarka] Testy kariery: {_ok} OK, {_bad} błędów.");
        }

        static void Act(Simulation sim, string station, int max = 200)
        {
            var p = sim.S.players[0];
            for (int i = 0; i < max; i++)
            {
                var j = CareerSim.JobAt(p, station); if (j == null) return;
                if (CareerSim.NeedsHands(j)) new KitchenAct(0, station).Execute(sim); else sim.Tick(0.25);
            }
        }

        static void Loop()
        {
            _log.AppendLine("Pętla kariery:");
            var w = new World(); var S = Simulation.NewGame(GameMode.Kariera, 7); var sim = new Simulation(S, w); var p = S.players[0]; var c = p.career;
            Check(c.active && p.money == CareerData.StartMoney && c.owned.Contains("rower") && c.owned.Contains("banka"), "start: 60 zł, rower, bańka, kuchnia");
            Check(S.orders.Count == 0 && p.workers.Count == 0, "start: bez zamówień i pracowników");
            Check(!new BuyMilkCan(0, 0).Execute(sim).Ok, "mleko kupuje się tylko w gospodarstwie");
            w.place = new Place(PlaceKind.Gospodarstwo, 0);
            double before = p.money; var r = new BuyMilkCan(0, 0).Execute(sim);
            Check(r.Ok && c.canister == 10 && p.money < before, "kupno 10 l do bańki: " + r.Msg);
            Check(!new BuyMilkCan(0, 0).Execute(sim).Ok, "pełna bańka — nie da się dokupić");
            Check(!new BidForContract(0, 2, 5).Execute(sim).Ok, "umowy dopiero z cysterną");
            w.place = new Place(PlaceKind.Dom);
            Check(new PourMilk(0).Execute(sim).Ok && c.fridge == 10 && c.canister == 0, "przelanie do lodówki");
            w.place = new Place(PlaceKind.Gospodarstwo, 1); new BuyMilkCan(0, 1).Execute(sim);
            w.place = new Place(PlaceKind.Dom); new PourMilk(0).Execute(sim);
            Check(c.fridge == 20, "druga bańka: lodówka pełna (20 l)");
            Check(!new StartKitchen(0, "maslo", 1).Execute(sim).Ok, "masło zablokowane bez maselnicy");
            Check(!new StartKitchen(0, "twarog", 3).Execute(sim).Ok, "partia ponad garnek zablokowana");
            Check(new StartKitchen(0, "twarog", 2).Execute(sim).Ok && c.fridge == 14, "start twarogu (6 l)");
            Check(!new StartKitchen(0, "jogurt", 1).Execute(sim).Ok, "kuchenka zajęta");
            Check(new StartKitchen(0, "smietanka", 1).Execute(sim).Ok, "zbieranie śmietanki w lodówce równolegle");
            Act(sim, "kuchenka"); Act(sim, "lodowka");
            Check(c.pantry[(int)Product.Twarog] == 2, "twaróg ×2 w spiżarce");
            Check(System.Math.Abs(c.cream - 0.5) < 1e-6 && System.Math.Abs(c.fridge - 13.5) < 1e-6, $"śmietanka 0,5 l i chude mleko wraca ({c.fridge:0.0} l)");
            Check(new StartKitchen(0, "smietana", 1).Execute(sim).Ok, "śmietana z 0,4 l śmietanki");
            Act(sim, "stol");
            Check(c.pantry[(int)Product.Smietana] == 1, "śmietana ×1");
            Check(new LoadBasket(0).Execute(sim).Ok && c.basket.Sum() == 3 && c.pantry.Sum() == 0, "towar do koszyka");
            Check(!new SellBasket(0).Execute(sim).Ok, "sprzedaż tylko na rynku");
            w.place = new Place(PlaceKind.Rynek);
            before = p.money; r = new SellBasket(0).Execute(sim);
            Check(r.Ok && p.money > before + 30 && c.basket.Sum() == 0, "sprzedaż na straganie: " + r.Msg);
            Check(c.demand[(int)Product.Twarog] < 1, "rynek się nasyca");
            double d = c.demand[(int)Product.Twarog]; sim.Tick(0.25); for (int i = 0; i < 400; i++) sim.Tick(0.25);
            Check(c.demand[(int)Product.Twarog] > d, "popyt wraca z czasem");
            Check(!new BuyCareerItem(0, "dostawczak").Execute(sim).Ok, "dostawczak wymaga przyczepki i pieniędzy");
            p.money += 100;
            Check(new BuyCareerItem(0, "maselnica").Execute(sim).Ok && c.owned.Contains("maselnica"), "kupno maselnicy");
            p.money = 100000;
            Check(new BuyCareerItem(0, "przyczepka").Execute(sim).Ok && c.canisterCap == 40 && c.basketCap == 20, "przyczepka: 40 l, 20 szt.");
            Check(new BuyCareerItem(0, "garnek_duzy").Execute(sim).Ok && CareerSim.MaxBatch(p) == 6, "duży garnek: partie do 6");
        }

        static void Locks()
        {
            _log.AppendLine("Blokady:");
            var S = Simulation.NewGame(GameMode.Kariera, 3); var p = S.players[0];
            Check(!CareerSim.Allowed(p, "dostawczak") && !CareerSim.Allowed(p, "cysterna") && !CareerSim.Allowed(p, "hala") && !CareerSim.Allowed(p, "skrzydlo"), "na starcie zablokowane: dostawczak, cysterna, hala, skrzydło");
            var F = Simulation.NewGame(GameMode.Swobodna, 3);
            Check(CareerSim.Allowed(F.players[0], "hala") && !CareerSim.Active(F.players[0]), "poza karierą nic nie jest blokowane");
            var sim = new Simulation(S, new World { place = new Place(PlaceKind.Dom) });
            p.money = 100000;
            Check(!new BuyCareerItem(0, "hala").Execute(sim).Ok, "hala wymaga cysterny");
            foreach (var id in new[] { "przyczepka", "dostawczak", "cysterna", "hala", "skrzydlo" }) new BuyCareerItem(0, id).Execute(sim);
            Check(CareerSim.Allowed(p, "skrzydlo"), "pełna ścieżka do skrzydła maszyn");
            S.t = 1; for (int i = 0; i < 2000; i++) sim.Tick(0.25);
            Check(S.orders.Count > 0, "z dostawczakiem pojawiają się zamówienia");
        }

        static void Rounding()
        {
            _log.AppendLine("Zaokrąglenia i dojenie po litrze:");
            var w = new World(); var S = Simulation.NewGame(GameMode.Kariera, 7); var sim = new Simulation(S, w); var p = S.players[0]; var c = p.career;
            w.place = new Place(PlaceKind.Dom);
            c.fridge = 2.97; var tw = CareerData.R("twarog");
            Check(CareerSim.StartBlock(p, tw, 1) == "", "2,97 l w lodówce wystarcza na twaróg (3 l) — drobna resztka nie blokuje");
            var r = new StartKitchen(0, "twarog", 1).Execute(sim);
            Check(r.Ok && c.fridge == 0, $"start twarogu z 2,97 l: {r.Msg}, w lodówce {c.fridge}");
            c.fridge = 2.9; c.jobs.Clear();
            string b = CareerSim.StartBlock(p, tw, 1);
            Check(b.Contains("jest 2,9 l") || b.Contains("jest 2.9 l"), "brak 0,1 l pokazuje prawdziwą ilość: " + b);
            Check(CareerSim.L(2.96) == "3" && (CareerSim.L(2.9) is "2,9" or "2.9"), "2,96 l (wystarcza na 3 l) pokazuje się jako 3 l, a 2,9 l jako 2,9 l: " + CareerSim.L(2.96));
            Check(CareerSim.L(3.0) == "3" && CareerSim.L(0.5) is "0,5" or "0.5", "3 l i 0,5 l bez zbędnych zer");
            c.fridge = 6; c.jobs.Clear();
            b = CareerSim.StartBlock(p, tw, 2) ;
            Check(b == "", "6 l: twaróg ×2 możliwy");
            c.fridge = 5; b = CareerSim.StartBlock(p, tw, 2);
            Check(b.Contains("starczy na 1 szt."), "5 l przy partii ×2: podpowiedź, na ile starczy: " + b);
            c.fridge = 0; c.canister = 0; c.cowMilk = 20;
            var m = new MilkOwnCow(0, true, 1).Execute(sim);
            Check(m.Ok && c.fridge == 1 && System.Math.Abs(c.cowMilk - 19) < 1e-6, "dojenie przytrzymaniem: 1 l na porcję");
        }

        static void Cows()
        {
            _log.AppendLine("Krowy:");
            var w = new World(); var S = Simulation.NewGame(GameMode.Kariera, 3); var sim = new Simulation(S, w); var p = S.players[0]; var c = p.career;
            Check(GameData.Farms.Length >= 20, $"więcej miejsc skupu: {GameData.Farms.Length} gospodarstw");
            bool ok = true;
            for (int i = 0; i < S.farms.Count; i++) { var f = S.farms[i]; int k = CareerSim.Cows(i); if (k < 1 || k > 3 || System.Math.Abs(f.max * CareerData.LitersPerFarmUnit - k * 20) > 1e-6) ok = false; }
            Check(ok, "gospodarstwo: zapas = krowy × 20 l (1–3 krowy)");
            var f0 = S.farms[0]; f0.stock = 0;
            for (int i = 0; i < 4 * 24 * 60; i++) sim.Tick(0.25);   // doba gry
            Check(System.Math.Abs(f0.stock * CareerData.LitersPerFarmUnit - CareerSim.Cows(0) * 20) < 0.6, $"po dobie gospodarstwo ma {f0.stock * CareerData.LitersPerFarmUnit:0.0} l (krowy {CareerSim.Cows(0)} × 20 l)");
            Check(!new MilkOwnCow(0).Execute(sim).Ok, "własną krowę doi się przy domu");
            w.place = new Place(PlaceKind.Dom);
            double money = p.money; c.fridge = 0; c.canister = 0; c.cowMilk = 20;
            var r = new MilkOwnCow(0).Execute(sim);
            Check(r.Ok && c.fridge == 20 && c.canister == 0 && p.money == money, "wydojenie 20 l do lodówki za darmo: " + r.Msg);
            Check(!new MilkOwnCow(0).Execute(sim).Ok, "wydojona krowa nie daje od razu więcej");
            for (int i = 0; i < 4 * 60 * 12; i++) sim.Tick(0.25);   // pół doby
            Check(System.Math.Abs(c.cowMilk - 10) < 0.3, $"po pół doby krowa ma {c.cowMilk:0.0} l");
            c.fridge = c.fridgeCap; r = new MilkOwnCow(0).Execute(sim);
            Check(r.Ok && c.canister == 10 && c.fridge == c.fridgeCap, "pełna lodówka — mleko do bańki: " + r.Msg);
            // stary zapis: gospodarstwa z dawnym zapasem dostają limit z krów
            var S2 = Simulation.NewGame(GameMode.Kariera, 5); S2.farms[1].max = 20; S2.farms[1].stock = 18; S2.farms.RemoveAt(S2.farms.Count - 1);
            Simulation.Migrate(S2);
            Check(S2.farms.Count == GameData.Farms.Length && S2.farms[1].stock * CareerData.LitersPerFarmUnit <= CareerSim.Cows(1) * 20 + 1e-6, "migracja: nowe gospodarstwa dodane, zapas przycięty do krów");
        }

        static void HandSale()
        {
            _log.AppendLine("Sprzedaż z ręki:");
            var w = new World(); var S = Simulation.NewGame(GameMode.Kariera, 11); var sim = new Simulation(S, w); var p = S.players[0]; var c = p.career;
            c.basket[(int)Product.Twarog] = 2;
            Check(new TakeFromBasket(0, Product.Twarog).Execute(sim).Ok && c.hand == (int)Product.Twarog && c.basket[(int)Product.Twarog] == 1, "z koszyka do ręki");
            Check(!new TakeFromBasket(0, Product.Twarog).Execute(sim).Ok, "druga sztuka — ręce zajęte");
            Check(!new HandSell(0, Product.Twarog).Execute(sim).Ok && c.hand >= 0, "sprzedaż tylko na rynku");
            w.place = new Place(PlaceKind.Rynek);
            Check(!new HandSell(0, Product.Jogurt).Execute(sim).Ok && c.hand == (int)Product.Twarog, "klient chce czegoś innego — towar zostaje w ręku");
            double before = p.money; var r = new HandSell(0, Product.Twarog).Execute(sim);
            Check(r.Ok && c.hand == -1 && p.money > before && c.unitsSold == 1, "podanie klientowi: " + r.Msg);
            Check(new TakeFromBasket(0, Product.Twarog).Execute(sim).Ok && new ReturnHand(0).Execute(sim).Ok && c.basket[(int)Product.Twarog] == 1 && c.hand == -1, "odłożenie do koszyka");
            var back = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(S)); Simulation.Migrate(back);
            Check(back.players[0].career.hand == -1 && back.players[0].carriedOrder == -1, "zapis: puste ręce");
        }

        static void Institutions()
        {
            _log.AppendLine("Placówki:");
            var w = new World(); var S = Simulation.NewGame(GameMode.Swobodna, 13); var sim = new Simulation(S, w); var p = S.players[0];
            var o = new OrderState { id = 9001, client = "Szkoła", icon = "school", reward = 50, deadline = 1e9 }; o.items[(int)Product.Uht] = 4; S.orders.Add(o);
            o.status = OrderStatus.Przyjete; o.owner = 0;
            for (int i = 0; i < o.items.Length; i++) p.vanCargo[i] = o.items[i];
            int school = Simulation.ClientIndex("Szkoła");
            w.place = new Place(PlaceKind.Placowka, Simulation.ClientIndex("Sklep"));
            Check(!new CarryOrder(0, o.id).Execute(sim).Ok, "zamówienie szkoły nie pod sklepem");
            w.place = new Place(PlaceKind.Placowka, school);
            Check(new CarryOrder(0, o.id).Execute(sim).Ok && p.vanCargo.Sum() == 0 && p.carried.Sum() == o.items.Sum() && p.carriedOrder == o.id, "skrzynka z dostawczaka do rąk");
            Check(new ReturnOrderToVan(0).Execute(sim).Ok && p.vanCargo.Sum() == o.items.Sum() && p.carriedOrder == -1, "odłożenie do dostawczaka");
            new CarryOrder(0, o.id).Execute(sim);
            double before = p.money; var r = new HandOverOrder(0).Execute(sim);
            Check(r.Ok && o.status == OrderStatus.Dostarczone && p.money > before && p.carried.Sum() == 0, "przekazanie odbiorcy: " + r.Msg);
        }

        static void Migration()
        {
            _log.AppendLine("Migracja:");
            var S = Simulation.NewGame(GameMode.Swobodna, 5);
            S.players[0].career = null;
            Simulation.Migrate(S);
            Check(S.players[0].career != null && !S.players[0].career.active, "stary zapis: kariera nieaktywna");
            var json = JsonUtility.ToJson(Simulation.NewGame(GameMode.Kariera, 5));
            var back = JsonUtility.FromJson<GameState>(json); Simulation.Migrate(back);
            Check(CareerSim.Active(back.players[0]) && back.players[0].career.owned.Count == 3, "zapis i odczyt kariery (JSON)");
        }
    }
}
