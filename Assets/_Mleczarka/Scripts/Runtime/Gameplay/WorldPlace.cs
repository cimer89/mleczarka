using System.Collections.Generic;
using Mleczarka.Core;
using UnityEngine;

namespace Mleczarka.Gameplay
{
    /// <summary>Strefa w świecie: gospodarstwo, mleczarnia albo rynek. Symulacja pyta o nie przez ScenePlaceProvider.</summary>
    public class WorldPlace : MonoBehaviour
    {
        public PlaceKind kind = PlaceKind.Gospodarstwo;
        public int farmId = -1;
        public float radius = 14f;
        public string displayName;

        public static readonly List<WorldPlace> All = new List<WorldPlace>();
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public bool Contains(Vector3 p)
        {
            var d = p - transform.position; d.y = 0;
            return d.sqrMagnitude <= radius * radius;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind == PlaceKind.Mleczarnia ? new Color(0.2f, 0.5f, 1f, 0.6f) : kind == PlaceKind.Rynek ? new Color(1f, 0.6f, 0.1f, 0.6f) : new Color(0.3f, 0.9f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
