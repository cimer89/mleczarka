using System.Collections.Generic;
using System.Linq;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEngine;

namespace Mleczarka.ProductionFloor
{
    /// <summary>
    /// Widok jednej maszyny nowego skrzydła (model z Blendera). Tylko odczytuje stan PlantSim
    /// i porusza nazwanymi częściami modelu: bęben, mieszadło, beczka, tłoki, wskazówki, rolki, harfa, ramię,
    /// drzwi, lampka stanu, krążki sera, poziom śmietanki, probówka. Logiki gry tu nie ma.
    /// </summary>
    public sealed class PlantMachineView : MonoBehaviour
    {
        public enum Kind { Machine, Intake, Lab, Cip, CreamTank, ColdRoom }
        public Kind kind;
        public string machineId;          // dla Kind.Machine: id z PlantData
        public string title;
        public Transform station, inPort, outPort, steamPoint, bubblePoint, interior;

        GameController G => GameController.Instance;
        PlayerState P => G.Sim.S.players[GameController.Me];

        class Spin { public Transform t; public Vector3 axisLocal; public float speed; public Vector3 pivotLocal; }
        class Slide { public Transform t; public Vector3 basePos; public float phase; }
        readonly List<Spin> _spins = new List<Spin>();
        readonly List<Slide> _pistons = new List<Slide>(), _rollers = new List<Slide>();
        readonly List<Transform> _wheels = new List<Transform>();
        Transform _needle, _arm, _harp, _door, _level, _tube, _body, _head, _press, _plate;
        Quaternion _needleRot, _armRot, _doorRot, _plateRot;
        Vector3 _harpPos, _levelScale, _tubePos, _bodyPos, _headPos, _pressPos;
        Renderer _lamp; MaterialPropertyBlock _mpb;
        float _spinSpeed, _needleAngle, _plateTarget, _plateAngle, _doorOpen;
        public float LastHandsTime { get; set; } = -10;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor"), BaseId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.EndsWith("_Korpus")) { _body = t; _bodyPos = t.localPosition; }
                else if (n.EndsWith("_Beben_Obrot") || n.EndsWith("_Mieszadlo_Obrot")) _spins.Add(new Spin { t = t, axisLocal = Vector3.up });
                else if (n.EndsWith("_Talerz_Obrot")) { _plate = t; _plateRot = t.localRotation; }
                else if (n.EndsWith("_Beczka_ObrotX")) _spins.Add(new Spin { t = t, axisLocal = Vector3.right });
                else if (n.EndsWith("_Glowica_Tlok")) { _head = t; _headPos = t.localPosition; }
                else if (n.Contains("_Tlok_")) _pistons.Add(new Slide { t = t, basePos = t.localPosition, phase = _pistons.Count * 2.094f });
                else if (n.EndsWith("_Tlok")) { _press = t; _pressPos = t.localPosition; }
                else if (n.EndsWith("_Wskazowka")) { _needle = t; _needleRot = t.localRotation; }
                else if (n.Contains("_Rolka_")) _rollers.Add(new Slide { t = t, basePos = t.localPosition });
                else if (n.EndsWith("_Harfa")) { _harp = t; _harpPos = t.localPosition; }
                else if (n.EndsWith("_Ramie")) { _arm = t; _armRot = t.localRotation; }
                else if (n.EndsWith("_Drzwi")) { _door = t; _doorRot = t.localRotation; }
                else if (n.EndsWith("_Lampka")) _lamp = t.GetComponent<Renderer>();
                else if (n.Contains("_Krazek_")) _wheels.Add(t);
                else if (n.EndsWith("_Poziom")) { _level = t; _levelScale = t.localScale; }
                else if (n.EndsWith("_Probowka")) { _tube = t; _tubePos = t.localPosition; }
            }
            _wheels.Sort((a, b) => Num(a.name).CompareTo(Num(b.name)));
            foreach (var s in _spins) { s.pivotLocal = s.t.localPosition; }
        }

        static int Num(string n) { int i = n.LastIndexOf('_'); return int.TryParse(n.Substring(i + 1), out var v) ? v : 0; }

        // ---------- stan ----------

        public MachineState Machine => kind == Kind.Machine && G && P.plant != null ? PlantSim.Machine(P, machineId) : null;
        public JobState Job => kind == Kind.Machine && G && P.plant != null ? PlantSim.JobAt(P, machineId) : null;

        public bool Running
        {
            get
            {
                var m = Machine; var j = Job; if (m == null || j == null || m.broken || j.Waiting) return false;
                var d = PlantSim.StepOf(j);
                if (d == null) return false;
                if (d.Kind == StepKind.Auto) return true;
                return d.Kind == StepKind.Hold && (Time.time - LastHandsTime < 0.8f || j.worker >= 0);
            }
        }
        public StepDef Step { get { var j = Job; return j != null && !j.Waiting ? PlantSim.StepOf(j) : null; } }

        void Update()
        {
            if (!G || G.Sim == null) return;
            float dt = Time.deltaTime;
            var m = Machine; var j = Job; var d = Step;
            bool run = Running, cip = m != null && m.cip > 0, broken = m != null && m.broken;
            bool hands = m != null && PlantSim.NeedsHands(P, m);

            // obroty (bęben, mieszadło, beczka): łagodny rozruch i wybieg
            float target = 0;
            if (run) target = machineId == "wirowka" ? 1400 : machineId == "maslnica" ? 160 : 70;
            else if (cip) target = machineId == "wirowka" ? 300 : 50;
            _spinSpeed = Mathf.MoveTowards(_spinSpeed, target, dt * (target > _spinSpeed ? 500 : 250));
            foreach (var s in _spins) s.t.localRotation *= Quaternion.AngleAxis(_spinSpeed * dt, s.axisLocal);

            // drgania korpusu przy pracy
            if (_body)
                _body.localPosition = _bodyPos + (run && _spinSpeed > 600 ? Random.insideUnitSphere * 0.004f : Vector3.zero);

            // tłoki homogenizatora: przesunięte w fazie o 120°
            bool pump = run || (machineId == "homogenizator" && hands && Time.time - LastHandsTime < 0.8f);
            foreach (var p in _pistons)
                p.t.localPosition = Vector3.Lerp(p.t.localPosition, p.basePos + (pump ? Vector3.up * (0.05f * Mathf.Sin(Time.time * 14 + p.phase)) : Vector3.zero), dt * 20);

            // głowica napełniająca: krótki ruch w dół przy każdym opakowaniu
            if (_head) _head.localPosition = _headPos + (run && d != null && d.Kind == StepKind.Auto ? Vector3.down * (0.08f * Mathf.Max(0, Mathf.Sin(Time.time * 6))) : Vector3.zero);

            // prasa: tłok zjeżdża w dół i trzyma nacisk
            if (_press)
            {
                float down = run ? 0.28f + 0.01f * Mathf.Sin(Time.time * 3) : 0;
                _press.localPosition = Vector3.Lerp(_press.localPosition, _pressPos + Vector3.down * down, dt * 2.5f);
            }

            // talerz rozlewaczki kubków: obrót skokowy co 60°
            if (_plate)
            {
                if (run && Mathf.Abs(_plateAngle - _plateTarget) < 0.5f) _plateTarget += 60;
                _plateAngle = Mathf.MoveTowards(_plateAngle, _plateTarget, dt * 180);
                _plate.localRotation = _plateRot * Quaternion.AngleAxis(_plateAngle, Vector3.up);
            }

            // rolki taśmy w rozlewaczce kartonów
            foreach (var r in _rollers) if (run) r.t.localRotation *= Quaternion.AngleAxis(-400 * dt, Vector3.right);

            // harfa serowarska: krojenie skrzepu tam i z powrotem
            if (_harp)
            {
                bool cut = j != null && d != null && d.Label.StartsWith("Krojenie") && (Time.time - LastHandsTime < 0.8f || j.worker >= 0);
                Vector3 goal = _harpPos + (cut ? Vector3.right * (0.45f * Mathf.Sin(Time.time * 2.2f)) : Vector3.zero) + (cut ? Vector3.down * 0.25f : Vector3.zero);
                _harp.localPosition = Vector3.Lerp(_harp.localPosition, goal, dt * 6);
            }

            // ramię pakowarki: przenosi opakowania do kartonu
            if (_arm) _arm.localRotation = _armRot * Quaternion.AngleAxis(run ? 35 * Mathf.Sin(Time.time * 4) : 0, Vector3.up);

            // wskazówka: temperatura (dla homogenizatora ciśnienie)
            if (_needle)
            {
                float value01;
                if (kind == Kind.Intake) value01 = P.tankCap > 0 ? (float)P.tankMilk / P.tankCap : 0;
                else if (machineId == "homogenizator") value01 = pump ? 0.8f + 0.03f * Mathf.Sin(Time.time * 14) : 0.02f;
                else value01 = m != null ? Mathf.Clamp01((float)m.temp / 150f) : 0;
                _needleAngle = Mathf.Lerp(_needleAngle, -120 + 240 * value01, dt * 4);
                _needle.localRotation = _needleRot * Quaternion.AngleAxis(_needleAngle, Vector3.forward);
            }

            // dojrzewalnia: krążki sera na półkach = sery w środku
            if (_wheels.Count > 0)
            {
                int n = 0; float ripe = 0;
                if (P.plant != null) foreach (var jj in PlantSim.JobsAt(P, machineId)) { n += jj.qty; var dd = PlantSim.StepOf(jj); if (dd != null && dd.Kind == StepKind.Auto) ripe = Mathf.Max(ripe, (float)(jj.progress / Mathf.Max(0.01f, (float)PlantSim.Need(G.Sim, jj, dd)))); }
                for (int i = 0; i < _wheels.Count; i++) _wheels[i].gameObject.SetActive(i < n);
            }

            // zbiornik śmietanki: poziom
            if (_level && P.plant != null)
            {
                float f = Mathf.Clamp01((float)(P.plant.cream / Mathf.Max(1, (float)P.plant.creamCap)));
                _level.localScale = Vector3.Lerp(_level.localScale, new Vector3(_levelScale.x, _levelScale.y * Mathf.Max(0.02f, f), _levelScale.z), dt * 3);
            }

            // probówka w laboratorium: podnosi się i świeci podczas badania
            if (_tube && P.plant != null)
            {
                bool testing = P.plant.sampleProgress > 0 || Time.time - LastHandsTime < 0.8f;
                _tube.localPosition = Vector3.Lerp(_tube.localPosition, _tubePos + (testing ? Vector3.up * 0.06f * (1 + Mathf.Sin(Time.time * 8)) : Vector3.zero), dt * 8);
            }

            // drzwi chłodni otwierają się, gdy ktoś podchodzi
            if (_door)
            {
                bool near = G.player && Vector3.Distance(G.player.transform.position, station ? station.position : transform.position) < 3.5f;
                _doorOpen = Mathf.MoveTowards(_doorOpen, near ? 1 : 0, dt * 1.5f);
                _door.localRotation = _doorRot * Quaternion.AngleAxis(-100 * Mathf.SmoothStep(0, 1, _doorOpen), Vector3.up);
            }

            // lampka stanu
            if (_lamp)
            {
                Color c;
                if (kind == Kind.Lab) c = P.plant != null && P.plant.sampleTested ? (P.plant.sampleResult >= 0.6 ? Color.green : Color.red) : P.tankerMilk > 0 ? new Color(1f, 0.6f, 0.1f) : Gray;
                else if (kind == Kind.Cip) c = P.plant != null && P.plant.machines.Any(x => x.cip > 0) ? new Color(0.2f, 0.5f, 1f) : Gray;
                else if (kind == Kind.Intake) c = P.tankMilk > 0 ? Color.green : Gray;
                else if (kind == Kind.ColdRoom) c = new Color(0.4f, 0.8f, 1f);
                else if (broken) c = Mathf.Repeat(Time.time, 0.6f) < 0.3f ? Color.red : new Color(0.3f, 0, 0);
                else if (cip) c = new Color(0.2f, 0.5f, 1f);
                else if (hands) c = Mathf.Repeat(Time.time, 1f) < 0.6f ? new Color(1f, 0.6f, 0.1f) : new Color(0.4f, 0.24f, 0.04f);
                else if (j != null && j.Waiting) c = new Color(1f, 0.85f, 0.2f);
                else if (run) c = Color.green;
                else if (m != null && m.dirt >= PlantData.DirtSlow) c = new Color(0.6f, 0.4f, 0.2f);
                else c = Gray;
                _lamp.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseId, c); _mpb.SetColor(EmissionId, c == Gray ? Color.black : c * 2.2f);
                _lamp.SetPropertyBlock(_mpb);
            }
        }

        static readonly Color Gray = new Color(0.32f, 0.33f, 0.35f);
    }
}
