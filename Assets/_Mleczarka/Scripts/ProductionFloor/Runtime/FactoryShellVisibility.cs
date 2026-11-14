using UnityEngine;
using Mleczarka.Gameplay;
namespace Mleczarka.ProductionFloor
{
    /// <summary>
    /// Ściany i dach hali chowają się tylko wtedy, gdy zasłaniają gracza przed kamerą
    /// (linia kamera → gracz przechodzi przez daną bryłę). Pozostałe ściany, okna i dach nad głową zostają,
    /// więc w środku widać prawdziwe wnętrze, a nie otwartą makietę.
    /// </summary>
    public sealed class FactoryShellVisibility : MonoBehaviour
    {
        public Renderer[] shell;
        public Vector3 localSize = new Vector3(28, 10, 26);
        Bounds[] _bounds;
        Camera _cam;

        void LateUpdate()
        {
            var gc = GameController.Instance;
            if (!gc || !gc.player || shell == null) return;
            if (!_cam) _cam = Camera.main;
            if (_bounds == null || _bounds.Length != shell.Length)
            {
                _bounds = new Bounds[shell.Length];
                for (int i = 0; i < shell.Length; i++) if (shell[i]) { var b = shell[i].bounds; b.Expand(0.25f); _bounds[i] = b; }
            }
            Vector3 p = transform.InverseTransformPoint(gc.PlayerPosition);
            bool near = Mathf.Abs(p.x) < localSize.x / 2 + 14 && Mathf.Abs(p.z) < localSize.z / 2 + 14;
            if (!near || !_cam || gc.player.CurrentVehicle && !(Mathf.Abs(p.x) < localSize.x / 2 && Mathf.Abs(p.z) < localSize.z / 2))
            {
                foreach (var r in shell) if (r && !r.enabled) r.enabled = true;
                return;
            }
            Vector3 cam = _cam.transform.position, pos = gc.PlayerPosition;
            Vector3 head = pos + Vector3.up * 1.6f, chest = pos + Vector3.up * 1.0f, feet = pos + Vector3.up * 0.3f;
            for (int i = 0; i < shell.Length; i++)
            {
                var r = shell[i]; if (!r) continue;
                bool hide = Blocks(_bounds[i], cam, head) || Blocks(_bounds[i], cam, chest) || Blocks(_bounds[i], cam, feet) || _bounds[i].Contains(cam);
                if (r.enabled == hide) r.enabled = !hide;
            }
        }

        static bool Blocks(Bounds b, Vector3 from, Vector3 to)
        {
            var d = to - from; float len = d.magnitude; if (len < 0.01f) return false;
            return b.IntersectRay(new Ray(from, d / len), out float dist) && dist < len - 0.2f;
        }
    }
}
