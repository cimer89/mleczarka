using System.Collections.Generic;
using System.Linq;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEngine;

namespace Mleczarka.ProductionFloor
{
    /// <summary>
    /// Podpowiedzi w skrzydle maszyn: podświetla maszyny, przy których trzeba teraz coś zrobić
    /// (świecące pole obsługi, strzałka nad maszyną, światło), i pokazuje u góry ekranu „Co dalej”.
    /// Przy panelu przyjęcia podświetla całą trasę wybranego produktu z numerami etapów.
    /// Tylko odczyt stanu symulacji.
    /// </summary>
    public sealed class PlantGuide : MonoBehaviour
    {
        public PlantFloor floor;
        GameController G => GameController.Instance;
        PlayerState P => G.Sim.S.players[GameController.Me];

        public enum Kind { Start, Hands, Broken, Wash, Pick, Wait, Plan }
        public struct Task { public PlantMachineView view; public Kind kind; public string text; public int order; }

        class Beacon { public GameObject root; public Transform arrow, ring; public TextMesh label; public Light light; public Renderer[] rends; }
        readonly Dictionary<PlantMachineView, Beacon> _beacons = new Dictionary<PlantMachineView, Beacon>();
        readonly List<Task> _tasks = new List<Task>();
        Material _mat; MaterialPropertyBlock _mpb;
        GUIStyle _style;
        public static bool Enabled = true;

        static readonly Color CStart = new Color(0.25f, 0.85f, 1f), CHands = new Color(1f, 0.62f, 0.1f), CBroken = new Color(1f, 0.2f, 0.15f),
                              CWash = new Color(0.35f, 0.5f, 1f), CPick = new Color(0.3f, 1f, 0.45f), CPlan = new Color(0.85f, 0.85f, 0.95f);

        void Start()
        {
            _mpb = new MaterialPropertyBlock();
            _mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _mat.EnableKeyword("_EMISSION"); _mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            _mat.SetFloat("_Smoothness", 0.6f);
        }

        // ---------------- co trzeba zrobić ----------------

        public IReadOnlyList<Task> Tasks => _tasks;

        void Collect()
        {
            _tasks.Clear();
            var pl = P.plant; if (pl == null) return;
            PlantMachineView V(string id) => floor.views.FirstOrDefault(v => v && v.kind == PlantMachineView.Kind.Machine && v.machineId == id);
            PlantMachineView K(PlantMachineView.Kind k) => floor.views.FirstOrDefault(v => v && v.kind == k);
            string Prod(JobState j) => $"{GameData.P(j.product).Name} ×{j.qty}";

            foreach (var m in pl.machines.Where(m => m.broken))
                _tasks.Add(new Task { view = V(m.id), kind = Kind.Broken, text = $"Awaria: {PlantData.M(m.id).Name} — przytrzymaj Q, żeby naprawić", order = 0 });

            foreach (var j in pl.jobs.Where(j => j.machine != null && !j.Waiting).OrderByDescending(j => j.stage))
            {
                var m = PlantSim.Machine(P, j.machine); if (m == null || m.broken) continue;
                var d = PlantSim.StepOf(j); if (d == null || d.Kind == StepKind.Auto) continue;
                if (!PlantSim.WorkersOff(P) && j.worker >= 0) continue;
                _tasks.Add(new Task { view = V(j.machine), kind = Kind.Hands, text = $"{PlantData.M(j.machine).Name}: {d.Label} ({(d.Kind == StepKind.Hold ? "przytrzymaj Q" : "Q")}) — {Prod(j)}", order = 1 });
            }

            // partie, które stoją, bo maszyna jest brudna
            foreach (var j in pl.jobs.Where(j => j.machine == null && j.Waiting && j.wait.Contains("umyj")))
            {
                string type = PlantSim.TypeOf(j);
                var m = pl.machines.FirstOrDefault(x => PlantData.M(x.id).Type == type && x.dirt >= PlantData.DirtBlock && x.cip <= 0);
                if (m != null && !_tasks.Any(t => t.view == V(m.id)))
                    _tasks.Add(new Task { view = V(m.id), kind = Kind.Wash, text = $"{PlantData.M(m.id).Name} jest brudna — C przy maszynie albo stacja CIP (Q)", order = 2 });
            }

            if (P.carried.Sum() > 0)
                _tasks.Add(new Task { view = K(PlantMachineView.Kind.ColdRoom), kind = Kind.Pick, text = "Zanieś skrzynkę do chłodni (Q przy drzwiach)", order = 3 });
            else if (P.packingRack.Sum() > 0)
                _tasks.Add(new Task { view = V("pakowarka"), kind = Kind.Pick, text = $"Gotowe: {P.packingRack.Sum()} szt. — odbierz skrzynkę przy pakowarce (Q)", order = 3 });

            if (P.tankerMilk > 0 && !pl.sampleTested)
                _tasks.Add(new Task { view = K(PlantMachineView.Kind.Lab), kind = Kind.Start, text = "W cysternie jest mleko — zbadaj próbkę w laboratorium (przytrzymaj Q), potem rozładuj (F przy cysternie)", order = 4 });

            bool anyJob = pl.jobs.Count > 0;
            if (P.tankMilk > 0 && pl.jobs.Count < PlantSim.MaxJobs && !_tasks.Any(t => t.kind == Kind.Hands || t.kind == Kind.Broken))
                _tasks.Add(new Task { view = K(PlantMachineView.Kind.Intake), kind = Kind.Start, text = anyJob ? "Możesz zacząć kolejną partię przy panelu przyjęcia mleka" : $"W silosie {P.tankMilk} l mleka — wybierz produkt przy panelu przyjęcia mleka i zacznij partię (Q)", order = 5 });

            if (!_tasks.Any(t => t.order <= 3))
            {
                var run = pl.jobs.FirstOrDefault(j => j.machine != null && PlantSim.StepOf(j) is StepDef sd && sd.Kind == StepKind.Auto);
                if (run != null)
                {
                    var d = PlantSim.StepOf(run); int left = Mathf.CeilToInt((float)(PlantSim.Need(G.Sim, run, d) - run.progress));
                    _tasks.Add(new Task { view = V(run.machine), kind = Kind.Wait, text = $"{PlantData.M(run.machine).Name} pracuje sama: {d.Label}, jeszcze ok. {left} s ({Prod(run)})", order = 6 });
                }
                else if (P.tankMilk <= 0 && !anyJob && P.tankerMilk <= 0)
                    _tasks.Add(new Task { view = null, kind = Kind.Wait, text = "Silos jest pusty — przywieź mleko cysterną z gospodarstw (M — mapa)", order = 7 });
            }

            // plan trasy przy panelu przyjęcia: numerowane etapy wybranego produktu
            var near = floor.NearView;
            if (near && near.kind == PlantMachineView.Kind.Intake)
            {
                var route = PlantData.Route(floor.SelectedProduct);
                for (int i = 0; i < route.Length; i++)
                {
                    var ms = PlantData.Machines.Where(x => x.Type == route[i]).ToList();
                    foreach (var md in ms) _tasks.Add(new Task { view = V(md.Id), kind = Kind.Plan, text = (i + 1).ToString(), order = 10 + i });
                }
            }
            _tasks.RemoveAll(t => t.view == null && t.order < 7);
        }

        // ---------------- znaczniki ----------------

        void Update()
        {
            if (!G || G.Sim == null || floor == null || P.plant == null) return;
            if (!CareerSim.Allowed(P, "skrzydlo")) { foreach (var b in _beacons.Values) Destroy(b.root); _beacons.Clear(); _tasks.Clear(); return; }
            Collect();
            bool inside = G.player && floor.wingRoot && Vector3.Distance(G.player.transform.position, floor.wingRoot.position) < 45;
            var want = new Dictionary<PlantMachineView, Task>();
            if (Enabled && inside)
                foreach (var t in _tasks.Where(t => t.view && t.kind != Kind.Wait).OrderBy(t => t.order))
                    if (!want.ContainsKey(t.view) || (want[t.view].kind == Kind.Plan && t.kind != Kind.Plan)) want[t.view] = t;
            // podświetlenie pracującej maszyny tylko gdy nic innego nie trzeba robić
            var wait = _tasks.FirstOrDefault(t => t.kind == Kind.Wait && t.view);
            if (Enabled && inside && wait.view && !want.ContainsKey(wait.view) && want.All(w => w.Value.kind == Kind.Plan || w.Value.kind == Kind.Start)) want[wait.view] = wait;

            foreach (var v in _beacons.Keys.ToList()) if (!want.ContainsKey(v)) { Destroy(_beacons[v].root); _beacons.Remove(v); }
            var cam = Camera.main;
            foreach (var kv in want)
            {
                if (!_beacons.TryGetValue(kv.Key, out var b)) { b = Make(kv.Key); _beacons[kv.Key] = b; }
                Pose(b, kv.Key, kv.Value, cam);
            }
        }

        Beacon Make(PlantMachineView v)
        {
            var b = new Beacon { root = new GameObject("Podświetlenie: " + v.name) };
            b.root.transform.SetParent(transform, false);
            b.ring = Prim(PrimitiveType.Cylinder, b.root.transform, new Vector3(1.35f, 0.012f, 1.35f));
            b.arrow = new GameObject("Strzałka").transform; b.arrow.SetParent(b.root.transform, false);
            var head = Prim(PrimitiveType.Cube, b.arrow, new Vector3(0.32f, 0.32f, 0.32f)); head.localRotation = Quaternion.Euler(45, 0, 45);
            var stem = Prim(PrimitiveType.Cube, b.arrow, new Vector3(0.1f, 0.4f, 0.1f)); stem.localPosition = new Vector3(0, 0.38f, 0);
            var lg = new GameObject("Opis"); lg.transform.SetParent(b.arrow, false); lg.transform.localPosition = new Vector3(0, 0.85f, 0);
            b.label = lg.AddComponent<TextMesh>(); b.label.anchor = TextAnchor.MiddleCenter; b.label.fontSize = 72; b.label.characterSize = 0.05f; b.label.fontStyle = FontStyle.Bold;
            b.light = new GameObject("Światło").AddComponent<Light>(); b.light.transform.SetParent(b.root.transform, false);
            b.light.type = LightType.Point; b.light.range = 3.2f; b.light.shadows = LightShadows.None;
            b.rends = new[] { b.ring.GetComponent<Renderer>(), head.GetComponent<Renderer>(), stem.GetComponent<Renderer>() };
            return b;
        }

        Transform Prim(PrimitiveType t, Transform parent, Vector3 scale)
        {
            var g = GameObject.CreatePrimitive(t); Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false); g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = _mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        void Pose(Beacon b, PlantMachineView v, Task t, Camera cam)
        {
            Color c = t.kind == Kind.Broken ? CBroken : t.kind == Kind.Hands ? CHands : t.kind == Kind.Wash ? CWash : t.kind == Kind.Pick ? CPick : t.kind == Kind.Plan ? CPlan : t.kind == Kind.Wait ? new Color(0.6f, 0.9f, 0.7f) : CStart;
            bool strong = t.kind != Kind.Plan && t.kind != Kind.Wait;
            float pulse = strong ? 0.6f + 0.4f * Mathf.Sin(Time.time * (t.kind == Kind.Broken ? 9 : 5)) : 0.5f;
            var st = v.station ? v.station.position : v.transform.position;
            b.ring.position = st + Vector3.up * 0.03f;
            float top = 0; foreach (var r in v.GetComponentsInChildren<Renderer>()) if (r.enabled && !r.transform.IsChildOf(transform)) top = Mathf.Max(top, r.bounds.max.y);
            if (top <= 0) top = v.transform.position.y + 2;
            b.arrow.position = new Vector3(v.transform.position.x, top + 0.7f + (strong ? 0.18f * Mathf.Sin(Time.time * 3) : 0), v.transform.position.z);
            b.arrow.localScale = Vector3.one * (strong ? 1f : 0.7f);
            if (cam) { var f = b.arrow.position - cam.transform.position; f.y = 0; if (f.sqrMagnitude > 0.01f) b.arrow.rotation = Quaternion.LookRotation(f); }
            b.label.text = t.kind == Kind.Plan ? t.text : t.kind == Kind.Broken ? "NAPRAW" : t.kind == Kind.Hands ? "TUTAJ" : t.kind == Kind.Wash ? "UMYJ" : t.kind == Kind.Pick ? (P.carried.Sum() > 0 ? "ODŁÓŻ" : "ODBIERZ") : t.kind == Kind.Wait ? "PRACUJE" : "START";
            b.label.color = c;
            b.light.transform.position = st + Vector3.up * 1.2f; b.light.color = c; b.light.intensity = strong ? 1.6f * pulse : 0; b.light.enabled = strong;
            foreach (var r in b.rends)
            {
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", c); _mpb.SetColor("_EmissionColor", c * (strong ? 2.2f * pulse : 0.8f));
                r.SetPropertyBlock(_mpb);
            }
        }

        // ---------------- „Co dalej” ----------------

        void OnGUI()
        {
            if (!Enabled || !G || G.Sim == null || floor == null || !G.player || G.player.InputBlocked) return;
            if (floor.wingRoot && Vector3.Distance(G.player.transform.position, floor.wingRoot.position) > 45) return;
            var top = _tasks.Where(t => t.kind != Kind.Plan).OrderBy(t => t.order).FirstOrDefault();
            if (string.IsNullOrEmpty(top.text)) return;
            float scale = Mathf.Clamp(Screen.width / 1500f, 0.65f, 1.5f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true, richText = true, alignment = TextAnchor.MiddleLeft };
            float w = Mathf.Min(760, Screen.width / scale - 40), x = (Screen.width / scale - w) / 2, y = 92;
            int more = _tasks.Count(t => t.kind == Kind.Hands || t.kind == Kind.Broken || t.kind == Kind.Wash) - (top.kind == Kind.Hands || top.kind == Kind.Broken || top.kind == Kind.Wash ? 1 : 0);
            string dist = top.view && top.view.station ? $"  <color=#9fb>{Vector3.Distance(G.player.transform.position, top.view.station.position):0} m</color>" : "";
            string txt = $"<b>Co dalej:</b> {top.text}{dist}" + (more > 0 ? $"\n<color=#cdd>+{more} {(more == 1 ? "inna maszyna czeka" : "inne maszyny czekają")} (świecące pola)</color>" : "");
            float h = _style.CalcHeight(new GUIContent(txt), w - 28) + 14;
            Color c = top.kind == Kind.Broken ? CBroken : top.kind == Kind.Hands ? CHands : top.kind == Kind.Wash ? CWash : top.kind == Kind.Pick ? CPick : CStart;
            var prev = GUI.color;
            GUI.color = new Color(0.03f, 0.1f, 0.13f, 0.92f); GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = c; GUI.DrawTexture(new Rect(x, y, 6, h), Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.Label(new Rect(x + 18, y + 7, w - 28, h - 14), txt, _style);
        }
    }
}
