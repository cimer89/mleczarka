using System.Collections.Generic;

namespace Mleczarka.Core
{
    public enum Product { Uht = 0, Jogurt = 1, Maslo = 2, Ser = 3, MlekoPast = 4, Kefir = 5, Smietana = 6, Twarog = 7 }
    public enum StationKind { Zbiornik, Praca, Czas, Magazyn }

    public sealed class ProductDef
    {
        public Product Id; public string Name, Unit; public int MilkPerUnit; public double Price; public bool Ready; public int MaxBatch;
        public int CreamPerUnit;            // śmietanka z wirówki (śmietana, masło) zamiast mleka
        public double IngredientCost;       // kultury, podpuszczka, opakowania na partię (zł za sztukę)
    }

    public sealed class StationDef
    {
        public string Id, Name, Info; public StationKind Kind; public int Slots = 1;
    }

    public sealed class RecipeStage
    {
        public string Station; public StationKind Kind; public double Base, PerUnit, Hours; public string Label;
    }

    public sealed class FarmDef
    {
        public string Name; public double Stock, Max, Rate, BasePrice; public int Owner; // -1 = wolne
    }

    /// <summary>Dane gry z prototypu (data.js): produkty, stanowiska, receptury, gospodarstwa, klienci, ciekawostki.</summary>
    public static class GameData
    {
        public const int ProductCount = 8;

        public static readonly ProductDef[] Products =
        {
            new ProductDef { Id = Product.Uht, Name = "Mleko UHT", Unit = "karton", MilkPerUnit = 1, Price = 4.5, Ready = true, MaxBatch = 10, IngredientCost = 0.2 },
            new ProductDef { Id = Product.Jogurt, Name = "Jogurt", Unit = "zgrzewka", MilkPerUnit = 2, Price = 12, Ready = true, MaxBatch = 6, IngredientCost = 0.8 },
            new ProductDef { Id = Product.Maslo, Name = "Masło", Unit = "zgrzewka kostek", MilkPerUnit = 0, CreamPerUnit = 3, Price = 30, Ready = true, MaxBatch = 4, IngredientCost = 0.6 },
            new ProductDef { Id = Product.Ser, Name = "Ser żółty", Unit = "krążek", MilkPerUnit = 5, Price = 42, Ready = true, MaxBatch = 4, IngredientCost = 1.5 },
            new ProductDef { Id = Product.MlekoPast, Name = "Mleko świeże", Unit = "karton", MilkPerUnit = 1, Price = 4, Ready = true, MaxBatch = 10, IngredientCost = 0.2 },
            new ProductDef { Id = Product.Kefir, Name = "Kefir", Unit = "zgrzewka", MilkPerUnit = 2, Price = 11, Ready = true, MaxBatch = 6, IngredientCost = 0.8 },
            new ProductDef { Id = Product.Smietana, Name = "Śmietana", Unit = "zgrzewka", MilkPerUnit = 0, CreamPerUnit = 1, Price = 9, Ready = true, MaxBatch = 6, IngredientCost = 0.5 },
            new ProductDef { Id = Product.Twarog, Name = "Twaróg", Unit = "kostka", MilkPerUnit = 3, Price = 18, Ready = true, MaxBatch = 6, IngredientCost = 0.6 },
        };
        public static ProductDef P(Product p) => Products[(int)p];
        /// <summary>Mleko surowe na sztukę w starej hali (produkty ze śmietanki liczą ok. 4 l mleka na 1 l śmietanki).</summary>
        public static int LegacyMilk(Product p) { var d = P(p); return d.MilkPerUnit > 0 ? d.MilkPerUnit : d.CreamPerUnit * 4; }
        public static readonly Product[] Ready = { Product.Uht, Product.MlekoPast, Product.Jogurt, Product.Kefir, Product.Smietana, Product.Maslo, Product.Twarog, Product.Ser };

        public static readonly Dictionary<string, StationDef> Stations = new Dictionary<string, StationDef>
        {
            ["tank"] = new StationDef { Id = "tank", Name = "Zbiornik mleka", Kind = StationKind.Zbiornik, Info = "Tu czeka schłodzone mleko z cysterny." },
            ["uht"] = new StationDef { Id = "uht", Name = "Linia UHT", Kind = StationKind.Praca, Info = "Ogrzewa mleko do ok. 135–140°C na 2–4 sekundy." },
            ["past"] = new StationDef { Id = "past", Name = "Pasteryzator", Kind = StationKind.Praca, Info = "Do jogurtu grzeje mleko do ok. 90°C i schładza do 42°C." },
            ["ferm"] = new StationDef { Id = "ferm", Name = "Fermentownia", Kind = StationKind.Czas, Slots = 2, Info = "Bakterie jogurtowe pracują w 42°C. Trwa to kilka godzin." },
            ["packC"] = new StationDef { Id = "packC", Name = "Pakowarka kartonów", Kind = StationKind.Praca, Info = "Pakuje mleko UHT w sterylne kartony." },
            ["packK"] = new StationDef { Id = "packK", Name = "Pakowarka kubków", Kind = StationKind.Praca, Info = "Rozlewa jogurt do kubków i zamyka je wieczkiem." },
            ["store"] = new StationDef { Id = "store", Name = "Magazyn", Kind = StationKind.Magazyn, Info = "Gotowe produkty czekają tu na dostawczak." },
        };

        /// <summary>Stanowiska, na które pracownik idzie sam (najpierw kończymy produkty).</summary>
        public static readonly string[] WorkOrder = { "packC", "packK", "uht", "past" };

        public static readonly Dictionary<Product, RecipeStage[]> Recipes = new Dictionary<Product, RecipeStage[]>
        {
            [Product.Uht] = new[]
            {
                new RecipeStage { Station = "uht", Kind = StationKind.Praca, Base = 4, PerUnit = 0.5, Label = "Ogrzewanie UHT" },
                new RecipeStage { Station = "packC", Kind = StationKind.Praca, Base = 3, PerUnit = 0.4, Label = "Pakowanie w kartony" },
            },
            [Product.Jogurt] = new[]
            {
                new RecipeStage { Station = "past", Kind = StationKind.Praca, Base = 4, PerUnit = 0.6, Label = "Pasteryzacja" },
                new RecipeStage { Station = "ferm", Kind = StationKind.Czas, Hours = 4, Label = "Fermentacja" },
                new RecipeStage { Station = "packK", Kind = StationKind.Praca, Base = 3, PerUnit = 0.5, Label = "Pakowanie w kubki" },
            },
            // uproszczone receptury dla starej hali (bez pełnego ciągu maszyn); pełne trasy są w PlantData
            [Product.MlekoPast] = new[] { new RecipeStage { Station = "past", Kind = StationKind.Praca, Base = 3, PerUnit = 0.4, Label = "Pasteryzacja" }, new RecipeStage { Station = "packC", Kind = StationKind.Praca, Base = 3, PerUnit = 0.4, Label = "Pakowanie w kartony" } },
            [Product.Kefir] = new[] { new RecipeStage { Station = "past", Kind = StationKind.Praca, Base = 4, PerUnit = 0.6, Label = "Pasteryzacja" }, new RecipeStage { Station = "ferm", Kind = StationKind.Czas, Hours = 6, Label = "Fermentacja kefirowa" }, new RecipeStage { Station = "packK", Kind = StationKind.Praca, Base = 3, PerUnit = 0.5, Label = "Rozlew do butelek" } },
            [Product.Smietana] = new[] { new RecipeStage { Station = "past", Kind = StationKind.Praca, Base = 3, PerUnit = 0.4, Label = "Pasteryzacja śmietanki" }, new RecipeStage { Station = "packK", Kind = StationKind.Praca, Base = 3, PerUnit = 0.5, Label = "Rozlew do kubków" } },
            [Product.Maslo] = new[] { new RecipeStage { Station = "past", Kind = StationKind.Praca, Base = 4, PerUnit = 0.5, Label = "Pasteryzacja śmietanki" }, new RecipeStage { Station = "packC", Kind = StationKind.Praca, Base = 6, PerUnit = 1, Label = "Ubijanie i pakowanie" } },
            [Product.Twarog] = new[] { new RecipeStage { Station = "past", Kind = StationKind.Praca, Base = 4, PerUnit = 0.5, Label = "Pasteryzacja" }, new RecipeStage { Station = "ferm", Kind = StationKind.Czas, Hours = 2, Label = "Krzepnięcie" }, new RecipeStage { Station = "packK", Kind = StationKind.Praca, Base = 4, PerUnit = 0.5, Label = "Odciekanie i pakowanie" } },
            [Product.Ser] = new[] { new RecipeStage { Station = "past", Kind = StationKind.Praca, Base = 4, PerUnit = 0.6, Label = "Pasteryzacja" }, new RecipeStage { Station = "ferm", Kind = StationKind.Czas, Hours = 8, Label = "Krzepnięcie i dojrzewanie" }, new RecipeStage { Station = "packC", Kind = StationKind.Praca, Base = 5, PerUnit = 0.8, Label = "Prasowanie i pakowanie" } },
        };

        public static readonly FarmDef[] Farms =
        {
            new FarmDef { Name = "Kowalscy", Stock = 14, Max = 20, Rate = 0.8, BasePrice = 1.2, Owner = 0 },
            new FarmDef { Name = "Nowakowie", Stock = 10, Max = 20, Rate = 0.7, BasePrice = 1.3, Owner = 0 },
            new FarmDef { Name = "Wiśniewscy", Stock = 16, Max = 24, Rate = 0.9, BasePrice = 1.4, Owner = -1 },
            new FarmDef { Name = "Zielińscy", Stock = 18, Max = 24, Rate = 0.9, BasePrice = 1.5, Owner = -1 },
            new FarmDef { Name = "Wójcikowie", Stock = 12, Max = 20, Rate = 0.8, BasePrice = 1.3, Owner = -1 },
            new FarmDef { Name = "Kamińscy", Stock = 15, Max = 22, Rate = 0.9, BasePrice = 1.2, Owner = 1 },
            // duża mapa (gmina na wzór Podlasia): kolejne gospodarstwa we wsiach i koloniach
            new FarmDef { Name = "Sawiccy", Stock = 12, Max = 22, Rate = 0.8, BasePrice = 1.3, Owner = -1 },
            new FarmDef { Name = "Romanowscy", Stock = 16, Max = 26, Rate = 1.0, BasePrice = 1.4, Owner = -1 },
            new FarmDef { Name = "Kirylukowie", Stock = 14, Max = 24, Rate = 0.9, BasePrice = 1.35, Owner = -1 },
            new FarmDef { Name = "Łapińscy", Stock = 18, Max = 28, Rate = 1.1, BasePrice = 1.5, Owner = -1 },
            new FarmDef { Name = "Ostrowscy", Stock = 20, Max = 30, Rate = 1.2, BasePrice = 1.6, Owner = -1 },
            new FarmDef { Name = "Jaroszewiczowie", Stock = 15, Max = 24, Rate = 0.9, BasePrice = 1.3, Owner = 1 },
        };

        public static readonly (string name, string icon)[] Clients =
        {
            ("Szkoła", "school"), ("Sklep", "shop"), ("Restauracja", "restaurant"), ("Przedszkole", "kids"), ("Kawiarnia", "cafe"), ("Hotel", "hotel"),
        };

        public static readonly string[] WorkerNames = { "Ania", "Bartek", "Celina", "Darek", "Ewa" };

        public static readonly Dictionary<string, string> Facts = new Dictionary<string, string>
        {
            ["cow"] = "Krowa daje dziennie około 25 litrów mleka. Zwykle doi się ją dwa razy dziennie.",
            ["cold"] = "Zaraz po dojeniu mleko schładza się do ok. 4°C. W zimnie bakterie prawie się nie mnożą.",
            ["tanker"] = "Cysterna działa jak wielki termos: mleko zostaje zimne w drodze do mleczarni.",
            ["uht"] = "Mleko UHT grzeje się do ok. 135–140°C przez 2–4 sekundy i trafia do sterylnych kartonów. Zamknięte może stać miesiące bez lodówki.",
            ["past"] = "Pasteryzacja to podgrzanie mleka, które zabija groźne bakterie. Do jogurtu mleko grzeje się mocniej, do ok. 90°C, żeby jogurt był gęsty.",
            ["ferm"] = "Jogurt robią pożyteczne bakterie. W cieple (ok. 42°C) zamieniają cukier z mleka w kwas mlekowy. To trwa kilka godzin i nie da się tego przyspieszyć.",
            ["pack"] = "Produkty pakuje się szczelnie, żeby nie dostały się do nich zarazki z powietrza.",
            ["money"] = "Zanim kupisz mleko, policz: ile zapłacisz rolnikowi, a ile dostaniesz za gotowy produkt?",
            ["order"] = "Klienci zamawiają z wyprzedzeniem. Dobra mleczarnia planuje, kiedy zebrać mleko, żeby zdążyć.",
            ["fridge"] = "Gotowy jogurt trzyma się w lodówce. Mleko UHT dopiero po otwarciu.",
        };
    }
}
