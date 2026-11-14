using Mleczarka.Vehicles.Model;
using UnityEngine;

namespace Mleczarka.Vehicles
{
    /// <summary>Podwozie danego typu pojazdu: model z punktami „Slot_…”, parametry bazowe i części domyślne.</summary>
    [CreateAssetMenu(menuName = "Mleczarka/Pojazdy/Podwozie", fileName = "Podwozie_")]
    public class VehicleDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public VehicleType type;
        public GameObject chassisPrefab;
        public VehicleStats baseStats = new VehicleStats { massKg = 2200, motorTorque = 1400, maxSpeedKmh = 70, grip = 1f, capacityL = 0 };
        public VehiclePartDefinition[] defaultParts;
    }
}
