using UnityEngine;
namespace Mleczarka.ProductionFloor
{
    public sealed class FloorBillboard : MonoBehaviour
    {
        Renderer labelRenderer;
        void Start()
        {
            transform.localScale *= 0.35f;
            labelRenderer = GetComponent<Renderer>();
        }
        void LateUpdate()
        {
            var camera = Camera.main;
            if (!camera) return;
            transform.rotation = camera.transform.rotation;
            if (labelRenderer) labelRenderer.enabled = Vector3.Distance(camera.transform.position, transform.position) < 13;
        }
    }
}
