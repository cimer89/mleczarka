using UnityEngine;

namespace Mleczarka.ProductionFloor
{
    public enum FloorAction { Intake, Uht, Pasteurize, Ferment, PackMilk, PackYogurt, Warehouse, Dispatch }
    public sealed class FloorStation : MonoBehaviour
    {
        public FloorAction action;
        public string title;
        public Transform stockAnchor;
        public Transform secondStockAnchor;
    }
}
