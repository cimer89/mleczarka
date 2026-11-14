using System.Collections.Generic;
using System.Linq;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using static Mleczarka.UI.UiKit;

namespace Mleczarka.UI
{
    /// <summary>
    /// Interfejs gry: pasek z pieniędzmi i czasem, stan pojazdu, podpowiedź akcji, komunikaty, ciekawostki,
    /// panel mleczarni (TAB, w mleczarni) i lista zamówień (O).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HudUI : MonoBehaviour
    {
        GameController G => GameController.Instance;
        VisualElement _root, _top, _vehicle, _toasts, _panel;
        Label _hint, _keys;
        VisualElement _fact; float _factT;
        float _refresh;
        string _open;                                        // "dairy", "orders" albo null
        readonly Dictionary<Product, int> _qty = new Dictionary<Product, int> { [Product.Uht] = 4, [Product.Jogurt] = 2 };
        readonly List<(VisualElement el, float t)> _toastList = new List<(VisualElement, float)>();

        void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement; _root.Clear();
            _top = Card(Panel, 16, 8, 14); _top.style.position = Position.Absolute; _top.style.left = 20; _top.style.top = 20; _top.style.flexDirection = FlexDirection.Row;
            _vehicle = Card(Panel, 16, 8, 14); _vehicle.style.position = Position.Absolute; _vehicle.style.left = 20; _vehicle.style.top = 78;
            _hint = L("", 20, Color.white, true); _hint.style.position = Position.Absolute; _hint.style.bottom = 70; _hint.style.alignSelf = Align.Center;
            _hint.style.backgroundColor = Dark; Round(_hint, 14); Pad(_hint, 10, 18);
            var hintWrap = new VisualElement(); hintWrap.style.position = Position.Absolute; hintWrap.style.left = 0; hintWrap.style.right = 0; hintWrap.style.bottom = 70; hintWrap.style.alignItems = Align.Center; hintWrap.pickingMode = PickingMode.Ignore;
            _hint.style.position = Position.Relative; _hint.style.bottom = 0; hintWrap.Add(_hint);
            _keys = L("E wsiądź / wysiądź  ·  F akcja  ·  O zamówienia  ·  TAB mleczarnia  ·  G garaż  ·  M mapa  ·  R postaw pojazd", 15, new Color(1, 1, 1, 0.9f), true);
            _keys.style.position = Position.Absolute; _keys.style.left = 20; _keys.style.bottom = 18; _keys.style.backgroundColor = Dark; Round(_keys, 10); Pad(_keys, 6, 12);
            _toasts = new VisualElement(); _toasts.style.position = Position.Absolute; _toasts.style.right = 20; _toasts.style.bottom = 20; _toasts.style.width = 440; _toasts.pickingMode = PickingMode.Ignore;
            _root.Add(_top); _root.Add(_vehicle); _root.Add(hintWrap); _root.Add(_keys); _root.Add(_toasts);
        }

        void Start()
        {
            if (!G) return;
            G.Message += Toast;
            G.Fact += ShowFact;
        }

        void OnDestroy() { if (G) { G.Message -= Toast; G.Fact -= ShowFact; } }

        void Update()
        {
            if (!G) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.tabKey.wasPressedThisFrame) Toggle("dairy");
                if (kb.oKey.wasPressedThisFrame) Toggle("orders");
                if (_open != null && kb.escapeKey.wasPressedThisFrame) Close();
            }
            if (_open == "dairy" && G.PlaceOf(GameController.Me).Kind != PlaceKind.Mleczarnia) Close();

            _refresh -= Time.unscaledDeltaTime;
            if (_refresh <= 0) { _refresh = 0.25f; RefreshTop(); RefreshVehicle(); if (_open != null) BuildPanel(); }
            var hint = _open == null ? G.ContextHint() : null;
            _hint.text = hint ?? ""; _hint.style.display = string.IsNullOrEmpty(hint) ? DisplayStyle.None : DisplayStyle.Flex;

            for (int i = _toastList.Count - 1; i >= 0; i--)
            {
                var (el, t) = _toastList[i]; t -= Time.unscaledDeltaTime; _toastList[i] = (el, t);
                el.style.opacity = Mathf.Clamp01(t);
                if (t <= 0) { el.RemoveFromHierarchy(); _toastList.RemoveAt(i); }
            }
            if (_fact != null && (_factT -= Time.unscaledDeltaTime) <= 0) { _fact.RemoveFromHierarchy(); _fact = null; }
        }

        // ---------- górny pasek i pojazd ----------

        void RefreshTop()
        {
            var S = G.Sim.S; var p = S.players[GameController.Me]; var c = G.Sim.Clock();
            _top.Clear();
            _top.Add(Pill($"{Money.Zl(p.money)} zł", Good, Color.white, 20));
            _top.Add(Pill($"Dzień {c.day}  {c.text}", Brand, Color.white, 20));
            _top.Add(Pill($"★ {p.rep}", new Color(0.95f, 0.7f, 0.1f), Color.white, 20));
            if (S.mission.active && !S.mission.over)
            {
                double left = S.mission.limit - S.t;
                _top.Add(Pill(S.noTimeLimits ? $"Misja: {S.mission.title}" : $"Misja: {S.mission.title} — {(int)(left / 60)}:{(int)(left % 60):00}", new Color(0.55f, 0.3f, 0.7f), Color.white, 18));
            }
            int claimed = G.Sim.ClaimedBy(GameController.Me).Count(), open = S.orders.Count(o => o.status == OrderStatus.Otwarte);
            _top.Add(Pill($"Zamówienia: {claimed} przyjęte, {open} wolne", Chip, Ink, 16));
        }

        void RefreshVehicle()
        {
            var S = G.Sim.S; var p = S.players[GameController.Me];
            _vehicle.Clear();
            bool driving = G.player && G.player.CurrentVehicle;
            if (!driving) { _vehicle.style.display = DisplayStyle.None; return; }
            _vehicle.style.display = DisplayStyle.Flex;
            if (p.vehicle == VehicleKind.Cysterna)
            {
                _vehicle.Add(L($"Cysterna: {p.tankerMilk} / {p.tankerCap} l mleka", 18, Ink, true));
                _vehicle.Add(Bar(p.tankerMilk / (float)Mathf.Max(1, p.tankerCap), Brand, 260, 12));
            }
            else
            {
                _vehicle.Add(L($"Dostawczak: {p.vanCargo.Sum()} / {p.vanCap} szt.", 18, Ink, true));
                var r = Row(); foreach (var k in GameData.Ready) if (p.vanCargo[(int)k] > 0) r.Add(Pill($"{p.vanCargo[(int)k]}× {GameData.P(k).Name}", Chip, Ink)); _vehicle.Add(r);
            }
            if (S.pump.Active && S.pump.pid == GameController.Me)
                _vehicle.Add(L(S.pump.kind == PumpKind.Odbior ? $"Pompa: zbieranie… {S.pump.moved} l ({Money.Zl(S.pump.cost)} zł)" : $"Pompa: rozładunek… {S.pump.moved} l", 16, Brand, true));
        }

        // ---------- komunikaty i ciekawostki ----------

        void Toast(string msg, bool error)
        {
            var c = Card(error ? new Color(0.98f, 0.88f, 0.85f, 0.97f) : Panel, 14, 8, 14);
            c.style.marginTop = 6; c.style.borderLeftWidth = 6; c.style.borderLeftColor = error ? Bad : Good;
            var l = L(msg, 17, Ink, true); l.style.whiteSpace = WhiteSpace.Normal; c.Add(l);
            _toasts.Add(c); _toastList.Add((c, 4.5f));
            while (_toastList.Count > 4) { _toastList[0].el.RemoveFromHierarchy(); _toastList.RemoveAt(0); }
        }

        void ShowFact(string text)
        {
            _fact?.RemoveFromHierarchy();
            _fact = Card(new Color(0.93f, 0.97f, 1f, 0.97f), 16, 10, 16);
            _fact.style.position = Position.Absolute; _fact.style.left = 20; _fact.style.bottom = 64; _fact.style.width = 520;
            _fact.style.borderLeftWidth = 6; _fact.style.borderLeftColor = Brand;
            _fact.Add(L("Ciekawostka", 15, Brand, true));
            var l = L(text, 17, Ink); l.style.whiteSpace = WhiteSpace.Normal; _fact.Add(l);
            _root.Add(_fact); _factT = 10f;
        }

        // ---------- panele ----------

        void Toggle(string which)
        {
            if (_open == which) { Close(); return; }
            if (which == "dairy" && G.PlaceOf(GameController.Me).Kind != PlaceKind.Mleczarnia) { Toast("Panel mleczarni otwiera się w mleczarni.", true); return; }
            _open = which;
            if (G.player) { G.player.orbitWhenBlocked = false; G.player.InputBlocked = true; }
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            BuildPanel();
        }

        void Close()
        {
            _open = null; _panel?.RemoveFromHierarchy(); _panel = null;
            if (G.player) { G.player.InputBlocked = false; G.player.orbitWhenBlocked = true; }
        }

        void BuildPanel()
        {
            float scroll = 0;
            if (_panel != null) { var sv = _panel.Q<ScrollView>(); if (sv != null) scroll = sv.scrollOffset.y; _panel.RemoveFromHierarchy(); }
            _panel = Card(Panel, 22, 18, 22);
            _panel.style.position = Position.Absolute; _panel.style.right = 24; _panel.style.top = 24; _panel.style.bottom = 24; _panel.style.width = 600;
            _panel.style.borderBottomWidth = 6; _panel.style.borderBottomColor = Brand;
            var head = Row(); head.style.justifyContent = Justify.SpaceBetween;
            head.Add(L(_open == "dairy" ? "Mleczarnia" : "Zamówienia", 30, Brand, true));
            head.Add(Btn("Zamknij", Close, Brand, Color.white));
            _panel.Add(head);
            var view = new ScrollView(ScrollViewMode.Vertical); view.style.flexGrow = 1; view.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _panel.Add(view);
            if (_open == "dairy") Dairy(view); else Orders(view);
            _root.Add(_panel);
            view.schedule.Execute(() => view.scrollOffset = new Vector2(0, scroll));
        }

        void Section(VisualElement v, string t) { var l = L(t, 20, Brand, true); l.style.marginTop = 14; l.style.marginBottom = 4; v.Add(l); }

        void Dairy(VisualElement v)
        {
            var sim = G.Sim; var p = sim.S.players[GameController.Me];
            var stock = Row(); stock.style.flexWrap = Wrap.Wrap;
            stock.Add(Pill($"Zbiornik: {p.tankMilk} / {p.tankCap} l", Brand, Color.white, 16));
            stock.Add(Pill($"Wolne miejsca: {Simulation.FreeStorage(p)} / {p.storeCap}", Chip, Ink, 16));
            if (p.manualHandling)
            {
                stock.Add(Pill($"Przy pakowaniu: {p.packingRack.Sum()} szt.", Chip, Ink, 16));
                stock.Add(Pill($"Niesiesz: {p.carried.Sum()} szt.", Chip, Ink, 16));
            }
            foreach (var k in GameData.Ready) stock.Add(Pill($"{p.store[(int)k]}× {GameData.P(k).Name}", Chip, Ink, 16));
            v.Add(stock);

            Section(v, G.usePhysicalProduction ? "Praca w hali" : "Produkcja");
            if (G.usePhysicalProduction)
            {
                v.Add(L("Partie rozpoczynasz przy zbiorniku, a pracę wykonujesz przy maszynach (Q). Gotowy towar przenosisz na regał i do dostawczaka.", 16, Ink));
                v.Add(Btn(p.productionStaffAssist ? "Pomoc załogi: włączona" : "Pomoc załogi: wyłączona — pracujesz osobiście",
                    () => { p.productionStaffAssist = !p.productionStaffAssist; BuildPanel(); }, p.productionStaffAssist ? Good : Brand, Color.white));
            }
            if (!G.usePhysicalProduction) foreach (var k in GameData.Ready)
            {
                var d = GameData.P(k); var r = Row(); r.style.marginBottom = 6;
                var name = L($"{d.Name}", 18, Ink, true); name.style.width = 120; r.Add(name);
                r.Add(Btn("−", () => { _qty[k] = Mathf.Max(1, _qty[k] - 1); BuildPanel(); }, Chip, Ink, 18, 32));
                var q = L($"{_qty[k]}", 18, Ink, true); q.style.width = 30; q.style.unityTextAlign = TextAnchor.MiddleCenter; r.Add(q);
                r.Add(Btn("+", () => { _qty[k] = Mathf.Min(d.MaxBatch, _qty[k] + 1); BuildPanel(); }, Chip, Ink, 18, 32));
                var info = L($"{_qty[k] * d.MilkPerUnit} l → {Money.Zl(d.Price * _qty[k])} zł", 15, Muted); info.style.width = 150; r.Add(info);
                string block = sim.BatchBlock(GameController.Me, k, _qty[k]);
                var start = Btn("Start", () => { G.Send(new StartBatch(GameController.Me, k, _qty[k])); BuildPanel(); }, block == "" ? Good : new Color(0.7f, 0.7f, 0.7f), Color.white);
                r.Add(start);
                v.Add(r);
                if (block != "") { var b = L(block, 14, Bad); b.style.marginLeft = 120; v.Add(b); }
            }

            if (p.batches.Count > 0)
            {
                Section(v, "W toku");
                foreach (var b in p.batches)
                {
                    var sg = Simulation.StageOf(b); var r = Row(); r.style.marginBottom = 4;
                    var l = L($"{b.qty}× {GameData.P(b.product).Name}: {sg.Label}", 16, Ink, true); l.style.width = 300; r.Add(l);
                    r.Add(Bar((float)(b.progress / sim.StageNeed(b)), sg.Kind == StationKind.Czas ? new Color(0.6f, 0.4f, 0.85f) : Brand, 150));
                    if (!G.usePhysicalProduction && sg.Kind == StationKind.Praca && !b.Waiting) r.Add(Btn("Pomóż!", () => { G.Send(new HelpStation(GameController.Me, sg.Station)); }, new Color(0.95f, 0.6f, 0.1f), Color.white, 14, 28));
                    v.Add(r);
                    if (b.Waiting) v.Add(L(b.wait, 14, Bad));
                }
            }

            Section(v, "Ludzie");
            if (PlantSim.WorkersOff(p))
            {
                var off = L(p.workers.Count > 0 ? $"Pracownicy wyłączeni ({p.workers.Count} w zapisie, bez pensji). Halę obsługujesz sam. Włączysz ich w menu Escape." : "Pracownicy wyłączeni. Halę obsługujesz sam. Włączysz ich w menu Escape.", 15, Muted);
                off.style.whiteSpace = WhiteSpace.Normal; v.Add(off);
            }
            else
            {
            foreach (var w in p.workers)
            {
                var r = Row(); r.style.marginBottom = 4;
                bool sel = p.selected == w.id;
                r.Add(Btn(w.name + (sel ? " ✔" : ""), () => { G.Send(new Mleczarka.Core.SelectWorkerCmd(GameController.Me, w.id)); BuildPanel(); }, sel ? Brand : Chip, sel ? Color.white : Ink, 15, 30));
                string where = PlantData.M(w.Target ?? "")?.Name ?? (GameData.Stations.TryGetValue(w.Target ?? "", out var sd) ? sd.Name : w.Target);
                var st = L(w.Walking ? $"idzie: {where}" : where, 15, Ink); st.style.width = 190; r.Add(st);
                var works = Simulation.WorkerAt(p, w.station) == w ? sim.BatchesAt(p, w.station).FirstOrDefault(x => Simulation.StageOf(x).Kind == StationKind.Praca) : null;
                r.Add(L(works != null ? "pracuje" : w.Walking ? "w drodze" : "czeka", 14, works != null ? Good : Muted, true));
                r.Add(Btn("Zmień stanowisko", () => { G.Send(new MoveWorker(GameController.Me, w.id, NextStation(w.Target))); BuildPanel(); }, Chip, Ink, 13, 28));
                v.Add(r);
            }
            var hire = Row(); hire.Add(Btn($"Zatrudnij ({GameConfig.HireCost} zł)", () => { G.Send(new HireWorker(GameController.Me)); BuildPanel(); }, Good, Color.white));
            hire.Add(L("Wybrany pracownik pracuje na 100%, pozostali na 70%.", 13, Muted)); v.Add(hire);
            }

            Section(v, "Rozbudowa");
            var names = new[] { "Większa cysterna (+10 l)", "Większy dostawczak (+6 szt.)", "Większy zbiornik (+20 l)", "Większy magazyn (+12)", "Szybsze maszyny (×1,35)" };
            for (int i = 0; i < 5; i++)
            {
                var kind = (UpgradeKind)i; var cost = Simulation.UpgradeCost(p, kind); var r = Row(); r.style.marginBottom = 4;
                var l = L($"{names[i]} — poziom {p.upgrades[i]}", 16, Ink); l.style.width = 330; r.Add(l);
                if (cost.HasValue) r.Add(Btn($"{cost.Value} zł", () => { G.Send(new BuyUpgrade(GameController.Me, kind)); BuildPanel(); }, p.money >= cost.Value ? Good : new Color(0.7f, 0.7f, 0.7f), Color.white, 15, 30));
                else r.Add(Pill("maks.", Chip, Ink));
                v.Add(r);
            }
        }

        static string NextStation(string cur)
        {
            var list = new[] { "uht", "past", "packC", "packK", "tank" };
            int i = System.Array.IndexOf(list, cur);
            return list[(i + 1) % list.Length];
        }

        void Orders(VisualElement v)
        {
            var sim = G.Sim; var S = sim.S;
            if (S.mission.over)
            {
                v.Add(L(S.mission.win ? $"Misja wykonana! ★ {S.mission.stars}" : "Misja nieudana.", 22, S.mission.win ? Good : Bad, true));
                var r = Row(); r.Add(Btn("Graj dalej (tryb swobodny)", () => { sim.ContinueFree(); BuildPanel(); }, Good, Color.white));
                r.Add(Btn("Zacznij misję od nowa", () => { G.NewGame(GameMode.Misja); BuildPanel(); }, Brand, Color.white)); v.Add(r);
            }
            v.Add(L("Przyjmij zamówienie, zrób towar w mleczarni i dowieź go dostawczakiem na rynek przed terminem.", 15, Muted));
            foreach (var o in S.orders.OrderBy(o => o.status).ThenBy(o => o.deadline))
            {
                if (o.status != OrderStatus.Otwarte && o.status != OrderStatus.Przyjete) continue;
                var c = Card(new Color(1, 1, 1, 0.7f), 14, 8, 12); c.style.marginTop = 8;
                var r = Row(); r.style.justifyContent = Justify.SpaceBetween;
                r.Add(L(o.client + (o.mission ? "  (misja)" : ""), 19, Ink, true));
                r.Add(Pill($"{o.reward} zł", Good, Color.white, 16));
                c.Add(r);
                var items = Row(); items.style.flexWrap = Wrap.Wrap;
                for (int i = 0; i < o.items.Length; i++) if (o.items[i] > 0) items.Add(Pill($"{o.items[i]}× {GameData.Products[i].Name}", Chip, Ink));
                double left = o.deadline - S.t;
                if (!S.noTimeLimits) items.Add(Pill($"zostało {(int)(left / 60)}:{(int)(left % 60):00}", left < 90 ? Bad : Brand, Color.white));
                c.Add(items);
                if (o.status == OrderStatus.Otwarte) c.Add(Btn("Przyjmij", () => { G.Send(new ClaimOrder(GameController.Me, o.id)); BuildPanel(); }, Brand, Color.white));
                else c.Add(L(o.owner == GameController.Me ? "Przyjęte — dowieź na rynek" : "Zajęte", 15, Good, true));
                v.Add(c);
            }
        }
    }
}
