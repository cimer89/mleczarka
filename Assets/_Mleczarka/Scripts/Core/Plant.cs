using System;
using System.Collections.Generic;
using System.Linq;

namespace Mleczarka.Core
{
    /// <summary>
    /// Pełny ciąg technologiczny mleczarni (logika bez Unity):
    /// partie przechodzą przez kolejne maszyny, każda maszyna ma kroki obsługi (czynność, przytrzymanie, praca automatyczna),
    /// brudzi się (mycie CIP), zużywa i psuje (naprawa, przegląd). Pracownicy wykonują te same kroki co gracz.
    /// </summary>
    public static class PlantSim
    {
        public const int MaxJobs = 8;

        // ---------------- pomocnicze ----------------

        public static void Ensure(PlayerState p)
        {
            if (p.plant == null) p.plant = new PlantState();
            foreach (var d in PlantData.Machines)
                if (!p.plant.machines.Any(m => m.id == d.Id)) p.plant.machines.Add(new MachineState { id = d.Id, temp = d.IdleTemp });
        }

        public static void Enable(PlayerState p) { Ensure(p); p.plant.enabled = true; }

        public static MachineState Machine(PlayerState p, string id) => p.plant.machines.FirstOrDefault(m => m.id == id);
        public static string TypeOf(JobState j) { var r = PlantData.Route(j.product); return j.stage < r.Length ? r[j.stage] : null; }
        public static StepDef[] StepsOf(JobState j) => PlantData.Steps(TypeOf(j), j.product);
        public static StepDef StepOf(JobState j) { var s = StepsOf(j); return j.step < s.Length ? s[j.step] : null; }
        public static IEnumerable<JobState> JobsAt(PlayerState p, string machineId) => p.plant.jobs.Where(j => j.machine == machineId);
        public static JobState JobAt(PlayerState p, string machineId) => JobsAt(p, machineId).FirstOrDefault();

        public static double Need(Simulation sim, JobState j, StepDef d)
        {
            if (d == null) return 0;
            if (d.Kind == StepKind.Tap) return 1;
            return d.Hours > 0 ? d.Hours * sim.SecPerHour : d.Base + d.PerUnit * j.qty;
        }

        public static double Speed(PlayerState p, MachineState m)
        {
            double s = Math.Pow(GameConfig.Upgrades[(int)UpgradeKind.Maszyny].Mult, p.upgrades[(int)UpgradeKind.Maszyny]);
            if (m.dirt > PlantData.DirtSlow) s *= 1 - 0.4 * (m.dirt - PlantData.DirtSlow) / (PlantData.DirtBlock - PlantData.DirtSlow);
            if (m.condition < 50) s *= 0.8 + 0.4 * m.condition / 100;
            return Math.Max(0.2, s);
        }

        /// <summary>Czy na tej maszynie czeka czynność człowieka (zawór, przytrzymanie, naprawa)?</summary>
        public static bool NeedsHands(PlayerState p, MachineState m)
        {
            if (m.broken) return false;                  // naprawia gracz
            var j = JobAt(p, m.id); var d = j != null ? StepOf(j) : null;
            return d != null && d.Kind != StepKind.Auto && !j.Waiting;
        }

        public static string Status(Simulation sim, PlayerState p, MachineState m)
        {
            var def = PlantData.M(m.id);
            if (m.broken) return "AWARIA: " + m.fault;
            if (m.cip > 0) return $"Mycie CIP… {Math.Ceiling(m.cip)} s";
            var jobs = JobsAt(p, m.id).ToList();
            if (jobs.Count == 0) return m.dirt >= PlantData.DirtBlock ? "Brudna — wymaga mycia" : "Wolna";
            var j = jobs[0]; var d = StepOf(j);
            string what = $"{GameData.P(j.product).Name} ×{j.qty}";
            if (j.Waiting) return $"{what}: {j.wait}";
            if (d == null) return what;
            if (def.Slots > 1 && jobs.Count > 1) what += $" (+{jobs.Count - 1})";
            return $"{what}: {d.Label}";
        }

        // ---------------- start partii ----------------

        public static string StartBlock(Simulation sim, int pid, Product product, int qty)
        {
            var p = sim.S.players[pid]; var d = GameData.P(product);
            if (p.plant == null || !p.plant.enabled) return "Nowa hala nie jest jeszcze uruchomiona.";
            if (qty < 1 || qty > d.MaxBatch) return $"Partia: od 1 do {d.MaxBatch} szt.";
            if (p.plant.jobs.Count >= MaxJobs) return "W hali jest już pełno partii. Dokończ którąś.";
            if (d.MilkPerUnit > 0 && p.tankMilk < d.MilkPerUnit * qty) return $"Brakuje {d.MilkPerUnit * qty - p.tankMilk} l mleka w silosie.";
            if (d.CreamPerUnit > 0 && p.plant.cream + 1e-9 < d.CreamPerUnit * qty) return $"Brakuje {Math.Ceiling(d.CreamPerUnit * qty - p.plant.cream)} l śmietanki. Śmietanka powstaje na wirówce przy mleku, twarogu i serze.";
            double cost = Money.M2(d.IngredientCost * qty);
            if (p.money < cost) return $"Dodatki i opakowania kosztują {Money.Zl(cost)} zł.";
            return "";
        }

        public static Result StartJob(Simulation sim, int pid, Product product, int qty)
        {
            var p = sim.S.players[pid];
            if (!sim.IsAtPlace(pid, PlaceKind.Mleczarnia)) return Result.Fail("Produkcję uruchamia się w mleczarni.");
            string why = StartBlock(sim, pid, product, qty);
            if (why != "") return Result.Fail(why);
            var d = GameData.P(product);
            p.tankMilk -= d.MilkPerUnit * qty;
            p.plant.cream = Math.Max(0, p.plant.cream - d.CreamPerUnit * qty);
            p.money = Money.M2(p.money - d.IngredientCost * qty);
            var j = new JobState { id = p.plant.nextJobId++, product = product, qty = qty, quality = p.plant.rawQuality };
            p.plant.jobs.Add(j);
            sim.EmitEvent("jobStart", pid, j.id, qty, product.ToString());
            return Result.Success($"Partia: {d.Name} ×{qty}. Pierwsza maszyna: {PlantData.M(FirstOfType(PlantData.Route(product)[0])).Name}.");
        }

        static string FirstOfType(string type) => PlantData.Machines.First(m => m.Type == type).Id;

        // ---------------- czynności gracza ----------------

        /// <summary>Q przy maszynie: naprawa, czynność przy partii albo przytrzymanie. Wywoływane co 0,5 s przy przytrzymaniu.</summary>
        public static Result Act(Simulation sim, int pid, string machineId)
        {
            var p = sim.S.players[pid]; var m = Machine(p, machineId);
            if (m == null) return Result.Fail("Nie ma takiej maszyny.");
            if (!sim.IsAtPlace(pid, PlaceKind.Mleczarnia)) return Result.Fail("Jesteś poza mleczarnią.");
            if (m.broken)
            {
                if (p.money < PlantData.RepairCost) return Result.Fail($"Części do naprawy kosztują {PlantData.RepairCost} zł.");
                m.service += PlantData.HoldPerAct;
                if (m.service < PlantData.RepairSeconds) return Result.Success($"Naprawa… {(int)(100 * m.service / PlantData.RepairSeconds)}%");
                m.service = 0; m.broken = false; m.fault = null; m.condition = 100;
                p.money = Money.M2(p.money - PlantData.RepairCost);
                sim.EmitEvent("repaired", pid, -1, PlantData.RepairCost, machineId);
                foreach (var j in JobsAt(p, machineId)) j.wait = null;
                return Result.Success($"Naprawione (−{PlantData.RepairCost} zł za części).");
            }
            if (m.cip > 0) return Result.Fail("Trwa mycie. Poczekaj chwilę.");
            var job = JobsAt(p, machineId).FirstOrDefault(x => !x.Waiting && StepOf(x) != null && StepOf(x).Kind != StepKind.Auto);
            if (job == null)
            {
                var any = JobAt(p, machineId);
                if (any == null) return Result.Fail("Maszyna jest wolna. Partię zaczyna się przy silosie mleka.");
                var d0 = StepOf(any);
                return Result.Fail(any.Waiting ? any.wait : d0 != null && d0.Kind == StepKind.Auto ? $"Maszyna pracuje sama: {d0.Label}." : "Teraz nic tu nie trzeba robić.");
            }
            var d = StepOf(job);
            if (d.Kind == StepKind.Tap) { CompleteStep(sim, p, job, m); return Result.Success(d.Label + " ✔"); }
            job.progress += PlantData.HoldPerAct * Speed(p, m);
            if (job.progress >= Need(sim, job, d)) { CompleteStep(sim, p, job, m); return Result.Success(d.Label + " ✔"); }
            return Result.Success($"{d.Label}… {(int)(100 * job.progress / Need(sim, job, d))}%");
        }

        public static Result StartCip(Simulation sim, int pid, string machineId)
        {
            var p = sim.S.players[pid]; var m = Machine(p, machineId); var def = PlantData.M(machineId);
            if (m == null || def == null) return Result.Fail("Nie ma takiej maszyny.");
            if (!def.Cleanable) return Result.Fail("Tej części hali nie myje się instalacją CIP.");
            if (m.broken) return Result.Fail("Najpierw napraw maszynę.");
            if (m.cip > 0) return Result.Fail("Mycie już trwa.");
            if (JobAt(p, machineId) != null) return Result.Fail("W maszynie jest partia. Myje się pustą maszynę.");
            if (m.dirt < 5) return Result.Fail("Maszyna jest czysta.");
            if (p.money < PlantData.CipCost) return Result.Fail($"Środki myjące kosztują {PlantData.CipCost} zł.");
            p.money = Money.M2(p.money - PlantData.CipCost);
            m.cip = PlantData.CipSeconds;
            sim.EmitEvent("cipStart", pid, -1, 0, machineId);
            return Result.Success("Mycie CIP: ług, płukanie, kwas, płukanie.");
        }

        public static Result Service(Simulation sim, int pid, string machineId)
        {
            var p = sim.S.players[pid]; var m = Machine(p, machineId);
            if (m == null) return Result.Fail("Nie ma takiej maszyny.");
            if (m.broken) return Act(sim, pid, machineId);
            if (JobAt(p, machineId) != null) return Result.Fail("Przegląd robi się przy pustej maszynie.");
            if (m.condition >= 99) return Result.Fail("Maszyna jest w świetnym stanie.");
            if (p.money < PlantData.ServiceCost) return Result.Fail($"Przegląd kosztuje {PlantData.ServiceCost} zł.");
            m.service += PlantData.HoldPerAct;
            if (m.service < PlantData.ServiceSeconds) return Result.Success($"Przegląd… {(int)(100 * m.service / PlantData.ServiceSeconds)}%");
            m.service = 0; m.condition = Math.Min(100, m.condition + 40);
            p.money = Money.M2(p.money - PlantData.ServiceCost);
            return Result.Success($"Przegląd zrobiony (−{PlantData.ServiceCost} zł).");
        }

        public static Result TestSample(Simulation sim, int pid)
        {
            var p = sim.S.players[pid]; var pl = p.plant;
            if (pl == null || !pl.enabled) return Result.Fail("Laboratorium działa w nowej hali.");
            if (!sim.IsAtPlace(pid, PlaceKind.Mleczarnia)) return Result.Fail("Próbkę bada się w laboratorium mleczarni.");
            if (p.tankerMilk < 1) return Result.Fail("Cysterna jest pusta — nie ma czego badać.");
            if (pl.sampleTested) return Result.Success(SampleText(pl.sampleResult));
            pl.sampleProgress += PlantData.HoldPerAct;
            if (pl.sampleProgress < PlantData.SampleSeconds) return Result.Success($"Badanie próbki… {(int)(100 * pl.sampleProgress / PlantData.SampleSeconds)}%");
            pl.sampleProgress = 0; pl.sampleTested = true; pl.sampleResult = p.tankerQuality;
            sim.EmitEvent("sample", pid, -1, p.tankerQuality);
            return Result.Success(SampleText(pl.sampleResult));
        }

        public static string SampleText(double q)
        {
            double fat = 3.2 + 0.9 * q, bact = q >= 0.8 ? 20 + (1 - q) * 300 : 120 + (0.8 - q) * 900;
            string verdict = q >= 0.8 ? "klasa ekstra — można przyjąć" : q >= 0.6 ? "klasa I — można przyjąć" : "ZA DUŻO BAKTERII — lepiej odrzucić";
            return $"Próbka: tłuszcz {fat:0.0}%, bakterie {bact:0} tys./ml — {verdict}.";
        }

        public static Result RejectMilk(Simulation sim, int pid)
        {
            var p = sim.S.players[pid]; var pl = p.plant;
            if (pl == null || !pl.sampleTested) return Result.Fail("Najpierw zbadaj próbkę.");
            if (p.tankerMilk < 1) return Result.Fail("Cysterna jest pusta.");
            int l = p.tankerMilk; p.tankerMilk = 0; pl.sampleTested = false; pl.sampleResult = -1; p.tankerQuality = 1;
            sim.EmitEvent("rejected", pid, -1, l);
            return Result.Success($"Odrzucono {l} l mleka. Lepsza strata niż zepsuty towar u klientów.");
        }

        public static Result AssignWorker(Simulation sim, int pid, int wid, string machineId)
        {
            var p = sim.S.players[pid];
            if (WorkersOff(p)) return Result.Fail("Pracownicy są wyłączeni — halę obsługujesz sam (można to zmienić w menu Escape).");
            if (wid < 0 || wid >= p.workers.Count) return Result.Fail("Nie ma takiego pracownika.");
            var w = p.workers[wid];
            foreach (var m in p.plant.machines) if (m.worker == wid) m.worker = -1;
            if (string.IsNullOrEmpty(machineId)) { w.auto = true; return Result.Success($"{w.name} pracuje tam, gdzie trzeba."); }
            var mm = Machine(p, machineId); if (mm == null) return Result.Fail("Nie ma takiej maszyny.");
            mm.worker = wid; w.auto = false; w.walkTo = machineId; w.walkT = GameConfig.WorkerWalkSeconds;
            return Result.Success($"{w.name} obsługuje: {PlantData.M(machineId).Name}.");
        }

        // ---------------- krok czasu ----------------

        static readonly string[] Faults = { "pęknięta uszczelka", "zatarte łożysko", "przepalony silnik", "nieszczelny zawór", "zepsuty czujnik temperatury" };

        public static void Tick(Simulation sim, PlayerState p, double dt)
        {
            var pl = p.plant; Ensure(p);

            // mycie, temperatura
            foreach (var m in pl.machines)
            {
                var def = PlantData.M(m.id);
                if (m.cip > 0) { m.cip -= dt; m.temp += (75 - m.temp) * Math.Min(1, dt * 0.6); if (m.cip <= 0) { m.cip = 0; m.dirt = 0; sim.EmitEvent("cipDone", p.id, -1, 0, m.id); } continue; }
                var j = JobAt(p, m.id); var d = j != null && !j.Waiting ? StepOf(j) : null;
                double target = def.IdleTemp;
                if (d != null && d.TargetTemp > 0)
                {
                    // przy podgrzewaniu temperatura rośnie razem z postępem kroku
                    target = d.Kind == StepKind.Hold ? def.IdleTemp + (d.TargetTemp - def.IdleTemp) * Math.Min(1, j.progress / Math.Max(0.01, Need(sim, j, d))) : d.TargetTemp;
                }
                else if (j != null && j.step > 0) target = m.temp;   // między krokami temperatura się trzyma
                m.temp += (target - m.temp) * Math.Min(1, dt * 1.5);
            }

            // partie w kolejce: przydział wolnej maszyny
            foreach (var j in pl.jobs.ToList())
            {
                if (j.machine != null) continue;
                string type = TypeOf(j);
                if (type == null) { Finish(sim, p, j); continue; }
                var cands = pl.machines.Where(m => PlantData.M(m.id).Type == type).ToList();
                var free = cands.FirstOrDefault(m => !m.broken && m.cip <= 0 && m.dirt < PlantData.DirtBlock && JobsAt(p, m.id).Count() < PlantData.M(m.id).Slots);
                if (free == null)
                {
                    var c0 = cands[0];
                    j.wait = cands.All(m => m.broken) ? $"{PlantData.M(c0.id).Name}: awaria — napraw ją" :
                             cands.All(m => m.dirt >= PlantData.DirtBlock) ? $"{PlantData.M(c0.id).Name} jest brudna — umyj ją (CIP)" :
                             $"Czeka na: {PlantData.M(c0.id).Name}";
                    continue;
                }
                j.machine = free.id; j.step = 0; j.progress = 0; j.wait = null; j.worker = -1;
                // zużyta maszyna może się zepsuć przy rozruchu
                if (free.condition < 40 && Rng.Value(sim.S) < 0.05 + (40 - free.condition) / 100.0)
                {
                    free.broken = true; free.fault = Rng.Pick(sim.S, Faults);
                    sim.EmitEvent("breakdown", p.id, -1, 0, free.id);
                }
                sim.EmitEvent("jobAt", p.id, j.id, 0, free.id);
            }

            // praca na maszynach
            foreach (var j in pl.jobs.ToList())
            {
                if (j.machine == null) continue;
                var m = Machine(p, j.machine);
                if (m.broken) { j.wait = $"Awaria: {m.fault}. Napraw (przytrzymaj Q przy maszynie)."; continue; }
                if (j.Waiting && j.wait.StartsWith("Awaria")) j.wait = null;
                if (j.Waiting) { TryOutput(sim, p, j); continue; }
                var d = StepOf(j); if (d == null) { NextStage(sim, p, j, m); continue; }
                double need = Need(sim, j, d), sp = Speed(p, m);
                if (d.Kind == StepKind.Auto)
                {
                    j.progress += dt * (d.Hours > 0 ? 1 : sp);   // procesów biologicznych nie przyspieszy czysta maszyna
                    if (j.progress >= need) CompleteStep(sim, p, j, m);
                    continue;
                }
                // czynność człowieka: pracownik przy tej maszynie (w wersji bez pracowników — tylko gracz)
                if (pl.workersOff) { j.worker = -1; continue; }
                var w = p.workers.FirstOrDefault(x => !x.Walking && x.station == m.id);
                j.worker = w != null ? w.id : -1;
                if (w == null) continue;
                double eff = p.selected == w.id ? GameConfig.WorkerSelectedEfficiency : GameConfig.WorkerAutoEfficiency;
                j.progress += dt * eff * (d.Kind == StepKind.Hold ? sp : 1);
                if (j.progress >= (d.Kind == StepKind.Tap ? PlantData.WorkerTapDelay : need)) CompleteStep(sim, p, j, m);
            }

            if (!pl.workersOff) WorkersTick(sim, p, dt);
        }

        public static bool WorkersOff(PlayerState p) => p.plant != null && p.plant.enabled && p.plant.workersOff;

        static void CompleteStep(Simulation sim, PlayerState p, JobState j, MachineState m)
        {
            var steps = StepsOf(j); var d = steps[j.step];
            j.step++; j.progress = 0; j.worker = -1;
            sim.EmitEvent("step", p.id, j.id, j.step, m.id);
            if (PlantData.M(m.id).Type == "wirowka" && d.Label.StartsWith("Skieruj"))
            {
                double add = GameData.P(j.product).MilkPerUnit * j.qty * PlantData.CreamYield;
                p.plant.cream = Math.Min(p.plant.creamCap, p.plant.cream + add);
            }
            if (j.step >= steps.Length) NextStage(sim, p, j, m);
        }

        static void NextStage(Simulation sim, PlayerState p, JobState j, MachineState m)
        {
            var def = PlantData.M(m.id);
            m.dirt = Math.Min(PlantData.DirtBlock, m.dirt + def.DirtPerJob);
            m.condition = Math.Max(0, m.condition - def.WearPerJob);
            m.jobsDone++;
            if (m.dirt >= PlantData.DirtBlock) sim.EmitEvent("dirty", p.id, -1, 0, m.id);
            j.stage++; j.step = 0; j.progress = 0;
            if (j.stage >= PlantData.Route(j.product).Length) { j.wait = null; if (!TryOutput(sim, p, j)) j.wait = "Chłodnia jest pełna — wywieź towar."; return; }
            j.machine = null; j.wait = null;
        }

        static bool TryOutput(Simulation sim, PlayerState p, JobState j)
        {
            if (j.stage < PlantData.Route(j.product).Length) return false;
            if (Simulation.FreeStorage(p) < j.qty) { j.wait = "Chłodnia jest pełna — wywieź towar."; return false; }
            Finish(sim, p, j);
            return true;
        }

        static void Finish(Simulation sim, PlayerState p, JobState j)
        {
            (p.manualHandling ? p.packingRack : p.store)[(int)j.product] += j.qty;
            p.made[(int)j.product] += j.qty;
            p.plant.jobs.Remove(j);
            if (j.quality < 0.6) { p.rep = Math.Max(0, p.rep - 1); sim.EmitEvent("badQuality", p.id, j.id, j.quality, j.product.ToString()); }
            sim.EmitEvent("jobDone", p.id, j.id, j.qty, j.product.ToString());
        }

        static void WorkersTick(Simulation sim, PlayerState p, double dt)
        {
            var pl = p.plant;
            foreach (var w in p.workers)
                if (w.Walking) { w.walkT -= dt; if (w.walkT <= 0) { w.station = w.walkTo; w.walkTo = null; } }
            foreach (var w in p.workers)
            {
                if (w.Walking) continue;
                var fixedM = pl.machines.FirstOrDefault(m => m.worker == w.id);
                if (fixedM != null) { if (w.station != fixedM.id) { w.walkTo = fixedM.id; w.walkT = GameConfig.WorkerWalkSeconds; } continue; }
                if (!w.auto) continue;
                var here = Machine(p, w.station);
                if (here != null && NeedsHands(p, here)) continue;
                // najpierw maszyny bliżej końca trasy (żeby kończyć partie), potem reszta; potem mycie brudnych, wolnych maszyn
                var target = pl.jobs.Where(j => j.machine != null && !j.Waiting && StepOf(j) != null && StepOf(j).Kind != StepKind.Auto)
                    .OrderByDescending(j => j.stage).Select(j => Machine(p, j.machine))
                    .FirstOrDefault(m => !m.broken && !p.workers.Any(o => o != w && o.Target == m.id));
                if (target != null) { w.walkTo = target.id; w.walkT = GameConfig.WorkerWalkSeconds; continue; }
                var dirty = pl.machines.FirstOrDefault(m => PlantData.M(m.id).Cleanable && m.cip <= 0 && !m.broken && m.dirt >= PlantData.DirtSlow && JobAt(p, m.id) == null && p.money >= PlantData.CipCost);
                if (dirty != null)
                {
                    if (w.station == dirty.id) { p.money = Money.M2(p.money - PlantData.CipCost); dirty.cip = PlantData.CipSeconds; sim.EmitEvent("cipStart", p.id, w.id, 0, dirty.id); }
                    else if (!p.workers.Any(o => o != w && o.Target == dirty.id)) { w.walkTo = dirty.id; w.walkT = GameConfig.WorkerWalkSeconds; }
                }
            }
        }
    }

    // ---------------- komendy ----------------

    public sealed class StartJob : Cmd
    {
        public readonly Product Product; public readonly int Qty;
        public StartJob(int pid, Product product, int qty) : base(pid) { Product = product; Qty = qty; }
        public override Result Execute(Simulation s) => PlantSim.StartJob(s, PlayerId, Product, Qty);
    }
    public sealed class MachineAct : Cmd
    {
        public readonly string Machine;
        public MachineAct(int pid, string machine) : base(pid) { Machine = machine; }
        public override Result Execute(Simulation s) => PlantSim.Act(s, PlayerId, Machine);
    }
    public sealed class StartCip : Cmd
    {
        public readonly string Machine;
        public StartCip(int pid, string machine) : base(pid) { Machine = machine; }
        public override Result Execute(Simulation s) => PlantSim.StartCip(s, PlayerId, Machine);
    }
    public sealed class ServiceMachine : Cmd
    {
        public readonly string Machine;
        public ServiceMachine(int pid, string machine) : base(pid) { Machine = machine; }
        public override Result Execute(Simulation s) => PlantSim.Service(s, PlayerId, Machine);
    }
    public sealed class TestSample : Cmd
    {
        public TestSample(int pid) : base(pid) { }
        public override Result Execute(Simulation s) => PlantSim.TestSample(s, PlayerId);
    }
    public sealed class RejectMilk : Cmd
    {
        public RejectMilk(int pid) : base(pid) { }
        public override Result Execute(Simulation s) => PlantSim.RejectMilk(s, PlayerId);
    }
    public sealed class AssignWorker : Cmd
    {
        public readonly int Worker; public readonly string Machine;
        public AssignWorker(int pid, int worker, string machine) : base(pid) { Worker = worker; Machine = machine; }
        public override Result Execute(Simulation s) => PlantSim.AssignWorker(s, PlayerId, Worker, Machine);
    }
}

namespace Mleczarka.Core
{
    /// <summary>Włącza albo wyłącza pracowników w hali (ustawienie zapisu gry).</summary>
    public sealed class SetWorkersOff : Cmd
    {
        public readonly bool Off;
        public SetWorkersOff(int pid, bool off) : base(pid) { Off = off; }
        public override Result Execute(Simulation s)
        {
            var p = s.S.players[PlayerId]; PlantSim.Ensure(p);
            p.plant.workersOff = Off;
            if (Off) foreach (var j in p.plant.jobs) j.worker = -1;
            return Result.Success(Off ? "Pracownicy wyłączeni: całą halę obsługujesz sam, bez pensji." : "Pracownicy włączeni: pomagają w hali i dostają pensję.");
        }
    }
}
