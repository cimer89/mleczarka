using UnityEngine;
using Mleczarka.Gameplay;
namespace Mleczarka.ProductionFloor
{
    public sealed class FactoryShellVisibility : MonoBehaviour
    {
        public Renderer[] shell;
        public Vector3 localSize = new Vector3(28, 10, 26);
        void LateUpdate()
        {
            var gc = GameController.Instance;
            if (!gc || !gc.player) return;
            Vector3 p = transform.InverseTransformPoint(gc.PlayerPosition);
            bool inside = Mathf.Abs(p.x) < localSize.x / 2 + 1 && Mathf.Abs(p.z) < localSize.z / 2 + 1 && !gc.player.CurrentVehicle;
            foreach (var renderer in shell) if (renderer) renderer.enabled = !inside;
        }
    }
}
