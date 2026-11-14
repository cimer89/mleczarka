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
            try { Loop(); Locks(); Migration(); }
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
