namespace Mleczarka.Core
{
    /// <summary>Liczby do strojenia gry w jednym miejscu (przeniesione 1:1 z prototypu HTML, config.js). Czas w sekundach rzeczywistych.</summary>
    public static class GameConfig
    {
        public const double DaySeconds = 1440;         // 1 dzień gry = 24 minuty (1 godzina gry = 1 minuta); duża mapa, przejazdy do 7 minut
        public const double StartHour = 6;

        public const double StartMoney = 80;
        public const int StartWorkers = 3;
        public const int MaxWorkers = 5;

        public const int TruckCapacity = 20;           // litry umowne w cysternie
        public const int VanCapacity = 12;             // sztuki w dostawczaku
        public const int TankCapacity = 40;            // zbiornik mleka surowego w mleczarni
        public const int StoreCapacity = 24;           // magazyn gotowych produktów

        public const double PumpLitersPerSecond = 4;

        public const double WorkerSelectedEfficiency = 1.0;
        public const double WorkerAutoEfficiency = 0.7;
        public const double WorkerWalkSeconds = 2.5;
        public const double HelpTapSeconds = 0.5;

        public const double WagePerDay = 10;
        public const double HireCost = 60;

        public static readonly UpgradeDef[] Upgrades =
        {
            new UpgradeDef(UpgradeKind.Cysterna, 10, 1, new double[] { 70, 130, 220 }),
            new UpgradeDef(UpgradeKind.Dostawczak, 6, 1, new double[] { 60, 120 }),
            new UpgradeDef(UpgradeKind.Zbiornik, 20, 1, new double[] { 50, 100, 180 }),
            new UpgradeDef(UpgradeKind.Magazyn, 12, 1, new double[] { 50, 100, 180 }),
            new UpgradeDef(UpgradeKind.Maszyny, 0, 1.35, new double[] { 90, 170 }),
        };

        public const double FarmRegenMultiplier = 1.5;

        public const double ContractFee = 15;
        public const double ContractTakeoverFeeMult = 2;
        public const double ContractMinRaise = 0.2;
        public const double ContractProtectSeconds = 420;
        public const double ContractMaxPrice = 3;

        public const double OrderSpawnSeconds = 150;
        public const int OrderMaxActive = 6;
        public const int OrderMaxClaimed = 3;
        public const int OrderDeadlineMin = 900;      // 15–25 minut: dojazd z mleczarni na rynek to ok. 1–2 minuty, ale trzeba zebrać mleko i zrobić towar
        public const int OrderDeadlineMax = 1500;
        public const double OrderRewardMult = 1.6;
        public const double MarketSellMult = 1.0;

        public const bool RivalEnabled = true;
        public const double RivalMoney = 300;
        public const double RivalBidEvery = 260;
        public const double RivalClaimEvery = 210;
        public const double RivalTakeoverChance = 0.35;
        public const double RivalTravelSeconds = 150;   // w Unity rywal nie jeździ po grafie dróg prototypu: czas przejazdu

        public const double MissionSeconds = 2400;     // misja 1: 40 minut (duża mapa)
        public const double RaceMilkPrice = 1.0;
    }

    public enum UpgradeKind { Cysterna = 0, Dostawczak = 1, Zbiornik = 2, Magazyn = 3, Maszyny = 4 }

    public readonly struct UpgradeDef
    {
        public readonly UpgradeKind Kind; public readonly int Step; public readonly double Mult; public readonly double[] Cost;
        public UpgradeDef(UpgradeKind kind, int step, double mult, double[] cost) { Kind = kind; Step = step; Mult = mult; Cost = cost; }
    }
}
