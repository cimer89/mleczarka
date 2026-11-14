using System.Collections.Generic;
using UnityEngine;

namespace Mleczarka.World
{
    /// <summary>Dane mapy gminy dla interfejsu: wypalona tekstura, zasięg świata, nazwane miejsca.</summary>
    public class MapInfo : MonoBehaviour
    {
        public static MapInfo Instance { get; private set; }
        public Texture2D mapTexture;
        public Vector2 worldMin = new Vector2(-2560, -2560);
        public float worldSize = 5120f;

        [System.Serializable]
        public class Label { public string name; public Vector2 pos; public int kind; }   // 0 wieś, 1 miasto, 2 mleczarnia, 3 rywal
        public List<Label> labels = new List<Label>();

        [System.Serializable]
        public class RoadPath { public string name; public int type; public Vector3[] points; }   // typ: 0 asfalt, 1 szuter, 2 leśna; punkty co ok. 8 m
        public List<RoadPath> roads = new List<RoadPath>();

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        /// <summary>Pozycja w świecie → współrzędne 0..1 na mapie (v rośnie na północ).</summary>
        public Vector2 ToMap(Vector3 world) => new Vector2((world.x - worldMin.x) / worldSize, (world.z - worldMin.y) / worldSize);
    }
}
