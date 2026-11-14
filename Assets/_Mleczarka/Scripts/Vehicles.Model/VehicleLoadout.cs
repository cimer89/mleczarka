using System;
using System.Collections.Generic;

namespace Mleczarka.Vehicles.Model
{
    /// <summary>Typ pojazdu gracza. W grze są dwa: cysterna (odbiór mleka) i dostawczak (zamówienia).</summary>
    public enum VehicleType { Cysterna = 0, Dostawczak = 1 }

    /// <summary>Miejsca montażu części. Każdy pojazd ma w modelu podwozia punkty o nazwach „Slot_&lt;nazwa&gt;”.</summary>
    public enum PartSlot { Kabina = 0, Zabudowa = 1, Kola = 2, ZderzakPrzedni = 3, ZderzakTylny = 4, Dach = 5, Spoiler = 6, Wydech = 7 }

    /// <summary>Poziom części: od zwykłych po wyścigowe.</summary>
    public enum PartTier { Zwykla = 0, Tuning = 1, Wyscigowa = 2 }

    /// <summary>Kolor RGB w 0..1 (bez zależności od UnityEngine, żeby dało się go zapisać i przesłać siecią).</summary>
    [Serializable]
    public struct Rgb
    {
        public float r, g, b;
        public Rgb(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }
    }

    /// <summary>
    /// Konfiguracja konkretnego pojazdu gracza: wybrane części i lakier.
    /// To czyste dane: trafiają do zapisu gry, a w multiplayerze do synchronizacji.
    /// </summary>
    [Serializable]
    public class VehicleLoadout
    {
        public int version = 1;
        public VehicleType type;
        public string ownerPlayerId;
        public string vehicleId;          // nazwa pojazdu w świecie (do odtworzenia po wczytaniu gry)
        public List<SlotChoice> parts = new List<SlotChoice>();
        public Rgb paintPrimary = new Rgb(0.95f, 0.95f, 0.92f);
        public Rgb paintSecondary = new Rgb(0.20f, 0.45f, 0.85f);
        public Rgb paintAccent = new Rgb(0.90f, 0.20f, 0.18f);
        public string decalId;

        [Serializable]
        public struct SlotChoice
        {
            public PartSlot slot;
            public string partId;
        }

        public string Get(PartSlot slot)
        {
            foreach (var p in parts) if (p.slot == slot) return p.partId;
            return null;
        }

        public void Set(PartSlot slot, string partId)
        {
            parts.RemoveAll(p => p.slot == slot);
            if (!string.IsNullOrEmpty(partId)) parts.Add(new SlotChoice { slot = slot, partId = partId });
        }
    }

    /// <summary>Parametry jazdy wynikające z podwozia i części.</summary>
    [Serializable]
    public struct VehicleStats
    {
        public float massKg;
        public float motorTorque;
        public float maxSpeedKmh;
        public float grip;        // mnożnik przyczepności opon
        public float capacityL;   // pojemność zbiornika lub skrzyni (litry mleka)

        public static VehicleStats operator +(VehicleStats a, VehicleStats b) => new VehicleStats
        {
            massKg = a.massKg + b.massKg,
            motorTorque = a.motorTorque + b.motorTorque,
            maxSpeedKmh = a.maxSpeedKmh + b.maxSpeedKmh,
            grip = a.grip + b.grip,
            capacityL = a.capacityL + b.capacityL,
        };
    }

    /// <summary>Liczy parametry pojazdu: baza podwozia plus modyfikatory części.</summary>
    public static class VehicleStatCalculator
    {
        public static VehicleStats Compute(VehicleStats chassis, IEnumerable<VehicleStats> partModifiers)
        {
            var s = chassis;
            foreach (var m in partModifiers) s += m;
            s.massKg = Math.Max(300f, s.massKg);
            s.motorTorque = Math.Max(100f, s.motorTorque);
            s.maxSpeedKmh = Math.Max(10f, s.maxSpeedKmh);
            s.grip = Math.Max(0.3f, s.grip);
            s.capacityL = Math.Max(0f, s.capacityL);
            return s;
        }
    }
}
