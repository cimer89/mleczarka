using Mleczarka.Vehicles.Model;
using UnityEngine;

namespace Mleczarka.Vehicles
{
    /// <summary>Znacznik zamontowanej części (zapisuje się w scenie, więc składanie wie, co usunąć przy zmianie).</summary>
    public class VehiclePartInstance : MonoBehaviour
    {
        public PartSlot slot;
        public string partId;
    }
}
