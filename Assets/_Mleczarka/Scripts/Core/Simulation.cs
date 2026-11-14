using System;
using System.Collections.Generic;
using System.Linq;

namespace Mleczarka.Core
{
    /// <summary>
    /// Symulacja mleczarni: czysta logika bez Unity (port engine/game.js z prototypu).
    /// Każda akcja ma id gracza, więc ten sam kod obsłuży wielu graczy; stan to GameState (zapis i synchronizacja).
    /// </summary>
    public class Simulation
    {
        public GameState S { get; private set; }
        public IPlaceProvider Places { get; set; }
        public readonly List<GameEvent> Events = new List<GameEvent>();

        public Simulation(GameState state, IPlaceProvider places = null) { S = state; Places = places; }

        public double SecPerHour => GameConfig.DaySeconds / 24.0;
        void Emit(string type, int pid = -1, int id = -1, double value = 0, string text = null) => Events.Add(new GameEvent(type, pid, id, value, text));
        static Result Ok(string m = "") => Result.Success(m);
        static Result Fail(string m) => Result.Fail(m);
        static double M2(double x) => Money.M2(x);
        internal void EmitEvent(string type, int pid = -1, int id = -1, double value = 0, string text = null) => Emit(type, pid, id, value, text);
        internal bool IsAtPlace(int pid, PlaceKind kind) => IsAt(pid, kind);

        // ---------------- tworzenie gry ----------------

        /// <summary>Uzupełnia stan ze starszego zapisu o nowe dane (np. gospodarstwa dodane z większą mapą).</summary>
        public static void Migrate(GameState s)
        {
            foreach (var p in s.players)
            {
                // tablice produktów rosną razem z listą produktów (stare zapisy miały 2–4 pozycje)
                p.packingRack = Grow(p.packingRack); p.carried = Grow(p.carried); p.store = Grow(p.store);
                p.vanCargo = Grow(p.vanCargo); p.made = Grow(p.made);
                if (p.plant == null) p.plant = new PlantState();
                if (p.plant.machines == null) p.plant.machines = new List<MachineState>();
                if (p.plant.jobs == null) p.plant.jobs = new List<JobState>();
                if (p.tankerQuality <= 0) p.tankerQuality = 1;
                if (p.plant.rawQuality <= 0) p.plant.rawQuality = 1;
                if (p.plant.creamCap <= 0) p.plant.creamCap = 40;
                if (p.plant.enabled) PlantSim.Ensure(p);
            }
            foreach (var o in s.orders) o.items = Grow(o.items);
            if (s.noTimeLimits)
            {
                // bez ograniczeń czasowych: zamówienia, które przepadły tylko z braku czasu, wracają, a przegrana z czasu misja trwa dalej
                foreach (var o in s.orders.Where(o => o.status == OrderStatus.Przeterminowane))
                    o.status = o.owner >= 0 ? OrderStatus.Przyjete : OrderStatus.Otwarte;
                if (s.mission != null && s.mission.active && s.mission.over && !s.mission.win) s.mission.over = false;
            }
            for (int i = s.farms.Count; i < GameData.Farms.Length; i++)
            {
                var f = GameData.Farms[i];
                s.farms.Add(new FarmState { id = i, name = f.Name, stock = f.Stock, max = f.Max, rate = f.Rate, basePrice = f.BasePrice, price = f.BasePrice, owner = f.Owner });
            }
        }

        static int[] Grow(int[] a)
        {
            if (a != null && a.Length >= GameData.ProductCount) return a;
            var n = new int[GameData.ProductCount];
            if (a != null) Array.Copy(a, n, a.Length);
            return n;
        }

        public static GameState NewGame(GameMode mode = GameMode.Misja, uint seed = 12345)
        {
            var S = new GameState { mode = mode, calm = mode == GameMode.Misja, rng = seed == 0 ? 1u : seed, nextOrderT = GameConfig.OrderSpawnSeconds };
            S.players.Add(MkPlayer(0, "Twoja mleczarnia", GameConfig.StartMoney, false));
            S.players.Add(MkPlayer(1, "Mleczpol", GameConfig.RivalMoney, true));
            for (int i = 0; i < GameData.Farms.Length; i++)
            {
                var f = GameData.Farms[i];
                S.farms.Add(new FarmState { id = i, name = f.Name, stock = f.Stock, max = f.Max, rate = f.Rate, basePrice = f.BasePrice, price = f.BasePrice, owner = f.Owner });
            }
            var p = S.players[0];
            foreach (var st in new[] { "uht", "past", "packC" }.Take(GameConfig.StartWorkers)) AddWorker(p, st);
            if (mode == GameMode.Misja)
            {
                double L = GameConfig.MissionSeconds;
                S.mission = new MissionState { active = true, id = 1, title = "Mleko dla szkoły", limit = L };
                S.orders.Add(MkOrder(S, "Szkoła", "school", Items((Product.Uht, 4)), 40, L * 0.8, true, 0));
                S.orders.Add(MkOrder(S, "Sklep", "shop", Items((Product.Jogurt, 2)), 45, L * 0.9, true, 0));
                S.orders.Add(MkOrder(S, "Restauracja", "restaurant", Items((Product.Uht, 3), (Product.Jogurt, 2)), 60, L, true, 0));
            }
            else for (int i = 0; i < 3; i++) S.orders.Add(GenOrder(S));
            return S;
        }

        static PlayerState MkPlayer(int id, string name, double money, bool ai) => new PlayerState
        {
            id = id, name = name, ai = ai, money = money, vehicle = VehicleKind.Cysterna,
            tankerCap = GameConfig.TruckCapacity, vanCap = GameConfig.VanCapacity, tankCap = GameConfig.TankCapacity, storeCap = GameConfig.StoreCapacity,
        };

        public static void AddWorker(PlayerState p, string station)
        {
            p.workers.Add(new WorkerState { id = p.workers.Count, name = GameData.WorkerNames[p.workers.Count % GameData.WorkerNames.Length], station = station, auto = true });
        }

        static int[] Items(params (Product p, int q)[] list) { var a = new int[GameData.ProductCount]; foreach (var (p, q) in list) a[(int)p] = q; return a; }

        static OrderState MkOrder(GameState S, string client, string icon, int[] items, double reward, double deadline, bool mission = false, int owner = -1) => new OrderState
        {
            id = S.nextOrderId++, client = client, icon = icon, items = items, reward = reward, deadline = deadline,
            status = owner >= 0 ? OrderStatus.Przyjete : OrderStatus.Otwarte, owner = owner, mission = mission,
        };

        public static OrderState GenOrder(GameState S)
        {
            var (client, icon) = Rng.Pick(S, GameData.Clients);
            var items = new int[GameData.ProductCount];
            // klient zamawia 1–3 różne produkty; mleko najczęściej, sery i masło rzadziej i w mniejszych ilościach
            var pool = new (Product p, double w, int min, int max)[]
            {
                (Product.Uht, 3, 3, 8), (Product.MlekoPast, 2, 3, 6), (Product.Jogurt, 2.5, 1, 3), (Product.Kefir, 1.5, 1, 3),
                (Product.Smietana, 1.5, 1, 3), (Product.Maslo, 1.2, 1, 2), (Product.Twarog, 1.2, 1, 3), (Product.Ser, 0.8, 1, 2),
            };
            int kinds = Rng.Value(S) < 0.5 ? 1 : Rng.Value(S) < 0.7 ? 2 : 3;
            for (int k = 0; k < kinds; k++)
            {
                double total = pool.Sum(x => items[(int)x.p] > 0 ? 0 : x.w), r = Rng.Value(S) * total;
                foreach (var x in pool)
                {
                    if (items[(int)x.p] > 0) continue;
                    r -= x.w; if (r <= 0) { items[(int)x.p] = Rng.Range(S, x.min, x.max); break; }
                }
            }
            double b = 0; for (int i = 0; i < items.Length; i++) b += GameData.Products[i].Price * items[i];
            return MkOrder(S, client, icon, items, Math.Round(b * GameConfig.OrderRewardMult + 5), S.t + Rng.Range(S, GameConfig.OrderDeadlineMin, GameConfig.OrderDeadlineMax));
        }

        // ---------------- czas ----------------

        public (int day, int hh, int mm, string text) Clock()
        {
            double h = GameConfig.StartHour + S.t / SecPerHour;
            int day = (int)Math.Floor(h / 24) + 1, hh = (int)Math.Floor(h % 24), mm = (int)Math.Floor(h % 1 * 60);
            return (day, hh, mm, $"{hh:00}:{mm:00}");
        }

        // ---------------- miejsca ----------------

        Place PlaceOf(int pid) => Places?.PlaceOf(pid) ?? Place.None;
        bool IsAt(int pid, PlaceKind kind, int farm = -1)
        {
            var pl = PlaceOf(pid);
            return pl.Kind == kind && (farm < 0 || pl.FarmId == farm);
        }

        // ---------------- pompowanie mleka ----------------

        public Result StartCollect(int pid, int fid, double liters = 1e9)
        {
            var p = S.players[pid]; var f = S.farms[fid];
            if (S.pump.Active) return Fail("Pompa już pracuje.");
            if (!IsAt(pid, PlaceKind.Gospodarstwo, fid)) return Fail("Podjedź bliżej gospodarstwa.");
            if (p.vehicle != VehicleKind.Cysterna) return Fail("Mleko zbiera cysterna, nie dostawczak.");
            if (f.owner != pid) return Fail("Najpierw podpisz umowę z tym gospodarstwem.");
            if (f.stock < 1) return Fail("Tu nie ma teraz mleka. Krowy dają je powoli.");
            if (p.tankerMilk >= p.tankerCap) return Fail("Cysterna jest pełna.");
            if (p.money < f.price) return Fail($"Brakuje pieniędzy. Litr kosztuje {Money.Zl(f.price)} zł.");
            // jakość mleka z tego gospodarstwa przy tym odbiorze (zwykle dobra; czasem za dużo bakterii)
            double q = Rng.Value(S) < PlantData.BadMilkChance ? 0.35 + 0.2 * Rng.Value(S) : 0.85 + 0.15 * Rng.Value(S);
            S.pump = new PumpState { kind = PumpKind.Odbior, pid = pid, fid = fid, left = liters, quality = q };
            Fact("cow");
            return Ok();
        }

        public Result StartUnload(int pid)
        {
            var p = S.players[pid];
            if (S.pump.Active) return Fail("Pompa już pracuje.");
            if (!IsAt(pid, PlaceKind.Mleczarnia)) return Fail("Podjedź do mleczarni.");
            if (p.vehicle != VehicleKind.Cysterna) return Fail("Rozładowuje się cysternę.");
            if (p.tankerMilk < 1) return Fail("Cysterna jest pusta.");
            if (p.tankMilk >= p.tankCap) return Fail("Zbiornik w mleczarni jest pełny. Zrób najpierw produkty.");
            if (p.plant != null && p.plant.enabled && !p.plant.sampleTested) return Fail("Najpierw zbadaj próbkę mleka w laboratorium przyjęć (Q przy stanowisku badania).");
            S.pump = new PumpState { kind = PumpKind.Rozladunek, pid = pid, left = 1e9 };
            Fact("tanker");
            return Ok();
        }

        public void StopPump() { if (S.pump.Active) FinishPump(""); }

        string CollectOne(PlayerState p, FarmState f)
        {
            if (f.stock < 1) return "W gospodarstwie skończyło się mleko.";
            if (p.tankerMilk >= p.tankerCap) return "Cysterna pełna!";
            if (p.money < f.price) return "Skończyły się pieniądze.";
            f.stock -= 1; p.tankerQuality = (p.tankerQuality * p.tankerMilk + S.pump.quality) / (p.tankerMilk + 1); p.tankerMilk += 1; p.money = M2(p.money - f.price);
            if (p.plant != null) { p.plant.sampleTested = false; p.plant.sampleResult = -1; }   // nowe mleko = nowa próbka
            return null;
        }

        string UnloadOne(PlayerState p)
        {
            if (p.tankerMilk < 1) return "Cysterna pusta!";
            if (p.tankMilk >= p.tankCap) return "Zbiornik w mleczarni jest pełny.";
            if (p.plant != null) p.plant.rawQuality = (p.plant.rawQuality * p.tankMilk + p.tankerQuality) / (p.tankMilk + 1);
            p.tankerMilk -= 1; p.tankMilk += 1;
            if (p.tankerMilk == 0) { p.tankerQuality = 1; if (p.plant != null) { p.plant.sampleTested = false; p.plant.sampleResult = -1; } }
            return null;
        }

        void FinishPump(string why)
        {
            var P = S.pump; S.pump = new PumpState();
            Emit("pumpDone", P.pid, P.fid, P.moved, why);
        }

        void PumpTick(double dt)
        {
            var P = S.pump; if (!P.Active) return;
            var p = S.players[P.pid];
            P.acc += GameConfig.PumpLitersPerSecond * dt;
            while (P.acc >= 1)
            {
                if (P.left <= 0) { FinishPump(""); return; }
                string err = P.kind == PumpKind.Odbior ? CollectOne(p, S.farms[P.fid]) : UnloadOne(p);
                if (err != null) { FinishPump(err); return; }
                P.acc -= 1; P.left -= 1; P.moved += 1;
                if (P.kind == PumpKind.Odbior) P.cost = M2(P.cost + S.farms[P.fid].price);
            }
            if (P.left <= 0) FinishPump("");
        }

        // ---------------- produkcja i pracownicy ----------------

        public static RecipeStage StageOf(BatchState b) => GameData.Recipes[b.product][b.stage];
        public IEnumerable<BatchState> BatchesAt(PlayerState p, string st) => p.batches.Where(b => StageOf(b).Station == st);
        public double StageNeed(BatchState b) { var sg = StageOf(b); return sg.Kind == StationKind.Praca ? sg.Base + sg.PerUnit * b.qty : sg.Hours * SecPerHour; }
        public static WorkerState WorkerAt(PlayerState p, string st) => p.workers.FirstOrDefault(w => !w.Walking && w.station == st);
        static WorkerState WorkerHeading(PlayerState p, string st) => p.workers.FirstOrDefault(w => w.Target == st);
        bool NeedsWork(PlayerState p, string st) => BatchesAt(p, st).Any(b => !b.Waiting && StageOf(b).Kind == StationKind.Praca);
        static double Efficiency(PlayerState p, WorkerState w) => p.selected == w.id ? GameConfig.WorkerSelectedEfficiency : GameConfig.WorkerAutoEfficiency;
        static double MachineMult(PlayerState p) => Math.Pow(GameConfig.Upgrades[(int)UpgradeKind.Maszyny].Mult, p.upgrades[(int)UpgradeKind.Maszyny]);
        static int Slots(string st) => GameData.Stations.TryGetValue(st, out var d) ? d.Slots : 1;

        public Result MoveWorker(int pid, int wid, string st)
        {
            var p = S.players[pid];
            if (wid < 0 || wid >= p.workers.Count || !GameData.Stations.ContainsKey(st)) return Fail("Nie ma takiego miejsca.");
            var w = p.workers[wid]; string cur = w.Target;
            if (cur == st) return Ok();
            var other = p.workers.FirstOrDefault(o => o != w && o.Target == st && GameData.Stations[st].Kind == StationKind.Praca);
            if (other != null) { other.walkTo = cur; other.walkT = GameConfig.WorkerWalkSeconds; }   // zamiana miejsc
            w.walkTo = st; w.walkT = GameConfig.WorkerWalkSeconds;
            return Ok(other != null ? $"{other.name} zamienia się miejscem z {w.name}." : "");
        }

        public Result SetWorkerAuto(int pid, int wid, bool auto) { var p = S.players[pid]; if (wid >= 0 && wid < p.workers.Count) p.workers[wid].auto = auto; return Ok(); }
        public Result SelectWorker(int pid, int wid) { var p = S.players[pid]; if (wid >= 0 && wid < p.workers.Count) p.selected = wid; return Ok(); }

        void WorkersTick(PlayerState p, double dt)
        {
            if (p.manualHandling && !p.productionStaffAssist) return;
            foreach (var w in p.workers)
                if (w.Walking) { w.walkT -= dt; if (w.walkT <= 0) { w.station = w.walkTo; w.walkTo = null; } }
            foreach (var w in p.workers)
            {
                if (w.Walking || !w.auto || NeedsWork(p, w.station)) continue;
                var st = GameData.WorkOrder.FirstOrDefault(s => NeedsWork(p, s) && WorkerHeading(p, s) == null);
                if (st != null) MoveWorker(p.id, w.id, st);
            }
        }

        void Advance(PlayerState p, BatchState b)
        {
            var R = GameData.Recipes[b.product]; int i = b.stage + 1;
            if (i < R.Length)
            {
                string st = R[i].Station;
                if (BatchesAt(p, st).Count() < Slots(st)) { b.stage = i; b.progress = 0; b.wait = null; Emit("stage", p.id, b.id, 0, st); Fact(st == "ferm" ? "ferm" : st.StartsWith("pack") ? "pack" : null); }
                else b.wait = $"{GameData.Stations[st].Name} jest zajęta.";
                return;
            }
            int free = FreeStorage(p);
            if (free >= b.qty)
            {
                (p.manualHandling ? p.packingRack : p.store)[(int)b.product] += b.qty; p.made[(int)b.product] += b.qty;
                p.batches.Remove(b);
                Emit("batchDone", p.id, b.id, b.qty, b.product.ToString());
            }
            else b.wait = "Magazyn jest pełny.";
        }

        void BatchesTick(PlayerState p, double dt)
        {
            double mult = MachineMult(p);
            foreach (var b in p.batches.ToList())
            {
                var sg = StageOf(b);
                if (b.Waiting) { Advance(p, b); continue; }
                if (sg.Kind == StationKind.Praca)
                {
                    var w = p.manualHandling && !p.productionStaffAssist ? null : WorkerAt(p, sg.Station);
                    b.worker = w != null ? w.id : -1;
                    if (w != null) b.progress += dt * Efficiency(p, w) * mult;
                }
                else b.progress += dt;
                if (b.progress >= StageNeed(b)) Advance(p, b);
            }
        }

        public string BatchBlock(int pid, Product product, int qty)
        {
            var p = S.players[pid]; var P = GameData.P(product);
            if (!P.Ready) return "Tego jeszcze nie umiemy robić.";
            if (qty < 1) return "Wybierz ilość.";
            int need = qty * GameData.LegacyMilk(product);
            if (p.tankMilk < need) return $"Brakuje {need - p.tankMilk} l mleka w zbiorniku.";
            string st = GameData.Recipes[product][0].Station;
            if (BatchesAt(p, st).Count() >= Slots(st)) return $"{GameData.Stations[st].Name} jest zajęta.";
            return "";
        }

        public Result StartBatch(int pid, Product product, int qty)
        {
            var p = S.players[pid];
            if (!IsAt(pid, PlaceKind.Mleczarnia)) return Fail("Produkcję uruchamia się w mleczarni.");
            string why = BatchBlock(pid, product, qty);
            if (why != "") return Fail(why);
            p.tankMilk -= qty * GameData.LegacyMilk(product);
            p.batches.Add(new BatchState { id = S.nextBatchId++, product = product, qty = qty });
            Emit("batchStart", pid, (int)product, qty);
            Fact(product == Product.Uht ? "uht" : "past");
            return Ok();
        }

        public Result Help(int pid, string st)
        {
            var p = S.players[pid]; var b = BatchesAt(p, st).FirstOrDefault(x => !x.Waiting);
            if (b == null) return Fail("Tu nic teraz nie pracuje.");
            if (StageOf(b).Kind == StationKind.Czas) return Fail("Bakterie potrzebują czasu. Tego nie da się przyspieszyć.");
            b.progress += GameConfig.HelpTapSeconds;
            if (b.progress >= StageNeed(b)) Advance(p, b);
            return Ok();
        }

        // ---------------- zamówienia, dostawczak, rynek ----------------

        public IEnumerable<OrderState> ClaimedBy(int pid) => S.orders.Where(o => o.status == OrderStatus.Przyjete && o.owner == pid);

        public Result ClaimOrder(int pid, int oid)
        {
            var o = S.orders.FirstOrDefault(x => x.id == oid);
            if (o == null || o.status != OrderStatus.Otwarte) return Fail("To zamówienie jest już zajęte.");
            if (ClaimedBy(pid).Count() >= GameConfig.OrderMaxClaimed) return Fail("Masz już 3 zamówienia. Najpierw je dostarcz.");
            o.status = OrderStatus.Przyjete; o.owner = pid;
            Fact("order");
            return Ok($"Przyjęto zamówienie: {o.client}.");
        }

        int[] Reserved(int pid)
        {
            var r = new int[GameData.ProductCount];
            foreach (var o in ClaimedBy(pid)) for (int i = 0; i < r.Length; i++) r[i] += o.items[i];
            return r;
        }

        public Result LoadVan(int pid)
        {
            var p = S.players[pid];
            if (p.manualHandling) return Fail("Zanieś skrzynkę z magazynu do dostawczaka i załaduj ją przy pojeździe.");
            if (!IsAt(pid, PlaceKind.Mleczarnia)) return Fail("Towar ładuje się w mleczarni.");
            if (p.vehicle != VehicleKind.Dostawczak) return Fail("Najpierw weź dostawczak.");
            var need = Reserved(pid);
            int space = p.vanCap - p.vanCargo.Sum(), n = 0;
            for (int pass = 0; pass < 2; pass++)
                foreach (var k in GameData.Ready)
                {
                    int i = (int)k;
                    int want = pass == 0 ? Math.Max(0, need[i] - p.vanCargo[i]) : p.store[i];
                    int q = Math.Min(want, Math.Min(p.store[i], space));
                    if (q > 0) { p.store[i] -= q; p.vanCargo[i] += q; space -= q; n += q; }
                }
            if (n == 0) return Fail(space <= 0 ? "Dostawczak jest pełny." : "Magazyn jest pusty. Zrób najpierw produkty.");
            return Ok($"Załadowano {n} szt.");
        }

        public static int FreeStorage(PlayerState p)
        {
            int occupied = p.store.Sum();
            if (p.manualHandling) occupied += p.packingRack.Sum() + p.carried.Sum();
            return Math.Max(0, p.storeCap - occupied);
        }

        public Result UnloadVan(int pid)
        {
            var p = S.players[pid];
            if (!IsAt(pid, PlaceKind.Mleczarnia) || p.vehicle != VehicleKind.Dostawczak) return Fail("Rozładować można tylko w mleczarni.");
            int free = FreeStorage(p);
            if (free == 0) return Fail("Brak wolnego miejsca w magazynie.");
            int n = 0;
            foreach (var k in GameData.Ready)
            {
                int i = (int)k, q = Math.Min(p.vanCargo[i], free);
                free -= q;
                p.vanCargo[i] -= q; p.store[i] += q; n += q;
            }
            return n > 0 ? Ok($"Odłożono {n} szt. do magazynu.") : Fail("Nie ma co odłożyć.");
        }

        public static bool CanDeliver(PlayerState p, OrderState o) { for (int i = 0; i < o.items.Length; i++) if (p.vanCargo[i] < o.items[i]) return false; return true; }

        public Result Deliver(int pid, int oid)
        {
            var p = S.players[pid]; var o = S.orders.FirstOrDefault(x => x.id == oid);
            if (!IsAt(pid, PlaceKind.Rynek)) return Fail("Zamówienia dowozi się na rynek.");
            if (p.vehicle != VehicleKind.Dostawczak) return Fail("Towar wozi dostawczak.");
            if (o == null || o.status != OrderStatus.Przyjete || o.owner != pid) return Fail("To nie jest twoje zamówienie.");
            if (!CanDeliver(p, o))
            {
                var miss = Enumerable.Range(0, o.items.Length).Where(i => p.vanCargo[i] < o.items[i]).Select(i => $"{o.items[i] - p.vanCargo[i]}× {GameData.Products[i].Name}");
                return Fail($"Brakuje: {string.Join(", ", miss)}.");
            }
            for (int i = 0; i < o.items.Length; i++) p.vanCargo[i] -= o.items[i];
            p.money = M2(p.money + o.reward); p.earned += o.reward; p.delivered++; p.rep++;
            o.status = OrderStatus.Dostarczone; o.doneAt = S.t;
            Emit("delivered", pid, o.id, o.reward, o.client);
            return Ok($"+{o.reward} zł");
        }

        public (double value, int[] q) SellValue(int pid)
        {
            var p = S.players[pid]; var need = Reserved(pid); var q = new int[GameData.ProductCount]; double v = 0;
            foreach (var k in GameData.Ready) { int i = (int)k; q[i] = Math.Max(0, p.vanCargo[i] - need[i]); v += q[i] * GameData.Products[i].Price * GameConfig.MarketSellMult; }
            return (M2(v), q);
        }

        public Result SellRest(int pid)
        {
            var p = S.players[pid];
            if (!IsAt(pid, PlaceKind.Rynek) || p.vehicle != VehicleKind.Dostawczak) return Fail("Sprzedawać można na rynku, z dostawczaka.");
            var (value, q) = SellValue(pid);
            if (value <= 0) return Fail("Nie ma nadwyżek. Cały towar jest do zamówień.");
            for (int i = 0; i < q.Length; i++) p.vanCargo[i] -= q[i];
            p.money = M2(p.money + value); p.earned += value;
            Emit("sold", pid, -1, value);
            return Ok($"+{Money.Zl(value)} zł");
        }

        /// <summary>Zmiana pojazdu (cysterna ↔ dostawczak). W Unity gracz po prostu wsiada do drugiego auta; to ustawia, którym jedzie.</summary>
        public Result SetVehicle(int pid, VehicleKind kind)
        {
            if (S.pump.Active && S.pump.pid == pid) return Fail("Poczekaj, aż pompa skończy.");
            S.players[pid].vehicle = kind;
            return Ok(kind == VehicleKind.Dostawczak ? "Jedziesz dostawczakiem" : "Jedziesz cysterną");
        }

        // ---------------- umowy z gospodarstwami ----------------

        public (bool mine, bool free, double min, double fee, double protectedLeft) OfferInfo(int pid, int fid)
        {
            var f = S.farms[fid];
            if (f.owner == pid) return (true, false, 0, 0, 0);
            bool free = f.owner < 0;
            return (false, free, free ? f.basePrice : M2(f.price + GameConfig.ContractMinRaise),
                    free ? GameConfig.ContractFee : GameConfig.ContractFee * GameConfig.ContractTakeoverFeeMult,
                    free ? 0 : Math.Max(0, f.protectUntil - S.t));
        }

        public Result Bid(int pid, int fid, double price)
        {
            var p = S.players[pid]; var f = S.farms[fid]; var I = OfferInfo(pid, fid);
            if (!p.ai && !IsAt(pid, PlaceKind.Gospodarstwo, fid)) return Fail("Ofertę składa się na miejscu, w gospodarstwie.");
            if (I.mine) return Fail("To już twoje gospodarstwo.");
            if (I.protectedLeft > 0) return Fail($"Ta umowa jest chroniona jeszcze {Math.Ceiling(I.protectedLeft)} s.");
            price = M2(price);
            if (price < I.min - 1e-9) return Fail($"Zaproponuj co najmniej {Money.Zl(I.min)} zł za litr.");
            if (price > GameConfig.ContractMaxPrice) return Fail("Tak wysokiej ceny rolnik się nie spodziewa.");
            if (p.money < I.fee) return Fail($"Podpisanie umowy kosztuje {I.fee} zł.");
            int prev = f.owner;
            p.money = M2(p.money - I.fee); f.owner = pid; f.price = price; f.protectUntil = S.t + GameConfig.ContractProtectSeconds;
            if (S.pump.Active && S.pump.fid == fid && S.pump.pid != pid) StopPump();
            Emit("contract", pid, fid, price, prev.ToString());
            Fact("money");
            return Ok($"Umowa podpisana: {f.name}, {Money.Zl(price)} zł/l.");
        }

        // ---------------- ludzie i rozbudowa ----------------

        public Result Hire(int pid)
        {
            var p = S.players[pid];
            if (PlantSim.WorkersOff(p)) return Fail("Pracownicy są wyłączeni — włączysz ich w menu Escape.");
            if (p.workers.Count >= GameConfig.MaxWorkers) return Fail("Hala jest już pełna ludzi.");
            if (p.money < GameConfig.HireCost) return Fail($"Zatrudnienie kosztuje {GameConfig.HireCost} zł.");
            p.money = M2(p.money - GameConfig.HireCost); AddWorker(p, "tank");
            return Ok($"Witaj w zespole, {p.workers[p.workers.Count - 1].name}!");
        }

        public static double? UpgradeCost(PlayerState p, UpgradeKind kind)
        {
            var u = GameConfig.Upgrades[(int)kind]; int l = p.upgrades[(int)kind];
            return l < u.Cost.Length ? u.Cost[l] : (double?)null;
        }

        public Result Upgrade(int pid, UpgradeKind kind)
        {
            var p = S.players[pid]; var c = UpgradeCost(p, kind);
            if (c == null) return Fail("To już największy rozmiar.");
            if (p.money < c.Value) return Fail($"Potrzeba {c.Value} zł.");
            p.money = M2(p.money - c.Value); p.upgrades[(int)kind]++;
            int st = GameConfig.Upgrades[(int)kind].Step;
            switch (kind)
            {
                case UpgradeKind.Cysterna: p.tankerCap += st; break;
                case UpgradeKind.Dostawczak: p.vanCap += st; break;
                case UpgradeKind.Zbiornik: p.tankCap += st; break;
                case UpgradeKind.Magazyn: p.storeCap += st; break;
            }
            return Ok("Ulepszone!");
        }

        // ---------------- rywal (Mleczpol) ----------------

        void RivalTick(double dt)
        {
            if (!GameConfig.RivalEnabled || S.players.Count < 2) return;
            var R = S.rival; var rp = S.players[1];
            if (S.t >= R.nextBidT)
            {
                R.nextBidT = S.t + GameConfig.RivalBidEvery;
                if (!S.calm)
                {
                    var free = S.farms.Where(f => f.owner < 0).ToList();
                    var mine = S.farms.Where(f => f.owner == 0).ToList();
                    if (free.Count > 0) { var f = Rng.Pick(S, free); Bid(1, f.id, f.basePrice + 0.1); }
                    else if (mine.Count > 1 && Rng.Value(S) < GameConfig.RivalTakeoverChance)
                    {
                        var can = mine.Where(f => f.protectUntil <= S.t).ToList();
                        if (can.Count > 0) { var f = Rng.Pick(S, can); Bid(1, f.id, f.price + GameConfig.ContractMinRaise); }
                    }
                }
            }
            if (S.t >= R.nextClaimT)
            {
                R.nextClaimT = S.t + GameConfig.RivalClaimEvery;
                var o = S.orders.FirstOrDefault(x => x.status == OrderStatus.Otwarte && !x.mission && x.items.Sum() <= R.goods);
                if (o != null && !S.calm)
                {
                    o.status = OrderStatus.Rywal; o.owner = 1; o.doneAt = S.t; R.goods -= o.items.Sum(); rp.money = M2(rp.money + o.reward);
                    Emit("rivalOrder", 1, o.id, o.reward, o.client);
                }
            }
            if (R.wait > 0) { R.wait -= dt; return; }
            if (R.travel > 0) { R.travel -= dt; if (R.travel <= 0) RivalArrived(); return; }
            var fs = S.farms.Where(f => f.owner == 1 && f.stock >= 5).OrderByDescending(f => f.stock).FirstOrDefault();
            if (fs != null) { R.target = fs.id; R.phase = "toFarm"; R.travel = GameConfig.RivalTravelSeconds; }
            else R.wait = 6;
        }

        void RivalArrived()
        {
            var R = S.rival; var rp = S.players[1];
            if (R.phase == "toFarm")
            {
                var f = S.farms[R.target];
                int q = f.owner == 1 ? (int)Math.Floor(Math.Min(f.stock, Math.Min(20, rp.money / f.price))) : 0;
                f.stock -= q; rp.money = M2(rp.money - q * f.price); R.load = q;
                R.phase = "toBase"; R.wait = 4; R.travel = GameConfig.RivalTravelSeconds;
            }
            else if (R.phase == "toBase") { R.goods += (int)Math.Floor(R.load / 1.5); R.load = 0; R.phase = "idle"; R.wait = 8; }
        }

        // ---------------- zamówienia w czasie, misja ----------------

        void OrdersTick()
        {
            var p = S.players[0];
            foreach (var o in S.orders)
                if (!S.noTimeLimits && (o.status == OrderStatus.Otwarte || o.status == OrderStatus.Przyjete) && S.t > o.deadline)
                {
                    if (o.status == OrderStatus.Przyjete && o.owner == 0) { p.rep = Math.Max(0, p.rep - 1); Emit("late", 0, o.id, 0, o.client); }
                    o.status = OrderStatus.Przeterminowane; o.doneAt = S.t;
                }
            S.orders.RemoveAll(o => !o.mission && (o.status == OrderStatus.Dostarczone || o.status == OrderStatus.Przeterminowane || o.status == OrderStatus.Rywal) && S.t - o.doneAt > 45);
            if (S.t >= S.nextOrderT)
            {
                S.nextOrderT = S.t + GameConfig.OrderSpawnSeconds;
                int active = S.orders.Count(o => o.status == OrderStatus.Otwarte || o.status == OrderStatus.Przyjete);
                if (active < GameConfig.OrderMaxActive) { S.orders.Add(GenOrder(S)); Emit("newOrder"); }
            }
        }

        void MissionTick()
        {
            var M = S.mission;
            if (!M.active || M.over) return;
            var mo = S.orders.Where(o => o.mission).ToList();
            if (mo.All(o => o.status == OrderStatus.Dostarczone))
            {
                double left = M.limit - S.t;
                M.over = true; M.win = true; M.stars = S.noTimeLimits ? 3 : left >= 300 ? 3 : left >= 120 ? 2 : 1; M.time = S.t;
                Emit("missionEnd", 0, M.id, M.stars, "win");
            }
            else if (!S.noTimeLimits && (mo.Any(o => o.status == OrderStatus.Przeterminowane) || S.t > M.limit))
            {
                M.over = true; M.win = false; M.time = S.t;
                Emit("missionEnd", 0, M.id, 0, "lose");
            }
        }

        public void ContinueFree() { S.mode = GameMode.Swobodna; S.mission = new MissionState(); S.calm = false; }

        void Fact(string key)
        {
            if (string.IsNullOrEmpty(key) || S.seenFacts.Contains(key)) return;
            S.seenFacts.Add(key);
            Emit("fact", -1, -1, 0, GameData.Facts[key]);
        }

        // ---------------- główny krok ----------------

        public void Tick(double dt)
        {
            if (S.paused || S.mission.over) return;
            dt = Math.Min(dt, 0.25);
            S.t += dt;
            double k = GameConfig.FarmRegenMultiplier * dt / SecPerHour;
            foreach (var f in S.farms) f.stock = Math.Min(f.max, f.stock + f.rate * k);
            PumpTick(dt);
            foreach (var p in S.players) if (!p.ai)
                {
                    if (p.plant != null && p.plant.enabled)
                    {
                        PlantSim.Tick(this, p, dt);
                        if (p.batches.Count > 0) BatchesTick(p, dt);   // partie zaczęte na dawnej linii kończą się normalnie
                    }
                    else { WorkersTick(p, dt); BatchesTick(p, dt); }
                }
            OrdersTick();
            RivalTick(dt);
            int day = Clock().day;
            if (day > S.lastDay)
            {
                S.lastDay = day;
                foreach (var p in S.players) if (!p.ai && !PlantSim.WorkersOff(p)) { double w = p.workers.Count * GameConfig.WagePerDay; p.money = M2(p.money - w); Emit("wages", p.id, day, w); }
            }
            MissionTick();
        }
    }
}
