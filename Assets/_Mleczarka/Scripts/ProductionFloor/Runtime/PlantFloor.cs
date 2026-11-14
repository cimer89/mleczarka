using System;
using System.Collections.Generic;
using System.Linq;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using Mleczarka.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mleczarka.ProductionFloor
{
    /// <summary>
    /// Skrzydło maszyn: obsługa pełnego ciągu technologicznego (PlantSim) przez gracza i pracowników.
    /// Gracz podchodzi do stanowiska maszyny: Q — czynność / przytrzymanie, C — mycie CIP, X — przegląd lub naprawa,
    /// P — przypisz pracownika. Przepływ mleka rurami, produkty na taśmie, skrzynki na palecie i w chłodni
    /// oraz pracownicy z animacją są tylko obrazem stanu symulacji.
    /// </summary>
    public sealed class PlantFloor : MonoBehaviour
    {
        [Serializable] public class PipeRoute { public string from, to; public Vector3[] points; public bool curd; }

        public Transform wingRoot;
        public PlantMachineView[] views;
        public PipeRoute[] pipes;
        public Transform beltA, beltB, pallet, coldInterior;
        public GameObject workerPrefab, cartonPrefab, cupPrefab, cratePrefab;
        public Material milkMat, curdMat, butterMat, cheeseMat, twarogMat, steamMat;
        public float aisleZ = -1f;

        public static PlantFloor Instance { get; private set; }
        /// <summary>Gracz stoi przy stanowisku skrzydła — stara hala nie przejmuje wtedy klawisza Q.</summary>
        public static bool Busy => Instance && Instance._near;

        GameController G => GameController.Instance;
        PlayerState P => G.Sim.S.players[GameController.Me];

        PlantMachineView _near;
        public PlantMachineView NearView => _near;
        public Product SelectedProduct => Order[_productIndex];
        StartMenuUI _menu;
        float _nextHold;
        int _productIndex, _qty = 2;
        string _msg = ""; float _msgUntil;
        GUIStyle _text, _small, _title;
        Animator _playerAnim; bool _playerHasWork; float _playerWorkUntil;

        static readonly Product[] Order = { Product.Uht, Product.MlekoPast, Product.Jogurt, Product.Kefir, Product.Smietana, Product.Maslo, Product.Twarog, Product.Ser };
        static readonly HashSet<string> SolidFrom = new HashSet<string> { "rozlewKarton", "rozlewKubki", "maslnica", "prasa", "dojrzewalnia" };

        class Mover { public Transform t; public Vector3[] path; public float s, speed, length; public Action done; public bool orient; }
        readonly List<Mover> _movers = new List<Mover>();
        readonly Dictionary<int, string> _lastMachine = new Dictionary<int, string>();
        readonly Dictionary<string, float> _emitClock = new Dictionary<string, float>();
        readonly Dictionary<string, int> _emitted = new Dictionary<string, int>();
        readonly List<GameObject> _palletCrates = new List<GameObject>(), _coldCrates = new List<GameObject>();
        readonly List<(Transform t, float age, Vector3 v)> _puffs = new List<(Transform, float, Vector3)>();
        Transform _fx;

        class WorkerVis { public GameObject go; public Animator anim; public Vector3 pos, from; public string target; public bool walking; public Renderer[] rends; }
        readonly List<WorkerVis> _workers = new List<WorkerVis>();

        // ---------------- start ----------------

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; if (G) G.SimEvent -= OnSimEvent; }

        void Start()
        {
            if (!G || !G.player) { enabled = false; return; }
            Simulation.Migrate(G.Sim.S);
            // stare partie z dawnej linii dokończą się same; nowe zaczyna się w skrzydle
            if (P.plant == null || !P.plant.enabled) { PlantSim.Enable(P); G.Say("Nowe skrzydło maszyn działa. Partie zaczynasz przy panelu przyjęcia mleka (Q)."); }
            P.manualHandling = true;
            G.SimEvent += OnSimEvent;
            _fx = new GameObject("Skrzydło — animacja procesu").transform; _fx.SetParent(wingRoot ? wingRoot : transform, false);
            foreach (var j in P.plant.jobs) if (j.machine != null) _lastMachine[j.id] = j.machine;
            if (!GetComponent<PlantGuide>()) gameObject.AddComponent<PlantGuide>().floor = this;
            _playerAnim = G.player.GetComponentInChildren<Animator>();
            _playerHasWork = _playerAnim && _playerAnim.runtimeAnimatorController && _playerAnim.parameters.Any(p => p.name == "Work");
        }

        PlantMachineView View(string id) => views.FirstOrDefault(v => v && v.kind == PlantMachineView.Kind.Machine && v.machineId == id);
        PlantMachineView ViewOf(PlantMachineView.Kind k) => views.FirstOrDefault(v => v && v.kind == k);

        // ---------------- pętla ----------------

        void Update()
        {
            if (!G || G.Sim == null || P.plant == null) return;
            if (!P.plant.enabled) PlantSim.Enable(P);
            _near = NearestView();
            HandleInput();
            EmitFromFillers();
            MoveItems();
            Puffs();
            Workers();
            Stacks();
            if (_playerHasWork) _playerAnim.SetBool("Work", Time.time < _playerWorkUntil);
        }

        PlantMachineView NearestView()
        {
            if (!G.player || G.player.CurrentVehicle) return null;
            var pp = G.player.transform.position;
            PlantMachineView best = null; float bd = 1.8f;
            foreach (var v in views)
            {
                if (!v || !v.station) continue;
                var d = v.station.position - pp; d.y = 0; float dist = d.magnitude;
                if (Mathf.Abs(v.station.position.y - pp.y) < 2f && dist < bd) { bd = dist; best = v; }
            }
            return best;
        }

        void Say(Result r) { if (!string.IsNullOrEmpty(r.Msg)) { _msg = r.Msg; _msgUntil = Time.unscaledTime + 4; } }
        void Say(string s) { _msg = s; _msgUntil = Time.unscaledTime + 4; }

        bool Blocked()
        {
            if (!G.player || G.player.InputBlocked || G.player.CurrentVehicle) return true;
            if (!_menu) _menu = FindFirstObjectByType<StartMenuUI>();
            return _menu && _menu.IsOpen;
        }

        // ---------------- sterowanie ----------------

        void HandleInput()
        {
            var kb = Keyboard.current;
            if (kb == null || _near == null || Blocked()) return;
            var v = _near;
            for (int i = 0; i < 8; i++)
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) _productIndex = i;
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) _qty = Mathf.Max(1, _qty - 1);
            if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) _qty = Mathf.Min(GameData.P(Order[_productIndex]).MaxBatch, _qty + 1);
            _qty = Mathf.Clamp(_qty, 1, GameData.P(Order[_productIndex]).MaxBatch);

            bool q = kb.qKey.wasPressedThisFrame, qHold = kb.qKey.isPressed && Time.time >= _nextHold;
            if (q || qHold) Primary(v, q);
            if (kb.cKey.wasPressedThisFrame && v.kind == PlantMachineView.Kind.Machine) Say(G.Send(new StartCip(GameController.Me, v.machineId)));
            if ((kb.xKey.wasPressedThisFrame || (kb.xKey.isPressed && Time.time >= _nextHold)))
            {
                if (v.kind == PlantMachineView.Kind.Machine) { Work(v); Say(G.SendHold(new ServiceMachine(GameController.Me, v.machineId))); _nextHold = Time.time + 0.5f; }
                else if (v.kind == PlantMachineView.Kind.Lab && kb.xKey.wasPressedThisFrame) Say(G.Send(new RejectMilk(GameController.Me)));
            }
            if (kb.pKey.wasPressedThisFrame && v.kind == PlantMachineView.Kind.Machine) CycleWorker(v.machineId);
        }

        void Work(PlantMachineView v)
        {
            v.LastHandsTime = Time.time; _playerWorkUntil = Time.time + 0.7f;
            var to = (v.transform.position - G.player.transform.position); to.y = 0;
            if (to.sqrMagnitude > 0.01f) G.player.transform.rotation = Quaternion.LookRotation(to.normalized);
        }

        void Primary(PlantMachineView v, bool pressed)
        {
            int me = GameController.Me;
            switch (v.kind)
            {
                case PlantMachineView.Kind.Intake:
                    if (pressed) Say(G.Send(new StartJob(me, Order[_productIndex], _qty)));
                    break;
                case PlantMachineView.Kind.Lab:
                    Work(v); Say(G.SendHold(new TestSample(me))); _nextHold = Time.time + 0.5f;
                    break;
                case PlantMachineView.Kind.Cip:
                    if (!pressed) break;
                    var dirty = P.plant.machines.Where(m => PlantData.M(m.id).Cleanable && !m.broken && m.cip <= 0 && m.dirt >= 5 && PlantSim.JobAt(P, m.id) == null)
                        .OrderByDescending(m => m.dirt).FirstOrDefault();
                    Say(dirty == null ? Result.Fail("Wszystkie wolne maszyny są czyste.") : G.Send(new StartCip(me, dirty.id)));
                    break;
                case PlantMachineView.Kind.CreamTank:
                    if (pressed) Say($"Śmietanka: {P.plant.cream:0.0} / {P.plant.creamCap:0} l. Powstaje na wirówce, idzie na masło i śmietanę.");
                    break;
                case PlantMachineView.Kind.ColdRoom:
                    if (!pressed) break;
                    if (P.carried.Sum() > 0) Say(G.Send(new MoveCrate(me, CrateAction.Store)));
                    else
                    {
                        var prod = Order[_productIndex];
                        if (P.store[(int)prod] <= 0) { int best = Enumerable.Range(0, GameData.ProductCount).OrderByDescending(i => P.store[i]).First(); prod = (Product)best; }
                        Say(G.Send(new MoveCrate(me, CrateAction.PickStored, prod)));
                    }
                    break;
                default:
                {
                    var m = v.Machine; if (m == null) break;
                    if (v.machineId == "pakowarka" && pressed && !m.broken && !PlantSim.NeedsHands(P, m) && P.packingRack.Sum() > 0)
                    {
                        if (P.carried.Sum() > 0) { Say("Najpierw odłóż niesioną skrzynkę (chłodnia albo magazyn)."); break; }
                        int best = Enumerable.Range(0, GameData.ProductCount).OrderByDescending(i => P.packingRack[i]).First();
                        Say(G.Send(new MoveCrate(me, CrateAction.PickPacked, (Product)best)));
                        break;
                    }
                    var d = v.Step;
                    bool holdable = m.broken || (d != null && d.Kind == StepKind.Hold);
                    if (!pressed && !holdable) break;
                    if (P.carried.Sum() > 0 && pressed) { Say("Odłóż skrzynkę przed obsługą maszyny."); break; }
                    var r = G.SendHold(new MachineAct(me, v.machineId));
                    if (r.Ok) Work(v);
                    Say(r); _nextHold = Time.time + 0.5f;
                    break;
                }
            }
            if (pressed) _nextHold = Mathf.Max(_nextHold, Time.time + 0.5f);
        }

        void CycleWorker(string machineId)
        {
            var m = PlantSim.Machine(P, machineId);
            if (PlantSim.WorkersOff(P)) { Say("Pracownicy są wyłączeni — tę maszynę obsługujesz sam (Q)."); return; }
            if (P.workers.Count == 0) { Say("Nie masz pracowników. Zatrudnisz ich w zarządzaniu mleczarnią (TAB)."); return; }
            int next = m.worker + 1;
            if (next >= P.workers.Count) { Say(G.Send(new AssignWorker(GameController.Me, Mathf.Max(0, m.worker), ""))); return; }
            Say(G.Send(new AssignWorker(GameController.Me, next, machineId)));
        }

        // ---------------- przepływ: rury, taśma, skrzynki ----------------

        void OnSimEvent(GameEvent e)
        {
            if (e.Pid != GameController.Me) return;
            if (e.Type == "jobAt")
            {
                var job = P.plant.jobs.FirstOrDefault(x => x.id == e.Id);
                string to = e.Text;
                string from = _lastMachine.TryGetValue(e.Id, out var f) ? f : job != null && job.stage == 0 ? SourceOf(job.product) : null;
                _lastMachine[e.Id] = to;
                if (job != null && from != null) Transfer(from, to, job);
            }
            else if (e.Type == "jobDone")
            {
                _lastMachine.Remove(e.Id);
                var pak = View("pakowarka");
                if (pak && pak.outPort && pallet) SpawnMover(MakeCrate(), new[] { pak.outPort.position, pallet.position + Vector3.up * 0.5f }, 1.2f, null);
            }
            else if (e.Type == "cipStart")
            {
                var v = View(e.Text); var cip = ViewOf(PlantMachineView.Kind.Cip);
                if (v && cip) Pulse(cip.outPort ? cip.outPort.position : cip.transform.position + Vector3.up, v.inPort ? v.inPort.position : v.transform.position + Vector3.up, new Color(0.55f, 0.75f, 1f));
            }
        }

        static string SourceOf(Product p) => p == Product.Maslo || p == Product.Smietana ? "smietanka" : "silos";

        void Transfer(string from, string to, JobState job)
        {
            if (SolidFrom.Contains(from))
            {
                // rozlewaczki i maślnica wysyłają towar na taśmę na bieżąco; tu przenosimy krążki i twaróg z prasy oraz sery z dojrzewalni
                if (from == "prasa" || from == "dojrzewalnia")
                    for (int i = 0; i < Mathf.Min(job.qty, 6); i++) { int k = i; Delay(k * 0.5f, () => OnBelt(from, to, job.product)); }
                return;
            }
            var pipe = pipes?.FirstOrDefault(p => p.from == from && p.to == to);
            if (pipe != null && pipe.points != null && pipe.points.Length > 1)
            {
                var c = pipe.curd ? new Color(0.95f, 0.93f, 0.8f) : job.product == Product.Maslo || job.product == Product.Smietana ? new Color(1f, 0.95f, 0.75f) : Color.white;
                for (int i = 0; i < 10; i++) { int k = i; Delay(k * 0.18f, () => SpawnMover(Slug(c), pipe.points, 3.2f, null, true)); }
            }
        }

        void Pulse(Vector3 a, Vector3 b, Color c)
        {
            var mid = new Vector3((a.x + b.x) / 2, Mathf.Max(a.y, b.y) + 2f, (a.z + b.z) / 2);
            var path = new[] { a, new Vector3(a.x, mid.y, a.z), new Vector3(b.x, mid.y, b.z), b };
            for (int i = 0; i < 8; i++) { int k = i; Delay(k * 0.2f, () => SpawnMover(Slug(c), path, 3f, null, true)); }
        }

        readonly List<(float at, Action a)> _delayed = new List<(float, Action)>();
        void Delay(float s, Action a) => _delayed.Add((Time.time + s, a));

        Vector3 OnBeltPoint(Vector3 p)
        {
            if (!beltA || !beltB) return p;
            Vector3 a = beltA.position, b = beltB.position, ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            return a + ab * t + Vector3.up * 0.12f;
        }

        void OnBelt(string from, string to, Product product)
        {
            var a = View(from); var b = View(to);
            if (!a || !b) return;
            Vector3 s = a.outPort ? a.outPort.position : a.transform.position + Vector3.up;
            Vector3 e = b.inPort ? b.inPort.position : b.transform.position + Vector3.up;
            Vector3 s1 = OnBeltPoint(s), e1 = OnBeltPoint(e);
            SpawnMover(Item(product), new[] { s, s1, e1, e }, 0.9f, null);
        }

        /// <summary>Rozlewaczki i maślnica: w czasie pracy na taśmę wyjeżdżają kolejne opakowania (tyle, ile sztuk w partii).</summary>
        void EmitFromFillers()
        {
            for (int i = _delayed.Count - 1; i >= 0; i--) if (Time.time >= _delayed[i].at) { var a = _delayed[i].a; _delayed.RemoveAt(i); a(); }
            foreach (var id in new[] { "rozlewKarton", "rozlewKubki", "maslnica" })
            {
                var v = View(id); if (!v) continue;
                var j = v.Job; var d = v.Step;
                if (j == null || d == null) continue;
                bool emitting = id == "maslnica" ? d.Label.StartsWith("Uformuj") && v.Running : d.Kind == StepKind.Auto && v.Running;
                string key = id + ":" + j.id;
                if (!emitting) continue;
                _emitted.TryGetValue(key, out int n);
                _emitClock.TryGetValue(key, out float next);
                if (n >= j.qty || Time.time < next) continue;
                _emitted[key] = n + 1; _emitClock[key] = Time.time + Mathf.Max(0.35f, (float)PlantSim.Need(G.Sim, j, d) / Mathf.Max(1, j.qty) * 0.8f);
                OnBelt(id, "pakowarka", j.product);
            }
        }

        void SpawnMover(GameObject go, Vector3[] path, float speed, Action done, bool orient = false)
        {
            if (!go) return;
            go.transform.SetParent(_fx, true);
            float len = 0; for (int i = 1; i < path.Length; i++) len += Vector3.Distance(path[i - 1], path[i]);
            go.transform.position = path[0];
            _movers.Add(new Mover { t = go.transform, path = path, speed = speed, length = len, done = done, orient = orient });
        }

        void MoveItems()
        {
            for (int i = _movers.Count - 1; i >= 0; i--)
            {
                var m = _movers[i];
                if (!m.t) { _movers.RemoveAt(i); continue; }
                m.s += m.speed * Time.deltaTime;
                if (m.s >= m.length) { m.done?.Invoke(); Destroy(m.t.gameObject); _movers.RemoveAt(i); continue; }
                float s = m.s;
                for (int k = 1; k < m.path.Length; k++)
                {
                    float seg = Vector3.Distance(m.path[k - 1], m.path[k]);
                    if (s <= seg || k == m.path.Length - 1)
                    {
                        var dir = m.path[k] - m.path[k - 1];
                        m.t.position = m.path[k - 1] + dir * Mathf.Clamp01(seg > 0 ? s / seg : 1);
                        if (m.orient && dir.sqrMagnitude > 1e-4f) m.t.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                        break;
                    }
                    s -= seg;
                }
            }
        }

        GameObject Slug(Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(go.GetComponent<Collider>());
            go.name = "Mleko w rurze"; go.transform.localScale = new Vector3(0.2f, 0.09f, 0.2f);
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = milkMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mpb = new MaterialPropertyBlock(); mpb.SetColor("_BaseColor", c); mpb.SetColor("_EmissionColor", c * 0.6f); r.SetPropertyBlock(mpb);
            return go;
        }

        GameObject Item(Product p)
        {
            GameObject go;
            switch (p)
            {
                case Product.Uht: case Product.MlekoPast: go = cartonPrefab ? Small(Instantiate(cartonPrefab), 0.45f) : Prim(PrimitiveType.Cube, new Vector3(0.12f, 0.24f, 0.12f), milkMat); break;
                case Product.Jogurt: case Product.Kefir: case Product.Smietana: go = cupPrefab ? Small(Instantiate(cupPrefab), 0.4f) : Prim(PrimitiveType.Cylinder, new Vector3(0.12f, 0.08f, 0.12f), milkMat); break;
                case Product.Maslo: go = Prim(PrimitiveType.Cube, new Vector3(0.18f, 0.08f, 0.1f), butterMat); break;
                case Product.Ser: go = Prim(PrimitiveType.Cylinder, new Vector3(0.34f, 0.07f, 0.34f), cheeseMat); break;
                default: go = Prim(PrimitiveType.Cube, new Vector3(0.2f, 0.1f, 0.14f), twarogMat); break;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            go.name = "Na taśmie: " + GameData.P(p).Name;
            return go;
        }

        static GameObject Small(GameObject g, float k) { g.transform.localScale *= k; return g; }

        static GameObject Prim(PrimitiveType t, Vector3 s, Material m)
        {
            var go = GameObject.CreatePrimitive(t); go.transform.localScale = s;
            if (m) go.GetComponent<Renderer>().sharedMaterial = m; return go;
        }

        GameObject MakeCrate()
        {
            var go = cratePrefab ? Instantiate(cratePrefab) : Prim(PrimitiveType.Cube, new Vector3(0.5f, 0.3f, 0.35f), null);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            return go;
        }

        /// <summary>Skrzynki na palecie przy pakowarce i w chłodni: po jednej na każde 4 sztuki.</summary>
        void Stacks()
        {
            Sync(_palletCrates, pallet, Mathf.Min(16, Mathf.CeilToInt(P.packingRack.Sum() / 4f)), 2, 2);
            Sync(_coldCrates, coldInterior, Mathf.Min(36, Mathf.CeilToInt(P.store.Sum() / 4f)), 3, 3);
        }

        void Sync(List<GameObject> list, Transform at, int n, int nx, int nz)
        {
            if (!at) return;
            while (list.Count < n)
            {
                int i = list.Count, layer = i / (nx * nz), cell = i % (nx * nz);
                var c = MakeCrate(); c.name = "Skrzynka"; c.transform.SetParent(at, false);
                c.transform.localPosition = new Vector3((cell % nx - (nx - 1) / 2f) * 0.6f, 0.15f + layer * 0.34f, (cell / nx - (nz - 1) / 2f) * 0.45f);
                list.Add(c);
            }
            while (list.Count > n) { Destroy(list[list.Count - 1]); list.RemoveAt(list.Count - 1); }
        }

        void Puffs()
        {
            foreach (var v in views)
            {
                if (!v || v.kind != PlantMachineView.Kind.Machine) continue;
                var m = v.Machine; if (m == null) continue;
                if (v.steamPoint && (m.temp > 85 || m.cip > 0) && UnityEngine.Random.value < Time.deltaTime * 10) Puff(v.steamPoint.position, new Vector3(UnityEngine.Random.Range(-0.1f, 0.1f), 0.7f, UnityEngine.Random.Range(-0.1f, 0.1f)), 0.25f);
                if (v.bubblePoint && v.Running && UnityEngine.Random.value < Time.deltaTime * 6) Puff(v.bubblePoint.position + UnityEngine.Random.insideUnitSphere * 0.3f, new Vector3(0, 0.25f, 0), 0.08f);
            }
            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                var (t, age, vel) = _puffs[i]; age += Time.deltaTime;
                if (!t || age > 2f) { if (t) Destroy(t.gameObject); _puffs.RemoveAt(i); continue; }
                t.position += vel * Time.deltaTime; t.localScale *= 1 + Time.deltaTime * 0.6f;
                _puffs[i] = (t, age, vel);
            }
        }

        void Puff(Vector3 at, Vector3 vel, float size)
        {
            if (_puffs.Count > 80) return;
            var go = Prim(PrimitiveType.Sphere, Vector3.one * size, steamMat); Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(_fx, true); go.transform.position = at;
            _puffs.Add((go.transform, 0, vel));
        }

        // ---------------- pracownicy ----------------

        Vector3 PosFor(string id, int index)
        {
            var v = View(id) ?? ViewOf(PlantMachineView.Kind.Intake);
            if (!v || !v.station) return wingRoot ? wingRoot.position : transform.position;
            // pracownicy stoją obok pola obsługi (gracz ma wolne miejsce), kolejni dalej
            float side = index % 2 == 0 ? 1 : -1;
            return v.station.position + v.transform.right * side * (0.8f + 0.55f * (index / 2)) + (v.station.position - v.transform.position).normalized * 0.2f * (index / 2);
        }

        Vector3[] Path(Vector3 a, Vector3 b)
        {
            if (!wingRoot) return new[] { a, b };
            Vector3 la = wingRoot.InverseTransformPoint(a), lb = wingRoot.InverseTransformPoint(b);
            var p1 = wingRoot.TransformPoint(new Vector3(la.x, la.y, aisleZ));
            var p2 = wingRoot.TransformPoint(new Vector3(lb.x, lb.y, aisleZ));
            return new[] { a, p1, p2, b };
        }

        static Vector3 Along(Vector3[] path, float f)
        {
            float len = 0; for (int i = 1; i < path.Length; i++) len += Vector3.Distance(path[i - 1], path[i]);
            float s = f * len;
            for (int i = 1; i < path.Length; i++)
            {
                float seg = Vector3.Distance(path[i - 1], path[i]);
                if (s <= seg) return Vector3.Lerp(path[i - 1], path[i], seg > 0 ? s / seg : 1);
                s -= seg;
            }
            return path[path.Length - 1];
        }

        void Workers()
        {
            if (!workerPrefab) return;
            int want = PlantSim.WorkersOff(P) ? 0 : P.workers.Count;
            while (_workers.Count > want) { Destroy(_workers[_workers.Count - 1].go); _workers.RemoveAt(_workers.Count - 1); }
            if (want == 0) return;
            while (_workers.Count < P.workers.Count)
            {
                var go = Instantiate(workerPrefab, _fx);
                go.name = "Pracownik: " + P.workers[_workers.Count].name;
                var vis = new WorkerVis { go = go, anim = go.GetComponentInChildren<Animator>(), rends = go.GetComponentsInChildren<Renderer>() };
                if (vis.anim) vis.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                vis.pos = PosFor(P.workers[_workers.Count].station, _workers.Count); go.transform.position = vis.pos;
                // każdy pracownik w innym kolorze fartucha
                var tint = Color.HSVToRGB(0.55f + 0.13f * _workers.Count % 1f, 0.35f, 1f);
                var mpb = new MaterialPropertyBlock();
                foreach (var r in vis.rends) { if (r.sharedMaterial && r.sharedMaterial.name.Contains("Peasant")) { r.GetPropertyBlock(mpb); mpb.SetColor("_BaseColor", tint); r.SetPropertyBlock(mpb); } }
                _workers.Add(vis);
            }
            while (_workers.Count > P.workers.Count) { Destroy(_workers[_workers.Count - 1].go); _workers.RemoveAt(_workers.Count - 1); }

            for (int i = 0; i < _workers.Count; i++)
            {
                var w = P.workers[i]; var vis = _workers[i];
                var goal = PosFor(w.Target, i);
                if (w.Walking)
                {
                    if (!vis.walking || vis.target != w.walkTo) { vis.walking = true; vis.target = w.walkTo; vis.from = vis.pos; }
                    float f = 1 - Mathf.Clamp01((float)(w.walkT / GameConfig.WorkerWalkSeconds));
                    var np = Along(Path(vis.from, goal), Mathf.SmoothStep(0, 1, f));
                    var dir = np - vis.pos; dir.y = 0;
                    if (dir.sqrMagnitude > 1e-5f) vis.go.transform.rotation = Quaternion.Slerp(vis.go.transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 10);
                    vis.pos = np;
                    if (vis.anim) { vis.anim.SetFloat("Speed", 2f, 0.1f, Time.deltaTime); SetWork(vis.anim, false); }
                }
                else
                {
                    vis.walking = false;
                    vis.pos = Vector3.MoveTowards(vis.pos, goal, Time.deltaTime * 2f);
                    var mv = View(w.station);
                    if (mv) { var to = mv.transform.position - vis.pos; to.y = 0; if (to.sqrMagnitude > 0.01f) vis.go.transform.rotation = Quaternion.Slerp(vis.go.transform.rotation, Quaternion.LookRotation(to.normalized), Time.deltaTime * 6); }
                    var m = PlantSim.Machine(P, w.station);
                    var j = m != null ? PlantSim.JobAt(P, m.id) : null;
                    bool busy = j != null && j.worker == w.id;
                    if (busy && mv) mv.LastHandsTime = Time.time;
                    if (vis.anim) { vis.anim.SetFloat("Speed", 0f, 0.1f, Time.deltaTime); SetWork(vis.anim, busy || (m != null && m.cip > 0 && m.cip > PlantData.CipSeconds - 2)); }
                }
                vis.go.transform.position = vis.pos;
            }
        }

        static void SetWork(Animator a, bool on) { if (a.runtimeAnimatorController) a.SetBool("Work", on); }

        // ---------------- panel ----------------

        static void Fill(Rect r, Color c) { var p = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = p; }

        void Bar(ref float y, float x, float w, string label, float f, Color c)
        {
            GUI.Label(new Rect(x, y, 160, 22), label, _small);
            Fill(new Rect(x + 165, y + 8, w - 165, 7), new Color(0.16f, 0.28f, 0.3f));
            Fill(new Rect(x + 165, y + 8, (w - 165) * Mathf.Clamp01(f), 7), c);
            y += 24;
        }

        bool Button(Rect r, string s) => GUI.Button(r, s);

        void OnGUI()
        {
            if (!G || _near == null || Blocked()) return;
            float scale = Mathf.Clamp(Screen.width / 1500f, 0.65f, 1.5f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            if (_text == null)
            {
                _text = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, richText = true };
                _small = new GUIStyle(_text) { fontSize = 15 };
                _title = new GUIStyle(_text) { fontSize = 24, fontStyle = FontStyle.Bold };
            }
            float W = Mathf.Min(620, Screen.width / scale - 32), x = 16, inner = W - 32;
            float H = 456, top = Mathf.Max(8, Screen.height / scale - 150 - H);
            var accent = new Color(0.29f, 0.83f, 0.73f);
            Fill(new Rect(x, top, W, H), new Color(0.035f, 0.12f, 0.15f, 0.95f)); Fill(new Rect(x, top, 4, H), accent);
            float y = top + 10, lx = x + 16;
            var v = _near;
            switch (v.kind)
            {
                case PlantMachineView.Kind.Intake: IntakePanel(ref y, lx, inner); break;
                case PlantMachineView.Kind.Lab: LabPanel(ref y, lx, inner); break;
                case PlantMachineView.Kind.Cip: CipPanel(ref y, lx, inner); break;
                case PlantMachineView.Kind.CreamTank:
                    GUI.Label(new Rect(lx, y, inner, 32), "Zbiornik śmietanki", _title); y += 38;
                    Bar(ref y, lx, inner, $"Śmietanka {P.plant.cream:0.0} l", (float)(P.plant.cream / P.plant.creamCap), new Color(1f, 0.93f, 0.7f));
                    GUI.Label(new Rect(lx, y, inner, 80), "Wirówka oddziela śmietankę przy mleku, jogurcie, kefirze, twarogu i serze (10% litrów). Ze śmietanki robisz masło (3 l na zgrzewkę) i śmietanę (1 l).", _text); y += 80;
                    break;
                case PlantMachineView.Kind.ColdRoom: ColdPanel(ref y, lx, inner); break;
                default: MachinePanel(v, ref y, lx, inner); break;
            }
            if (Time.unscaledTime < _msgUntil) { GUI.Label(new Rect(lx, top + H - 34, inner, 30), _msg, _small); }
        }

        void MachinePanel(PlantMachineView v, ref float y, float lx, float inner)
        {
            var m = v.Machine; if (m == null) return;
            var def = PlantData.M(v.machineId);
            GUI.Label(new Rect(lx, y, inner, 32), def.Name, _title); y += 36;
            GUI.Label(new Rect(lx, y, inner, 26), PlantSim.Status(G.Sim, P, m), _text); y += 28;
            var j = v.Job;
            if (j != null)
            {
                var route = PlantData.Route(j.product);
                string rt = string.Join(" → ", route.Select((t, i) => { string n = PlantData.Machines.First(x => x.Type == t).Name.Split(' ')[0]; return i < j.stage ? $"<color=#7fe0a0>{n}</color>" : i == j.stage ? $"<b><color=#ffd166>{n}</color></b>" : $"<color=#9bb>{n}</color>"; }));
                GUI.Label(new Rect(lx, y, inner, 22), "Trasa: " + rt, _small); y += 24;
                var steps = PlantSim.StepsOf(j);
                for (int i = 0; i < steps.Length; i++)
                {
                    var s = steps[i];
                    string mark = i < j.step ? "<color=#7fe0a0>✔</color>" : i == j.step ? "<color=#ffd166>▶</color>" : "·";
                    string how = s.Kind == StepKind.Tap ? "Q" : s.Kind == StepKind.Hold ? "przytrzymaj Q" : "maszyna";
                    GUI.Label(new Rect(lx, y, inner, 22), $"{mark} {s.Label} <color=#9fb>({how})</color>", _small); y += 22;
                }
                var d = PlantSim.StepOf(j);
                if (d != null && !j.Waiting)
                {
                    float need = (float)PlantSim.Need(G.Sim, j, d);
                    float f = d.Kind == StepKind.Tap ? (j.worker >= 0 ? (float)(j.progress / PlantData.WorkerTapDelay) : 0) : (float)(j.progress / Mathf.Max(0.01f, need));
                    string left = d.Kind == StepKind.Auto ? $" · zostało {Mathf.CeilToInt(Mathf.Max(0, need - (float)j.progress))} s" : "";
                    Bar(ref y, lx, inner, "Postęp" + left, f, new Color(0.29f, 0.83f, 0.73f));
                }
            }
            else y += 4;
            Bar(ref y, lx, inner, $"Temperatura {m.temp:0}°C", (float)m.temp / 150f, new Color(1f, 0.5f, 0.3f));
            if (def.Cleanable) Bar(ref y, lx, inner, $"Zabrudzenie {m.dirt:0}%", (float)m.dirt / 100f, m.dirt >= PlantData.DirtSlow ? new Color(0.85f, 0.55f, 0.2f) : new Color(0.6f, 0.7f, 0.5f));
            Bar(ref y, lx, inner, $"Stan techniczny {m.condition:0}%", (float)m.condition / 100f, m.condition < 40 ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.4f, 0.8f, 0.5f));
            string worker = m.worker >= 0 && m.worker < P.workers.Count ? P.workers[m.worker].name : P.workers.Any(w => w.station == m.id && !w.Walking) ? P.workers.First(w => w.station == m.id).name + " (sam podszedł)" : "brak";
            GUI.Label(new Rect(lx, y, inner, 22), PlantSim.WorkersOff(P) ? $"Obsługa: tylko Ty · Partii tu: {PlantSim.JobsAt(P, m.id).Count()}" : $"Pracownik: {worker} · Partii tu: {PlantSim.JobsAt(P, m.id).Count()}", _small); y += 26;
            GUI.Label(new Rect(lx, y, inner, 44), "<color=#9bb>" + def.Info + "</color>", _small); y += 46;
            bool crew = !PlantSim.WorkersOff(P);
            float bw = (inner - 6 * (crew ? 3 : 2)) / (crew ? 4 : 3);
            if (v.machineId == "pakowarka" && P.packingRack.Sum() > 0 && !PlantSim.NeedsHands(P, m))
            { if (Button(new Rect(lx, y, bw, 34), "Odbierz skrzynkę (Q)")) Primary(v, true); }
            else if (Button(new Rect(lx, y, bw, 34), m.broken ? "Naprawa (Q)" : "Obsłuż (Q)")) Primary(v, true);
            if (def.Cleanable && Button(new Rect(lx + bw + 6, y, bw, 34), $"Mycie CIP (C)")) Say(G.Send(new StartCip(GameController.Me, v.machineId)));
            if (Button(new Rect(lx + 2 * (bw + 6), y, bw, 34), "Przegląd (X)")) Say(G.Send(new ServiceMachine(GameController.Me, v.machineId)));
            if (crew && Button(new Rect(lx + 3 * (bw + 6), y, bw, 34), "Pracownik (P)")) CycleWorker(v.machineId);
        }

        void IntakePanel(ref float y, float lx, float inner)
        {
            GUI.Label(new Rect(lx, y, inner, 32), "Przyjęcie mleka · start partii", _title); y += 36;
            GUI.Label(new Rect(lx, y, inner, 24), $"Silos: {P.tankMilk}/{P.tankCap} l · jakość {P.plant.rawQuality * 100:0}% · śmietanka {P.plant.cream:0.0} l · konto {Money.Zl(P.money)} zł", _small); y += 26;
            float bw = (inner - 18) / 4;
            for (int i = 0; i < Order.Length; i++)
            {
                var d = GameData.P(Order[i]);
                var r = new Rect(lx + (i % 4) * (bw + 6), y + (i / 4) * 52, bw, 48);
                string need = d.MilkPerUnit > 0 ? $"{d.MilkPerUnit} l mleka" : $"{d.CreamPerUnit} l śmietanki";
                var prev = GUI.backgroundColor; if (i == _productIndex) GUI.backgroundColor = new Color(0.3f, 0.9f, 0.75f);
                if (GUI.Button(r, $"{i + 1}. {d.Name}\n{need} · {Money.Zl(d.Price)} zł")) _productIndex = i;
                GUI.backgroundColor = prev;
            }
            y += 108;
            var pd = GameData.P(Order[_productIndex]);
            _qty = Mathf.Clamp(_qty, 1, pd.MaxBatch);
            if (GUI.Button(new Rect(lx, y, 40, 32), "−")) _qty = Mathf.Max(1, _qty - 1);
            GUI.Label(new Rect(lx + 48, y + 4, 200, 28), $"Partia: {_qty} × {pd.Unit}", _text);
            if (GUI.Button(new Rect(lx + 250, y, 40, 32), "+")) _qty = Mathf.Min(pd.MaxBatch, _qty + 1);
            string block = PlantSim.StartBlock(G.Sim, GameController.Me, Order[_productIndex], _qty);
            if (GUI.Button(new Rect(lx + inner - 210, y, 210, 32), "Rozpocznij partię (Q)")) Primary(_near, true);
            y += 38;
            GUI.Label(new Rect(lx, y, inner, 24), block == "" ? $"Trasa: {string.Join(" → ", PlantData.Route(pd.Id).Select(t => PlantData.Machines.First(m => m.Type == t).Name.Split(' ')[0]))}" : "<color=#ffb070>" + block + "</color>", _small); y += 28;
            GUI.Label(new Rect(lx, y, inner, 22), "Partie w hali:", _small); y += 22;
            foreach (var j in P.plant.jobs.Take(5))
            {
                string where = j.machine != null ? PlantData.M(j.machine).Name : j.Waiting ? j.wait : "w drodze";
                GUI.Label(new Rect(lx, y, inner, 20), $"• {GameData.P(j.product).Name} ×{j.qty} — {where} ({j.stage + 1}/{PlantData.Route(j.product).Length})" + (j.Waiting ? $" <color=#ffd166>{j.wait}</color>" : ""), _small); y += 20;
            }
        }

        void LabPanel(ref float y, float lx, float inner)
        {
            GUI.Label(new Rect(lx, y, inner, 32), "Laboratorium przyjęć", _title); y += 36;
            var pl = P.plant;
            GUI.Label(new Rect(lx, y, inner, 26), $"W cysternie: {P.tankerMilk} l", _text); y += 28;
            if (pl.sampleTested) { GUI.Label(new Rect(lx, y, inner, 50), PlantSim.SampleText(pl.sampleResult), _text); y += 52; }
            else if (pl.sampleProgress > 0) Bar(ref y, lx, inner, "Badanie próbki", (float)(pl.sampleProgress / PlantData.SampleSeconds), new Color(0.5f, 0.8f, 1f));
            else { GUI.Label(new Rect(lx, y, inner, 50), P.tankerMilk > 0 ? "Przed rozładunkiem trzeba zbadać próbkę. Przytrzymaj Q." : "Przywieź mleko cysterną z gospodarstw. Próbkę bada się przed rozładunkiem.", _text); y += 52; }
            GUI.Label(new Rect(lx, y, inner, 60), "<color=#9bb>Sprawdza się tłuszcz i liczbę bakterii. Mleko z za dużą liczbą bakterii psuje całą partię — lepiej je odrzucić (X).</color>", _small); y += 62;
            if (Button(new Rect(lx, y, 220, 34), "Badaj próbkę (Q)")) Primary(_near, true);
            if (pl.sampleTested && Button(new Rect(lx + 230, y, 220, 34), "Odrzuć mleko (X)")) Say(G.Send(new RejectMilk(GameController.Me)));
        }

        void CipPanel(ref float y, float lx, float inner)
        {
            GUI.Label(new Rect(lx, y, inner, 32), "Stacja mycia CIP", _title); y += 36;
            GUI.Label(new Rect(lx, y, inner, 40), $"Q — umyj najbrudniejszą wolną maszynę ({Money.Zl(PlantData.CipCost)} zł środków). Mycie: ług, płukanie, kwas, płukanie.", _small); y += 42;
            foreach (var m in P.plant.machines.Where(x => PlantData.M(x.id).Cleanable).OrderByDescending(x => x.dirt).Take(9))
                Bar(ref y, lx, inner, PlantData.M(m.id).Name.Split(' ')[0] + (m.cip > 0 ? " (mycie)" : ""), (float)m.dirt / 100f, m.cip > 0 ? new Color(0.3f, 0.55f, 1f) : m.dirt >= PlantData.DirtSlow ? new Color(0.85f, 0.55f, 0.2f) : new Color(0.6f, 0.7f, 0.5f));
            if (Button(new Rect(lx, y + 4, 260, 34), "Umyj najbrudniejszą (Q)")) Primary(_near, true);
        }

        void ColdPanel(ref float y, float lx, float inner)
        {
            GUI.Label(new Rect(lx, y, inner, 32), "Chłodnia wyrobów gotowych", _title); y += 36;
            GUI.Label(new Rect(lx, y, inner, 26), $"Zajęte: {P.store.Sum()}/{P.storeCap} · niesiesz: {P.carried.Sum()} · 4°C", _text); y += 30;
            for (int i = 0; i < Order.Length; i++)
            {
                var p = Order[i];
                string sel = i == _productIndex ? "<color=#ffd166>▶</color> " : "";
                GUI.Label(new Rect(lx + (i % 2) * (inner / 2), y + (i / 2) * 24, inner / 2, 24), $"{sel}{i + 1}. {GameData.P(p).Name}: {P.store[(int)p]}", _small);
            }
            y += 100;
            GUI.Label(new Rect(lx, y, inner, 40), "<color=#9bb>Gotowe skrzynki odbierasz przy pakowarce (Q) i odkładasz tutaj. Stąd bierzesz towar do dostawczaka.</color>", _small); y += 44;
            if (Button(new Rect(lx, y, 300, 34), P.carried.Sum() > 0 ? "Odłóż skrzynkę (Q)" : "Weź skrzynkę (Q, 1–8 produkt)")) Primary(_near, true);
        }
    }
}
