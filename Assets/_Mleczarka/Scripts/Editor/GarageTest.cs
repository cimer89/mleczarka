using System.IO;
using Mleczarka.Vehicles;
using Mleczarka.Vehicles.Model;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.EditorTools
{
    /// <summary>Test w trybie gry: zmiana części dostawczaka w locie (ta sama ścieżka co przyciski garażu).</summary>
    public static class GarageTest
    {
        [MenuItem("Mleczarka/8. Test zmiany części (tryb gry)", priority = 8)]
        public static void Run()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Mleczarka] Uruchom tryb gry."); return; }
            var go = GameObject.Find("Dostawczak mleczarni");
            var a = go ? go.GetComponent<VehicleAssembler>() : null;
            if (!a) { Debug.LogWarning("[Mleczarka] Brak dostawczaka."); return; }
            var before = a.Stats;
            a.loadout.Set(PartSlot.Kabina, "Kabina_Dostawcza_Sportowa");
            a.loadout.Set(PartSlot.Zabudowa, "Skrzynia_Wyscigowa");
            a.loadout.Set(PartSlot.Kola, "Kola_Sportowe");
            a.loadout.Set(PartSlot.Spoiler, "Spoiler_Wyscigowy");
            a.loadout.paintPrimary = new Rgb(0.98f, 0.55f, 0.05f);
            a.Rebuild();
            Debug.Log($"[Mleczarka] Test garażu: v.max {before.maxSpeedKmh} → {a.Stats.maxSpeedKmh} km/h, masa {before.massKg} → {a.Stats.massKg} kg, części: {a.transform.GetComponentsInChildren<Transform>().Length} obiektów.");
        }
    }
}
