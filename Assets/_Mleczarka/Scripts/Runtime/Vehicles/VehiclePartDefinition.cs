using Mleczarka.Vehicles.Model;
using UnityEngine;

namespace Mleczarka.Vehicles
{
    /// <summary>Część pojazdu (kabina, zbiornik, koła, spoiler…). Nową część dodaje się jako asset, bez programowania.</summary>
    [CreateAssetMenu(menuName = "Mleczarka/Pojazdy/Część pojazdu", fileName = "Czesc_")]
    public class VehiclePartDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public PartSlot slot;
        public PartTier tier;
        public VehicleType[] fitsTypes = { VehicleType.Cysterna, VehicleType.Dostawczak };
        public GameObject prefab;
        public int price;
        [Tooltip("Zmiana parametrów względem podwozia (np. +200 Nm, +5 km/h, -150 kg).")]
        public VehicleStats modifiers;
    }

}
