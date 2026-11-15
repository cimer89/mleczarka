using System.Collections.Generic;

namespace Mleczarka.Core
{
    /// <summary>
    /// Zmiana stanu gry tylko przez komendy. Komenda ma id gracza i dane; dziś wykonuje ją LocalCommandBus,
    /// w multiplayerze ten sam strumień pójdzie do autorytatywnego hosta (Mleczarka.Net.Abstractions).
    /// </summary>
    public interface ICommand
    {
        int PlayerId { get; }
        Result Execute(Simulation sim);
    }

    public interface ICommandBus
    {
        Result Send(ICommand cmd);
    }

    /// <summary>Wykonuje komendy od razu na lokalnej symulacji i zapamiętuje historię (do testów i powtórek).</summary>
    public sealed class LocalCommandBus : ICommandBus
    {
        readonly Simulation _sim;
        public readonly List<(double t, ICommand cmd, Result res)> Log = new List<(double, ICommand, Result)>();
        public LocalCommandBus(Simulation sim) { _sim = sim; }
        public Result Send(ICommand cmd)
        {
            var r = cmd.Execute(_sim);
            Log.Add((_sim.S.t, cmd, r));
            if (Log.Count > 500) Log.RemoveAt(0);
            return r;
        }
    }

    public abstract class Cmd : ICommand
    {
        public int PlayerId { get; }
        protected Cmd(int pid) { PlayerId = pid; }
        public abstract Result Execute(Simulation sim);
    }

    public sealed class CollectMilk : Cmd { public readonly int Farm; public CollectMilk(int pid, int farm) : base(pid) { Farm = farm; } public override Result Execute(Simulation s) => s.StartCollect(PlayerId, Farm); }
    public sealed class UnloadMilk : Cmd { public UnloadMilk(int pid) : base(pid) { } public override Result Execute(Simulation s) => s.StartUnload(PlayerId); }
    public sealed class StopPump : Cmd { public StopPump(int pid) : base(pid) { } public override Result Execute(Simulation s) { s.StopPump(); return Result.Success(); } }
    public sealed class StartBatch : Cmd { public readonly Product Product; public readonly int Qty; public StartBatch(int pid, Product p, int q) : base(pid) { Product = p; Qty = q; } public override Result Execute(Simulation s) => s.StartBatch(PlayerId, Product, Qty); }
    public sealed class MoveWorker : Cmd { public readonly int Worker; public readonly string Station; public MoveWorker(int pid, int w, string st) : base(pid) { Worker = w; Station = st; } public override Result Execute(Simulation s) => s.MoveWorker(PlayerId, Worker, Station); }
    public sealed class SelectWorkerCmd : Cmd { public readonly int Worker; public SelectWorkerCmd(int pid, int w) : base(pid) { Worker = w; } public override Result Execute(Simulation s) => s.SelectWorker(PlayerId, Worker); }
    public sealed class HelpStation : Cmd { public readonly string Station; public HelpStation(int pid, string st) : base(pid) { Station = st; } public override Result Execute(Simulation s) => s.Help(PlayerId, Station); }
    public sealed class ClaimOrder : Cmd { public readonly int Order; public ClaimOrder(int pid, int o) : base(pid) { Order = o; } public override Result Execute(Simulation s) => s.ClaimOrder(PlayerId, Order); }
    public sealed class LoadVan : Cmd { public LoadVan(int pid) : base(pid) { } public override Result Execute(Simulation s) => s.LoadVan(PlayerId); }
    public sealed class UnloadVan : Cmd { public UnloadVan(int pid) : base(pid) { } public override Result Execute(Simulation s) => s.UnloadVan(PlayerId); }
    public sealed class CarryOrder : Cmd { public readonly int Order; public CarryOrder(int pid, int o) : base(pid) { Order = o; } public override Result Execute(Simulation s) => s.CarryOrder(PlayerId, Order); }
    public sealed class HandOverOrder : Cmd { public HandOverOrder(int pid) : base(pid) { } public override Result Execute(Simulation s) => s.HandOverOrder(PlayerId); }
    public sealed class ReturnOrderToVan : Cmd { public ReturnOrderToVan(int pid) : base(pid) { } public override Result Execute(Simulation s) => s.ReturnOrderToVan(PlayerId); }
    public sealed class Deliver : Cmd { public readonly int Order; public Deliver(int pid, int o) : base(pid) { Order = o; } public override Result Execute(Simulation s) => s.Deliver(PlayerId, Order); }
    public sealed class SellRest : Cmd { public SellRest(int pid) : base(pid) { } public override Result Execute(Simulation s) => s.SellRest(PlayerId); }
    public sealed class SetVehicle : Cmd { public readonly VehicleKind Kind; public SetVehicle(int pid, VehicleKind k) : base(pid) { Kind = k; } public override Result Execute(Simulation s) => s.SetVehicle(PlayerId, Kind); }
    public sealed class BidForContract : Cmd { public readonly int Farm; public readonly double Price; public BidForContract(int pid, int f, double price) : base(pid) { Farm = f; Price = price; } public override Result Execute(Simulation s) => s.Bid(PlayerId, Farm, Price); }
    public sealed class HireWorker : Cmd { public HireWorker(int pid) : base(pid) { } public override Result Execute(Simulation s) => s.Hire(PlayerId); }
    public sealed class BuyUpgrade : Cmd { public readonly UpgradeKind Kind; public BuyUpgrade(int pid, UpgradeKind k) : base(pid) { Kind = k; } public override Result Execute(Simulation s) => s.Upgrade(PlayerId, Kind); }
}
