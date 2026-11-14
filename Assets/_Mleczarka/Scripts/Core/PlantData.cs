using System.Collections.Generic;

namespace Mleczarka.Core
{
    /// <summary>Rodzaj kroku obsługi maszyny.</summary>
    public enum StepKind
    {
        Tap = 0,    // jedna czynność (zawór, przycisk, załadunek) — gracz: Q, pracownik: chwila
        Hold = 1,   // czynność trwająca (podgrzewanie do temperatury, krojenie skrzepu) — gracz przytrzymuje Q
        Auto = 2,   // maszyna pracuje sama przez określony czas
    }

    public sealed class StepDef
    {
        public StepKind Kind; public string Label;
        public double Base, PerUnit;        // sekundy (Hold, Auto) = Base + PerUnit × ilość
        public double Hours;                // dla długich procesów: godziny gry (fermentacja, dojrzewanie); zastępuje Base
        public double TargetTemp;           // temperatura, do której dochodzi maszyna w tym kroku (dla wskaźnika); 0 = bez zmiany
        public StepDef(StepKind k, string label, double b = 0, double per = 0, double hours = 0, double temp = 0)
        { Kind = k; Label = label; Base = b; PerUnit = per; Hours = hours; TargetTemp = temp; }
    }

    public sealed class MachineDef
    {
        public string Id, Type, Name, Info;
        public int Slots = 1;                // ile partii naraz (dojrzewalnia: wiele)
        public double DirtPerJob, WearPerJob;
        public double IdleTemp = 8;          // temperatura spoczynkowa (wskaźnik)
        public bool Cleanable = true;
    }

    /// <summary>
    /// Pełny ciąg technologiczny mleczarni: maszyny, ich kroki obsługi i trasy produktów.
    /// Czas w sekundach rzeczywistych; procesy biologiczne w godzinach gry (1 h = 1 min przy obecnej długości dnia).
    /// </summary>
    public static class PlantData
    {
        public static readonly MachineDef[] Machines =
        {
            new MachineDef { Id = "wirowka", Type = "wirowka", Name = "Wirówka (separator)", DirtPerJob = 12, WearPerJob = 3, Info = "Bęben kręci się kilka tysięcy razy na minutę. Cięższe mleko odchodzi na zewnątrz, lżejsza śmietanka zostaje w środku. Tak ustala się zawartość tłuszczu." },
            new MachineDef { Id = "homogenizator", Type = "homogenizator", Name = "Homogenizator", DirtPerJob = 8, WearPerJob = 3, Info = "Tłoki przepychają mleko pod ciśnieniem ok. 200 barów przez wąską szczelinę. Kuleczki tłuszczu się rozbijają, więc śmietanka nie wypływa na wierzch." },
            new MachineDef { Id = "pasteryzator", Type = "pasteryzator", Name = "Pasteryzator płytowy", DirtPerJob = 10, WearPerJob = 2.5, IdleTemp = 20, Info = "Mleko płynie między gorącymi płytami: 72°C przez 15 sekund zabija groźne bakterie. Do jogurtu i kefiru grzeje się mocniej, do 90°C." },
            new MachineDef { Id = "uht", Type = "uht", Name = "Sterylizator UHT", DirtPerJob = 12, WearPerJob = 3, IdleTemp = 20, Info = "135–140°C przez 2–4 sekundy. Takie mleko w szczelnym kartonie wytrzymuje miesiące bez lodówki." },
            new MachineDef { Id = "fermentator1", Type = "fermentator", Name = "Fermentator 1", DirtPerJob = 15, WearPerJob = 1, IdleTemp = 8, Info = "Bakterie jogurtowe i kefirowe pracują w cieple. Zamieniają cukier mleczny w kwas mlekowy — mleko gęstnieje." },
            new MachineDef { Id = "fermentator2", Type = "fermentator", Name = "Fermentator 2", DirtPerJob = 15, WearPerJob = 1, IdleTemp = 8, Info = "Drugi zbiornik pozwala robić jogurt i kefir jednocześnie." },
            new MachineDef { Id = "rozlewKarton", Type = "rozlewKarton", Name = "Rozlewaczka kartonów", DirtPerJob = 6, WearPerJob = 3, Info = "Składa karton z rolki, napełnia go i zgrzewa — kilkadziesiąt kartonów na minutę." },
            new MachineDef { Id = "rozlewKubki", Type = "rozlewKubki", Name = "Rozlewaczka kubków", DirtPerJob = 6, WearPerJob = 3, Info = "Napełnia kubki i butelki, nakłada wieczko z folii i zgrzewa je na gorąco." },
            new MachineDef { Id = "maslnica", Type = "maslnica", Name = "Maślnica", DirtPerJob = 15, WearPerJob = 3, Info = "Obracająca się beczka ubija śmietanę. Tłuszcz zlepia się w ziarno masła, a płyn (maślanka) się oddziela." },
            new MachineDef { Id = "wanna", Type = "wanna", Name = "Wanna serowarska", DirtPerJob = 15, WearPerJob = 2, IdleTemp = 20, Info = "Do ciepłego mleka dodaje się zakwas albo podpuszczkę. Mleko krzepnie w skrzep, który kroi się harfą na kostki." },
            new MachineDef { Id = "prasa", Type = "prasa", Name = "Prasa do sera", DirtPerJob = 8, WearPerJob = 3, Info = "Prasa wyciska serwatkę z form. Im mocniej i dłużej, tym twardszy ser." },
            new MachineDef { Id = "dojrzewalnia", Type = "dojrzewalnia", Name = "Dojrzewalnia serów", Slots = 6, DirtPerJob = 0, WearPerJob = 0.5, IdleTemp = 12, Cleanable = false, Info = "Ser żółty dojrzewa na drewnianych półkach w 12°C. Smak i dziurki biorą się z pracy bakterii." },
            new MachineDef { Id = "pakowarka", Type = "pakowarka", Name = "Pakowarka zbiorcza", DirtPerJob = 3, WearPerJob = 2, Info = "Układa kartony i kubki w zgrzewki i skrzynki, gotowe do chłodni." },
        };

        static readonly Dictionary<string, MachineDef> _byId = new Dictionary<string, MachineDef>();
        public static MachineDef M(string id)
        {
            if (_byId.Count == 0) foreach (var m in Machines) _byId[m.Id] = m;
            return _byId.TryGetValue(id, out var d) ? d : null;
        }

        /// <summary>Kroki obsługi maszyny danego typu dla danego produktu.</summary>
        public static StepDef[] Steps(string type, Product p)
        {
            switch (type)
            {
                case "wirowka": return new[]
                {
                    new StepDef(StepKind.Tap, "Otwórz zawór z silosu"),
                    new StepDef(StepKind.Auto, "Odwirowanie i normalizacja tłuszczu", 4, 0.4),
                    new StepDef(StepKind.Tap, "Skieruj śmietankę do zbiornika"),
                };
                case "homogenizator": return new[]
                {
                    new StepDef(StepKind.Hold, "Podnieś ciśnienie do 200 bar", 2),
                    new StepDef(StepKind.Auto, "Homogenizacja", 3, 0.3),
                };
                case "pasteryzator":
                {
                    bool hot = p == Product.Jogurt || p == Product.Kefir;
                    double t = hot ? 90 : p == Product.Smietana || p == Product.Maslo ? 85 : 72;
                    return new[]
                    {
                        new StepDef(StepKind.Tap, "Otwórz dopływ"),
                        new StepDef(StepKind.Hold, $"Podgrzej do {t:0}°C", hot ? 4 : 3, 0, 0, t),
                        new StepDef(StepKind.Auto, "Przetrzymanie w temperaturze", 4, 0.4, 0, t),
                        new StepDef(StepKind.Auto, hot ? "Schładzanie do 42°C" : "Schładzanie do 4°C", 3, 0, 0, hot ? 42 : 4),
                    };
                }
                case "uht": return new[]
                {
                    new StepDef(StepKind.Tap, "Uruchom sterylizator"),
                    new StepDef(StepKind.Hold, "Podgrzej do 138°C", 4, 0, 0, 138),
                    new StepDef(StepKind.Auto, "Sterylizacja i aseptyczne schłodzenie", 4, 0.4, 0, 20),
                };
                case "fermentator": return new[]
                {
                    new StepDef(StepKind.Tap, p == Product.Kefir ? "Dodaj ziarna kefirowe" : "Dodaj kultury bakterii"),
                    new StepDef(StepKind.Auto, p == Product.Kefir ? "Fermentacja kefirowa" : p == Product.Smietana ? "Ukwaszanie śmietany" : "Fermentacja jogurtu", 0, 0, FermentHours(p), p == Product.Kefir ? 22 : p == Product.Smietana ? 24 : 42),
                    new StepDef(StepKind.Tap, "Schłódź i przepompuj do rozlewu", 0, 0, 0, 6),
                };
                case "rozlewKarton": return new[]
                {
                    new StepDef(StepKind.Tap, "Załaduj rolkę kartonu"),
                    new StepDef(StepKind.Auto, "Formowanie, napełnianie i zgrzewanie", 2, 0.35),
                };
                case "rozlewKubki": return new[]
                {
                    new StepDef(StepKind.Tap, "Załaduj kubki i wieczka"),
                    new StepDef(StepKind.Auto, "Napełnianie i zgrzewanie", 2, 0.4),
                };
                case "maslnica": return new[]
                {
                    new StepDef(StepKind.Tap, "Wlej śmietanę"),
                    new StepDef(StepKind.Auto, "Ubijanie", 6, 0.8),
                    new StepDef(StepKind.Tap, "Spuść maślankę"),
                    new StepDef(StepKind.Hold, "Uformuj kostki", 3, 0.3),
                };
                case "wanna": return new[]
                {
                    new StepDef(StepKind.Tap, p == Product.Ser ? "Dodaj podpuszczkę" : "Dodaj zakwas", 0, 0, 0, p == Product.Ser ? 32 : 28),
                    new StepDef(StepKind.Auto, "Krzepnięcie mleka", 0, 0, p == Product.Ser ? 1 : 2, p == Product.Ser ? 32 : 28),
                    new StepDef(StepKind.Hold, "Krojenie skrzepu harfą", 4),
                };
                case "prasa": return new[]
                {
                    new StepDef(StepKind.Tap, p == Product.Ser ? "Napełnij formy" : "Przełóż skrzep do worków"),
                    new StepDef(StepKind.Auto, p == Product.Ser ? "Prasowanie" : "Odciekanie i prasowanie", 6, 0.5),
                };
                case "dojrzewalnia": return new[]
                {
                    new StepDef(StepKind.Tap, "Ułóż krążki na półkach"),
                    new StepDef(StepKind.Auto, "Dojrzewanie", 0, 0, 12, 12),
                };
                case "pakowarka": return new[]
                {
                    new StepDef(StepKind.Tap, "Złóż karton zbiorczy"),
                    new StepDef(StepKind.Auto, "Pakowanie i etykietowanie", 2, 0.2),
                };
            }
            return new StepDef[0];
        }

        /// <summary>Trasa produktu przez typy maszyn.</summary>
        public static string[] Route(Product p)
        {
            switch (p)
            {
                case Product.Uht: return new[] { "wirowka", "homogenizator", "uht", "rozlewKarton", "pakowarka" };
                case Product.MlekoPast: return new[] { "wirowka", "homogenizator", "pasteryzator", "rozlewKarton", "pakowarka" };
                case Product.Jogurt: return new[] { "wirowka", "homogenizator", "pasteryzator", "fermentator", "rozlewKubki", "pakowarka" };
                case Product.Kefir: return new[] { "wirowka", "homogenizator", "pasteryzator", "fermentator", "rozlewKubki", "pakowarka" };
                case Product.Smietana: return new[] { "pasteryzator", "fermentator", "rozlewKubki", "pakowarka" };
                case Product.Maslo: return new[] { "pasteryzator", "maslnica", "pakowarka" };
                case Product.Twarog: return new[] { "wirowka", "pasteryzator", "wanna", "prasa", "pakowarka" };
                case Product.Ser: return new[] { "wirowka", "pasteryzator", "wanna", "prasa", "dojrzewalnia", "pakowarka" };
            }
            return new string[0];
        }

        // Śmietana kwaśnieje krótko w fermentatorze (2 h), więc jej krok fermentacji jest krótszy niż jogurtu
        public static double FermentHours(Product p) => p == Product.Kefir ? 6 : p == Product.Smietana ? 2 : 4;

        public const double CreamYield = 0.1;          // z każdego litra przez wirówkę 0,1 l śmietanki
        public const double CipSeconds = 15;           // mycie jednej maszyny
        public const double CipCost = 3;               // środki myjące
        public const double DirtBlock = 100;           // przy tylu punktach brudu maszyna staje
        public const double DirtSlow = 60;             // od tylu zwalnia
        public const double RepairCost = 35;           // części przy awarii
        public const double ServiceCost = 12;          // przegląd profilaktyczny
        public const double RepairSeconds = 5;         // przytrzymanie Q
        public const double ServiceSeconds = 3;
        public const double WorkerTapDelay = 1.2;      // pracownik wykonuje czynność po takiej chwili
        public const double HoldPerAct = 0.5;          // jedna „porcja” przytrzymania Q (wywołanie co 0,5 s)
        public const double SampleSeconds = 3;         // badanie próbki mleka w laboratorium
        public const double BadMilkChance = 0.06;      // szansa na słabe mleko z gospodarstwa
    }
}
