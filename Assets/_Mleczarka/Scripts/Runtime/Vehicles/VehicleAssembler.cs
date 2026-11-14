using System.Collections.Generic;
using System.Linq;
using Mleczarka.Vehicles.Model;
using UnityEngine;

namespace Mleczarka.Vehicles
{
    /// <summary>
    /// Składa pojazd z podwozia i części według VehicleLoadout, nakłada lakier
    /// i przekazuje wyliczone parametry do fizyki (WheeledVehicle).
    /// Lakier: materiały o nazwach zaczynających się od „Lakier_Glowny”, „Lakier_Dodatkowy” i „Lakier_Akcent”
    /// dostają kolor przez MaterialPropertyBlock (bez kopiowania materiałów).
    /// </summary>
    [ExecuteAlways]
    public class VehicleAssembler : MonoBehaviour
    {
        public VehicleDefinition definition;
        public VehiclePartDefinition[] catalog;
        public VehicleLoadout loadout = new VehicleLoadout();

        public VehicleStats Stats { get; private set; }

        readonly Dictionary<PartSlot, GameObject> _spawned = new Dictionary<PartSlot, GameObject>();
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        // MaterialPropertyBlock nie zapisuje się w scenie, więc lakier nakładamy przy każdym włączeniu obiektu.
        void OnEnable()
        {
            ApplyPaint();
            RecomputeStats(Application.isPlaying);
        }

        /// <summary>Parametry z aktualnej konfiguracji (bez przebudowy modeli); opcjonalnie przekazuje je do fizyki.</summary>
        public void RecomputeStats(bool applyToPhysics)
        {
            var mods = loadout.parts.Select(c => FindPart(c.partId) ?? definition?.defaultParts?.FirstOrDefault(p => p && p.id == c.partId))
                                    .Where(p => p).Select(p => p.modifiers);
            Stats = VehicleStatCalculator.Compute(definition ? definition.baseStats : default, mods);
            if (applyToPhysics) ApplyStatsToPhysics();
        }

        void ApplyStatsToPhysics()
        {
            var veh = GetComponent<WheeledVehicle>();
            if (veh)
            {
                veh.motorTorque = Stats.motorTorque;
                veh.maxSpeedKmh = Stats.maxSpeedKmh;
                foreach (var w in veh.wheels)
                {
                    var f = w.collider.forwardFriction; f.stiffness = 1.4f * Stats.grip; w.collider.forwardFriction = f;
                    var s = w.collider.sidewaysFriction; s.stiffness = 1.6f * Stats.grip; w.collider.sidewaysFriction = s;
                }
            }
            var rb = GetComponent<Rigidbody>();
            if (rb) rb.mass = Stats.massKg;
        }

        public VehiclePartDefinition FindPart(string id) => catalog?.FirstOrDefault(p => p && p.id == id);

        /// <summary>Wypełnia puste sloty częściami domyślnymi podwozia.</summary>
        public void ApplyDefaults()
        {
            if (!definition) return;
            loadout.type = definition.type;
            foreach (var p in definition.defaultParts)
                if (p && string.IsNullOrEmpty(loadout.Get(p.slot))) loadout.Set(p.slot, p.id);
        }

        public void Rebuild()
        {
            // stare części (także zapisane w scenie) odpinamy przed usunięciem, żeby ich punkty montażu nie były już znajdowane
            foreach (var old in GetComponentsInChildren<VehiclePartInstance>(true))
                if (old) { old.transform.SetParent(null, false); old.gameObject.SetActive(false); DestroySafe(old.gameObject); }
            _spawned.Clear();

            var mods = new List<VehicleStats>();
            // kolejność slotów: najpierw kabina i zabudowa, bo na kabinie są punkty dachu i spoilera
            foreach (var choice in loadout.parts.OrderBy(c => (int)c.slot))
            {
                var part = FindPart(choice.partId) ?? definition?.defaultParts?.FirstOrDefault(p => p && p.id == choice.partId);
                if (!part || !part.prefab || !part.fitsTypes.Contains(loadout.type)) continue;
                if (part.slot == PartSlot.Kola)
                {
                    ApplyWheels(part.prefab);
                    mods.Add(part.modifiers);
                    continue;
                }
                var anchor = FindAnchor("Slot_" + part.slot);
                if (!anchor) { Debug.LogWarning($"[Mleczarka] Brak punktu Slot_{part.slot} w {name}"); continue; }
                var go = Instantiate(part.prefab, anchor, false);
                go.name = part.id;
                var tag = go.AddComponent<VehiclePartInstance>(); tag.slot = part.slot; tag.partId = part.id;
                _spawned[part.slot] = go;
                mods.Add(part.modifiers);
            }

            ApplyPaint();
            Stats = VehicleStatCalculator.Compute(definition ? definition.baseStats : default, mods);
            ApplyStatsToPhysics();
        }

        public void ApplyPaint()
        {
            var mpb = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    var n = mats[i].name;
                    Rgb? c = n.StartsWith("Lakier_Glowny") ? loadout.paintPrimary
                           : n.StartsWith("Lakier_Dodatkowy") ? loadout.paintSecondary
                           : n.StartsWith("Lakier_Akcent") ? loadout.paintAccent : (Rgb?)null;
                    if (c == null) continue;
                    r.GetPropertyBlock(mpb, i);
                    mpb.SetColor(BaseColor, new Color(c.Value.r, c.Value.g, c.Value.b));
                    r.SetPropertyBlock(mpb, i);
                }
            }
        }

        /// <summary>Punkt montażu: najpierw szukany w już zamontowanych częściach (np. dach kabiny), potem na podwoziu.</summary>
        Transform FindAnchor(string n)
        {
            foreach (var go in _spawned.Values.Reverse())
            {
                var t = go ? go.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n) : null;
                if (t) return t;
            }
            return GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n && !t.GetComponentInParent<VehiclePartInstance>(true));
        }

        /// <summary>Komplet kół: podmienia siatkę i materiały kół wizualnych podwozia (fizyka kół zostaje ta sama).</summary>
        void ApplyWheels(GameObject prefab)
        {
            var srcFilter = prefab.GetComponentInChildren<MeshFilter>(true);
            var srcRenderer = prefab.GetComponentInChildren<MeshRenderer>(true);
            if (!srcFilter || !srcRenderer) return;
            var veh = GetComponent<WheeledVehicle>();
            float meshDiameter = Mathf.Max(0.01f, srcFilter.sharedMesh.bounds.size.y);
            foreach (var t in GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("wheel-")))
            {
                var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
                if (!mf || !mr) continue;
                mf.sharedMesh = srcFilter.sharedMesh;
                mr.sharedMaterials = srcRenderer.sharedMaterials;
                // komplet kół pasuje do każdego podwozia: skala do promienia koła fizycznego
                var w = veh ? veh.wheels.FirstOrDefault(x => x.visual == t) : null;
                if (w != null && w.collider)
                {
                    float parentScale = t.parent ? t.parent.lossyScale.y : 1f;
                    t.localScale = Vector3.one * (w.collider.radius * 2f / meshDiameter / parentScale);
                }
            }
        }

        static void DestroySafe(GameObject go)
        {
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
    }
}
