using System;
using System.Linq;

namespace Mleczarka.Core
{
    /// <summary>Przenoszenie towaru w obrębie tego samego stanu gry, bez tworzenia kopii zapasów.</summary>
    public static class ProductionLogistics
    {
        public const int CarryCapacity = 4;
        static bool Valid(PlayerState p, Product product)
        {
            int i = (int)product;
            return p != null && p.manualHandling && i >= 0 && i < GameData.ProductCount && GameData.P(product).Ready
                && p.carried != null && p.packingRack != null && p.store != null && p.vanCargo != null;
        }

        public static Result Pick(PlayerState p, Product product, bool fromRack)
        {
            if (!Valid(p, product)) return Result.Fail("Nieprawidłowy produkt lub tryb pracy.");
            if (p.carried.Sum() > 0) return Result.Fail("Najpierw odłóż niesioną skrzynkę.");
            int i = (int)product; var source = fromRack ? p.packingRack : p.store;
            int qty = Math.Min(CarryCapacity, source[i]);
            if (qty <= 0) return Result.Fail(fromRack ? "Najpierw dokończ pakowanie." : "Brak tego produktu w magazynie.");
            source[i] -= qty; p.carried[i] += qty; p.carriedFromStore = !fromRack;
            return Result.Success("Niesiesz " + qty + " × " + GameData.P(product).Name + ".");
        }

        public static Result PutInStore(PlayerState p)
        {
            if (p == null || !p.manualHandling || p.carried == null) return Result.Fail("Brak trybu pracy w hali.");
            int qty = p.carried.Sum();
            if (qty <= 0) return Result.Fail("Nie niesiesz skrzynki.");
            if (p.store.Sum() + qty > p.storeCap) return Result.Fail("Brak miejsca w magazynie.");
            for (int i = 0; i < GameData.ProductCount; i++) { p.store[i] += p.carried[i]; p.carried[i] = 0; }
            p.carriedFromStore = false;
            return Result.Success("Odłożono " + qty + " szt. do magazynu.");
        }

        public static Result PutInVan(PlayerState p)
        {
            if (p == null || !p.manualHandling || p.carried == null) return Result.Fail("Brak trybu pracy w hali.");
            int qty = p.carried.Sum();
            if (qty <= 0) return Result.Fail("Weź skrzynkę z magazynu.");
            // produkty bez chłodzenia (UHT, ser dojrzały) można zanieść od razu z pakowania do dostawczaka
            if (!p.carriedFromStore)
                for (int i = 0; i < GameData.ProductCount; i++)
                    if (p.carried[i] > 0 && GameData.P((Product)i).NeedsCold)
                        return Result.Fail(GameData.P((Product)i).Name + " musi najpierw trafić do chłodni (magazyn).");
            if (p.vanCargo.Sum() + qty > p.vanCap) return Result.Fail("Skrzynka nie mieści się w dostawczaku. Zwolnij miejsce.");
            for (int i = 0; i < GameData.ProductCount; i++) { p.vanCargo[i] += p.carried[i]; p.carried[i] = 0; }
            p.carriedFromStore = false;
            return Result.Success("Załadowano " + qty + " szt. Jedź do punktu sprzedaży.");
        }
    }

    public enum CrateAction { PickPacked, PickStored, Store, Load }
    public sealed class MoveCrate : Cmd
    {
        public readonly CrateAction Action;
        public readonly Product Product;
        public MoveCrate(int playerId, CrateAction action, Product product = Product.Uht) : base(playerId)
        { Action = action; Product = product; }
        public override Result Execute(Simulation sim)
        {
            if (PlayerId < 0 || PlayerId >= sim.S.players.Count || sim.Places == null || sim.Places.PlaceOf(PlayerId).Kind != PlaceKind.Mleczarnia)
                return Result.Fail("Skrzynki przenosi się w mleczarni.");
            var p = sim.S.players[PlayerId];
            switch (Action)
            {
                case CrateAction.PickPacked: return ProductionLogistics.Pick(p, Product, true);
                case CrateAction.PickStored: return ProductionLogistics.Pick(p, Product, false);
                case CrateAction.Store: return ProductionLogistics.PutInStore(p);
                case CrateAction.Load: return ProductionLogistics.PutInVan(p);
                default: return Result.Fail("Nieznana czynność.");
            }
        }
    }
}
