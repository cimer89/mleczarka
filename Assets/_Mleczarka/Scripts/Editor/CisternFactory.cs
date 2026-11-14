using Mleczarka.Vehicles.Model;
using UnityEngine;

namespace Mleczarka.EditorTools
{
    /// <summary>Zgodność wstecz: cysterna budowana przez VehicleFactory.</summary>
    public static class CisternFactory
    {
        public static GameObject BuildInScene(Vector3 pos, float yaw) => VehicleFactory.BuildInScene(VehicleType.Cysterna, "Cysterna mleczarni", pos, yaw);
        public static GameObject BuildInScene(string name, Vector3 pos, float yaw, string[] parts, Rgb[] paint) => VehicleFactory.BuildInScene(VehicleType.Cysterna, name, pos, yaw, parts, paint);
    }
}
