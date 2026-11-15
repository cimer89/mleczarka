using System.Collections.Generic;
using System.Linq;
using Mleczarka.Core;
using Mleczarka.Player;
using Mleczarka.Save;
using Mleczarka.Vehicles;
using Mleczarka.Vehicles.Model;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mleczarka.Gameplay
{
    /// <summary>
    /// Łączy symulację (Mleczarka.Core) ze światem: strefy, pojazd gracza, przycisk akcji (F), zapis gry.
    /// Prezentacja tylko czyta stan i wysyła komendy.
    /// </summary>
    public class GameController : MonoBehaviour, IPlaceProvider
    {
        public static GameController Instance { get; private set; }

        public FarmerController player;
        public string saveSlot = "gra1";
        public float autosaveSeconds = 15f;
        public GameMode newGameMode = GameMode.Misja;
        public bool usePhysicalProduction;

        public Simulation Sim { get; private set; }
        public ICommandBus Bus { get; private set; }
        public const int Me = 0;

        public event System.Action<string, bool> Message;     // tekst, czy błąd
        public event System.Action<string> Fact;
        public event System.Action<GameEvent> SimEvent;

        float _autosave;

        /// <summary>Ustawiane przez menu „Nowa gra” przed przeładowaniem sceny: zaczynamy od zera mimo istniejącego zapisu.</summary>
        public static GameMode? PendingNewGame;
        public bool StartedFresh { get; private set; }

        void Awake()
        {
            Instance = this;
            SaveFile save = null;
#if UNITY_EDITOR
            // test w edytorze: świeża gra w wybranym trybie, bez czytania i zapisywania zapisu gracza
            int testMode = PlayerPrefs.GetInt("mleczarka_test_mode", -1);
            if (testMode >= 0) { PlayerPrefs.DeleteKey("mleczarka_test_mode"); PendingNewGame = (GameMode)testMode; SuspendSaving = true; }
#endif
            if (PendingNewGame.HasValue) { newGameMode = PendingNewGame.Value; PendingNewGame = null; StartedFresh = true; }
            else save = SaveSystem.Load(saveSlot);
            var state = save?.game ?? Simulation.NewGame(newGameMode, (uint)System.Environment.TickCount);
            Simulation.Migrate(state);
            Sim = new Simulation(state, this);
            Bus = new LocalCommandBus(Sim);
            if (save != null) RestoreVehicles(save);
        }

        void Start()
        {
            FarmerController.CanEnter = VehicleLock;
            FarmerController.Locked -= OnVehicleLocked; FarmerController.Locked += OnVehicleLocked;
            var me = Sim.S.players[Me];
            if (CareerSim.Active(me) && Sim.S.t <= 0 && player)
            {
                // kariera zaczyna się w domu, przy letniej kuchni
                var home = WorldPlace.All.FirstOrDefault(w => w && w.kind == PlaceKind.Dom);
                var spawn = home ? home.transform.Find("Start gracza") : null;
                if (spawn) { var cc = player.GetComponent<CharacterController>(); if (cc) cc.enabled = false; player.transform.SetPositionAndRotation(spawn.position, spawn.rotation); if (cc) cc.enabled = true; }
            }
            if (Sim.S.t > 0) Say($"Wczytano grę: dzień {Sim.Clock().day}, {Sim.Clock().text}.");
            else if (CareerSim.Active(me)) Say("Kariera: masz rower, bańkę na mleko i letnią kuchnię. Kup mleko u gospodarzy (F), zrób twaróg i sprzedaj go na rynku. B — rower, K — rozwój.");
            else Say(Sim.S.mission.active ? $"Misja: {Sim.S.mission.title}. Zbierz mleko cysterną, zrób produkty i dowieź zamówienia na rynek." : "Nowa gra.");
        }

        // ---------- miejsca ----------

        public Vector3 PlayerPosition => player ? (player.CurrentVehicle ? player.CurrentVehicle.transform.position : player.transform.position) : Vector3.zero;

        public Place PlaceOf(int playerId)
        {
            if (playerId != Me) return Place.None;
            var pos = PlayerPosition;
            foreach (var w in WorldPlace.All)
                if (w && w.Contains(pos)) return new Place(w.kind, w.farmId);
            return Place.None;
        }

        public WorldPlace CurrentPlace()
        {
            var pos = PlayerPosition;
            return WorldPlace.All.FirstOrDefault(w => w && w.Contains(pos));
        }

        // ---------- pętla ----------

        void Update()
        {
            SyncVehicle();
            Sim.Tick(Time.deltaTime);
            foreach (var e in Sim.Events) HandleEvent(e);
            Sim.Events.Clear();

            var blocked = player && player.InputBlocked;
            bool action = !blocked && ((Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame));
            if (action) DoContextAction();

            _autosave += Time.unscaledDeltaTime;
            if (_autosave >= autosaveSeconds) { _autosave = 0; SaveGame(); }
        }

        void OnApplicationPause(bool pause) { if (pause) SaveGame(); }
        void OnApplicationQuit() => SaveGame();

        /// <summary>Pojazd, którym jedzie gracz, ustala typ w symulacji i pojemność (z części: 300 l zbiornika = 1 l umowny, 200 l skrzyni = 1 sztuka).</summary>
        void SyncVehicle()
        {
            var p = Sim.S.players[Me];
            var v = player ? player.CurrentVehicle : null;
            if (!v) return;
            var asm = v.GetComponent<VehicleAssembler>();
            if (!asm) return;
            var kind = asm.loadout.type == VehicleType.Cysterna ? VehicleKind.Cysterna : VehicleKind.Dostawczak;
            if (p.vehicle != kind) Bus.Send(new SetVehicle(Me, kind));
            if (kind == VehicleKind.Cysterna)
            {
                int cap = Mathf.Max(5, Mathf.RoundToInt(asm.Stats.capacityL / 300f)) + 10 * p.upgrades[(int)UpgradeKind.Cysterna];
                if (p.tankerCap != cap) p.tankerCap = cap;
            }
            else
            {
                int cap = Mathf.Max(4, Mathf.RoundToInt(asm.Stats.capacityL / 200f)) + 6 * p.upgrades[(int)UpgradeKind.Dostawczak];
                if (p.vanCap != cap) p.vanCap = cap;
            }
        }

        // ---------- przycisk akcji ----------

        /// <summary>Akcje zależne od tego, co jest tuż obok (klient na rynku, odbiorca w placówce, koszyk roweru) — mają pierwszeństwo przed akcją miejsca.</summary>
        /// <summary>Kilka źródeł naraz (sprzedaż z ręki, własna krowa): liczy się pierwsze, które coś zwróci.</summary>
        public static System.Func<string> HintHook;
        public static System.Func<Result?> ActionHook;
        static string FirstHint() { if (HintHook == null) return null; foreach (System.Func<string> f in HintHook.GetInvocationList()) { var h = f(); if (h != null) return h; } return null; }
        static Result? FirstAction() { if (ActionHook == null) return null; foreach (System.Func<Result?> f in ActionHook.GetInvocationList()) { var r = f(); if (r.HasValue) return r; } return null; }

        public string ContextHint()
        {
            var hook = FirstHint(); if (hook != null) return hook;
            var p = Sim.S.players[Me]; var place = PlaceOf(Me); var v = player ? player.CurrentVehicle : null;
            bool tanker = v && p.vehicle == VehicleKind.Cysterna, van = v && p.vehicle == VehicleKind.Dostawczak;
            if (CareerSim.Active(p))
            {
                string h = CareerHint(p, place, v, tanker, van);
                if (h != null) return h;
            }
            switch (place.Kind)
            {
                case PlaceKind.Gospodarstwo:
                    var f = Sim.S.farms[place.FarmId];
                    if (f.owner != Me) { var o = Sim.OfferInfo(Me, f.id); return $"F — podpisz umowę: {f.name}, {Money.Zl(o.min)} zł/l (opłata {o.fee} zł)"; }
                    if (!tanker) return $"{f.name}: {Mathf.FloorToInt((float)f.stock)} l mleka. Przyjedź cysterną.";
                    return Sim.S.pump.Active ? "F — zatrzymaj pompę" : $"F — pompuj mleko ({Mathf.FloorToInt((float)f.stock)} l, {Money.Zl(f.price)} zł/l)";
                case PlaceKind.Mleczarnia:
                    if (tanker) return Sim.S.pump.Active ? "F — zatrzymaj pompę" : "F — rozładuj mleko do zbiornika   ·   TAB — mleczarnia";
                    if (van) return "F — załaduj towar   ·   TAB — mleczarnia";
                    return usePhysicalProduction ? "Wejdź do hali · Q — praca przy stanowisku · TAB — zarządzanie" : "TAB — mleczarnia (produkcja, ludzie, rozbudowa)";
                case PlaceKind.Rynek:
                    return van ? "F — dostarcz zamówienia i sprzedaj nadwyżki" : "Rynek: przyjedź dostawczakiem z towarem.";
            }
            return null;
        }

        public void DoContextAction()
        {
            var hooked = FirstAction();
            if (hooked.HasValue) { if (!string.IsNullOrEmpty(hooked.Value.Msg)) Say(hooked.Value.Msg, !hooked.Value.Ok); return; }
            var p = Sim.S.players[Me]; var place = PlaceOf(Me); var v = player ? player.CurrentVehicle : null;
            Result r = Result.Fail("Tu nie ma nic do zrobienia.");
            if (CareerSim.Active(p) && CareerAction(p, place, v, ref r)) { if (!string.IsNullOrEmpty(r.Msg)) Say(r.Msg, !r.Ok); return; }
            switch (place.Kind)
            {
                case PlaceKind.Gospodarstwo:
                    var f = Sim.S.farms[place.FarmId];
                    if (f.owner != Me) r = Bus.Send(new BidForContract(Me, f.id, Sim.OfferInfo(Me, f.id).min));
                    else if (Sim.S.pump.Active) { r = Bus.Send(new StopPump(Me)); }
                    else r = v ? Bus.Send(new CollectMilk(Me, f.id)) : Result.Fail("Przyjedź cysterną.");
                    break;
                case PlaceKind.Mleczarnia:
                    if (!v) { r = Result.Fail("Naciśnij TAB, żeby otworzyć panel mleczarni."); break; }
                    if (Sim.S.pump.Active) r = Bus.Send(new StopPump(Me));
                    else r = p.vehicle == VehicleKind.Cysterna ? Bus.Send(new UnloadMilk(Me)) : Bus.Send(new LoadVan(Me));
                    break;
                case PlaceKind.Rynek:
                    if (!v || p.vehicle != VehicleKind.Dostawczak) { r = Result.Fail("Towar wozi dostawczak."); break; }
                    int n = 0;
                    foreach (var o in Sim.ClaimedBy(Me).ToList())
                        if (Simulation.CanDeliver(p, o) && Bus.Send(new Deliver(Me, o.id)).Ok) n++;
                    var sell = Bus.Send(new SellRest(Me));
                    r = n > 0 || sell.Ok ? Result.Success((n > 0 ? $"Dostarczono zamówień: {n}. " : "") + (sell.Ok ? "Sprzedano nadwyżki " + sell.Msg : ""))
                                         : Result.Fail(Sim.ClaimedBy(Me).Any() ? "Brakuje towaru do zamówień (O — lista zamówień)." : "Nie masz przyjętych zamówień ani nadwyżek. O — zamówienia.");
                    break;
            }
            if (!string.IsNullOrEmpty(r.Msg)) Say(r.Msg, !r.Ok);
        }

        // ---------- kariera „od kuchni” ----------

        string CareerHint(PlayerState p, Place place, WheeledVehicle v, bool tanker, bool van)
        {
            var c = p.career;
            switch (place.Kind)
            {
                case PlaceKind.Gospodarstwo:
                    if (v) return null;
                    var f = Sim.S.farms[place.FarmId];
                    double price = Money.M2(f.price * CareerData.MilkPriceFactor);
                    int cows = CareerSim.Cows(place.FarmId);
                    return $"F — kup mleko do bańki ({c.canister:0}/{c.canisterCap:0} l, {Money.Zl(price)} zł/l, mają {Mathf.FloorToInt((float)(f.stock * CareerData.LitersPerFarmUnit))} l · {f.name}: {cows} {(cows == 1 ? "krowa" : "krowy")}, {cows * CareerData.LitersPerCow:0} l na dobę)";
                case PlaceKind.Dom:
                    if (tanker) return "F — rozładuj mleko do lodówek";
                    if (van) return "F — załaduj towar ze spiżarki";
                    return c.canister >= 1 ? "F — przelej mleko z bańki do lodówki   ·   Q przy sprzęcie — gotowanie" : "F — weź towar ze spiżarki do koszyka   ·   Q przy sprzęcie — gotowanie   ·   K — rozwój";
                case PlaceKind.Rynek:
                    if (van) return null;
                    if (c.hand >= 0) return $"Masz w rękach: {GameData.P((Product)c.hand).Name} — podejdź do klienta, który tego chce";
                    return c.basket.Sum() > 0 ? "Klienci czekają przy straganie — weź towar z koszyka roweru (F przy rowerze) i podaj klientowi" : "Rynek: przywieź towar w koszyku roweru.";
                case PlaceKind.Mleczarnia:
                    if (!CareerSim.Allowed(p, "hala")) return "Hala mleczarni jest na sprzedaż — " + CareerSim.LockedText("hala");
                    return null;
            }
            return null;
        }

        bool CareerAction(PlayerState p, Place place, WheeledVehicle v, ref Result r)
        {
            switch (place.Kind)
            {
                case PlaceKind.Gospodarstwo:
                    if (v) return false;
                    r = Bus.Send(new BuyMilkCan(Me, place.FarmId)); if (r.Ok) Act(Mleczarka.Player.ActionAnim.Kind.Przelewa, 1.6f); return true;
                case PlaceKind.Dom:
                    if (v && p.vehicle == VehicleKind.Cysterna) { r = Bus.Send(new UnloadTankerHome(Me)); return true; }
                    if (v) { r = Bus.Send(new LoadVanHome(Me)); return true; }
                    bool pour = p.career.canister >= 1;
                    r = pour ? Bus.Send(new PourMilk(Me)) : Bus.Send(new LoadBasket(Me));
                    if (r.Ok) Act(pour ? Mleczarka.Player.ActionAnim.Kind.Przelewa : Mleczarka.Player.ActionAnim.Kind.Bierze, pour ? 1.6f : 1.3f);
                    return true;
                case PlaceKind.Rynek:
                    if (v) return false;
                    r = Result.Fail(p.career.hand >= 0 ? "Podejdź bliżej do klienta, który chce ten towar." : "Weź towar z koszyka roweru (F przy rowerze) i podaj go klientowi."); return true;
                case PlaceKind.Mleczarnia:
                    if (CareerSim.Allowed(p, "hala")) return false;
                    r = Result.Fail(CareerSim.LockedText("hala")); return true;
            }
            return false;
        }

        /// <summary>Animacja czynności rolnika (bierze, przelewa, podaje…) — tylko pieszo.</summary>
        public void Act(Mleczarka.Player.ActionAnim.Kind k, float seconds)
        {
            if (!player || player.CurrentVehicle || player.OnBike) return;
            var a = Mleczarka.Player.ActionAnim.For(player); if (a) a.Play(k, seconds);
        }

        static void OnVehicleLocked(string msg) { if (Instance) Instance.Say(msg, true); }

        /// <summary>Kariera: pojazdy są dostępne dopiero po zakupie.</summary>
        string VehicleLock(WheeledVehicle v)
        {
            var p = Sim.S.players[Me];
            if (!CareerSim.Active(p) || !v) return null;
            var asm = v.GetComponent<VehicleAssembler>();
            bool tanker = asm && asm.loadout.type == VehicleType.Cysterna;
            string need = tanker ? "cysterna" : "dostawczak";
            return CareerSim.Allowed(p, need) ? null : "To nie twój pojazd. " + CareerSim.LockedText(need);
        }

        /// <summary>Komenda bez dymka „w trakcie” (np. przytrzymanie Q z postępem w %) — dymek tylko przy błędzie albo końcu czynności.</summary>
        public Result SendHold(ICommand cmd)
        {
            var r = Bus.Send(cmd);
            if (!string.IsNullOrEmpty(r.Msg) && (!r.Ok || !r.Msg.Contains("%"))) Say(r.Msg, !r.Ok);
            return r;
        }

        /// <summary>Komenda bez dymka — dla akcji, które zwracają wynik do DoContextAction (on pokazuje komunikat raz).</summary>
        public Result Run(ICommand cmd) => Bus.Send(cmd);

        public Result Send(ICommand cmd)
        {
            var r = Bus.Send(cmd);
            if (!string.IsNullOrEmpty(r.Msg)) Say(r.Msg, !r.Ok);
            return r;
        }

        // ---------- zdarzenia ----------

        void HandleEvent(GameEvent e)
        {
            SimEvent?.Invoke(e);
            switch (e.Type)
            {
                case "pumpDone":
                    if (e.Value > 0) Say(e.Id >= 0 ? $"Zebrano {e.Value} l mleka." : $"Rozładowano {e.Value} l do zbiornika.");
                    if (!string.IsNullOrEmpty(e.Text)) Say(e.Text, true);
                    break;
                case "kitchenDone": Say(e.Text == "smietanka" || e.Text == "wirowanie" ? $"Śmietanka: +{e.Value:0.0} l w lodówce." : $"Gotowe: {e.Value} × {NameOf(e.Text)} — w spiżarce."); break;
                case "kitchenStep": if (!string.IsNullOrEmpty(e.Text)) Say(e.Text); break;
                case "batchDone": Say($"Gotowe: {e.Value} × {NameOf(e.Text)}. " + (Sim.S.players[Me].manualHandling ? "Odbierz produkty przy pakowarce." : "Towar jest w magazynie.")); break;
                case "jobDone": Say($"Gotowe: {e.Value} × {NameOf(e.Text)}. Odbierz skrzynki przy pakowarce (Q) i zanieś do chłodni."); break;
                case "breakdown": Say($"Awaria: {MachineName(e.Text)}! Podejdź i przytrzymaj Q, żeby naprawić.", true); break;
                case "dirty": Say($"{MachineName(e.Text)} jest brudna i stanęła. Umyj ją (C przy maszynie albo stacja CIP).", true); break;
                case "cipDone": Say($"{MachineName(e.Text)}: mycie zakończone."); break;
                case "badQuality": Say($"Partia {NameOf(e.Text)} wyszła słaba (złe mleko). Klienci to zauważą: −1 reputacji.", true); break;
                case "delivered": Say($"Dostarczono: {e.Text}, +{e.Value} zł"); break;
                case "contract": if (e.Pid == 1) Say($"Mleczpol podpisał umowę: {Sim.S.farms[e.Id].name}.", true); break;
                case "rivalOrder": Say($"Mleczpol przejął zamówienie: {e.Text}.", true); break;
                case "late": Say($"Spóźnienie: {e.Text}. Reputacja spada.", true); break;
                case "newOrder": Say("Nowe zamówienie (O — lista)."); break;
                case "wages": Say($"Dzień {e.Id}: wypłacono pensje {e.Value} zł."); break;
                case "fact": Fact?.Invoke(e.Text); break;
                case "missionEnd": Say(e.Text == "win" ? $"Misja wykonana! Gwiazdki: {e.Value}." : "Misja nieudana. Spróbuj jeszcze raz.", e.Text != "win"); break;
            }
        }

        static string MachineName(string id) => PlantData.M(id ?? "")?.Name ?? id;
        static string NameOf(string productEnum) => System.Enum.TryParse<Product>(productEnum, out var p) ? GameData.P(p).Name : productEnum;
        public void Say(string msg, bool error = false) => Message?.Invoke(msg, error);

        /// <summary>Przeskok czasu (szybki transport): symulacja biegnie krokami po 0,25 s, zdarzenia obsługiwane po drodze.</summary>
        public void SkipTime(double seconds)
        {
            double left = seconds;
            while (left > 0)
            {
                double dt = System.Math.Min(0.25, left); left -= dt;   // Sim.Tick przyjmuje najwyżej 0,25 s na krok
                Sim.Tick(dt);
                foreach (var e in Sim.Events) HandleEvent(e);
                Sim.Events.Clear();
            }
        }

        // ---------- zapis ----------

        public void SaveGame()
        {
            if (_skipSaveOnExit || SuspendSaving) return;
            var file = new SaveFile { game = Sim.S };
            foreach (var a in FindObjectsByType<VehicleAssembler>(FindObjectsSortMode.None))
            {
                a.loadout.vehicleId = a.name;
                file.vehicles.Add(a.loadout);
            }
            try { SaveSystem.Save(saveSlot, file); } catch (System.Exception ex) { Debug.LogWarning("[Mleczarka] Zapis nie powiódł się: " + ex.Message); }
        }

        void RestoreVehicles(SaveFile save)
        {
            var map = FindObjectsByType<VehicleAssembler>(FindObjectsSortMode.None).GroupBy(a => a.name).ToDictionary(g => g.Key, g => g.First());
            foreach (var l in save.vehicles)
                if (l != null && !string.IsNullOrEmpty(l.vehicleId) && map.TryGetValue(l.vehicleId, out var a) && a.loadout.type == l.type)
                { a.loadout = l; a.Rebuild(); }
        }

        /// <summary>
        /// Nowa gra od początku: obecny zapis trafia do kopii z datą (nie jest kasowany), scena ładuje się od nowa,
        /// więc pojazdy, hala i gracz wracają do stanu wyjściowego.
        /// </summary>
        public void RestartFromScratch(GameMode mode)
        {
            string path = SaveSystem.PathFor(saveSlot);
            try
            {
                if (System.IO.File.Exists(path))
                {
                    string copy = SaveSystem.PathFor(saveSlot + "_kopia_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    System.IO.File.Copy(path, copy, true);
                    System.IO.File.Delete(path);
                    if (System.IO.File.Exists(path + ".bak")) System.IO.File.Delete(path + ".bak");
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[Mleczarka] Kopia zapisu nie powiodła się: " + ex.Message); }
            _skipSaveOnExit = true;
            PendingNewGame = mode;
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex >= 0
                ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex : 0);
        }
        bool _skipSaveOnExit;
        /// <summary>Testy i podglądy w edytorze wyłączają zapis, żeby nie zmieniać prawdziwej gry gracza.</summary>
        [System.NonSerialized] public bool SuspendSaving;
        public bool SkipSaveOnExit => _skipSaveOnExit;

        public void NewGame(GameMode mode)
        {
            Sim = new Simulation(Simulation.NewGame(mode, (uint)System.Environment.TickCount), this);
            Bus = new LocalCommandBus(Sim);
            SaveGame();
            Say(mode == GameMode.Misja ? $"Misja: {Sim.S.mission.title}." : "Nowa gra swobodna.");
        }
    }
}
