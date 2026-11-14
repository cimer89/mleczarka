using System;
using System.Collections.Generic;

namespace Mleczarka.Core
{
    // Stan gry jako zwykłe dane z polami publicznymi: ten sam obiekt służy do zapisu (JSON) i w przyszłości do synchronizacji.
    // Bez słowników i właściwości — tylko listy i tablice, żeby serializator Unity (JsonUtility) zapisywał wszystko.

    public enum GameMode { Misja = 0, Swobodna = 1, Kariera = 2 }
    public enum VehicleKind { Cysterna = 0, Dostawczak = 1 }
    public enum OrderStatus { Otwarte = 0, Przyjete = 1, Dostarczone = 2, Przeterminowane = 3, Rywal = 4 }
    public enum PumpKind { Brak = 0, Odbior = 1, Rozladunek = 2 }

    [Serializable]
    public class GameState
    {
        public int version = 1;
        public GameMode mode;
        public double t;
        public bool paused;
        public bool calm;
        public bool noTimeLimits = true;     // bez ograniczeń czasowych: zamówienia nie przepadają, misja nie ma limitu
        public uint rng = 0x9E3779B9u;
        public int nextOrderId = 1, nextBatchId = 1;
        public double nextOrderT;
        public int lastDay = 1;
        public List<PlayerState> players = new List<PlayerState>();
        public List<FarmState> farms = new List<FarmState>();
        public List<OrderState> orders = new List<OrderState>();
        public PumpState pump = new PumpState();
        public RivalState rival = new RivalState();
        public MissionState mission = new MissionState();
        public List<string> seenFacts = new List<string>();
    }

    [Serializable]
    public class PlayerState
    {
        public int id;
        public string name;
        public bool ai;
        public double money;
        public int rep, delivered;
        public double earned;
        public VehicleKind vehicle;
        public int tankerMilk, tankerCap;
        public int[] vanCargo = new int[GameData.ProductCount];
        public int vanCap;
        public int tankMilk, tankCap;
        public int[] store = new int[GameData.ProductCount];
        public int storeCap;
        public int[] upgrades = new int[5];
        public List<WorkerState> workers = new List<WorkerState>();
        public List<BatchState> batches = new List<BatchState>();
        public int selected;
        public int[] made = new int[GameData.ProductCount];
        // Tryb pracy w hali jest opcjonalny; stare zapisy zachowują dotychczasowe reguły.
        public bool manualHandling;
        public bool productionStaffAssist;
        public int[] packingRack = new int[GameData.ProductCount];
        public int[] carried = new int[GameData.ProductCount];
        public bool carriedFromStore;
        // Pełny ciąg maszyn (nowa hala). Gdy włączony, zastępuje uproszczone partie (batches).
        public PlantState plant = new PlantState();
        public double tankerQuality = 1;      // jakość mleka w cysternie (średnia z gospodarstw)
        // Tryb kariery „od kuchni” (w pozostałych trybach nieaktywny)
        public CareerState career = new CareerState();
    }

    [Serializable]
    public class PlantState
    {
        public bool enabled;
        public bool workersOff = true;        // wersja bez pracowników: całą halę obsługuje gracz (pracownicy zostają w zapisie, bez pensji)
        public double rawQuality = 1;         // jakość mleka w silosie surowego mleka (tankMilk)
        public double cream, creamCap = 40;   // zbiornik śmietanki z wirówki (litry umowne)
        public bool sampleTested;             // próbka z obecnej cysterny zbadana
        public double sampleProgress;
        public double sampleResult = -1;      // wynik badania (jakość) albo -1
        public int nextJobId = 1;
        public List<MachineState> machines = new List<MachineState>();
        public List<JobState> jobs = new List<JobState>();
    }

    [Serializable]
    public class MachineState
    {
        public string id;
        public double condition = 100;        // stan techniczny 0–100
        public double dirt;                   // brud 0–100 (mycie CIP)
        public bool broken;
        public string fault;                  // opis awarii
        public double temp = 8;               // temperatura do wskaźnika
        public double cip;                    // > 0: trwa mycie (pozostałe sekundy)
        public double service;                // postęp naprawy / przeglądu (przytrzymanie Q)
        public int jobsDone;
        public int worker = -1;               // pracownik przypisany na stałe (-1 = dowolny)
    }

    [Serializable]
    public class JobState
    {
        public int id;
        public Product product;
        public int qty;
        public int stage;                     // indeks na trasie produktu
        public int step;                      // indeks kroku na bieżącej maszynie
        public double progress;               // postęp bieżącego kroku (sekundy)
        public string machine;                // id maszyny, na której jest partia (pusty = czeka w kolejce)
        public string wait;                   // powód czekania
        public double quality = 1;
        public int worker = -1;               // kto wykonuje bieżący krok
        public bool Waiting => !string.IsNullOrEmpty(wait);
    }

    [Serializable]
    public class WorkerState
    {
        public int id;
        public string name;
        public string station;
        public string walkTo;     // pusty = stoi
        public double walkT;
        public bool auto = true;
        public bool Walking => !string.IsNullOrEmpty(walkTo);
        public string Target => Walking ? walkTo : station;
    }

    [Serializable]
    public class BatchState
    {
        public int id;
        public Product product;
        public int qty, stage;
        public double progress;
        public string wait;       // pusty = pracuje
        public int worker = -1;
        public bool Waiting => !string.IsNullOrEmpty(wait);
    }

    [Serializable]
    public class FarmState
    {
        public int id;
        public string name;
        public double stock, max, rate, basePrice, price;
        public int owner = -1;
        public double protectUntil;
    }

    [Serializable]
    public class OrderState
    {
        public int id;
        public string client, icon;
        public int[] items = new int[GameData.ProductCount];
        public double reward, deadline, doneAt;
        public OrderStatus status;
        public int owner = -1;
        public bool mission;
    }

    [Serializable]
    public class PumpState
    {
        public PumpKind kind;
        public int pid = -1, fid = -1;
        public double left, acc, cost;
        public double quality = 1;
        public int moved;
        public bool Active => kind != PumpKind.Brak;
    }

    [Serializable]
    public class RivalState
    {
        public string phase = "idle";   // idle, toFarm, toBase
        public int target = -1;
        public double wait = 6, travel;
        public int load, goods = 4;
        public double nextBidT = GameConfig.RivalBidEvery, nextClaimT = GameConfig.RivalClaimEvery;
    }

    [Serializable]
    public class MissionState
    {
        public bool active;
        public int id;
        public string title;
        public double limit;
        public bool over, win;
        public int stars;
        public double time;
    }
}
