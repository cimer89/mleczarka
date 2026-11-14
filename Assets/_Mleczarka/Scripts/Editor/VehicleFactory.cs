using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mleczarka.Vehicles;
using Mleczarka.Vehicles.Model;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.EditorTools
{
    /// <summary>
    /// Dane i składanie pojazdów mleczarni (cysterna, dostawczak) z modułów zrobionych w Blenderze.
    /// Katalog części i podwozia zapisuje jako ScriptableObject w Data/Vehicles.
    /// </summary>
    public static class VehicleFactory
    {
        const string ArtC = "Assets/_Mleczarka/Art/Vehicles/Cysterna/";
        const string ArtD = "Assets/_Mleczarka/Art/Vehicles/Dostawczak/";
        const string DataDir = "Assets/_Mleczarka/Data/Vehicles/";
        static readonly VehicleType C = VehicleType.Cysterna, D = VehicleType.Dostawczak;

        static VehiclePartDefinition Part(string art, string id, string name, PartSlot slot, PartTier tier, VehicleStats mods, int price, params VehicleType[] fits)
        {
            string path = DataDir + "Czesci/" + id + ".asset";
            var p = AssetDatabase.LoadAssetAtPath<VehiclePartDefinition>(path);
            if (!p) { p = ScriptableObject.CreateInstance<VehiclePartDefinition>(); AssetDatabase.CreateAsset(p, path); }
            p.id = id; p.displayName = name; p.slot = slot; p.tier = tier; p.modifiers = mods; p.price = price;
            p.fitsTypes = fits.Length > 0 ? fits : new[] { C, D };
            p.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(art + id + ".fbx");
            if (!p.prefab) Debug.LogWarning("[Mleczarka] Brak modelu części " + id);
            EditorUtility.SetDirty(p);
            return p;
        }

        /// <summary>Pełny katalog części obu pojazdów.</summary>
        public static VehiclePartDefinition[] EnsureCatalog()
        {
            Directory.CreateDirectory(DataDir + "Czesci");
            AssetDatabase.Refresh();
            var list = new List<VehiclePartDefinition>
            {
                // ---- cysterna: zwykłe
                Part(ArtC, "Kabina_Zwykla", "Kabina zwykła", PartSlot.Kabina, PartTier.Zwykla, default, 0, C),
                Part(ArtC, "Zbiornik_Sredni", "Zbiornik 6000 l", PartSlot.Zabudowa, PartTier.Zwykla, new VehicleStats { massKg = 900, capacityL = 6000 }, 0, C),
                Part(ArtC, "Zbiornik_Maly", "Zbiornik 4000 l", PartSlot.Zabudowa, PartTier.Zwykla, new VehicleStats { massKg = 650, capacityL = 4000, maxSpeedKmh = 3 }, 0, C),
                Part(ArtC, "Zderzak_Zwykly_Przod", "Zderzak przedni zwykły", PartSlot.ZderzakPrzedni, PartTier.Zwykla, new VehicleStats { massKg = 60 }, 0, C),
                Part(ArtC, "Zderzak_Zwykly_Tyl", "Zderzak tylny zwykły", PartSlot.ZderzakTylny, PartTier.Zwykla, new VehicleStats { massKg = 40 }, 0, C),
                Part(ArtC, "Wydech_Pionowy", "Wydech pionowy", PartSlot.Wydech, PartTier.Zwykla, new VehicleStats { motorTorque = 50 }, 0, C),
                // ---- cysterna: tuning
                Part(ArtC, "Zbiornik_Duzy", "Zbiornik 9000 l", PartSlot.Zabudowa, PartTier.Tuning, new VehicleStats { massKg = 1300, capacityL = 9000, maxSpeedKmh = -4 }, 6500, C),
                Part(ArtC, "Wydech_Podwojny", "Wydech podwójny", PartSlot.Wydech, PartTier.Tuning, new VehicleStats { motorTorque = 250, maxSpeedKmh = 3 }, 1800, C),
                // ---- cysterna: wyścigowe
                Part(ArtC, "Kabina_Sportowa", "Kabina sportowa", PartSlot.Kabina, PartTier.Wyscigowa, new VehicleStats { massKg = -250, maxSpeedKmh = 10 }, 12000, C),
                Part(ArtC, "Zbiornik_Wyscigowy", "Zbiornik wyścigowy 5000 l", PartSlot.Zabudowa, PartTier.Wyscigowa, new VehicleStats { massKg = 550, capacityL = 5000, maxSpeedKmh = 8 }, 15000, C),
                Part(ArtC, "Zderzak_Sportowy_Przod", "Zderzak sportowy przedni", PartSlot.ZderzakPrzedni, PartTier.Wyscigowa, new VehicleStats { massKg = 40, maxSpeedKmh = 2 }, 2800, C),
                Part(ArtC, "Zderzak_Sportowy_Tyl", "Dyfuzor tylny", PartSlot.ZderzakTylny, PartTier.Wyscigowa, new VehicleStats { massKg = 30, grip = 0.05f }, 2200, C),
                // ---- wspólne (oba pojazdy)
                Part(ArtC, "Kola_Standard", "Koła standardowe", PartSlot.Kola, PartTier.Zwykla, default, 0),
                Part(ArtC, "Kola_Terenowe", "Koła terenowe", PartSlot.Kola, PartTier.Tuning, new VehicleStats { grip = 0.25f, maxSpeedKmh = -3, massKg = 120 }, 2400),
                Part(ArtC, "Kola_Sportowe", "Koła sportowe", PartSlot.Kola, PartTier.Wyscigowa, new VehicleStats { grip = 0.35f, maxSpeedKmh = 5, massKg = -60 }, 5200),
                Part(ArtC, "Dach_Belka_Swietlna", "Belka świetlna", PartSlot.Dach, PartTier.Tuning, new VehicleStats { massKg = 25 }, 900),
                Part(ArtC, "Spoiler_Wyscigowy", "Spoiler wyścigowy", PartSlot.Spoiler, PartTier.Wyscigowa, new VehicleStats { grip = 0.15f, massKg = 30 }, 3500),
                // ---- dostawczak: zwykłe
                Part(ArtD, "Kabina_Dostawcza", "Kabina dostawcza", PartSlot.Kabina, PartTier.Zwykla, default, 0, D),
                Part(ArtD, "Skrzynia_Chlodnia", "Chłodnia 2500 l", PartSlot.Zabudowa, PartTier.Zwykla, new VehicleStats { massKg = 500, capacityL = 2500 }, 0, D),
                Part(ArtD, "Skrzynia_Otwarta_Banki", "Skrzynia z bańkami 1500 l", PartSlot.Zabudowa, PartTier.Zwykla, new VehicleStats { massKg = 250, capacityL = 1500, maxSpeedKmh = 3 }, 0, D),
                Part(ArtD, "Zderzak_Dostawczy_Przod", "Zderzak przedni chromowany", PartSlot.ZderzakPrzedni, PartTier.Zwykla, new VehicleStats { massKg = 35 }, 0, D),
                Part(ArtD, "Zderzak_Dostawczy_Tyl", "Zderzak tylny", PartSlot.ZderzakTylny, PartTier.Zwykla, new VehicleStats { massKg = 25 }, 0, D),
                Part(ArtD, "Wydech_Dostawczy", "Wydech", PartSlot.Wydech, PartTier.Zwykla, default, 0, D),
                // ---- dostawczak: wyścigowe
                Part(ArtD, "Kabina_Dostawcza_Sportowa", "Kabina dostawcza sportowa", PartSlot.Kabina, PartTier.Wyscigowa, new VehicleStats { massKg = -150, maxSpeedKmh = 12 }, 9000, D),
                Part(ArtD, "Skrzynia_Wyscigowa", "Skrzynia wyścigowa 1800 l", PartSlot.Zabudowa, PartTier.Wyscigowa, new VehicleStats { massKg = 300, capacityL = 1800, maxSpeedKmh = 10 }, 11000, D),
                Part(ArtD, "Zderzak_Dostawczy_Sportowy_Przod", "Zderzak sportowy", PartSlot.ZderzakPrzedni, PartTier.Wyscigowa, new VehicleStats { massKg = 25, maxSpeedKmh = 2 }, 2400, D),
                Part(ArtD, "Wydech_Dostawczy_Sportowy", "Wydech sportowy", PartSlot.Wydech, PartTier.Tuning, new VehicleStats { motorTorque = 180, maxSpeedKmh = 3 }, 1500, D),
            };
            AssetDatabase.SaveAssets();
            return list.ToArray();
        }

        static VehicleDefinition Chassis(string id, string name, VehicleType type, string fbx, VehicleStats stats, VehiclePartDefinition[] catalog, params string[] defaults)
        {
            string path = DataDir + "Podwozie_" + id + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(path);
            if (!def) { def = ScriptableObject.CreateInstance<VehicleDefinition>(); AssetDatabase.CreateAsset(def, path); }
            def.id = id; def.displayName = name; def.type = type;
            def.chassisPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            def.baseStats = stats;
            def.defaultParts = catalog.Where(p => defaults.Contains(p.id)).ToArray();
            EditorUtility.SetDirty(def);
            return def;
        }

        public static VehicleDefinition Cysterna(VehiclePartDefinition[] cat) => Chassis("Cysterna_Wiejska", "Cysterna wiejska", C, ArtC + "Podwozie_Cysterna.fbx",
            new VehicleStats { massKg = 3600, motorTorque = 2400, maxSpeedKmh = 62, grip = 1f }, cat,
            "Kabina_Zwykla", "Zbiornik_Sredni", "Kola_Standard", "Zderzak_Zwykly_Przod", "Zderzak_Zwykly_Tyl", "Wydech_Pionowy");

        public static VehicleDefinition Dostawczak(VehiclePartDefinition[] cat) => Chassis("Dostawczak", "Dostawczak mleczarni", D, ArtD + "Podwozie_Dostawczak.fbx",
            new VehicleStats { massKg = 1900, motorTorque = 1300, maxSpeedKmh = 85, grip = 1f }, cat,
            "Kabina_Dostawcza", "Skrzynia_Chlodnia", "Kola_Standard", "Zderzak_Dostawczy_Przod", "Zderzak_Dostawczy_Tyl", "Wydech_Dostawczy");

        /// <summary>Składa pojazd w scenie: podwozie z fizyką kół, części z katalogu, lakier.</summary>
        public static GameObject BuildInScene(VehicleType type, string name, Vector3 pos, float yaw, string[] parts = null, Rgb[] paint = null)
        {
            var cat = EnsureCatalog();
            var def = type == C ? Cysterna(cat) : Dostawczak(cat);
            AssetDatabase.SaveAssets();
            string fbx = AssetDatabase.GetAssetPath(def.chassisPrefab);
            var go = DemoFarmBuilder.BuildVehicle(fbx, name, 0f, def.baseStats.massKg, pos, yaw);
            var asm = go.AddComponent<VehicleAssembler>();
            asm.definition = def;
            asm.catalog = cat.Where(p => p.fitsTypes.Contains(type)).ToArray();
            asm.loadout = new VehicleLoadout { type = type, ownerPlayerId = "lokalny" };
            if (parts != null)
                foreach (var id in parts) { var p = asm.catalog.FirstOrDefault(c => c.id == id); if (p) asm.loadout.Set(p.slot, p.id); else Debug.LogWarning($"[Mleczarka] Część {id} nie pasuje do {type}"); }
            if (paint != null && paint.Length == 3) { asm.loadout.paintPrimary = paint[0]; asm.loadout.paintSecondary = paint[1]; asm.loadout.paintAccent = paint[2]; }
            asm.ApplyDefaults();
            asm.Rebuild();

            // kolizja nadwozia (kabina i zabudowa), bez kół i dolnych 35 cm
            var rs = go.GetComponentsInChildren<Renderer>().Where(r => !r.name.StartsWith("wheel")).ToArray();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                var box = go.GetComponent<BoxCollider>(); if (!box) box = go.AddComponent<BoxCollider>();
                var c = go.transform.InverseTransformPoint(b.center);
                var size = go.transform.InverseTransformVector(b.size);
                size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                float bottom = c.y - size.y * 0.5f, lift = 0.35f - bottom;
                if (lift > 0) { c.y += lift * 0.5f; size.y -= lift; }
                box.center = c; box.size = size;
            }
            var veh = go.GetComponent<WheeledVehicle>();
            float half = type == C ? 1.2f : 1.05f;
            if (veh && veh.cameraTarget) veh.cameraTarget.localPosition = new Vector3(0, type == C ? 2.8f : 2.2f, 0);
            if (veh && veh.exitPoint) veh.exitPoint.localPosition = new Vector3(-(half + 1.2f), 0.1f, 1.2f);
            Debug.Log($"[Mleczarka] {name}: masa {asm.Stats.massKg} kg, moment {asm.Stats.motorTorque} Nm, v.max {asm.Stats.maxSpeedKmh} km/h, pojemność {asm.Stats.capacityL} l.");
            return go;
        }
    }
}
