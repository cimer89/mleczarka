using System.Collections.Generic;
using System.Linq;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEngine;

namespace Mleczarka.ProductionFloor
{
    /// <summary>
    /// Pokazuje produkcję jako proces, a nie pojawianie się towaru:
    /// mleko płynie rurami między zbiornikiem a maszynami, pracujące maszyny drgają, świecą lampką i parują,
    /// zbiorniki fermentacji bąbelkują i mają wskaźnik postępu, a z pakowarki taśmą jadą kartony albo kubki
    /// do skrzynki, która zapełnia się na oczach. Działa wyłącznie na podstawie stanu symulacji (tylko odczyt).
    /// </summary>
    public sealed class ProcessAnimator : MonoBehaviour
    {
        public ProductionFloor floor;
        Transform root;
        GameController G => GameController.Instance;

        // ---------- dane linii (współrzędne lokalne hali z MainFactoryBuilder) ----------
        class Machine
        {
            public string station; public Transform t; public Vector3 basePos; public Bounds bounds;
            public Renderer lamp; public float steam; public bool steams;
        }
        class Flow { public string station; public Vector3[] path; public float length; public readonly List<Transform> drops = new List<Transform>(); public float phase; }
        class Line
        {
            public string packStation; public Product product; public Vector3 from, to; public GameObject prefab;
            public Transform crate; public readonly List<GameObject> inCrate = new List<GameObject>();
            public readonly List<(Transform t, float s)> moving = new List<(Transform, float)>();
            public int emitted; public BatchState batch;
        }

        readonly Dictionary<string, Machine> _machines = new Dictionary<string, Machine>();
        readonly List<Flow> _flows = new List<Flow>();
        readonly List<Line> _lines = new List<Line>();
        readonly Dictionary<BatchState, double> _lastProgress = new Dictionary<BatchState, double>();
        readonly Dictionary<string, float> _activeUntil = new Dictionary<string, float>();
        readonly List<(Transform t, float age, float life, Vector3 vel, float size)> _puffs = new List<(Transform, float, float, Vector3, float)>();
        readonly Stack<Transform> _puffPool = new Stack<Transform>();
        Transform _tankGauge, _fermGaugeA, _fermGaugeB; Machine _fermA, _fermB;
        Material _milk, _steam, _lampOff, _lampWork, _lampWait, _gaugeBg, _gaugeMilk, _gaugeFerm, _pipe;
        Transform _fx;

        Vector3 L(float x, float y, float z) => root.TransformPoint(new Vector3(x, y, z));
        PlayerState P => G.Sim.S.players[GameController.Me];

        void Start()
        {
            if (!floor) floor = GetComponent<ProductionFloor>();
            root = floor ? floor.facilityRoot : null;
            if (!root || !G) { enabled = false; return; }
            _fx = new GameObject("Animacja procesu").transform; _fx.SetParent(root, false);
            CreateMaterials();

            // maszyny: najbliższy model do znanego miejsca na posadzce
            AddMachine("tank", -10f, -2.5f, false);
            AddMachine("uht", -3f, -3f, true);
            AddMachine("packC", 4f, -3f, false);
            AddMachine("past", -10f, 9f, true);
            _fermA = AddMachine("ferm", -4f, 9.5f, false);
            _fermB = AddMachine("ferm2", -1.5f, 9.5f, false);
            AddMachine("packK", 4f, 9f, false);

            // rurociągi: istniejące rury na wysokości ~3 m + zejścia do maszyn dorobione tutaj
            AddFlow(new[] { "uht" }, Top("tank"), L(-10f, 3.2f, -2.5f), L(-3f, 3.2f, -2.5f), Top("uht"));
            AddFlow(new[] { "packC" }, Top("uht"), L(-3f, 3.2f, -2.5f), L(4f, 3.2f, -2.5f), Top("packC"));
            AddFlow(new[] { "past" }, Top("tank"), L(-11.2f, 3.2f, -2.5f), L(-11.2f, 3.0f, 9f), L(-10f, 3.0f, 9f), Top("past"));
            AddFlow(new[] { "ferm" }, Top("past"), L(-10f, 3.0f, 9f), L(-3f, 3.0f, 9f), Top("ferm"));
            AddFlow(new[] { "packK" }, Top("ferm"), L(-3f, 3.0f, 9f), L(4f, 3.0f, 9f), Top("packK"));

            // taśmy pakowania: od wyjścia pakowarki do skrzynki przy miejscu odbioru
            AddLine(FloorAction.PackMilk, "packC", Product.Uht, floor.milkPrefab, -3f);
            AddLine(FloorAction.PackYogurt, "packK", Product.Jogurt, floor.yogurtPrefab, 9f);

            // wskaźniki: poziom mleka w zbiorniku przyjęć i postęp fermentacji
            if (_machines.TryGetValue("tank", out var tank)) _tankGauge = Gauge(tank, _gaugeMilk);
            if (_fermA != null) _fermGaugeA = Gauge(_fermA, _gaugeFerm);
            if (_fermB != null) _fermGaugeB = Gauge(_fermB, _gaugeFerm);
        }

        void OnDestroy()
        {
            foreach (var m in new[] { _milk, _steam, _lampOff, _lampWork, _lampWait, _gaugeBg, _gaugeMilk, _gaugeFerm, _pipe }) if (m) Destroy(m);
        }

        // ---------- budowa ----------
        void CreateMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material M(Color c, float smooth = 0.4f, Color? emit = null)
            {
                var m = new Material(lit); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth);
                if (emit.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emit.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
                return m;
            }
            _milk = M(new Color(0.97f, 0.97f, 0.94f), 0.7f, new Color(0.25f, 0.25f, 0.24f));
            _lampOff = M(new Color(0.35f, 0.36f, 0.38f));
            _lampWork = M(new Color(0.2f, 0.9f, 0.35f), 0.6f, new Color(0.2f, 1.6f, 0.4f));
            _lampWait = M(new Color(1f, 0.65f, 0.1f), 0.6f, new Color(1.6f, 0.9f, 0.1f));
            _gaugeBg = M(new Color(0.12f, 0.16f, 0.2f), 0.5f);
            _gaugeMilk = M(new Color(0.95f, 0.95f, 0.9f), 0.5f, new Color(0.3f, 0.3f, 0.3f));
            _gaugeFerm = M(new Color(0.55f, 0.75f, 1f), 0.5f, new Color(0.2f, 0.35f, 0.6f));
            _pipe = M(new Color(0.72f, 0.75f, 0.78f), 0.75f); _pipe.SetFloat("_Metallic", 0.8f);
            _steam = M(new Color(1f, 1f, 1f, 0.45f), 0.1f);
            _steam.SetFloat("_Surface", 1f); _steam.SetFloat("_Blend", 0f);
            _steam.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); _steam.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _steam.SetFloat("_ZWrite", 0f); _steam.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); _steam.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _steam.SetOverrideTag("RenderType", "Transparent"); _steam.SetShaderPassEnabled("ShadowCaster", false);
        }

        Machine AddMachine(string station, float x, float z, bool steams)
        {
            var at = L(x, 0, z);
            Transform best = null; float bd = 2.5f; Bounds bb = default;
            foreach (Transform c in root)
            {
                var rs = c.GetComponentsInChildren<Renderer>(); if (rs.Length == 0 || c.name.StartsWith("Znacznik") || c.name.StartsWith("Spoina") || c.name.StartsWith("Pas ")) continue;
                var b = rs[0].bounds; foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds);
                if (b.size.y < 1.2f || b.size.x > 8f || b.size.z > 8f) continue;               // pomijamy posadzkę, ściany, znaczniki
                float d = new Vector2(b.center.x - at.x, b.center.z - at.z).magnitude;
                if (d < bd) { bd = d; best = c; bb = b; }
            }
            if (!best) return null;
            var m = new Machine { station = station, t = best, basePos = best.localPosition, bounds = bb, steams = steams };
            // lampka stanu na szczycie maszyny
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere); lamp.name = "Lampka stanu"; Destroy(lamp.GetComponent<Collider>());
            lamp.transform.SetParent(_fx, true); lamp.transform.position = new Vector3(bb.center.x, bb.max.y + 0.18f, bb.center.z); lamp.transform.localScale = Vector3.one * 0.22f;
            m.lamp = lamp.GetComponent<Renderer>(); m.lamp.sharedMaterial = _lampOff;
            _machines[station] = m;
            return m;
        }

        Vector3 Top(string station) { if (!_machines.TryGetValue(station, out var m)) return root.position; return new Vector3(m.bounds.center.x, m.bounds.max.y - 0.05f, m.bounds.center.z); }

        void AddFlow(string[] stations, params Vector3[] pts)
        {
            // zejścia z rurociągu do maszyn: widoczne odcinki rur
            Segment(pts[0], new Vector3(pts[0].x, pts[1].y, pts[0].z));
            Segment(pts[pts.Length - 1], new Vector3(pts[pts.Length - 1].x, pts[pts.Length - 2].y, pts[pts.Length - 1].z));
            var path = new List<Vector3> { pts[0], new Vector3(pts[0].x, pts[1].y, pts[0].z) };
            for (int i = 1; i < pts.Length - 1; i++) path.Add(pts[i]);
            path.Add(new Vector3(pts[pts.Length - 1].x, pts[pts.Length - 2].y, pts[pts.Length - 1].z)); path.Add(pts[pts.Length - 1]);
            // brakujące poziome odcinki (np. ze zbiornika do pasteryzatora) też dorysowujemy
            for (int i = 1; i < path.Count - 2; i++) if ((path[i] - path[i + 1]).sqrMagnitude > 0.01f && !ExistingPipe(path[i], path[i + 1])) Segment(path[i], path[i + 1]);
            var f = new Flow { station = stations[0], path = path.ToArray() };
            for (int i = 1; i < f.path.Length; i++) f.length += Vector3.Distance(f.path[i - 1], f.path[i]);
            int n = Mathf.Max(6, Mathf.RoundToInt(f.length / 0.9f));
            for (int i = 0; i < n; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Sphere); d.name = "Mleko w rurze"; Destroy(d.GetComponent<Collider>());
                d.transform.SetParent(_fx, true); d.transform.localScale = Vector3.one * 0.26f; d.GetComponent<Renderer>().sharedMaterial = _milk;
                d.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; d.SetActive(false); f.drops.Add(d.transform);
            }
            _flows.Add(f);
        }

        bool ExistingPipe(Vector3 a, Vector3 b)
        {
            // rury z budowniczego hali leżą na z=-2,5 (y 3,2) i z=9 (y 3,0); nie dublujemy ich
            var la = root.InverseTransformPoint(a); var lb = root.InverseTransformPoint(b);
            bool sameZ = Mathf.Abs(la.z - lb.z) < 0.05f && (Mathf.Abs(la.z + 2.5f) < 0.1f || Mathf.Abs(la.z - 9f) < 0.1f);
            return sameZ && Mathf.Min(la.x, lb.x) >= -10.05f && Mathf.Max(la.x, lb.x) <= 4.05f;
        }

        void Segment(Vector3 a, Vector3 b)
        {
            if ((a - b).sqrMagnitude < 0.01f) return;
            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder); p.name = "Rura (zejście)"; Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(_fx, true); p.transform.position = (a + b) / 2; p.transform.up = (b - a).normalized;
            p.transform.localScale = new Vector3(0.16f, Vector3.Distance(a, b) / 2, 0.16f); p.GetComponent<Renderer>().sharedMaterial = _pipe;
        }

        void AddLine(FloorAction packAction, string packStation, Product product, GameObject prefab, float z)
        {
            var station = floor.stations.FirstOrDefault(s => s && s.action == packAction);
            if (!_machines.TryGetValue(packStation, out var packer) || !station || !station.stockAnchor) return;
            // taśma: najbliższy przenośnik za pakowarką
            Transform conveyor = null; float bd = 4f; Bounds cb = default;
            foreach (Transform c in root)
            {
                if (!c.name.Contains("conveyor")) continue;
                var rs = c.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds);
                float d = Mathf.Abs(root.InverseTransformPoint(b.center).z - z);
                if (d < bd) { bd = d; conveyor = c; cb = b; }
            }
            float beltY = conveyor ? cb.min.y + cb.size.y * 0.78f : packer.bounds.min.y + 0.9f;
            var start = new Vector3(packer.bounds.max.x - 0.2f, beltY, packer.bounds.center.z);
            var anchor = station.stockAnchor.position;
            var end = new Vector3(anchor.x - 0.95f, beltY, packer.bounds.center.z);
            var line = new Line { packStation = packStation, product = product, from = start, to = end, prefab = prefab };
            // skrzynka w napełnianiu na końcu taśmy
            var crate = floor.cratePrefab ? Instantiate(floor.cratePrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "Skrzynka w napełnianiu"; foreach (var c in crate.GetComponentsInChildren<Collider>()) Destroy(c);
            crate.transform.SetParent(_fx, true); crate.transform.position = new Vector3(end.x + 0.15f, beltY - 0.05f, end.z);
            if (!floor.cratePrefab) crate.transform.localScale = new Vector3(0.6f, 0.3f, 0.45f);
            crate.SetActive(false); line.crate = crate.transform;
            _lines.Add(line);
        }

        Transform Gauge(Machine m, Material fill)
        {
            if (m == null) return null;
            var b = m.bounds; var side = new Vector3(b.min.x - 0.12f, b.min.y + 0.4f, b.center.z - b.extents.z * 0.4f);
            float h = Mathf.Max(1f, b.size.y - 0.8f);
            var bg = GameObject.CreatePrimitive(PrimitiveType.Cube); bg.name = "Wskaźnik (tło)"; Destroy(bg.GetComponent<Collider>());
            bg.transform.SetParent(_fx, true); bg.transform.position = side + Vector3.up * h / 2; bg.transform.localScale = new Vector3(0.08f, h, 0.22f);
            bg.GetComponent<Renderer>().sharedMaterial = _gaugeBg;
            var fg = GameObject.CreatePrimitive(PrimitiveType.Cube); fg.name = "Wskaźnik"; Destroy(fg.GetComponent<Collider>());
            var pivot = new GameObject("Wskaźnik — poziom").transform; pivot.SetParent(_fx, true); pivot.position = side + new Vector3(-0.03f, 0, 0);
            fg.transform.SetParent(pivot, false); fg.transform.localPosition = new Vector3(0, 0.5f, 0); fg.transform.localScale = new Vector3(0.06f, 1f, 0.16f);
            fg.GetComponent<Renderer>().sharedMaterial = fill;
            pivot.localScale = new Vector3(1, 0.001f, 1); pivot.name = h.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return pivot;
        }

        static void SetGauge(Transform pivot, float v)
        {
            if (!pivot) return;
            float h = float.Parse(pivot.name, System.Globalization.CultureInfo.InvariantCulture);
            var s = pivot.localScale; s.y = Mathf.Lerp(s.y, Mathf.Max(0.001f, Mathf.Clamp01(v) * h), 0.1f); pivot.localScale = s;
        }

        // ---------- klatka ----------
        bool Active(string st) => _activeUntil.TryGetValue(st, out var t) && Time.time < t;

        void Update()
        {
            if (!G || root == null) return;
            var p = P; float now = Time.time;

            // które stanowiska naprawdę pracują (postęp rośnie)
            foreach (var b in p.batches)
            {
                var st = Simulation.StageOf(b).Station;
                double prev = _lastProgress.TryGetValue(b, out var v) ? v : b.progress;
                if (b.progress > prev + 1e-6) _activeUntil[st] = now + 0.45f;
                _lastProgress[b] = b.progress;
            }
            foreach (var k in _lastProgress.Keys.Where(k => !p.batches.Contains(k)).ToList()) _lastProgress.Remove(k);

            // maszyny: drgania, lampki, para
            foreach (var m in _machines.Values)
            {
                string st = m.station == "ferm2" ? "ferm" : m.station;
                bool work = Active(st) && st != "tank";
                bool waiting = !work && st != "tank" && p.batches.Any(b => Simulation.StageOf(b).Station == st);
                m.lamp.sharedMaterial = work ? ((Mathf.Repeat(now * 2.5f, 1f) < 0.65f) ? _lampWork : _lampOff) : waiting ? _lampWait : _lampOff;
                if (work && st != "ferm")
                    m.t.localPosition = m.basePos + new Vector3(Mathf.Sin(now * 71f), Mathf.Abs(Mathf.Sin(now * 53f)) * 0.6f, Mathf.Cos(now * 67f)) * 0.012f;
                else m.t.localPosition = m.basePos;
                if (work && (m.steams || st == "ferm"))
                {
                    m.steam -= Time.deltaTime;
                    if (m.steam <= 0)
                    {
                        bool bubble = st == "ferm";
                        m.steam = bubble ? 0.35f : 0.12f;
                        var c = m.bounds.center; var top = new Vector3(c.x + Random.Range(-0.3f, 0.3f), m.bounds.max.y, c.z + Random.Range(-0.3f, 0.3f));
                        Puff(top, bubble ? new Vector3(0, 0.25f, 0) : new Vector3(Random.Range(-0.1f, 0.1f), 0.9f, Random.Range(-0.1f, 0.1f)), bubble ? 0.9f : 1.6f, bubble ? 0.18f : 0.5f);
                    }
                }
            }

            // mleko w rurach
            foreach (var f in _flows)
            {
                bool on = Active(f.station);
                f.phase += Time.deltaTime * (on ? 1.6f : 0f);
                for (int i = 0; i < f.drops.Count; i++)
                {
                    var d = f.drops[i];
                    if (!on) { if (d.gameObject.activeSelf) d.gameObject.SetActive(false); continue; }
                    float s = Mathf.Repeat(f.phase + i * (f.length / f.drops.Count), f.length);
                    d.position = Along(f.path, s); if (!d.gameObject.activeSelf) d.gameObject.SetActive(true);
                    d.localScale = Vector3.one * (0.24f + 0.05f * Mathf.Sin(now * 9f + i));
                }
            }

            // wskaźniki
            SetGauge(_tankGauge, p.tankCap > 0 ? p.tankMilk / (float)p.tankCap : 0);
            var ferm = p.batches.Where(b => Simulation.StageOf(b).Station == "ferm").ToList();
            SetGauge(_fermGaugeA, ferm.Count > 0 ? (float)(ferm[0].progress / G.Sim.StageNeed(ferm[0])) : 0);
            SetGauge(_fermGaugeB, ferm.Count > 1 ? (float)(ferm[1].progress / G.Sim.StageNeed(ferm[1])) : 0);

            // taśmy pakowania
            foreach (var line in _lines) UpdateLine(line, p);

            // para i bąbelki
            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                var (t, age, life, vel, size) = _puffs[i]; age += Time.deltaTime;
                if (age >= life) { t.gameObject.SetActive(false); _puffPool.Push(t); _puffs.RemoveAt(i); continue; }
                float k = age / life;
                t.position += vel * Time.deltaTime; t.localScale = Vector3.one * Mathf.Lerp(size * 0.4f, size, k);
                _puffs[i] = (t, age, life, vel, size);
            }
        }

        void UpdateLine(Line line, PlayerState p)
        {
            var batch = p.batches.FirstOrDefault(b => Simulation.StageOf(b).Station == line.packStation);
            if (batch != line.batch)
            {
                // nowa partia przy pakowarce albo partia zakończona: skrzynka jedzie dalej jako gotowy towar (wyświetla go StockDisplay)
                line.batch = batch; line.emitted = 0;
                foreach (var g in line.inCrate) Destroy(g); line.inCrate.Clear();
                line.crate.gameObject.SetActive(batch != null);
            }
            if (batch != null)
            {
                float frac = Mathf.Clamp01((float)(batch.progress / G.Sim.StageNeed(batch)));
                int due = Mathf.Min(batch.qty, Mathf.FloorToInt(frac * batch.qty + 0.0001f));
                while (line.emitted < due)
                {
                    line.emitted++;
                    var item = line.prefab ? Instantiate(line.prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                    item.name = GameData.P(line.product).Name + " na taśmie";
                    foreach (var c in item.GetComponentsInChildren<Collider>()) Destroy(c);
                    item.transform.SetParent(_fx, true); item.transform.position = line.from;
                    if (!line.prefab) item.transform.localScale = new Vector3(0.18f, 0.28f, 0.18f);
                    line.moving.Add((item.transform, 0f));
                }
            }
            float len = Vector3.Distance(line.from, line.to);
            for (int i = line.moving.Count - 1; i >= 0; i--)
            {
                var (t, s) = line.moving[i]; s += Time.deltaTime * 0.6f;               // taśma 0,6 m/s
                if (s >= len)
                {
                    line.moving.RemoveAt(i);
                    if (line.batch == null) { Destroy(t.gameObject); continue; }
                    int n = line.inCrate.Count;
                    t.position = line.crate.position + new Vector3((n % 3 - 1) * 0.16f, 0.12f + (n / 6) * 0.2f, ((n / 3) % 2 - 0.5f) * 0.18f);
                    line.inCrate.Add(t.gameObject);
                    continue;
                }
                t.position = Vector3.Lerp(line.from, line.to, s / len) + Vector3.up * (0.01f * Mathf.Sin(s * 20f));
                line.moving[i] = (t, s);
            }
        }

        static Vector3 Along(Vector3[] path, float s)
        {
            for (int i = 1; i < path.Length; i++)
            {
                float d = Vector3.Distance(path[i - 1], path[i]);
                if (s <= d) return Vector3.Lerp(path[i - 1], path[i], d > 0 ? s / d : 0);
                s -= d;
            }
            return path[path.Length - 1];
        }

        void Puff(Vector3 at, Vector3 vel, float life, float size)
        {
            Transform t;
            if (_puffPool.Count > 0) t = _puffPool.Pop();
            else
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); g.name = "Para"; Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(_fx, true); var r = g.GetComponent<Renderer>(); r.sharedMaterial = _steam; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t = g.transform;
            }
            t.position = at; t.localScale = Vector3.one * size * 0.4f; t.gameObject.SetActive(true);
            _puffs.Add((t, 0f, life, vel, size));
            if (_puffs.Count > 120) { var (old, _, _, _, _) = _puffs[0]; old.gameObject.SetActive(false); _puffPool.Push(old); _puffs.RemoveAt(0); }
        }
    }
}
