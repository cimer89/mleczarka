using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using Mleczarka.Vehicles;

namespace Mleczarka.ProductionFloor
{
    public sealed class ProductionFloor : MonoBehaviour
    {
        public FloorStation[] stations;
        public WheeledVehicle van;
        public Transform salePoint;
        public bool initializeDemoMilk = true;
        public bool showFullHud = true;
        public Transform facilityRoot;
        public GameObject cratePrefab, milkPrefab, yogurtPrefab;
        GameController G { get { return GameController.Instance; } }
        PlayerState P { get { return G.Sim.S.players[GameController.Me]; } }
        Product selected = Product.Uht;
        FloorStation nearby;
        string message = "";
        float messageUntil, nextHelp;
        GameObject carriedVisual;
        GUIStyle text, title;
        InputAction workAction, uhtAction, yogurtAction;
        bool workQueued;
        public int WorkPressCount { get; private set; }

        void OnEnable()
        {
            workAction = new InputAction("Praca w hali", InputActionType.Button, "<Keyboard>/q");
            uhtAction = new InputAction("Wybierz UHT", InputActionType.Button, "<Keyboard>/1");
            yogurtAction = new InputAction("Wybierz jogurt", InputActionType.Button, "<Keyboard>/2");
            // Zapamiętanie zdarzenia nie zależy od tego, między którymi klatkami naciśnięto klawisz.
            workAction.performed += context => { workQueued = true; WorkPressCount++; };
            uhtAction.performed += context => SelectProduct(Product.Uht);
            yogurtAction.performed += context => SelectProduct(Product.Jogurt);
            workAction.Enable(); uhtAction.Enable(); yogurtAction.Enable();
        }
        void OnDisable()
        {
            workAction?.Dispose(); uhtAction?.Dispose(); yogurtAction?.Dispose(); workQueued = false;
        }

        void Start()
        {
            if (!G || !G.player || !van) { enabled = false; Debug.LogError("Brak konfiguracji hali."); return; }
            Simulation.Migrate(G.Sim.S);
            if (!P.manualHandling)
            {
                P.manualHandling = true;
                // Osobny slot demonstracyjny: surowiec początkowy, nie odnawiany po wczytaniu.
                if (initializeDemoMilk && G.Sim.S.t < 0.1) P.tankMilk = Mathf.Min(24, P.tankCap);
                // Główna gra zachowuje pracowników i ich dane. Rdzeń w trybie ręcznym nie wykonuje za gracza pracy.
                if (initializeDemoMilk) foreach (var worker in P.workers) { worker.station = "tank"; worker.walkTo = null; worker.auto = false; }
            }
            G.Message += Show;
            carriedVisual = cratePrefab ? Instantiate(cratePrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            carriedVisual.name = "Niesiona skrzynka produktów";
            foreach (var collider in carriedVisual.GetComponentsInChildren<Collider>()) Destroy(collider);
            carriedVisual.transform.SetParent(G.player.transform, false);
            carriedVisual.transform.localPosition = new Vector3(0, 1, 0.6f);
            carriedVisual.transform.localScale = cratePrefab ? Vector3.one : new Vector3(0.6f, 0.4f, 0.4f);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader && !cratePrefab) { var mat = new Material(shader); mat.color = new Color(0.18f, 0.45f, 0.8f); carriedVisual.GetComponent<Renderer>().material = mat; }
            AddStock("Gotowe UHT", StockLocation.Packed, Product.Uht, StockAt(FloorAction.PackMilk, new Vector3(4, 1.2f, 3)));
            AddStock("Gotowy jogurt", StockLocation.Packed, Product.Jogurt, StockAt(FloorAction.PackYogurt, new Vector3(4, 1.2f, 3)));
            AddStock("UHT na regale", StockLocation.Warehouse, Product.Uht, StockAt(FloorAction.Warehouse, new Vector3(3, 0.7f, 3)));
            var warehouse = stations.First(s => s.action == FloorAction.Warehouse);
            AddStock("Jogurt na regale", StockLocation.Warehouse, Product.Jogurt, warehouse.secondStockAnchor ? warehouse.secondStockAnchor.position : warehouse.transform.position + new Vector3(3, 0.7f, 8));
            foreach (var product in new[] { Product.Uht, Product.Jogurt })
            {
                var cargo = AddStock(GameData.P(product).Name, StockLocation.Van, product, van.transform.position);
                cargo.transform.SetParent(van.transform, false);
                cargo.transform.localPosition = new Vector3(0, 1.5f, product == Product.Uht ? -1.7f : -1.1f);
            }
            var labels = facilityRoot ? stations.SelectMany(s => s.GetComponentsInChildren<TextMesh>()).ToArray() : FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
            foreach (var label in labels) if (!label.GetComponent<FloorBillboard>()) label.gameObject.AddComponent<FloorBillboard>();
            if (showFullHud) Show("Zacznij przy zbiorniku. W hali pracujesz osobiście — bez automatycznej produkcji.", false);
            // widoczny przebieg produkcji (rury, maszyny, taśmy) — tylko prezentacja stanu symulacji
            if (facilityRoot && !GetComponent<ProcessAnimator>()) gameObject.AddComponent<ProcessAnimator>().floor = this;
        }

        void OnDestroy() { if (G) G.Message -= Show; }
        Vector3 StockAt(FloorAction action, Vector3 fallback)
        { var station = stations.First(s => s.action == action); return station.stockAnchor ? station.stockAnchor.position : station.transform.position + fallback; }
        StockDisplay AddStock(string name, StockLocation location, Product product, Vector3 position)
        {
            var go = new GameObject(name); go.transform.position = position;
            var stock = go.AddComponent<StockDisplay>(); stock.location = location; stock.product = product;
            stock.itemPrefab = product == Product.Uht ? milkPrefab : yogurtPrefab; return stock;
        }
        public void SelectProduct(Product product) { if (product == Product.Uht || product == Product.Jogurt) selected = product; }
        public Result LoadAtVan()
        {
            if (!G || G.player.CurrentVehicle || Vector3.Distance(G.player.transform.position, van.transform.position) >= 4.5f)
                return Result.Fail("Podejdź pieszo do dostawczaka.");
            return G.Send(new MoveCrate(0, CrateAction.Load));
        }
        void Show(string value, bool error) { message = value; messageUntil = Time.unscaledTime + 5; }
        public FloorStation Nearest()
        {
            if (!G || !G.player || G.player.CurrentVehicle) return null;
            return stations.Where(s => s && Vector3.Distance(s.transform.position, G.player.transform.position) < 3.2f)
                .OrderBy(s => Vector3.Distance(s.transform.position, G.player.transform.position)).FirstOrDefault();
        }

        void Update()
        {
            if (!G) return;
            if (!P.manualHandling) P.manualHandling = true;   // np. po „Nowej grze” stan gracza jest świeży
            carriedVisual.SetActive(P.carried.Sum() > 0 && !G.player.CurrentVehicle);
            nearby = Nearest();
            ProcessWorkInput();
        }

        public void ProcessWorkInput()
        {
            if (!G || G.player.InputBlocked || PlantFloor.Busy) { workQueued = false; return; }
            nearby = Nearest();
            bool nearVan = !G.player.CurrentVehicle && Vector3.Distance(G.player.transform.position, van.transform.position) < 4.5f;
            if (workQueued)
            {
                workQueued = false;
                if (nearby) Act(nearby.action);
                else if (nearVan) LoadAtVan();
                else Show("Podejdź do niebieskiego pola stanowiska lub do dostawczaka.", false);
                nextHelp = Time.time + 0.5f;
            }
            else if (workAction != null && workAction.IsPressed() && nearby && IsWork(nearby.action) && Time.time >= nextHelp)
            { Act(nearby.action); nextHelp = Time.time + 0.5f; }
        }

        static bool IsWork(FloorAction action)
        { return action == FloorAction.Uht || action == FloorAction.Pasteurize || action == FloorAction.PackMilk || action == FloorAction.PackYogurt; }
        string StationId(FloorAction action)
        {
            switch (action)
            {
                case FloorAction.Uht: return "uht";
                case FloorAction.Pasteurize: return "past";
                case FloorAction.PackMilk: return "packC";
                case FloorAction.PackYogurt: return "packK";
                default: return "ferm";
            }
        }

        public void Act(FloorAction action)
        {
            if (!G || G.player.CurrentVehicle || G.player.InputBlocked) return;
            var station = Nearest();
            if (!station || station.action != action) { Show("Podejdź do odpowiedniego stanowiska.", true); return; }
            if (action == FloorAction.Intake)
            {
                if (P.carried.Sum() > 0) { Show("Najpierw odłóż skrzynkę.", true); return; }
                G.Send(new StartBatch(0, selected, selected == Product.Uht ? 4 : 2));
            }
            else if (action == FloorAction.Warehouse)
                G.Send(new MoveCrate(0, P.carried.Sum() > 0 ? CrateAction.Store : CrateAction.PickStored, selected));
            else if (action == FloorAction.Dispatch) Show("Weź skrzynkę z magazynu, podejdź do dostawczaka i naciśnij Q. E — wsiądź.", false);
            else
            {
                if (P.carried.Sum() > 0) { Show("Odłóż skrzynkę przed obsługą maszyny.", true); return; }
                string id = StationId(action);
                var batch = G.Sim.BatchesAt(P, id).FirstOrDefault();
                FaceStation(station);
                if (batch != null)
                {
                    if (action == FloorAction.Ferment) Show("Fermentacja trwa 4 minuty rzeczywiste. W tym czasie możesz robić UHT.", false);
                    else G.Send(new HelpStation(0, id));
                }
                else if (action == FloorAction.PackMilk || action == FloorAction.PackYogurt)
                    G.Send(new MoveCrate(0, CrateAction.PickPacked, action == FloorAction.PackMilk ? Product.Uht : Product.Jogurt));
                else Show("Najpierw rozpocznij partię przy zbiorniku mleka.", false);
            }
        }

        void FaceStation(FloorStation station)
        {
            // rolnik obraca się do maszyny, przy której pracuje (stanowisko stoi przed maszyną, maszyna jest dalej w głąb hali)
            var machine = facilityRoot ? facilityRoot.TransformPoint(facilityRoot.InverseTransformPoint(station.transform.position) + new Vector3(0, 0, 4f)) : station.transform.position;
            var to = machine - G.player.transform.position; to.y = 0;
            if (to.sqrMagnitude > 0.01f) G.player.transform.rotation = Quaternion.LookRotation(to.normalized);
        }

        string Hint()
        {
            if (G.player.CurrentVehicle)
            {
                if (G.PlaceOf(0).Kind == PlaceKind.Rynek) return "F — sprzedaj nadwyżki i dostarcz zamówienia · E — wysiądź";
                return (showFullHud ? "Jedź do sklepu na końcu drogi." : "Jedź z produktami na rynek (M — mapa).") + " E — wysiądź. Odległość: " + Vector3.Distance(G.PlayerPosition, salePoint.position).ToString("0") + " m";
            }
            if (nearby)
            {
                switch (nearby.action)
                {
                    case FloorAction.Intake: return "Q — rozpocznij partię: " + (selected == Product.Uht ? "4 kartony UHT" : "2 zgrzewki jogurtu") + " · 1 / 2 — produkt";
                    case FloorAction.Warehouse: return P.carried.Sum() > 0 ? "Q — odłóż skrzynkę do magazynu" : "Q — weź z magazynu: " + GameData.P(selected).Name + " · 1 / 2 — produkt";
                    case FloorAction.Dispatch: return "Zanieś skrzynkę z magazynu do dostawczaka. Q — załaduj przy pojeździe";
                    case FloorAction.Ferment: return "Fermentacja działa z upływem czasu. Możesz wrócić do linii UHT.";
                    default: return "Przytrzymaj Q — obsługuj maszynę. Po pakowaniu Q — podnieś gotową skrzynkę";
                }
            }
            if (Vector3.Distance(G.player.transform.position, van.transform.position) < 4.5f) return "Q — załaduj niesioną skrzynkę · E — wsiądź / wysiądź";
            return "Podejdź do stanowiska. WASD — chodzenie · Shift — bieg · kliknięcie — obrót kamery · Escape — zwolnij mysz";
        }

        static void Fill(Rect area, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void StationPanel(float scale)
        {
            float width = Mathf.Min(590, Screen.width / scale - 32);
            var accent = new Color(0.29f, 0.83f, 0.73f);
            float progress = -1;
            var action = nearby.action;
            string status = Hint();
            string button = "Obsłuż (Q)";
            if (IsWork(action) || action == FloorAction.Ferment)
            {
                var batch = G.Sim.BatchesAt(P, StationId(action)).FirstOrDefault();
                if (batch != null)
                {
                    double need = G.Sim.StageNeed(batch);
                    progress = Mathf.Clamp01((float)(batch.progress / need));
                    string detail = Mathf.FloorToInt(progress * 100) + "%";
                    if (action == FloorAction.Ferment && !batch.Waiting)
                    {
                        int seconds = Mathf.CeilToInt((float)(need - batch.progress));
                        detail = $"pozostało {seconds / 60}:{seconds % 60:00}";
                    }
                    status = $"{GameData.P(batch.product).Name} · {batch.qty} szt. · {detail}";
                    if (batch.Waiting) status += "\n" + batch.wait;
                    else if (IsWork(action)) status += "\nPrzytrzymaj Q, aby pracować.";
                }
                else
                {
                    bool milk = action == FloorAction.PackMilk;
                    bool packed = (milk || action == FloorAction.PackYogurt) && P.packingRack[(int)(milk ? Product.Uht : Product.Jogurt)] > 0;
                    status = packed ? "Gotowa partia czeka na odbiór. Zanieś skrzynkę do magazynu." : "Brak partii przy tej maszynie.";
                    if (packed) button = "Odbierz (Q)";
                }
            }
            float inner = width - 32;
            float heading = title.CalcHeight(new GUIContent(nearby.title), inner);
            float body = text.CalcHeight(new GUIContent(status), inner);
            bool feedback = Time.unscaledTime < messageUntil && !string.IsNullOrEmpty(message);
            float footer = feedback ? Mathf.Max(36, text.CalcHeight(new GUIContent(message), inner - 150)) : 36;
            float height = heading + body + footer + (progress >= 0 ? 52 : 40);
            float top = Mathf.Max(8, Screen.height / scale - 160 - height);
            Fill(new Rect(16, top, width, height), new Color(0.035f, 0.12f, 0.15f, 0.96f));
            Fill(new Rect(16, top, 4, height), accent);
            GUI.Label(new Rect(32, top + 8, inner, heading), nearby.title, title);
            float y = top + 14 + heading;
            GUI.Label(new Rect(32, y, inner, body), status, text);
            y += body + 8;
            if (progress >= 0)
            {
                Fill(new Rect(32, y, inner, 4), new Color(0.16f, 0.28f, 0.30f));
                Fill(new Rect(32, y, inner * progress, 4), accent);
                y += 12;
            }
            if (feedback) GUI.Label(new Rect(32, y, inner - 150, footer), message, text);
            if (GUI.Button(new Rect(width - 139, y, 139, 36), button)) Act(action);
        }

        void OnGUI()
        {
            if (!G || !enabled || G.player.InputBlocked || PlantFloor.Busy) return;
            if (!showFullHud && !nearby && P.carried.Sum() == 0 && !(G.player.CurrentVehicle && P.vanCargo.Sum() > 0)
                && Vector3.Distance(G.player.transform.position, van.transform.position) >= 4.5f) return;
            float scale = Mathf.Clamp(Screen.width / 1500f, 0.65f, 1.5f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            if (text == null) { text = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true }; title = new GUIStyle(text) { fontSize = 25, fontStyle = FontStyle.Bold }; }
            if (!showFullHud && nearby)
            {
                StationPanel(scale);
                return;
            }
            if (showFullHud)
            {
            GUI.Box(new Rect(16, 16, 730, 255), "");
            GUI.Label(new Rect(32, 28, 700, 38), "TWOJA MLECZARNIA · PRODUKCJA I DOSTAWY", title);
            string line = "Mleko: " + P.tankMilk + " l · Gotowe przy pakowaniu: " + P.packingRack.Sum() + " · Niesiesz: " + P.carried.Sum() +
                "\nMagazyn: " + P.store.Sum() + "/" + P.storeCap + " · Dostawczak: " + P.vanCargo.Sum() + "/" + P.vanCap + " · Konto: " + P.money.ToString("0.00") + " zł";
            foreach (var b in P.batches)
                line += "\n" + GameData.P(b.product).Name + ": " + GameData.Stations[Simulation.StageOf(b).Station].Name + " " + Mathf.Min(100, (float)(b.progress / G.Sim.StageNeed(b) * 100)).ToString("0") + "%";
            if (P.batches.Count == 0) line += "\nCel: zbiornik → UHT → pakowanie → magazyn → dostawczak → sklep";
            GUI.Label(new Rect(32, 70, 700, 185), line, text);
            }
            float bottom = Screen.height / scale;
            if (!showFullHud) bottom -= 150;
            GUI.Box(new Rect(16, bottom - 125, 1000, 110), "");
            GUI.Label(new Rect(32, bottom - 116, 970, 100), (nearby ? nearby.title + "\n" : "") + Hint() + (Time.unscaledTime < messageUntil ? "\n" + message : ""), text);
            if (!G.player.CurrentVehicle && nearby)
            {
                if (GUI.Button(new Rect(1030, bottom - 105, 260, 65), "Obsłuż stanowisko (Q)")) Act(nearby.action);
            }
            else if (!G.player.CurrentVehicle && Vector3.Distance(G.player.transform.position, van.transform.position) < 4.5f)
            {
                if (GUI.Button(new Rect(1030, bottom - 105, 260, 65), "Załaduj skrzynkę (Q)")) LoadAtVan();
            }
            else if (G.player.CurrentVehicle && G.PlaceOf(0).Kind == PlaceKind.Rynek)
            {
                if (GUI.Button(new Rect(1030, bottom - 105, 260, 65), "Sprzedaj towar (F)")) G.DoContextAction();
            }
        }
    }
}
