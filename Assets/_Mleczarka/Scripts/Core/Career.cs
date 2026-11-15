using System;
using System.Collections.Generic;
using System.Linq;

namespace Mleczarka.Core
{
    // =====================================================================
    //  Tryb „Kariera: od kuchni”. Start w domu: rower, bańka na mleko, kuchnia.
    //  Mleko kupuje się u gospodarzy na litry, produkty robi ręcznie w letniej kuchni,
    //  sprzedaje na straganie na rynku, a pieniądze idą na sprzęt, pojazdy i w końcu mleczarnię.
    //  Wszystko jest deterministyczne i liczone w symulacji — scena tylko pokazuje stan.
    // =====================================================================

    [Serializable]
    public class CareerState
    {
        public bool active;
        public double canister, canisterCap = 10;      // litry w bańce na rowerze
        public double fridge, fridgeCap = 20;          // litry mleka w lodówce
        public double cream;                           // litry śmietanki w lodówce
        public int[] pantry = new int[GameData.ProductCount];   // gotowe produkty w spiżarce
        public int[] basket = new int[GameData.ProductCount];   // towar w koszyku roweru
        public int basketCap = 6;
        public double[] demand = Enumerable.Repeat(1.0, GameData.ProductCount).ToArray();   // nasycenie rynku (1 = pełna cena)
        public List<string> owned = new List<string>();
        public List<KitchenJob> jobs = new List<KitchenJob>();
        public int nextJob;
        public int hand = -1;                          // produkt trzymany w dłoniach (sprzedaż z ręki klientowi), -1 = nic
        public double sold;                            // zarobek ze straganu (statystyka)
        public int unitsSold;
        public double cowMilk = 20;                    // mleko od własnej krowy na działce (darmowe, do 20 l na dobę)
    }

    [Serializable]
    public class KitchenJob
    {
        public int id; public string recipe; public int qty, step; public double progress;
        public bool Done => step >= CareerData.R(recipe).Steps.Length;
    }

    public sealed class Recipe
    {
        public string Id, Name, Station, Needs, Info;
        public int Product = -1;          // -1 = śmietanka (zbieranie z mleka)
        public double Milk, Cream;        // na sztukę (dla śmietanki: litry mleka na „porcję”)
        public double CreamOut;           // śmietanka z jednej porcji
        public StepDef[] Steps;
    }

    public sealed class CareerItem
    {
        public string Id, Name, Info, Requires; public double Price; public int Tier;
    }

    public static class CareerData
    {
        public const double StartMoney = 60;
        public const double MilkPriceFactor = 1.6;       // gospodarz sprzedaje na litry drożej niż w umowie
        public const double LitersPerFarmUnit = 10;      // 1 „litr umowny” zapasu gospodarstwa = 10 l w bańce
        public const int SmallPot = 2, BigPot = 6;
        public const double LitersPerCow = 20;           // krowa daje 20 l mleka na dobę
        public const double OwnCowMax = 20;              // własna krowa: tyle mleka czeka najwyżej do wydojenia

        // ceny na straganie (domowe produkty)
        public static double Price(Product p)
        {
            switch (p)
            {
                case Product.Twarog: return 14; case Product.MlekoPast: return 4; case Product.Jogurt: return 6; case Product.Kefir: return 6;
                case Product.Smietana: return 9; case Product.Maslo: return 13; case Product.Ser: return 45; default: return GameData.P(p).Price;
            }
        }

        public static readonly Recipe[] Recipes =
        {
            new Recipe { Id = "twarog", Name = "Twaróg", Product = (int)Product.Twarog, Station = "kuchenka", Milk = 3,
                Info = "Mleko podgrzewa się, aż się zetnie, potem odcedza przez gazę. Serwatka spływa, zostaje twaróg.",
                Steps = new[] { new StepDef(StepKind.Hold, "Podgrzej mleko w garnku", 2, 0.5), new StepDef(StepKind.Auto, "Mleko się ścina", 18, 4), new StepDef(StepKind.Hold, "Odcedź przez gazę", 2, 0.5) } },
            new Recipe { Id = "mleko", Name = "Mleko w butelkach", Product = (int)Product.MlekoPast, Station = "kuchenka", Milk = 1,
                Info = "Zagotowane i ostudzone mleko dłużej się trzyma.",
                Steps = new[] { new StepDef(StepKind.Hold, "Zagotuj mleko", 2, 0.3), new StepDef(StepKind.Auto, "Studzenie", 10, 1), new StepDef(StepKind.Tap, "Przelej do butelek") } },
            new Recipe { Id = "jogurt", Name = "Jogurt w słoikach", Product = (int)Product.Jogurt, Station = "kuchenka", Milk = 1,
                Info = "Mleko podgrzane do 45°C z łyżką jogurtu jako zakwasem gęstnieje w cieple przez kilka godzin.",
                Steps = new[] { new StepDef(StepKind.Hold, "Podgrzej do 45°C i dodaj zakwas", 2, 0.4), new StepDef(StepKind.Auto, "Fermentacja w ciepłym miejscu", 40, 4), new StepDef(StepKind.Tap, "Wstaw słoiki do lodówki") } },
            new Recipe { Id = "kefir", Name = "Kefir", Product = (int)Product.Kefir, Station = "stol", Milk = 1, Needs = "grzybek",
                Info = "Grzybek kefirowy (ziarna) zakwasza mleko w temperaturze pokojowej.",
                Steps = new[] { new StepDef(StepKind.Tap, "Dodaj grzybek do mleka"), new StepDef(StepKind.Auto, "Kefir dojrzewa", 45, 4), new StepDef(StepKind.Tap, "Przecedź i rozlej") } },
            new Recipe { Id = "smietanka", Name = "Zbieranie śmietanki", Product = -1, Station = "lodowka", Milk = 5, CreamOut = 0.5,
                Info = "Odstane mleko: śmietanka wypływa na wierzch i zbiera się ją łyżką. Zostaje chude mleko — dobre na twaróg.",
                Steps = new[] { new StepDef(StepKind.Tap, "Odstaw mleko w szerokim garnku"), new StepDef(StepKind.Auto, "Śmietanka wypływa na wierzch", 25, 3), new StepDef(StepKind.Tap, "Zbierz śmietankę łyżką") } },
            new Recipe { Id = "wirowanie", Name = "Wirówka ręczna", Product = -1, Station = "wirowka", Milk = 5, CreamOut = 0.8, Needs = "wirowka_r",
                Info = "Korba kręci bęben, siła odśrodkowa oddziela śmietankę — szybko i dokładniej niż łyżką.",
                Steps = new[] { new StepDef(StepKind.Hold, "Kręć korbą wirówki", 2, 0.6) } },
            new Recipe { Id = "smietana", Name = "Śmietana", Product = (int)Product.Smietana, Station = "stol", Cream = 0.4,
                Info = "Śmietanka z odrobiną zakwasu kwaśnieje i gęstnieje.",
                Steps = new[] { new StepDef(StepKind.Tap, "Dodaj zakwas do śmietanki"), new StepDef(StepKind.Auto, "Kwaśnienie", 25, 3), new StepDef(StepKind.Tap, "Rozlej do słoików") } },
            new Recipe { Id = "maslo", Name = "Masło", Product = (int)Product.Maslo, Station = "maselnica", Cream = 0.5, Needs = "maselnica",
                Info = "Ubijana śmietanka zbija się w ziarno masła; maślankę się odlewa, masło płucze i formuje.",
                Steps = new[] { new StepDef(StepKind.Hold, "Ubijaj w maselnicy", 4, 1), new StepDef(StepKind.Hold, "Wypłucz i uformuj kostki", 2, 0.5) } },
            new Recipe { Id = "ser", Name = "Ser żółty", Product = (int)Product.Ser, Station = "prasa", Milk = 8, Needs = "prasa_r",
                Info = "Mleko z podpuszczką krzepnie, skrzep się kroi i prasuje, a ser dojrzewa.",
                Steps = new[] { new StepDef(StepKind.Hold, "Podgrzej i dodaj podpuszczkę", 3, 0.5), new StepDef(StepKind.Auto, "Krzepnięcie", 25, 3), new StepDef(StepKind.Hold, "Kroj i prasuj", 3, 1), new StepDef(StepKind.Auto, "Dojrzewanie", 60, 6) } },
        };

        public static Recipe R(string id) => Recipes.FirstOrDefault(r => r.Id == id);

        public static readonly CareerItem[] Items =
        {
            new CareerItem { Id = "rower", Name = "Rower z koszykiem", Price = 0, Tier = 0, Info = "Na start. B — wsiądź / zsiądź." },
            new CareerItem { Id = "banka", Name = "Bańka na mleko (10 l)", Price = 0, Tier = 0, Info = "Na start. Wozi się na bagażniku roweru." },
            new CareerItem { Id = "kuchnia", Name = "Letnia kuchnia, garnek, słoiki", Price = 0, Tier = 0, Info = "Na start: twaróg, mleko, jogurt, śmietana." },
            new CareerItem { Id = "maselnica", Name = "Maselnica", Price = 90, Tier = 1, Info = "Odblokowuje masło." },
            new CareerItem { Id = "grzybek", Name = "Grzybek kefirowy", Price = 35, Tier = 1, Info = "Odblokowuje kefir." },
            new CareerItem { Id = "garnek_duzy", Name = "Duży garnek", Price = 150, Tier = 1, Info = "Partie do 6 sztuk zamiast 2." },
            new CareerItem { Id = "przyczepka", Name = "Przyczepka rowerowa", Price = 320, Tier = 2, Info = "Bańki 40 l zamiast 10 l, koszyk 20 sztuk zamiast 6." },
            new CareerItem { Id = "wirowka_r", Name = "Wirówka ręczna", Price = 280, Tier = 2, Info = "Szybkie oddzielanie śmietanki, 60% więcej śmietanki z mleka." },
            new CareerItem { Id = "lodowka2", Name = "Druga lodówka", Price = 420, Tier = 2, Info = "Lodówka 80 l zamiast 20 l." },
            new CareerItem { Id = "prasa_r", Name = "Prasa do sera", Price = 450, Tier = 2, Info = "Odblokowuje ser żółty." },
            new CareerItem { Id = "stragan", Name = "Własny stragan z daszkiem", Price = 600, Tier = 2, Info = "Ceny na rynku +15%, klienci szybciej wracają." },
            new CareerItem { Id = "dostawczak", Name = "Stary dostawczak", Price = 3500, Tier = 3, Requires = "przyczepka", Info = "Możesz jeździć dostawczakiem: ładujesz towar z domu (F) i realizujesz zamówienia." },
            new CareerItem { Id = "cysterna", Name = "Używana cysterna", Price = 7000, Tier = 3, Requires = "dostawczak", Info = "Umowy z gospodarstwami i odbiór mleka cysterną. Rozładunek do lodówek w domu." },
            new CareerItem { Id = "hala", Name = "Hala mleczarni", Price = 12000, Tier = 4, Requires = "cysterna", Info = "Przejmujesz halę produkcyjną: zbiornik, stara linia, magazyn, zarządzanie (TAB)." },
            new CareerItem { Id = "skrzydlo", Name = "Skrzydło maszyn", Price = 25000, Tier = 5, Requires = "hala", Info = "Pełny ciąg maszyn: wirówka, pasteryzator, UHT, fermentatory, maślnica, sery, pakowanie." },
        };

        public static CareerItem Item(string id) => Items.FirstOrDefault(i => i.Id == id);
        public static readonly string[] StartItems = { "rower", "banka", "kuchnia" };
    }

    /// <summary>Logika kariery. Wszystkie zmiany stanu przez komendy (zapis, testy).</summary>
    public static class CareerSim
    {
        public static bool Active(PlayerState p) => p.career != null && p.career.active;
        public static bool Has(PlayerState p, string id) => !Active(p) || p.career.owned.Contains(id);
        /// <summary>Czy funkcja dawnej gry jest dostępna (poza karierą: zawsze).</summary>
        public static bool Allowed(PlayerState p, string id) => !Active(p) || p.career.owned.Contains(id);
        public static string LockedText(string id) { var i = CareerData.Item(id); return i == null ? "Zablokowane." : $"Najpierw kup: {i.Name} ({Money.Zl(i.Price)} zł) — K: rozwój."; }

        public static int MaxBatch(PlayerState p) => p.career.owned.Contains("garnek_duzy") ? CareerData.BigPot : CareerData.SmallPot;

        public static void Ensure(PlayerState p)
        {
            if (p.career == null) p.career = new CareerState();
            var c = p.career;
            if (c.pantry == null || c.pantry.Length < GameData.ProductCount) c.pantry = Grow(c.pantry);
            if (c.basket == null || c.basket.Length < GameData.ProductCount) c.basket = Grow(c.basket);
            if (c.demand == null || c.demand.Length < GameData.ProductCount)
            { var d = Enumerable.Repeat(1.0, GameData.ProductCount).ToArray(); if (c.demand != null) Array.Copy(c.demand, d, c.demand.Length); c.demand = d; }
            if (c.owned == null) c.owned = new List<string>();
            if (c.jobs == null) c.jobs = new List<KitchenJob>();
        }
        /// <summary>Kariera: gospodarstwo daje tyle mleka, ile mają krowy (krowy × 20 l na dobę), i trzyma najwyżej zapas z jednej doby.</summary>
        public static void ApplyCows(GameState S, bool fill)
        {
            for (int i = 0; i < S.farms.Count && i < GameData.Farms.Length; i++)
            {
                var f = S.farms[i]; int cows = Math.Max(1, GameData.Farms[i].Cows);
                f.max = cows * CareerData.LitersPerCow / CareerData.LitersPerFarmUnit;
                f.rate = f.max / (24.0 * GameConfig.FarmRegenMultiplier);
                f.stock = fill ? f.max : Math.Min(f.stock, f.max);
            }
        }
        public static int Cows(int farm) => farm >= 0 && farm < GameData.Farms.Length ? Math.Max(1, GameData.Farms[farm].Cows) : 0;

        static int[] Grow(int[] a) { var n = new int[GameData.ProductCount]; if (a != null) Array.Copy(a, n, Math.Min(a.Length, n.Length)); return n; }

        public static void Start(GameState S)
        {
            var p = S.players[0]; Ensure(p);
            p.career.active = true;
            p.career.owned.AddRange(CareerData.StartItems);
            ApplyCows(S, true);
            p.money = CareerData.StartMoney;
            p.workers.Clear(); p.tankMilk = 0;
            p.plant.workersOff = true;
            S.orders.Clear();
            S.noTimeLimits = true;
        }

        static void ApplyItem(PlayerState p, string id)
        {
            var c = p.career;
            switch (id)
            {
                case "przyczepka": c.canisterCap = Math.Max(c.canisterCap, 40); c.basketCap = Math.Max(c.basketCap, 20); break;
                case "lodowka2": c.fridgeCap = Math.Max(c.fridgeCap, 80); break;
            }
        }

        // ---------------- komendy ----------------

        public static Result BuyMilk(Simulation sim, int pid, int fid)
        {
            var S = sim.S; var p = S.players[pid]; var c = p.career;
            if (!Active(p)) return Result.Fail("Tylko w karierze.");
            if (!sim.IsAtPlace(pid, PlaceKind.Gospodarstwo) || sim.PlaceOfPublic(pid).FarmId != fid) return Result.Fail("Podejdź do gospodarstwa.");
            var f = S.farms[fid];
            double room = Math.Floor(c.canisterCap - c.canister);
            if (room < 1) return Result.Fail("Bańka jest pełna. Zawieź mleko do domu (lodówka).");
            double avail = Math.Floor(f.stock * CareerData.LitersPerFarmUnit);
            if (avail < 1) return Result.Fail($"{f.name}: dziś nie mają już mleka. Wróć później.");
            double price = Money.M2(f.price * CareerData.MilkPriceFactor);
            double liters = Math.Min(room, avail);
            liters = Math.Min(liters, Math.Floor(p.money / price));
            if (liters < 1) return Result.Fail($"Za mało pieniędzy — litr kosztuje {Money.Zl(price)} zł.");
            double cost = Money.M2(liters * price);
            p.money = Money.M2(p.money - cost); f.stock -= liters / CareerData.LitersPerFarmUnit; c.canister += liters;
            return Result.Success($"Kupiono {liters:0} l mleka od: {f.name} za {Money.Zl(cost)} zł.");
        }

        /// <summary>Własna krowa na działce przy domu: mleko za darmo — najpierw do lodówki, resztę do bańki.</summary>
        public static Result MilkOwnCow(Simulation sim, int pid)
        {
            var p = sim.S.players[pid]; var c = p.career;
            if (!Active(p)) return Result.Fail("Tylko w karierze.");
            if (!sim.IsAtPlace(pid, PlaceKind.Dom)) return Result.Fail("Twoja krowa pasie się na działce przy domu.");
            double avail = Math.Floor(c.cowMilk + 0.01);
            if (avail < 1)
            {
                double h = (1 - c.cowMilk) / CareerData.LitersPerCow * 24;
                return Result.Fail($"Krowa jest wydojona. Następny litr za ok. {Math.Max(1, Math.Ceiling(h)):0} godz.");
            }
            double toFridge = Math.Min(avail, Math.Floor(c.fridgeCap - c.fridge));
            double toCan = Math.Min(avail - toFridge, Math.Floor(c.canisterCap - c.canister));
            if (toFridge + toCan < 1) return Result.Fail("Lodówka i bańka są pełne — najpierw zrób coś z mleka.");
            c.fridge += toFridge; c.canister += toCan; c.cowMilk = Math.Max(0, c.cowMilk - toFridge - toCan);
            string where = toCan > 0 ? $"{toFridge:0} l do lodówki, {toCan:0} l do bańki" : $"do lodówki ({c.fridge:0}/{c.fridgeCap:0} l)";
            return Result.Success($"Wydojono {toFridge + toCan:0} l od własnej krowy — za darmo, {where}.");
        }

        public static Result PourToFridge(Simulation sim, int pid)
        {
            var p = sim.S.players[pid]; var c = p.career;
            if (!sim.IsAtPlace(pid, PlaceKind.Dom)) return Result.Fail("Mleko przelewa się do lodówki w domu.");
            if (c.canister < 1) return Result.Fail("Bańka jest pusta.");
            double room = c.fridgeCap - c.fridge;
            if (room < 1) return Result.Fail("Lodówka jest pełna.");
            double l = Math.Min(room, c.canister);
            c.canister -= l; c.fridge += l;
            return Result.Success($"Przelano {l:0} l do lodówki ({c.fridge:0}/{c.fridgeCap:0} l).");
        }

        public static string StartBlock(PlayerState p, Recipe r, int qty)
        {
            var c = p.career;
            if (r.Needs != null && !c.owned.Contains(r.Needs)) return LockedText(r.Needs);
            if (c.jobs.Any(j => CareerData.R(j.recipe).Station == r.Station)) return "To stanowisko jest zajęte.";
            if (qty < 1 || qty > MaxBatch(p)) return $"Partia: 1–{MaxBatch(p)} szt.";
            if (r.Milk * qty > c.fridge + 1e-6) return $"Brakuje mleka w lodówce: potrzeba {r.Milk * qty:0} l, jest {c.fridge:0} l.";
            if (r.Cream * qty > c.cream + 1e-6) return $"Brakuje śmietanki: potrzeba {r.Cream * qty:0.0} l, jest {c.cream:0.0} l.";
            return "";
        }

        public static Result StartJob(Simulation sim, int pid, string recipe, int qty)
        {
            var p = sim.S.players[pid]; var c = p.career; var r = CareerData.R(recipe);
            if (r == null) return Result.Fail("Nie ma takiego przepisu.");
            if (!sim.IsAtPlace(pid, PlaceKind.Dom)) return Result.Fail("Gotuje się w letniej kuchni.");
            string why = StartBlock(p, r, qty); if (why != "") return Result.Fail(why);
            c.fridge -= r.Milk * qty; c.cream -= r.Cream * qty;
            c.jobs.Add(new KitchenJob { id = c.nextJob++, recipe = recipe, qty = qty });
            return Result.Success($"{r.Name} ×{qty}: {r.Steps[0].Label}" + (r.Steps[0].Kind == StepKind.Hold ? " (przytrzymaj Q)." : " (Q)."));
        }

        public static double Need(StepDef d, int qty) => d.Base + d.PerUnit * qty;

        public static KitchenJob JobAt(PlayerState p, string station) => p.career.jobs.FirstOrDefault(j => CareerData.R(j.recipe).Station == station);

        public static bool NeedsHands(KitchenJob j) { var r = CareerData.R(j.recipe); return !j.Done && r.Steps[j.step].Kind != StepKind.Auto; }

        public static Result Act(Simulation sim, int pid, string station)
        {
            var p = sim.S.players[pid]; var c = p.career;
            if (!sim.IsAtPlace(pid, PlaceKind.Dom)) return Result.Fail("Jesteś poza domem.");
            var j = JobAt(p, station); if (j == null) return Result.Fail("Nic się tu teraz nie robi. Wybierz przepis (1–9) i Q.");
            var r = CareerData.R(j.recipe); var d = r.Steps[j.step];
            if (d.Kind == StepKind.Auto) return Result.Fail($"{d.Label} — trzeba poczekać.");
            if (d.Kind == StepKind.Hold)
            {
                j.progress += PlantData.HoldPerAct;
                double need = Need(d, j.qty);
                if (j.progress < need) return Result.Success($"{d.Label}… {(int)(100 * j.progress / need)}%");
            }
            return Next(sim, p, j);
        }

        static Result Next(Simulation sim, PlayerState p, KitchenJob j)
        {
            var r = CareerData.R(j.recipe); string label = r.Steps[j.step].Label;
            j.step++; j.progress = 0;
            if (!j.Done) return Result.Success($"{label} ✔ Teraz: {r.Steps[j.step].Label}.");
            return Finish(sim, p, j);
        }

        static Result Finish(Simulation sim, PlayerState p, KitchenJob j)
        {
            var c = p.career; var r = CareerData.R(j.recipe);
            c.jobs.Remove(j);
            if (r.Product < 0)
            {
                double cream = r.CreamOut * j.qty;
                c.cream += cream; c.fridge = Math.Min(c.fridgeCap, c.fridge + (r.Milk - r.CreamOut) * j.qty);   // chude mleko wraca do lodówki
                sim.EmitEvent("kitchenDone", p.id, j.id, cream, r.Id);
                return Result.Success($"Zebrano {cream:0.0} l śmietanki. Chude mleko wróciło do lodówki.");
            }
            c.pantry[r.Product] += j.qty; p.made[r.Product] += j.qty;
            sim.EmitEvent("kitchenDone", p.id, j.id, j.qty, ((Product)r.Product).ToString());
            return Result.Success($"Gotowe: {r.Name} ×{j.qty} — w spiżarce.");
        }

        public static Result LoadBasket(Simulation sim, int pid)
        {
            var p = sim.S.players[pid]; var c = p.career;
            if (!sim.IsAtPlace(pid, PlaceKind.Dom)) return Result.Fail("Towar bierze się ze spiżarki w domu.");
            int room = c.basketCap - c.basket.Sum(), n = 0;
            if (room <= 0) return Result.Fail("Koszyk jest pełny.");
            if (c.pantry.Sum() == 0) return Result.Fail("Spiżarka jest pusta — najpierw coś zrób.");
            // najpierw najdroższe
            foreach (var i in Enumerable.Range(0, GameData.ProductCount).OrderByDescending(i => CareerData.Price((Product)i)))
                while (room > 0 && c.pantry[i] > 0) { c.pantry[i]--; c.basket[i]++; room--; n++; }
            return Result.Success($"Do koszyka: {n} szt. ({c.basket.Sum()}/{c.basketCap}).");
        }

        public static double StallPrice(PlayerState p, Product pr)
        {
            double k = p.career.owned.Contains("stragan") ? 1.15 : 1.0;
            return Money.M2(CareerData.Price(pr) * k * p.career.demand[(int)pr]);
        }

        public static Result SellBasket(Simulation sim, int pid)
        {
            var p = sim.S.players[pid]; var c = p.career;
            if (!sim.IsAtPlace(pid, PlaceKind.Rynek)) return Result.Fail("Sprzedaje się na rynku.");
            if (c.basket.Sum() == 0) return Result.Fail("Koszyk jest pusty. Weź towar ze spiżarki (F przy domu).");
            double total = 0; int n = 0;
            for (int i = 0; i < GameData.ProductCount; i++)
                while (c.basket[i] > 0)
                {
                    double price = StallPrice(p, (Product)i);
                    total += price; n++; c.basket[i]--;
                    c.demand[i] = Math.Max(0.35, c.demand[i] * 0.93);
                }
            total = Money.M2(total);
            p.money = Money.M2(p.money + total); p.earned += total; c.sold += total; c.unitsSold += n;
            sim.EmitEvent("stallSold", p.id, -1, total, n.ToString());
            return Result.Success($"Sprzedano {n} szt. za {Money.Zl(total)} zł.");
        }

        // ---------- sprzedaż z ręki ----------

        /// <summary>Bierze jedną sztukę z koszyka roweru do rąk.</summary>
        public static Result TakeFromBasket(Simulation sim, int pid, Product pr)
        {
            var p = sim.S.players[pid]; var c = p.career;
            if (!Active(p)) return Result.Fail("Tylko w karierze.");
            if (c.hand >= 0) return Result.Fail("Masz już towar w rękach — podaj go klientowi albo odłóż (F przy koszyku).");
            if (c.basket[(int)pr] <= 0) return Result.Fail($"W koszyku nie ma: {GameData.P(pr).Name}.");
            c.basket[(int)pr]--; c.hand = (int)pr;
            return Result.Success($"W rękach: {GameData.P(pr).Name}. Podejdź do klienta i naciśnij F.");
        }

        /// <summary>Odkłada towar z rąk z powrotem do koszyka.</summary>
        public static Result ReturnHand(Simulation sim, int pid)
        {
            var c = sim.S.players[pid].career;
            if (c.hand < 0) return Result.Fail("Nic nie trzymasz.");
            c.basket[c.hand]++; c.hand = -1;
            return Result.Success("Odłożono do koszyka.");
        }

        /// <summary>Podanie towaru klientowi na rynku: płaci cenę straganu z dopłatą za obsługę, jeśli dostał to, czego chciał.</summary>
        public static Result HandSell(Simulation sim, int pid, Product wanted)
        {
            var p = sim.S.players[pid]; var c = p.career;
            if (!sim.IsAtPlace(pid, PlaceKind.Rynek)) return Result.Fail("Klienci czekają na rynku.");
            if (c.hand < 0) return Result.Fail("Najpierw weź towar z koszyka (F przy rowerze).");
            if (c.hand != (int)wanted) return Result.Fail($"Klient chce: {GameData.P(wanted).Name}, a nie {GameData.P((Product)c.hand).Name}.");
            double price = Money.M2(StallPrice(p, wanted) * 1.1);
            c.hand = -1; c.demand[(int)wanted] = Math.Max(0.35, c.demand[(int)wanted] * 0.93);
            p.money = Money.M2(p.money + price); p.earned += price; c.sold += price; c.unitsSold++;
            sim.EmitEvent("handSold", p.id, (int)wanted, price, GameData.P(wanted).Name);
            return Result.Success($"Sprzedano: {GameData.P(wanted).Name} za {Money.Zl(price)} zł.");
        }

        public static Result Buy(Simulation sim, int pid, string id)
        {
            var p = sim.S.players[pid]; var c = p.career; var it = CareerData.Item(id);
            if (!Active(p)) return Result.Fail("Tylko w karierze.");
            if (it == null) return Result.Fail("Nie ma takiej rzeczy.");
            if (c.owned.Contains(id)) return Result.Fail("Już to masz.");
            if (it.Requires != null && !c.owned.Contains(it.Requires)) return Result.Fail($"Najpierw: {CareerData.Item(it.Requires).Name}.");
            if (p.money < it.Price) return Result.Fail($"Brakuje {Money.Zl(it.Price - p.money)} zł.");
            p.money = Money.M2(p.money - it.Price); c.owned.Add(id); ApplyItem(p, id);
            sim.EmitEvent("careerBuy", p.id, -1, it.Price, id);
            return Result.Success($"Kupiono: {it.Name}. {it.Info}");
        }

        /// <summary>Cysterna w karierze przed zakupem hali: rozładunek do lodówek w domu.</summary>
        public static Result UnloadTankerHome(Simulation sim, int pid)
        {
            var p = sim.S.players[pid]; var c = p.career;
            if (!sim.IsAtPlace(pid, PlaceKind.Dom)) return Result.Fail("Podjedź pod dom.");
            if (p.tankerMilk < 1) return Result.Fail("Cysterna jest pusta.");
            double room = c.fridgeCap - c.fridge;
            int units = (int)Math.Min(p.tankerMilk, Math.Floor(room / CareerData.LitersPerFarmUnit));
            if (units < 1) return Result.Fail("Lodówki są pełne.");
            p.tankerMilk -= units; c.fridge += units * CareerData.LitersPerFarmUnit;
            return Result.Success($"Do lodówek: {units * CareerData.LitersPerFarmUnit:0} l.");
        }

        /// <summary>Dostawczak w karierze: ładunek ze spiżarki w domu.</summary>
        public static Result LoadVanHome(Simulation sim, int pid)
        {
            var p = sim.S.players[pid]; var c = p.career;
            if (!sim.IsAtPlace(pid, PlaceKind.Dom)) return Result.Fail("Podjedź pod dom.");
            int room = p.vanCap - p.vanCargo.Sum(), n = 0;
            if (room <= 0) return Result.Fail("Dostawczak jest pełny.");
            for (int i = 0; i < GameData.ProductCount; i++) while (room > 0 && c.pantry[i] > 0) { c.pantry[i]--; p.vanCargo[i]++; room--; n++; }
            return n > 0 ? Result.Success($"Załadowano {n} szt. ze spiżarki.") : Result.Fail("Spiżarka jest pusta.");
        }

        public static void Tick(Simulation sim, PlayerState p, double dt)
        {
            var c = p.career;
            foreach (var j in c.jobs.ToList())
            {
                if (j.Done) continue;
                var d = CareerData.R(j.recipe).Steps[j.step];
                if (d.Kind != StepKind.Auto) continue;
                j.progress += dt;
                if (j.progress >= Need(d, j.qty))
                {
                    var r = Next(sim, p, j);
                    sim.EmitEvent("kitchenStep", p.id, j.id, 0, r.Msg);
                }
            }
            c.cowMilk = Math.Min(CareerData.OwnCowMax, c.cowMilk + CareerData.LitersPerCow * dt / GameConfig.DaySeconds);
            double rec = (c.owned.Contains("stragan") ? 0.09 : 0.05) * dt / (GameConfig.DaySeconds / 24);
            for (int i = 0; i < c.demand.Length; i++) c.demand[i] = Math.Min(1, c.demand[i] + rec);
        }
    }

    // ---------------- komendy kariery ----------------
    public sealed class BuyMilkCan : Cmd { public readonly int Farm; public BuyMilkCan(int pid, int f) : base(pid) { Farm = f; } public override Result Execute(Simulation s) => CareerSim.BuyMilk(s, PlayerId, Farm); }
    public sealed class MilkOwnCow : Cmd { public MilkOwnCow(int pid) : base(pid) { } public override Result Execute(Simulation s) => CareerSim.MilkOwnCow(s, PlayerId); }
    public sealed class PourMilk : Cmd { public PourMilk(int pid) : base(pid) { } public override Result Execute(Simulation s) => CareerSim.PourToFridge(s, PlayerId); }
    public sealed class StartKitchen : Cmd { public readonly string Recipe; public readonly int Qty; public StartKitchen(int pid, string r, int q) : base(pid) { Recipe = r; Qty = q; } public override Result Execute(Simulation s) => CareerSim.StartJob(s, PlayerId, Recipe, Qty); }
    public sealed class KitchenAct : Cmd { public readonly string Station; public KitchenAct(int pid, string st) : base(pid) { Station = st; } public override Result Execute(Simulation s) => CareerSim.Act(s, PlayerId, Station); }
    public sealed class LoadBasket : Cmd { public LoadBasket(int pid) : base(pid) { } public override Result Execute(Simulation s) => CareerSim.LoadBasket(s, PlayerId); }
    public sealed class TakeFromBasket : Cmd { public readonly Product Pr; public TakeFromBasket(int pid, Product pr) : base(pid) { Pr = pr; } public override Result Execute(Simulation s) => CareerSim.TakeFromBasket(s, PlayerId, Pr); }
    public sealed class ReturnHand : Cmd { public ReturnHand(int pid) : base(pid) { } public override Result Execute(Simulation s) => CareerSim.ReturnHand(s, PlayerId); }
    public sealed class HandSell : Cmd { public readonly Product Wanted; public HandSell(int pid, Product w) : base(pid) { Wanted = w; } public override Result Execute(Simulation s) => CareerSim.HandSell(s, PlayerId, Wanted); }
    public sealed class SellBasket : Cmd { public SellBasket(int pid) : base(pid) { } public override Result Execute(Simulation s) => CareerSim.SellBasket(s, PlayerId); }
    public sealed class BuyCareerItem : Cmd { public readonly string Id; public BuyCareerItem(int pid, string id) : base(pid) { Id = id; } public override Result Execute(Simulation s) => CareerSim.Buy(s, PlayerId, Id); }
    public sealed class UnloadTankerHome : Cmd { public UnloadTankerHome(int pid) : base(pid) { } public override Result Execute(Simulation s) => CareerSim.UnloadTankerHome(s, PlayerId); }
    public sealed class LoadVanHome : Cmd { public LoadVanHome(int pid) : base(pid) { } public override Result Execute(Simulation s) => CareerSim.LoadVanHome(s, PlayerId); }
}
