using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using Mleczarka.Player;
using Mleczarka.Vehicles.Model;
using Mleczarka.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mleczarka.EditorTools
{
    /// <summary>
    /// Gmina na wzór Podlasia, ok. 5 × 5 km: kręte drogi (asfalt, szuter, leśne), rzeka z łąkami i mostami,
    /// lasy sosnowe, pola-szachownica (pszenica, kukurydza, rzepak, ziemniaki), wsie ulicówki z drewnianymi chałupami,
    /// mleczarnia w środku, miasteczko z rynkiem, Mleczpol na zachodzie. Wszystko generowane deterministycznie z ziarna.
    /// </summary>
    public static partial class DemoFarmBuilder
    {
        const string PodlasieScenePath = "Assets/_Mleczarka/Scenes/Gmina_Podlasie.unity";
        const string PGEN = GEN + "Gmina/";
        const string PW = QuaterniusMaterials.Art + "World/";
        const float Half = 3200f, Step = 2.5f;          // świat [-3200, 3200] (6,4 km), siatka pomocnicza co 2,5 m
        const int GN = 2561;                             // 6400 / 2,5 + 1
        const int Tiles = 5; const float TileSize = 1280f;
        const float KS = 1.25f;                          // skala układu (pierwszy szkic był dla 5,1 km)
        static Vector2 V(float x, float z) => new Vector2(x * KS, z * KS);

        enum RoadType { Asfalt, Szuter, Lesna }
        static float HalfWidth(RoadType t) => t == RoadType.Asfalt ? 3.2f : t == RoadType.Szuter ? 2.8f : 1.9f;
        enum Crop { Pszenica, Kukurydza, Rzepak, Ziemniaki, Laka, Orne }

        class Road { public RoadType type; public List<Vector2> p = new List<Vector2>(); public float[] h; public string name; }
        class Village { public bool hamlet; public Road road; public int i0, i1; public string name, sign; public Vector2 c; public float axisDeg, half; public bool asphalt; public int[] farms; public Vector2 Axis => new Vector2(Mathf.Sin(axisDeg * Mathf.Deg2Rad), Mathf.Cos(axisDeg * Mathf.Deg2Rad)); public Vector2 A => c - Axis * half; public Vector2 B => c + Axis * half; }
        class HouseLot { public Vector2 pos; public float yaw; public Vector2 street; public int farm = -1; public Village v; public bool barnRight; }
        class Field { public Vector2 c, u; public float hl, hw; public Crop crop; }
        class Plaza { public Vector2 c; public float r; }

        static float[] _base, _h, _roadW, _roadH, _roadDist, _plazaW, _riverDist, _forest;
        static short[] _field; static byte[] _roadKind;
        static readonly List<Road> _roads = new List<Road>();
        static readonly List<Village> _villages = new List<Village>();
        static readonly List<HouseLot> _lots = new List<HouseLot>();
        static readonly List<Field> _fields = new List<Field>();
        static readonly List<Plaza> _plazas = new List<Plaza>();
        static readonly List<(Vector2 p, float dir)> _bridges = new List<(Vector2, float)>();
        static List<Vector2> _river; static float[] _riverWl;
        static Vector2 _dairy = Vector2.zero, _town = V(250, -1150), _rival = V(-2150, -520), _kolonia = V(-760, -470);
        static readonly List<Village> _hamlets = new List<Village>();
        static StringBuilder _report;

        // ---------- siatki ----------
        static int Idx(int i, int j) => i * GN + j;                        // i: wiersz (z), j: kolumna (x)
        static float GX(int j) => -Half + j * Step;
        static float Sample(float[] a, float x, float z)
        {
            float fx = Mathf.Clamp((x + Half) / Step, 0, GN - 1.001f), fz = Mathf.Clamp((z + Half) / Step, 0, GN - 1.001f);
            int j = (int)fx, i = (int)fz; float tx = fx - j, tz = fz - i;
            float a0 = Mathf.Lerp(a[Idx(i, j)], a[Idx(i, j + 1)], tx), a1 = Mathf.Lerp(a[Idx(i + 1, j)], a[Idx(i + 1, j + 1)], tx);
            return Mathf.Lerp(a0, a1, tz);
        }
        static T Nearest<T>(T[] a, float x, float z)
        {
            int j = Mathf.Clamp(Mathf.RoundToInt((x + Half) / Step), 0, GN - 1), i = Mathf.Clamp(Mathf.RoundToInt((z + Half) / Step), 0, GN - 1);
            return a[Idx(i, j)];
        }
        static float HAt(float x, float z) => Sample(_h, x, z);
        static Vector3 P3(Vector2 p, float dy = 0) => new Vector3(p.x, HAt(p.x, p.y) + dy, p.y);
        static Vector3 P3(float x, float z) => new Vector3(x, HAt(x, z), z);
        static float Pn(float x, float z, float scale, float ox = 0, float oz = 0) => Mathf.PerlinNoise(x / scale + 1000f + ox, z / scale + 1000f + oz) - 0.5f;
        static void Stamp(Vector2 c, float radius, System.Action<int, float> f)
        {
            int j0 = Mathf.Max(0, Mathf.FloorToInt((c.x - radius + Half) / Step)), j1 = Mathf.Min(GN - 1, Mathf.CeilToInt((c.x + radius + Half) / Step));
            int i0 = Mathf.Max(0, Mathf.FloorToInt((c.y - radius + Half) / Step)), i1 = Mathf.Min(GN - 1, Mathf.CeilToInt((c.y + radius + Half) / Step));
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    float dx = GX(j) - c.x, dz = GX(i) - c.y, d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d <= radius) f(Idx(i, j), d);
                }
        }

        // ---------- dane gminy ----------
        static void DefineLayout()
        {
            _villages.Clear(); _hamlets.Clear(); _roads.Clear(); _lots.Clear(); _fields.Clear(); _plazas.Clear(); _bridges.Clear();
            _villages.Add(new Village { name = "Olszanka", sign = "Olszanka", c = V(-250, 1000), axisDeg = 80, half = 380, asphalt = true, farms = new[] { 0, 1, 6 } });
            _villages.Add(new Village { name = "Zarzecze", sign = "Zarzecze", c = V(1550, 350), axisDeg = 10, half = 330, asphalt = true, farms = new[] { 2, 7 } });
            _villages.Add(new Village { name = "Sosnowo", sign = "Sosnowo", c = V(-1500, -1350), axisDeg = 120, half = 300, asphalt = false, farms = new[] { 3, 9 } });
            _villages.Add(new Village { name = "Dąbrówka", sign = "Dabrowka", c = V(1950, 2050), axisDeg = 45, half = 300, asphalt = false, farms = new[] { 4, 10 } });
            _villages.Add(new Village { name = "Bagnówka", sign = "Bagnowka", c = V(-2000, 450), axisDeg = 0, half = 300, asphalt = false, farms = new[] { 5, 11 } });

            var O = _villages[0]; var Z = _villages[1]; var S = _villages[2]; var Db = _villages[3]; var B = _villages[4];
            foreach (var v in _villages) AddRoad(v.asphalt ? RoadType.Asfalt : RoadType.Szuter, v.A, v.B, 8f, "ulica " + v.name);
            AddRoad(RoadType.Asfalt, _dairy, O.B, 55, "Mleczarnia – Olszanka");
            AddRoad(RoadType.Asfalt, _dairy, Z.A, 60, "Mleczarnia – Zarzecze");
            AddRoad(RoadType.Asfalt, _dairy, _town, 45, "Mleczarnia – Zabielsk");
            AddRoad(RoadType.Szuter, Z.B, Db.A, 80, "Zarzecze – Dąbrówka");
            AddRoad(RoadType.Szuter, O.B, Db.A, 95, "Olszanka – Dąbrówka");
            AddRoad(RoadType.Szuter, O.A, B.B, 85, "Olszanka – Bagnówka");
            AddRoad(RoadType.Asfalt, B.A, _rival, 50, "Bagnówka – Mleczpol");
            AddRoad(RoadType.Szuter, _rival, S.A, 70, "Mleczpol – Sosnowo");
            AddRoad(RoadType.Asfalt, S.B, _town, 70, "Sosnowo – Zabielsk");
            AddRoad(RoadType.Szuter, _dairy, _kolonia, 60, "Mleczarnia – kolonia");
            AddRoad(RoadType.Lesna, _kolonia, S.B, 55, "droga leśna do Sosnowa");
            AddRoad(RoadType.Lesna, _kolonia, B.A, 70, "droga leśna do Bagnówki");
            AddRoad(RoadType.Szuter, _town, Z.A, 90, "Zabielsk – Zarzecze");
            AddRoad(RoadType.Asfalt, _town, V(420, -2540), 60, "do powiatu");
            AddRoad(RoadType.Lesna, Db.B, V(2480, 2520), 30, "leśna za Dąbrówką");
            AddRoad(RoadType.Lesna, O.A, V(-1100, 2450), 60, "leśna na północ");

            _plazas.Add(new Plaza { c = _dairy, r = 22 });
            _plazas.Add(new Plaza { c = _town, r = 22 });
            _plazas.Add(new Plaza { c = _rival, r = 22 });
            _plazas.Add(new Plaza { c = _kolonia, r = 16 });
        }

        static void AddRoad(RoadType t, Vector2 a, Vector2 b, float amp, string name)
        {
            var r = new Road { type = t, name = name };
            float len = (b - a).magnitude; var dir = (b - a) / len; var nrm = new Vector2(-dir.y, dir.x);
            int segs = Mathf.Max(2, Mathf.RoundToInt(len / 140f));
            float seed = (a.x * 0.013f + b.y * 0.007f) % 50f;
            var ctrl = new List<Vector2>();
            for (int i = 0; i <= segs; i++)
            {
                float t0 = i / (float)segs;
                float off = (Mathf.PerlinNoise(seed + t0 * len / 380f, seed * 0.37f) - 0.5f) * 2f * amp + Mathf.Sin(t0 * Mathf.PI * (1 + (int)seed % 3)) * amp * 0.35f;
                off *= Mathf.Sin(t0 * Mathf.PI);   // końce dokładnie w węzłach
                ctrl.Add(a + dir * (len * t0) + nrm * off);
            }
            // Catmull-Rom co ~2 m
            r.p.Add(ctrl[0]);
            for (int i = 0; i < ctrl.Count - 1; i++)
            {
                Vector2 p0 = ctrl[Mathf.Max(0, i - 1)], p1 = ctrl[i], p2 = ctrl[i + 1], p3 = ctrl[Mathf.Min(ctrl.Count - 1, i + 2)];
                int n = Mathf.Max(2, Mathf.CeilToInt((p2 - p1).magnitude / 2f));
                for (int k = 1; k <= n; k++)
                {
                    float tq = k / (float)n, t2 = tq * tq, t3 = t2 * tq;
                    r.p.Add(0.5f * ((2 * p1) + (-p0 + p2) * tq + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3));
                }
            }
            _roads.Add(r);
        }

        static void DefineRiver()
        {
            var ctrl = new[] { V(600, 2620), V(780, 2050), V(520, 1550), V(860, 980), V(690, 520), V(960, 20), V(760, -480), V(1060, -1000), V(840, -1600), V(1010, -2150), V(940, -2620) };
            _river = new List<Vector2> { ctrl[0] };
            for (int i = 0; i < ctrl.Length - 1; i++)
            {
                Vector2 p0 = ctrl[Mathf.Max(0, i - 1)], p1 = ctrl[i], p2 = ctrl[i + 1], p3 = ctrl[Mathf.Min(ctrl.Length - 1, i + 2)];
                int n = Mathf.CeilToInt((p2 - p1).magnitude / 2f);
                for (int k = 1; k <= n; k++)
                {
                    float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                    var q = 0.5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
                    // drobne meandry
                    var d = (p2 - p1).normalized; q += new Vector2(-d.y, d.x) * Mathf.Sin((i + t) * 5.1f) * 18f;
                    _river.Add(q);
                }
            }
        }

        // ---------- teren ----------
        static float BaseFn(float x, float z, float riverD)
        {
            float h = 20f + 14f * Pn(x, z, 1400) + 6f * Pn(x, z, 480, 7, 3) + 1.6f * Pn(x, z, 120, 2, 9) + 0.4f * Pn(x, z, 35, 5, 5);
            h -= 5f * Mathf.Exp(-Mathf.Pow(riverD / 260f, 2)) + 2f * Mathf.Exp(-Mathf.Pow(riverD / 70f, 2));
            return h;
        }

        static void ComputeTerrain()
        {
            int n = GN * GN;
            _base = new float[n]; _h = new float[n]; _roadW = new float[n]; _roadH = new float[n]; _roadDist = new float[n]; _plazaW = new float[n];
            _riverDist = new float[n]; _forest = new float[n]; _field = new short[n]; _roadKind = new byte[n];
            for (int k = 0; k < n; k++) { _roadDist[k] = 999; _riverDist[k] = 999; }

            // 1. odległość od rzeki: dokładnie do 60 m, zgrubnie dalej (co 10 m, promień 600 m)
            var coarse = new float[n]; for (int k = 0; k < n; k++) coarse[k] = 999;
            for (int i = 0; i < _river.Count; i += 5) Stamp(_river[i], 600, (k, d) => { if (d < coarse[k]) coarse[k] = d; });
            for (int i = 0; i < _river.Count; i++) Stamp(_river[i], 60, (k, d) => { if (d < _riverDist[k]) _riverDist[k] = d; });
            for (int k = 0; k < n; k++) if (_riverDist[k] > 60) _riverDist[k] = coarse[k];

            for (int i = 0; i < GN; i++)
                for (int j = 0; j < GN; j++) { int k = Idx(i, j); _base[k] = BaseFn(GX(j), GX(i), _riverDist[k]); }

            // 2. poziom wody: wysokość doliny, nigdy nie rośnie z biegiem rzeki
            _riverWl = new float[_river.Count];
            float run = 999;
            for (int i = 0; i < _river.Count; i++) { run = Mathf.Min(run, Sample(_base, _river[i].x, _river[i].y) - 1.3f); _riverWl[i] = run; }

            // 3. wysokość dróg: wygładzona wysokość terenu, nad rzeką co najmniej 1,8 m nad wodą
            foreach (var r in _roads)
            {
                int m = r.p.Count; var h = new float[m];
                for (int i = 0; i < m; i++) h[i] = Sample(_base, r.p[i].x, r.p[i].y);
                h = Smooth1D(Smooth1D(h, 14), 14);
                var need = new float[m];
                for (int i = 0; i < m; i++)
                {
                    need[i] = -999;
                    var (ri, rd) = NearestRiver(r.p[i]);
                    if (rd < 45) need[i] = _riverWl[ri] + 2.0f;
                }
                for (int i = 0; i < m; i++) h[i] = Mathf.Max(h[i], need[i]);
                h = Smooth1D(h, 10);
                for (int i = 0; i < m; i++) h[i] = Mathf.Max(h[i], need[i]);
                r.h = h;
            }

            // 4. odciski dróg i placów
            foreach (var r in _roads)
            {
                float hw = HalfWidth(r.type), R = hw + 14f;
                for (int i = 0; i < r.p.Count; i++)
                {
                    float hr = r.h[i]; byte kind = (byte)(r.type + 1);
                    Stamp(r.p[i], R, (k, d) =>
                    {
                        if (d < _roadDist[k]) { _roadDist[k] = d; _roadKind[k] = kind; }
                        float w = d <= hw + 1.0f ? 1f : 1f - Smooth(0, 1, (d - hw - 1f) / (R - hw - 1f));
                        if (w > _roadW[k]) { _roadW[k] = w; _roadH[k] = hr; }
                    });
                }
            }
            foreach (var pl in _plazas) FlattenCircle(pl.c, pl.r + 10f, 30f, true);
            foreach (var lot in _lots) FlattenCircle(lot.pos, 20f, 12f, false);

            for (int k = 0; k < n; k++) _h[k] = Mathf.Lerp(_base[k], _roadH[k], _roadW[k]);

            // 5. koryto rzeki (pod drogami zostaje nasyp; przęsło mostu ma ok. 20 m)
            for (int i = 0; i < _river.Count; i++)
            {
                float wl = _riverWl[i];
                Stamp(_river[i], 34, (k, d) =>
                {
                    if (_roadW[k] > 0.6f && d > 8.5f) return;
                    float prof = d < 7f ? wl - 1.6f : d < 15f ? Mathf.Lerp(wl - 1.6f, wl + 0.5f, (d - 7f) / 8f) : Mathf.Lerp(wl + 0.5f, _h[k], (d - 15f) / 19f);
                    if (prof < _h[k]) _h[k] = prof;
                });
            }

            // 6. mosty: miejsca, gdzie droga przecina rzekę
            foreach (var r in _roads)
            {
                bool inside = false;
                for (int i = 0; i < r.p.Count; i++)
                {
                    var (ri, rd) = NearestRiver(r.p[i]);
                    if (rd < 3f && !inside)
                    {
                        inside = true;
                        var dir = r.p[Mathf.Min(r.p.Count - 1, i + 1)] - r.p[Mathf.Max(0, i - 1)];
                        _bridges.Add((r.p[i], Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg));
                        _report.AppendLine($"  most: {r.name} ({r.p[i].x:0}, {r.p[i].y:0}), droga {r.h[i]:0.0} m, woda {_riverWl[ri]:0.0} m");
                    }
                    if (rd > 20f) inside = false;
                }
            }
        }

        static void FlattenCircle(Vector2 c, float r, float fall, bool plaza)
        {
            float target = Sample(_base, c.x, c.y);
            if (!plaza) { float rh = Nearest(_roadW, c.x, c.y) > 0.3f ? Nearest(_roadH, c.x, c.y) : target; target = Mathf.Lerp(target, rh, 0.5f); }
            Stamp(c, r + fall, (k, d) =>
            {
                float w = d <= r ? 1f : 1f - Smooth(0, 1, (d - r) / fall);
                if (plaza && w > _plazaW[k]) _plazaW[k] = w;
                if (w > _roadW[k]) { _roadW[k] = w; _roadH[k] = target; }
            });
        }

        static float[] Smooth1D(float[] a, int win)
        {
            var o = new float[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                float s = 0; int c = 0;
                for (int k = Mathf.Max(0, i - win); k <= Mathf.Min(a.Length - 1, i + win); k++) { s += a[k]; c++; }
                o[i] = s / c;
            }
            return o;
        }

        static (int, float) NearestRiver(Vector2 p)
        {
            int best = 0; float bd = 1e9f;
            for (int i = 0; i < _river.Count; i += 4) { float d = (_river[i] - p).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            for (int i = Mathf.Max(0, best - 4); i < Mathf.Min(_river.Count, best + 5); i++) { float d = (_river[i] - p).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            return (best, Mathf.Sqrt(bd));
        }

        // ---------- wsie ----------
        static void LayoutVillages()
        {
            var rnd = new System.Random(21);
            foreach (var v in _villages)
            {
                var ax = v.Axis; var nrm = new Vector2(ax.y, -ax.x);
                var lots = new List<HouseLot>();
                for (int side = -1; side <= 1; side += 2)
                    for (float t = -v.half + 25f + (side > 0 ? 0 : 16f); t <= v.half - 20f; t += 34f + (float)rnd.NextDouble() * 10f)
                    {
                        var street = v.c + ax * t;
                        var pos = street + nrm * side * 21f;
                        // dom szczytem do ulicy: przód (+Z modelu) patrzy na ulicę
                        var face = -nrm * side;
                        lots.Add(new HouseLot { pos = pos, yaw = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg + (float)(rnd.NextDouble() * 6 - 3), street = street + nrm * side * 6f, v = v, barnRight = rnd.NextDouble() < 0.5 });
                    }
                // gospodarstwa mleczne rozdzielone po całej wsi
                for (int f = 0; f < v.farms.Length; f++)
                {
                    int idx = Mathf.Clamp(Mathf.RoundToInt((f + 0.5f) / v.farms.Length * lots.Count), 0, lots.Count - 1);
                    while (lots[idx].farm >= 0) idx = (idx + 1) % lots.Count;
                    lots[idx].farm = v.farms[f];
                }
                _lots.AddRange(lots);
            }
            // przysiółki przy drogach między wsiami (bez gospodarstw mlecznych, z tablicami)
            var defs = new (string name, string sign, string road, float frac, int count)[]
            {
                ("Grabowiec", "Grabowiec", "Mleczarnia – Zarzecze", 0.35f, 6), ("Zagórze", "Zagorze", "Zarzecze – Dąbrówka", 0.5f, 5),
                ("Łosinka", "Losinka", "Olszanka – Bagnówka", 0.5f, 6), ("Pasieki", "Pasieki", "Mleczpol – Sosnowo", 0.5f, 4),
                ("Krzywa", "Krzywa", "Sosnowo – Zabielsk", 0.45f, 6), ("Kruszewo", "Kruszewo", "Olszanka – Dąbrówka", 0.25f, 5),
            };
            foreach (var h in defs)
            {
                var road = _roads.First(r => r.name == h.road);
                int span = h.count * 9 + 10, mid = Mathf.RoundToInt(h.frac * road.p.Count);
                for (int tries = 0; tries < 8; tries++)
                {
                    bool ok = true;
                    for (int i = mid - span; i <= mid + span; i += 5) { int ii = Mathf.Clamp(i, 0, road.p.Count - 1); if (NearestRiver(road.p[ii]).Item2 < 160f) ok = false; }
                    if (ok) break;
                    mid += (tries % 2 == 0 ? 1 : -1) * (tries + 1) * 40;
                }
                mid = Mathf.Clamp(mid, span + 15, road.p.Count - span - 15);
                int i0 = mid - span, i1 = mid + span;
                var ax2 = (road.p[i1] - road.p[i0]).normalized;
                var hv = new Village { hamlet = true, road = road, i0 = i0, i1 = i1, name = h.name, sign = h.sign, c = road.p[mid], axisDeg = Mathf.Atan2(ax2.x, ax2.y) * Mathf.Rad2Deg, half = (road.p[i1] - road.p[i0]).magnitude / 2, farms = new int[0] };
                _hamlets.Add(hv);
                for (int k = 0; k < h.count; k++)
                {
                    int i = i0 + 8 + k * 17 / 2 * 2 + (k % 2) * 4; i = Mathf.Clamp(i, 1, road.p.Count - 2);
                    var d = (road.p[i + 1] - road.p[i - 1]).normalized; var n = new Vector2(d.y, -d.x); int side = k % 2 == 0 ? 1 : -1;
                    var face = -n * side;
                    _lots.Add(new HouseLot { pos = road.p[i] + n * side * 21f, yaw = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg + (float)(rnd.NextDouble() * 6 - 3), street = road.p[i], v = hv, barnRight = rnd.NextDouble() < 0.5 });
                }
            }
            // kolonia Kirylukowie (farma 8)
            _lots.Add(new HouseLot { pos = _kolonia + new Vector2(14, 16), yaw = 200, street = _kolonia, farm = 8, v = null, barnRight = true });
        }

        // ---------- pola, las ----------
        static void LayoutFields()
        {
            var rnd = new System.Random(33);
            Crop Pick()
            {
                double r = rnd.NextDouble();
                return r < 0.30 ? Crop.Pszenica : r < 0.55 ? Crop.Kukurydza : r < 0.67 ? Crop.Rzepak : r < 0.77 ? Crop.Ziemniaki : r < 0.90 ? Crop.Laka : Crop.Orne;
            }
            // szachownica pasów za zagrodami: prostopadle do ulicy
            foreach (var v in _villages.Concat(_hamlets))
            {
                var ax = v.Axis; var nrm = new Vector2(ax.y, -ax.x);
                for (int side = -1; side <= 1; side += 2)
                {
                    float t = -v.half - 60f;
                    while (t < v.half + 60f)
                    {
                        float w = 16f + (float)rnd.NextDouble() * 22f, L = 180f + (float)rnd.NextDouble() * 260f;
                        TryField(new Field { c = v.c + ax * (t + w / 2) + nrm * side * (48f + L / 2), u = nrm * side, hl = L / 2, hw = w / 2 - 0.8f, crop = Pick() });
                        t += w;
                    }
                }
            }
            // bloki pól w otwartym terenie
            for (int k = 0; k < 220; k++)
            {
                var c = new Vector2((float)rnd.NextDouble() * 6000 - 3000, (float)rnd.NextDouble() * 6000 - 3000);
                if (Pn(c.x, c.y, 700, 50, 50) + 0.15f * Pn(c.x, c.y, 200, 9, 1) > 0.05f) continue;   // tam będzie las
                float ang = (float)rnd.NextDouble() * Mathf.PI; var u = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)); var v2 = new Vector2(-u.y, u.x);
                int strips = 3 + rnd.Next(5); float off = 0, L = 160f + (float)rnd.NextDouble() * 220f;
                for (int s = 0; s < strips; s++)
                {
                    float w = 18f + (float)rnd.NextDouble() * 26f;
                    TryField(new Field { c = c + v2 * (off + w / 2), u = u, hl = L / 2, hw = w / 2 - 0.8f, crop = Pick() });
                    off += w;
                }
            }
        }

        static void TryField(Field f)
        {
            var v = new Vector2(-f.u.y, f.u.x);
            // odrzuć, jeśli na środku leży droga, rzeka, plac albo inne pole
            for (float a = -f.hl; a <= f.hl; a += 20f)
            {
                var p = f.c + f.u * a;
                if (Mathf.Abs(p.x) > Half - 30 || Mathf.Abs(p.y) > Half - 30) return;
                if (Nearest(_riverDist, p.x, p.y) < 45f || Nearest(_plazaW, p.x, p.y) > 0.1f) return;
                if (Nearest(_field, p.x, p.y) != 0) return;
            }
            foreach (var lot in _lots) if ((lot.pos - f.c).magnitude < f.hl + 4f && DistToRect(lot.pos, f) < 26f) return;
            _fields.Add(f); short id = (short)_fields.Count;
            float R = Mathf.Sqrt(f.hl * f.hl + f.hw * f.hw);
            Stamp(f.c, R, (k, d) =>
            {
                int i = k / GN, j = k % GN; var q = new Vector2(GX(j), GX(i)) - f.c;
                if (Mathf.Abs(Vector2.Dot(q, f.u)) <= f.hl && Mathf.Abs(Vector2.Dot(q, v)) <= f.hw && _field[k] == 0 && _roadDist[k] > HalfWidthAt(k) + 2.5f && _riverDist[k] > 30f && _plazaW[k] < 0.05f)
                    _field[k] = id;
            });
        }
        static float HalfWidthAt(int k) => _roadKind[k] == 0 ? 0f : HalfWidth((RoadType)(_roadKind[k] - 1));
        static float DistToRect(Vector2 p, Field f)
        {
            var q = p - f.c; var v = new Vector2(-f.u.y, f.u.x);
            float a = Mathf.Max(0, Mathf.Abs(Vector2.Dot(q, f.u)) - f.hl), b = Mathf.Max(0, Mathf.Abs(Vector2.Dot(q, v)) - f.hw);
            return Mathf.Sqrt(a * a + b * b);
        }

        static void ComputeForest()
        {
            for (int i = 0; i < GN; i++)
                for (int j = 0; j < GN; j++)
                {
                    int k = Idx(i, j); float x = GX(j), z = GX(i);
                    float f = -(Pn(x, z, 700, 50, 50) + 0.15f * Pn(x, z, 200, 9, 1)) + 0.12f * Pn(x, z, 60, 3, 3);
                    f += 0.25f * Mathf.Exp(-((x + 1690) * (x + 1690) + (z + 750) * (z + 750)) / (870f * 870f));   // puszcza między Sosnowem a kolonią
                    f += 0.22f * Mathf.Exp(-((x - 2620) * (x - 2620) + (z + 2000) * (z + 2000)) / (750f * 750f));
                    f += 0.18f * Mathf.Exp(-((x + 1250) * (x + 1250) + (z - 2620) * (z - 2620)) / (690f * 690f));
                    float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)); f += Smooth(2750, 3200, edge) * 0.35f;      // las na krawędziach mapy
                    float block = 0;
                    if (_field[k] != 0 || _plazaW[k] > 0.05f) block = 1;
                    if (_roadDist[k] < HalfWidthAt(k) + 5f) block = 1;
                    if (_riverDist[k] < 28f) block = 1;
                    _forest[k] = block > 0 ? 0 : Smooth(0.04f, 0.10f, f);
                }
            foreach (var v in _villages.Concat(_hamlets)) Stamp(v.c, v.half + 90f, (k, d) => _forest[k] = 0);
            foreach (var lot in _lots) Stamp(lot.pos, 34f, (k, d) => _forest[k] = 0);
        }

        // ---------- kafle Unity Terrain ----------
        static Terrain[,] BuildTiles(Transform parent)
        {
            Directory.CreateDirectory(PGEN);
            var layers = new[]
            {
                Layer("Trawa", "T_Trawa_Stylizowana", 7f), Layer("Laka", "T_Trawa_Laka", 9f), Layer("Ziemia", "T_Ziemia_Droga", 4f),
                PLayer("Gleba", "T_Gleba_Orna", 6f), PLayer("Sciernisko", "T_Sciernisko", 5f), PLayer("Sciolka", "T_Sciolka_Lesna", 6f),
            };
            var tm = AssetDatabase.LoadAssetAtPath<Material>(GEN + "Settings/M_Teren.mat");
            if (!tm) { tm = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")); SaveAsset(tm, GEN + "Settings/M_Teren.mat"); }
            var details = DetailPrototypes();
            var trees = TreePrototypes();
            var result = new Terrain[Tiles, Tiles];
            const float HY = 60f; const int HR = 513, AR = 1024, DR = 512;
            for (int tz = 0; tz < Tiles; tz++)
                for (int tx = 0; tx < Tiles; tx++)
                {
                    string path = PGEN + $"Teren_{tx}_{tz}.asset";
                    AssetDatabase.DeleteAsset(path);
                    var td = new TerrainData(); AssetDatabase.CreateAsset(td, path);
                    td.heightmapResolution = HR; td.alphamapResolution = AR; td.baseMapResolution = 512;
                    td.SetDetailResolution(DR, 32);
                    td.size = new Vector3(TileSize, HY, TileSize);
                    var hts = new float[HR, HR];
                    for (int i = 0; i < HR; i++)
                        for (int j = 0; j < HR; j++)
                            hts[i, j] = Mathf.Clamp01(_h[Idx(tz * 512 + i, tx * 512 + j)] / HY);
                    td.SetHeights(0, 0, hts);
                    td.terrainLayers = layers;
                    float ox = -Half + tx * TileSize, oz = -Half + tz * TileSize;
                    td.SetAlphamaps(0, 0, Alphamap(ox, oz, AR));
                    td.detailPrototypes = details;
                    td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
                    FillDetails(td, ox, oz, DR, details.Length);
                    td.treePrototypes = trees.protos;
                    td.SetTreeInstances(Trees(ox, oz, trees.kinds).ToArray(), true);
                    EditorUtility.SetDirty(td);
                    var go = Terrain.CreateTerrainGameObject(td);
                    go.name = $"Teren {tx}_{tz}"; go.transform.SetParent(parent); go.transform.position = new Vector3(ox, 0, oz);
                    var t = go.GetComponent<Terrain>();
                    t.materialTemplate = tm; t.drawInstanced = true; t.basemapDistance = 350f; t.heightmapPixelError = 5f;
                    t.detailObjectDistance = 140f; t.detailObjectDensity = 1f; t.treeDistance = 900f; t.treeBillboardDistance = 900f; t.treeMaximumFullLODCount = 5000;
                    t.groupingID = 1; t.allowAutoConnect = true;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                    result[tx, tz] = t;
                }
            for (int tz = 0; tz < Tiles; tz++)
                for (int tx = 0; tx < Tiles; tx++)
                    result[tx, tz].SetNeighbors(tx > 0 ? result[tx - 1, tz] : null, tz < Tiles - 1 ? result[tx, tz + 1] : null, tx < Tiles - 1 ? result[tx + 1, tz] : null, tz > 0 ? result[tx, tz - 1] : null);
            AssetDatabase.SaveAssets();
            return result;
        }

        static TerrainLayer PLayer(string name, string tex, float tile)
        {
            string path = GEN + "Settings/Warstwa_" + name + ".terrainlayer";
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (!l) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
            const string T = "Assets/_Mleczarka/Art/Textures/Teren/";
            l.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(T + tex + "_BaseColor.png");
            l.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(T + tex + "_Normal.png");
            l.normalScale = 0.6f; l.tileSize = new Vector2(tile, tile); l.smoothness = 0.05f;
            l.diffuseRemapMax = new Vector4(0.86f, 0.86f, 0.86f, 1f);
            EditorUtility.SetDirty(l);
            return l;
        }

        static Crop CropAt(int k) => _field[k] == 0 ? (Crop)(-1) : _fields[_field[k] - 1].crop;

        static float[,,] Alphamap(float ox, float oz, int AR)
        {
            var map = new float[AR, AR, 6]; float cs = TileSize / AR;
            for (int y = 0; y < AR; y++)
                for (int x = 0; x < AR; x++)
                {
                    float wx = ox + (x + 0.5f) * cs, wz = oz + (y + 0.5f) * cs;
                    int k = Idx(Mathf.Clamp(Mathf.RoundToInt((wz + Half) / Step), 0, GN - 1), Mathf.Clamp(Mathf.RoundToInt((wx + Half) / Step), 0, GN - 1));
                    float noise = Pn(wx, wz, 3f) * 0.8f + Pn(wx, wz, 11f, 4, 4) * 0.6f;
                    var w = new float[6];
                    float meadow = Smooth(0.0f, 0.18f, Pn(wx, wz, 90, 3, 9)) + Mathf.Exp(-Mathf.Pow(Sample(_riverDist, wx, wz) / 230f, 2)) * 0.9f;
                    w[0] = 1; w[1] = Mathf.Clamp01(meadow);
                    var crop = CropAt(k);
                    if ((int)crop >= 0)
                    {
                        switch (crop)
                        {
                            case Crop.Pszenica: w[4] = 3; break;
                            case Crop.Kukurydza: case Crop.Ziemniaki: case Crop.Orne: w[3] = 3; break;
                            case Crop.Rzepak: w[3] = 2; w[0] += 1; break;
                            case Crop.Laka: w[1] = 3; break;
                        }
                    }
                    float forest = Sample(_forest, wx, wz);
                    w[5] = forest * 4f;
                    float rd = Sample(_riverDist, wx, wz);
                    if (rd < 16f) w[2] += 3f * (1 - Smooth(10f, 16f, rd + noise * 3f));
                    float roadD = Sample(_roadDist, wx, wz), hw = HalfWidthAt(k);
                    if (hw > 0) w[2] += 6f * (1 - Smooth(hw + 0.4f, hw + 2.2f, roadD + noise * 1.2f));
                    float pl = Sample(_plazaW, wx, wz);
                    w[2] += 4f * Smooth(0.9f, 1.0f, pl) * Mathf.Clamp01(0.75f + noise * 0.8f);
                    float s = 0; for (int l = 0; l < 6; l++) s += w[l];
                    for (int l = 0; l < 6; l++) map[y, x, l] = w[l] / s;
                }
            return map;
        }

        // detale: 0 trawa krótka, 1 trawa wysoka, 2 kwiaty, 3-4 kukurydza, 5-6 pszenica, 7 rzepak, 8 ziemniaki
        static DetailPrototype[] DetailPrototypes()
        {
            DetailPrototype D(string path, float minW, float maxW, float minH, float maxH, Color dry, Color healthy)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!go) Debug.LogWarning("[Mleczarka] Brak modelu detalu: " + path);
                foreach (var r in go ? go.GetComponentsInChildren<Renderer>() : new Renderer[0])
                    foreach (var m in r.sharedMaterials) if (m && !m.enableInstancing) { m.enableInstancing = true; EditorUtility.SetDirty(m); }
                return new DetailPrototype
                {
                    prototype = go, usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
                    minWidth = minW, maxWidth = maxW, minHeight = minH, maxHeight = maxH, noiseSpread = 0.3f, dryColor = dry, healthyColor = healthy, alignToGround = 0f,
                };
            }
            float k = _kNature;
            return new[]
            {
                D(NAT + "Grass_Common_Short.fbx", k * 0.35f, k * 0.55f, k * 0.35f, k * 0.55f, Color.white, Color.white),
                D(NAT + "Grass_Wispy_Tall.fbx", k * 0.35f, k * 0.55f, k * 0.35f, k * 0.6f, Color.white, Color.white),
                D(NAT + "Flower_3_Group.fbx", k * 0.3f, k * 0.45f, k * 0.3f, k * 0.45f, Color.white, Color.white),
                D(PW + "Uprawa_Kukurydza_1.fbx", 0.9f, 1.1f, 0.85f, 1.1f, Color.white, Color.white),
                D(PW + "Uprawa_Kukurydza_2.fbx", 0.9f, 1.1f, 0.85f, 1.1f, Color.white, Color.white),
                D(PW + "Uprawa_Pszenica_1.fbx", 1.0f, 1.3f, 0.9f, 1.1f, Color.white, Color.white),
                D(PW + "Uprawa_Pszenica_2.fbx", 1.0f, 1.3f, 0.9f, 1.1f, Color.white, Color.white),
                D(PW + "Uprawa_Rzepak_1.fbx", 1.0f, 1.3f, 0.9f, 1.15f, Color.white, Color.white),
                D(PW + "Uprawa_Ziemniaki_1.fbx", 1.0f, 1.2f, 0.9f, 1.2f, Color.white, Color.white),
            };
        }

        static void FillDetails(TerrainData td, float ox, float oz, int DR, int count)
        {
            var layers = new int[count][,];
            for (int l = 0; l < count; l++) layers[l] = new int[DR, DR];
            float cs = TileSize / DR; var rnd = new System.Random((int)(ox * 3 + oz * 7));
            for (int y = 0; y < DR; y++)
                for (int x = 0; x < DR; x++)
                {
                    float wx = ox + (x + 0.5f) * cs, wz = oz + (y + 0.5f) * cs;
                    int k = Idx(Mathf.Clamp(Mathf.RoundToInt((wz + Half) / Step), 0, GN - 1), Mathf.Clamp(Mathf.RoundToInt((wx + Half) / Step), 0, GN - 1));
                    if (_roadDist[k] < HalfWidthAt(k) + 0.8f || _plazaW[k] > 0.5f || _riverDist[k] < 9f) continue;
                    var crop = CropAt(k);
                    if ((int)crop >= 0)
                    {
                        switch (crop)
                        {
                            case Crop.Kukurydza: layers[3 + rnd.Next(2)][y, x] = 3; break;
                            case Crop.Pszenica: layers[5 + rnd.Next(2)][y, x] = 3; break;
                            case Crop.Rzepak: layers[7][y, x] = 3; break;
                            case Crop.Ziemniaki: layers[8][y, x] = 2; break;
                            case Crop.Laka: layers[1][y, x] = 1; layers[0][y, x] = 2; if (rnd.NextDouble() < 0.25) layers[2][y, x] = 1; break;
                        }
                        continue;
                    }
                    if (_forest[k] > 0.5f) { if (rnd.NextDouble() < 0.15) layers[1][y, x] = 1; continue; }
                    bool meadow = _riverDist[k] < 230f || Pn(wx, wz, 90, 3, 9) > 0.08f;
                    layers[0][y, x] = meadow ? 2 : 1;
                    if (meadow) { layers[1][y, x] = rnd.NextDouble() < 0.5 ? 1 : 0; if (rnd.NextDouble() < 0.12) layers[2][y, x] = 1; }
                    else if (rnd.NextDouble() < 0.12) layers[1][y, x] = 1;
                }
            for (int l = 0; l < count; l++) td.SetDetailLayer(0, 0, l, layers[l]);
        }

        // drzewa: 0-4 sosny, 5-9 liściaste
        static (TreePrototype[] protos, int[] kinds) TreePrototypes()
        {
            Directory.CreateDirectory(PGEN + "Drzewa");
            var list = new List<TreePrototype>();
            var kinds = new List<int>();
            foreach (var n in new[] { "Pine_1", "Pine_2", "Pine_3", "Pine_4", "Pine_5", "CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4", "CommonTree_5" })
            {
                var src = Load(NAT + n + ".fbx"); if (!src) continue;
                // Terrain wymaga siatki na obiekcie głównym prefabu: łączymy części modelu w jedną siatkę z kilkoma materiałami.
                var inst = (GameObject)Object.Instantiate(src); inst.transform.position = Vector3.zero;
                var combine = new List<CombineInstance>(); var mats = new List<Material>();
                var scale = Matrix4x4.Scale(Vector3.one * _kNature);
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
                {
                    var mr = mf.GetComponent<MeshRenderer>(); if (!mr || !mf.sharedMesh) continue;
                    for (int sm = 0; sm < mf.sharedMesh.subMeshCount; sm++)
                    {
                        combine.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = sm, transform = scale * mf.transform.localToWorldMatrix });
                        mats.Add(mr.sharedMaterials[Mathf.Min(sm, mr.sharedMaterials.Length - 1)]);
                    }
                }
                Object.DestroyImmediate(inst);
                var mesh = new Mesh { name = n + "_drzewo", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(combine.ToArray(), false, true);
                mesh.RecalculateBounds();
                string mp = PGEN + "Drzewa/" + n + "_siatka.asset"; AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(mesh, mp);
                foreach (var m in mats) if (m && !m.enableInstancing) { m.enableInstancing = true; EditorUtility.SetDirty(m); }
                var root = new GameObject(n);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
                var c = root.AddComponent<CapsuleCollider>(); c.radius = 0.35f; c.height = 5f; c.center = Vector3.up * 2.5f;
                var pf = PrefabUtility.SaveAsPrefabAsset(root, PGEN + "Drzewa/" + n + ".prefab");
                Object.DestroyImmediate(root);
                list.Add(new TreePrototype { prefab = pf, bendFactor = 0 });
                kinds.Add(n.StartsWith("Pine") ? 0 : 1);
            }
            return (list.ToArray(), kinds.ToArray());
        }

        static List<TreeInstance> Trees(float ox, float oz, int[] kinds)
        {
            var res = new List<TreeInstance>(); var rnd = new System.Random((int)(ox * 11 + oz * 5 + 7));
            int[] pines = Enumerable.Range(0, kinds.Length).Where(i => kinds[i] == 0).ToArray(), leafy = Enumerable.Range(0, kinds.Length).Where(i => kinds[i] == 1).ToArray();
            void Add(float x, float z, bool pine, float scale)
            {
                if (x < ox || z < oz || x >= ox + TileSize || z >= oz + TileSize) return;
                var arr = pine ? pines : leafy; if (arr.Length == 0) return;
                float y = HAt(x, z);
                res.Add(new TreeInstance
                {
                    prototypeIndex = arr[rnd.Next(arr.Length)],
                    position = new Vector3((x - ox) / TileSize, y / 60f, (z - oz) / TileSize),
                    widthScale = scale, heightScale = scale * (float)(0.9 + rnd.NextDouble() * 0.25),
                    rotation = (float)(rnd.NextDouble() * Mathf.PI * 2), color = Color.white, lightmapColor = Color.white,
                });
            }
            // las: siatka 7 m z losowym przesunięciem
            for (float z = oz + 3; z < oz + TileSize; z += 7f)
                for (float x = ox + 3; x < ox + TileSize; x += 7f)
                {
                    float px = x + (float)(rnd.NextDouble() * 5 - 2.5), pz = z + (float)(rnd.NextDouble() * 5 - 2.5);
                    float f = Sample(_forest, px, pz);
                    if (f < 0.5f || rnd.NextDouble() > f) continue;
                    bool pine = rnd.NextDouble() < (Pn(px, pz, 400, 8, 8) > 0.12f ? 0.45 : 0.85);
                    Add(px, pz, pine, _kNature > 0 ? (float)(1.1 + rnd.NextDouble() * 0.6) : 1f);
                }
            // przy rzece: olchy i wierzby (liściaste) w kępach
            for (int i = 0; i < _river.Count; i += 9)
            {
                if (rnd.NextDouble() > 0.45) continue;
                var p = _river[i]; var d = (_river[Mathf.Min(_river.Count - 1, i + 1)] - p).normalized; var n = new Vector2(-d.y, d.x) * (rnd.NextDouble() < 0.5 ? -1 : 1);
                var q = p + n * (float)(18 + rnd.NextDouble() * 14);
                if (Nearest(_roadDist, q.x, q.y) < 8f || Nearest(_field, q.x, q.y) != 0) continue;
                Add(q.x, q.y, false, (float)(1.0 + rnd.NextDouble() * 0.5));
            }
            // aleje przy asfaltach
            foreach (var r in _roads)
            {
                if (r.type != RoadType.Asfalt) continue;
                for (int i = 0; i < r.p.Count; i += 7)
                {
                    if (rnd.NextDouble() > 0.35) continue;
                    var d = (r.p[Mathf.Min(r.p.Count - 1, i + 1)] - r.p[Mathf.Max(0, i - 1)]).normalized; var n = new Vector2(-d.y, d.x);
                    var q = r.p[i] + n * ((rnd.NextDouble() < 0.5 ? -1 : 1) * (HalfWidth(r.type) + 4.5f));
                    if (Nearest(_roadDist, q.x, q.y) < HalfWidth(r.type) + 3.5f || Nearest(_plazaW, q.x, q.y) > 0.05f || Nearest(_riverDist, q.x, q.y) < 25f) continue;
                    Add(q.x, q.y, false, (float)(1.0 + rnd.NextDouble() * 0.4));
                }
            }
            // sady i drzewa przy chałupach
            foreach (var lot in _lots)
                for (int t = 0; t < 3; t++)
                {
                    float a = (float)(rnd.NextDouble() * Mathf.PI * 2); var q = lot.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (float)(13 + rnd.NextDouble() * 10);
                    if (Nearest(_roadDist, q.x, q.y) < 7f || Nearest(_field, q.x, q.y) != 0) continue;
                    Add(q.x, q.y, false, (float)(0.7 + rnd.NextDouble() * 0.3));
                }
            return res;
        }

        // ---------- siatki dróg i wody ----------
        static Material RoadMat(string name, string tex)
        {
            string path = PGEN + "M_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            const string T = "Assets/_Mleczarka/Art/Textures/Teren/";
            var bm = AssetDatabase.LoadAssetAtPath<Texture2D>(T + tex + "_BaseColor.png");
            m.SetTexture("_BaseMap", bm); m.SetTexture("_MainTex", bm);
            var nm = AssetDatabase.LoadAssetAtPath<Texture2D>(T + tex + "_Normal.png");
            if (nm) { m.SetTexture("_BumpMap", nm); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.6f); }
            m.SetFloat("_Smoothness", name == "Asfalt" ? 0.25f : 0.1f);
            m.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.92f));
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void BuildRoadMeshes(Transform parent)
        {
            var mats = new Dictionary<RoadType, Material> { [RoadType.Asfalt] = RoadMat("Asfalt", "T_Droga_Asfalt"), [RoadType.Szuter] = RoadMat("Szuter", "T_Droga_Szuter"), [RoadType.Lesna] = RoadMat("Lesna", "T_Droga_Lesna") };
            var root = new GameObject("Drogi").transform; root.SetParent(parent);
            int ri = 0;
            foreach (var r in _roads.OrderBy(r => r.type == RoadType.Asfalt ? 2 : r.type == RoadType.Szuter ? 1 : 0))
            {
                ri++;
                float hw = HalfWidth(r.type), lift = (r.type == RoadType.Asfalt ? 0.06f : r.type == RoadType.Szuter ? 0.045f : 0.03f) + ri * 0.0015f;
                var go = new GameObject(r.name); go.transform.SetParent(root);
                const int Chunk = 60;
                float dist = 0;
                for (int s = 0; s < r.p.Count - 1; s += Chunk)
                {
                    int e = Mathf.Min(r.p.Count - 1, s + Chunk);
                    var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); var nrm = new List<Vector3>();
                    float d0 = dist;
                    for (int i = s; i <= e; i++)
                    {
                        if (i > s) d0 += (r.p[i] - r.p[i - 1]).magnitude;
                        var dir = (r.p[Mathf.Min(r.p.Count - 1, i + 1)] - r.p[Mathf.Max(0, i - 1)]).normalized; var n = new Vector2(-dir.y, dir.x);
                        // krawędzie przy samym terenie, środek na wysokości drogi
                        for (int c = 0; c < 3; c++)
                        {
                            float off = (c - 1) * hw; var q = r.p[i] + n * off;
                            float y = c == 1 ? r.h[i] : Mathf.Min(r.h[i], HAt(q.x, q.y) + 0.0f);
                            if (Mathf.Abs(y - r.h[i]) > 0.5f) y = r.h[i];   // nad rzeką krawędź jak środek (płyta mostu)
                            v.Add(new Vector3(q.x, y + lift, q.y)); uv.Add(new Vector2(c * 0.5f, d0 / (2f * hw))); nrm.Add(Vector3.up);
                        }
                        if (i > s) { int b = (i - s) * 3; tri.AddRange(new[] { b - 3, b - 2, b, b - 2, b + 1, b, b - 2, b - 1, b + 1, b - 1, b + 2, b + 1 }); }
                    }
                    dist = d0;
                    var mesh = new Mesh { name = r.name + "_" + s }; mesh.indexFormat = IndexFormat.UInt16;
                    mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
                    var part = new GameObject("odcinek " + s); part.transform.SetParent(go.transform);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = part.AddComponent<MeshRenderer>(); mr.sharedMaterial = mats[r.type]; mr.shadowCastingMode = ShadowCastingMode.Off;
                }
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                foreach (Transform t in go.transform) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        static void BuildRiverMesh(Transform parent)
        {
            string path = PGEN + "M_Woda.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(0.16f, 0.33f, 0.38f, 0.82f)); m.SetFloat("_Smoothness", 0.93f); m.SetFloat("_Metallic", 0.05f);
            var nm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Mleczarka/Art/Textures/Teren/T_Woda_Normal.png");
            if (nm) { m.SetTexture("_BumpMap", nm); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.35f); m.SetTextureScale("_BumpMap", new Vector2(1, 1)); }
            m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
            var go = new GameObject("Rzeka Olszówka"); go.transform.SetParent(parent);
            const int Chunk = 120; float dist = 0;
            for (int s = 0; s < _river.Count - 1; s += Chunk)
            {
                int e = Mathf.Min(_river.Count - 1, s + Chunk);
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
                for (int i = s; i <= e; i++)
                {
                    if (i > s) dist += (_river[i] - _river[i - 1]).magnitude;
                    var dir = (_river[Mathf.Min(_river.Count - 1, i + 1)] - _river[Mathf.Max(0, i - 1)]).normalized; var n = new Vector2(-dir.y, dir.x);
                    float w = 11.5f + Mathf.Sin(i * 0.013f) * 1.5f;
                    var a = _river[i] - n * w; var b = _river[i] + n * w;
                    v.Add(new Vector3(a.x, _riverWl[i], a.y)); v.Add(new Vector3(b.x, _riverWl[i], b.y));
                    uv.Add(new Vector2(0, dist / 18f)); uv.Add(new Vector2(w * 2 / 18f, dist / 18f));
                    if (i > s) { int k = (i - s) * 2; tri.AddRange(new[] { k - 2, k - 1, k, k - 1, k + 1, k }); }
                }
                var mesh = new Mesh { name = "Rzeka_" + s }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
                var part = new GameObject("odcinek " + s); part.transform.SetParent(go.transform);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = part.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            var sc = go.AddComponent<ScrollingTexture>(); sc.material = m; sc.speed = new Vector2(0, -0.04f);
        }

        static void BuildBridges(Transform parent)
        {
            var root = new GameObject("Mosty").transform; root.SetParent(parent);
            foreach (var (p, dir) in _bridges)
            {
                var road = _roads.OrderBy(r => r.p.Min(q => (q - p).sqrMagnitude)).First();
                int idx = 0; float bd = 1e9f; for (int i = 0; i < road.p.Count; i++) { float d = (road.p[i] - p).sqrMagnitude; if (d < bd) { bd = d; idx = i; } }
                float h = road.h[idx], hw = HalfWidth(road.type);
                var b = new GameObject("Most").transform; b.SetParent(root);
                b.position = new Vector3(p.x, h, p.y); b.rotation = Quaternion.Euler(0, dir, 0);
                var col = b.gameObject.AddComponent<BoxCollider>(); col.size = new Vector3(hw * 2 + 0.6f, 0.8f, 30f); col.center = new Vector3(0, -0.36f, 0);
                for (int s = -1; s <= 1; s += 2)
                    for (float z = -12f; z <= 12f; z += 4f)
                    {
                        var rail = Place(PW + "Most_Balustrada.fbx", b, new Vector3(s * (hw + 0.25f), 0.02f, z), 0f);
                        if (rail) rail.transform.localRotation = Quaternion.identity;
                    }
                // belki pod płytą
                var deck = GameObject.CreatePrimitive(PrimitiveType.Cube); deck.name = "Płyta mostu"; Object.DestroyImmediate(deck.GetComponent<Collider>());
                deck.transform.SetParent(b, false); deck.transform.localPosition = new Vector3(0, -0.45f, 0); deck.transform.localScale = new Vector3(hw * 2 + 0.6f, 0.8f, 26f);
                deck.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(PGEN + "M_Szuter.mat");
                MakeStatic(b.gameObject);
            }
        }

        // ---------- zabudowa ----------
        static WorldPlace PZone(Transform parent, string name, PlaceKind kind, Vector2 pos, float r, int farm = -1)
        {
            var z = new GameObject("Strefa: " + name).AddComponent<WorldPlace>();
            z.transform.SetParent(parent); z.transform.position = P3(pos);
            z.kind = kind; z.radius = r; z.farmId = farm; z.displayName = name;
            return z;
        }

        static GameObject PPlace(string file, Transform parent, Vector2 pos, float yaw, float scale = 1f, float dy = 0f)
        {
            var go = Place(file, parent, Vector3.zero, yaw, scale);
            if (go) go.transform.position = P3(pos, dy);
            return go;
        }

        static void BuildVillages(Transform parent)
        {
            var rnd = new System.Random(77);
            string[] chaty = { "Chata_Podlaska_A", "Chata_Podlaska_B", "Chata_Podlaska_C" };
            var root = new GameObject("Wsie").transform; root.SetParent(parent);
            var vroots = _villages.Concat(_hamlets).ToDictionary(v => v, v => { var t = new GameObject((v.hamlet ? "Przysiółek " : "Wieś ") + v.name).transform; t.SetParent(root); return t; });
            var kol = new GameObject("Kolonia Kirylukowie").transform; kol.SetParent(root);
            foreach (var lot in _lots)
            {
                var vr = lot.v != null ? vroots[lot.v] : kol;
                var house = PPlace(PW + chaty[rnd.Next(chaty.Length)] + ".fbx", vr, lot.pos, lot.yaw);
                if (house) foreach (var mf in house.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>();
                var q = Quaternion.Euler(0, lot.yaw, 0);
                Vector2 Local(float x, float z) { var w = q * new Vector3(x, 0, z); return lot.pos + new Vector2(w.x, w.z); }
                // stodoła za domem, bokiem
                var barn = PPlace(PW + "Stodola.fbx", vr, Local(lot.barnRight ? 9f : -9f, -16f), lot.yaw + 90f);
                if (barn) foreach (var mf in barn.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>();
                // płot sztachetowy od ulicy z bramą
                string plot = rnd.NextDouble() < 0.5 ? "Plot_Sztachety_Niebieski" : "Plot_Sztachety_Zielony";
                for (float x = -12f; x <= 12f; x += 2f)
                {
                    if (x > 2.5f && x < 8.5f) continue;   // brama na podwórze
                    var f = PPlace(PW + plot + ".fbx", vr, Local(x, 9.5f), lot.yaw + 90f);
                    if (f) f.AddComponent<BoxCollider>();
                }
                if (rnd.NextDouble() < 0.3) { var w = PPlace(PW + "Studnia_Zuraw.fbx", vr, Local(lot.barnRight ? -7f : 7f, 4f), lot.yaw + rnd.Next(360)); if (w) w.AddComponent<BoxCollider>(); }
                if (lot.farm >= 0)
                {
                    string fname = GameData.Farms[lot.farm].Name;
                    var gate = Local(5.5f, 15f);
                    PZone(vr, fname, PlaceKind.Gospodarstwo, gate, 13f, lot.farm);
                    var silo = PPlace(PW + "Silos_Mleka.fbx", vr, Local(lot.barnRight ? -9f : 9f, -6f), lot.yaw, 0.45f);
                    if (silo) foreach (var mf in silo.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>();
                }
            }
            foreach (var v in _villages)
            {
                var vr = vroots[v]; var ax = v.Axis; var n = new Vector2(ax.y, -ax.x);
                // tablice na wjazdach (po prawej stronie, przodem do nadjeżdżających)
                PPlace(PW + "Tablica_" + v.sign + ".fbx", vr, v.A - ax * 25f + n * 6f, Mathf.Atan2(-ax.x, -ax.y) * Mathf.Rad2Deg);
                PPlace(PW + "Tablica_" + v.sign + ".fbx", vr, v.B + ax * 25f - n * 6f, Mathf.Atan2(ax.x, ax.y) * Mathf.Rad2Deg);
                var k1 = PPlace(PW + "Kapliczka.fbx", vr, v.A - ax * 10f - n * 7f, Mathf.Atan2(n.x, n.y) * Mathf.Rad2Deg); if (k1) k1.AddComponent<BoxCollider>();
                var k2 = PPlace(PW + "Krzyz_Przydrozny.fbx", vr, v.B + ax * 8f + n * 8f, Mathf.Atan2(-n.x, -n.y) * Mathf.Rad2Deg); if (k2) k2.AddComponent<BoxCollider>();
                MakeStatic(vr.gameObject);
            }
            foreach (var hv in _hamlets)
            {
                var vr = vroots[hv]; var r = hv.road;
                for (int e = 0; e < 2; e++)
                {
                    int i = e == 0 ? Mathf.Max(1, hv.i0 - 10) : Mathf.Min(r.p.Count - 2, hv.i1 + 10);
                    var d = (r.p[i + 1] - r.p[i - 1]).normalized; if (e == 0) d = -d;      // kierunek wyjazdu z przysiółka
                    var n = new Vector2(-d.y, d.x);                                           // lewa strona patrząc na wyjazd = prawa dla wjeżdżających
                    PPlace(PW + "Tablica_" + hv.sign + ".fbx", vr, r.p[i] + n * (HalfWidth(r.type) + 3f), Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg);
                }
                if (hv.name == "Krzywa" || hv.name == "Grabowiec") { var c = PPlace(PW + "Krzyz_Przydrozny.fbx", vr, r.p[hv.i1] + new Vector2(6, 6), 30f); if (c) c.AddComponent<BoxCollider>(); }
                MakeStatic(vr.gameObject);
            }
            MakeStatic(kol.gameObject);
        }

        static void BuildDairyAndTown(Transform parent)
        {
            // mleczarnia
            var d = new GameObject("Mleczarnia").transform; d.SetParent(parent);
            PZone(d, "Mleczarnia", PlaceKind.Mleczarnia, _dairy + new Vector2(0, 6), 26f);
            var hallRoot = new GameObject("Hala").transform; hallRoot.SetParent(d);
            BuildHouse(hallRoot, P3(_dairy + new Vector2(-24, 14)), 90f);
            hallRoot.GetChild(0).localScale = Vector3.one * _kVillage * 2.4f;
            var mag = PPlace(PW + "Stodola.fbx", d, _dairy + new Vector2(-22, -18), 90f, 1.1f); if (mag) AddMeshColliders(mag);
            for (int i = 0; i < 2; i++) { var s = PPlace(WORLD + "Silos_Mleka.fbx", d, _dairy + new Vector2(-34 + i * 7, 34), 0f, 1f); if (s) AddMeshColliders(s); }
            var sign = PPlace(WORLD + "Szyld_Mleczarnia.fbx", d, _dairy + new Vector2(16, 30), 200f, 1f); if (sign) AddMeshColliders(sign);
            for (int c = 0; c < 6; c++) PPlace(VIL + "Prop_Crate.fbx", d, _dairy + new Vector2(-12 + c * 1.2f, -8), c * 37f, _kVillage * 0.8f);
            MakeStatic(d.gameObject);

            // miasteczko Zabielsk z rynkiem
            var t = new GameObject("Zabielsk").transform; t.SetParent(parent);
            PZone(t, "Rynek w Zabielsku", PlaceKind.Rynek, _town, 30f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 20f; var p = _town + new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * 14f;
                var st = PPlace(WORLD + "Stragan_Rynek.fbx", t, p, a + 180f); if (st) AddMeshColliders(st);
            }
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f + 15f; var p = _town + new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * 46f;
                if (Nearest(_roadDist, p.x, p.y) < 8f) continue;
                var hroot = new GameObject("Kamienica").transform; hroot.SetParent(t);
                BuildHouse(hroot, P3(p), a + 180f);
                hroot.GetChild(0).localScale = Vector3.one * _kVillage * 1.25f;
            }
            PPlace(PW + "Tablica_Zabielsk.fbx", t, _town + new Vector2(18, 70), 180f);
            PPlace(PW + "Tablica_Zabielsk.fbx", t, _town + new Vector2(-70, -12), 60f);
            MakeStatic(t.gameObject);

            // Mleczpol
            var m = new GameObject("Mleczpol (rywal)").transform; m.SetParent(parent);
            var mh = new GameObject("Hala Mleczpolu").transform; mh.SetParent(m);
            BuildHouse(mh, P3(_rival + new Vector2(-20, 0)), 90f);
            mh.GetChild(0).localScale = Vector3.one * _kVillage * 2.2f;
            for (int i = 0; i < 3; i++) { var s = PPlace(WORLD + "Silos_Mleka.fbx", m, _rival + new Vector2(-10 + i * 7, -26), 0, 0.9f); if (s) AddMeshColliders(s); }
            MakeStatic(m.gameObject);
        }

        // ---------- mapa do interfejsu ----------
        static Texture2D BakeMap()
        {
            const int S = 2048; var tex = new Texture2D(S, S, TextureFormat.RGB24, false);
            var px = new Color[S * S]; float cs = 2 * Half / S;
            Color grass = new Color(0.56f, 0.72f, 0.40f), meadow = new Color(0.62f, 0.78f, 0.45f), forest = new Color(0.25f, 0.45f, 0.26f), water = new Color(0.36f, 0.62f, 0.82f);
            var cropCol = new Dictionary<Crop, Color> { [Crop.Pszenica] = new Color(0.92f, 0.80f, 0.45f), [Crop.Kukurydza] = new Color(0.42f, 0.66f, 0.28f), [Crop.Rzepak] = new Color(0.98f, 0.88f, 0.25f), [Crop.Ziemniaki] = new Color(0.55f, 0.62f, 0.35f), [Crop.Laka] = meadow, [Crop.Orne] = new Color(0.62f, 0.48f, 0.34f) };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float wx = -Half + (x + 0.5f) * cs, wz = -Half + (y + 0.5f) * cs;
                    int k = Idx(Mathf.Clamp(Mathf.RoundToInt((wz + Half) / Step), 0, GN - 1), Mathf.Clamp(Mathf.RoundToInt((wx + Half) / Step), 0, GN - 1));
                    Color c = Sample(_riverDist, wx, wz) < 230 ? Color.Lerp(grass, meadow, 0.6f) : grass;
                    var crop = CropAt(k); if ((int)crop >= 0) c = cropCol[crop];
                    if (_forest[k] > 0.5f) c = forest;
                    if (_riverDist[k] < 10f) c = water;
                    if (_plazaW[k] > 0.5f) c = new Color(0.80f, 0.74f, 0.62f);
                    float shade = 1f + (HAt(wx + 8, wz + 8) - HAt(wx - 8, wz - 8)) * 0.02f;
                    px[y * S + x] = c * Mathf.Clamp(shade, 0.85f, 1.15f);
                }
            void Line(Vector2 a, Color col, float rad)
            {
                int cx = Mathf.RoundToInt((a.x + Half) / cs), cy = Mathf.RoundToInt((a.y + Half) / cs); int r = Mathf.CeilToInt(rad);
                for (int y = cy - r; y <= cy + r; y++) for (int x = cx - r; x <= cx + r; x++)
                        if (x >= 0 && y >= 0 && x < S && y < S && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= rad * rad) px[y * S + x] = col;
            }
            foreach (var r in _roads.OrderBy(r => r.type == RoadType.Asfalt ? 2 : r.type == RoadType.Szuter ? 1 : 0))
            {
                Color col = r.type == RoadType.Asfalt ? new Color(0.98f, 0.98f, 0.96f) : r.type == RoadType.Szuter ? new Color(0.95f, 0.88f, 0.70f) : new Color(0.82f, 0.70f, 0.50f);
                float rad = r.type == RoadType.Asfalt ? 3.4f : r.type == RoadType.Szuter ? 2.6f : 1.8f;
                for (int i = 0; i < r.p.Count; i += 2) Line(r.p[i], new Color(0.35f, 0.32f, 0.28f), rad + 1f);
                for (int i = 0; i < r.p.Count; i += 2) Line(r.p[i], col, rad);
            }
            foreach (var lot in _lots) Line(lot.pos, new Color(0.62f, 0.32f, 0.22f), 3.0f);
            tex.SetPixels(px); tex.Apply();
            Directory.CreateDirectory("Assets/_Mleczarka/UI");
            string path = "Assets/_Mleczarka/UI/Mapa_Gminy.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.isReadable = false; ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- czasy przejazdu (graf dróg) ----------
        static void MeasureTravel()
        {
            // węzły: punkty dróg łączone, gdy leżą bliżej niż 6 m (skrzyżowania i końce)
            var pts = new List<Vector2>(); var adj = new List<List<(int, float, RoadType)>>();
            int Node(Vector2 p) { for (int i = 0; i < pts.Count; i++) if ((pts[i] - p).sqrMagnitude < 36f) return i; pts.Add(p); adj.Add(new List<(int, float, RoadType)>()); return pts.Count - 1; }
            foreach (var r in _roads)
            {
                int prev = Node(r.p[0]); float acc = 0;
                for (int i = 1; i < r.p.Count; i++)
                {
                    acc += (r.p[i] - r.p[i - 1]).magnitude;
                    if (i % 10 == 0 || i == r.p.Count - 1) { int cur = Node(r.p[i]); adj[prev].Add((cur, acc, r.type)); adj[cur].Add((prev, acc, r.type)); prev = cur; acc = 0; }
                }
            }
            float Speed(RoadType t) => t == RoadType.Asfalt ? 60f / 3.6f : t == RoadType.Szuter ? 45f / 3.6f : 30f / 3.6f;
            int start = Node(_dairy); var time = Enumerable.Repeat(float.MaxValue, pts.Count).ToArray(); var dist = new float[pts.Count]; time[start] = 0;
            var open = new SortedSet<(float, int)> { (0, start) };
            while (open.Count > 0)
            {
                var (t0, u) = open.Min; open.Remove(open.Min);
                foreach (var (v, len, type) in adj[u])
                {
                    float t = t0 + len / Speed(type);
                    if (t < time[v]) { open.Remove((time[v], v)); time[v] = t; dist[v] = dist[u] + len; open.Add((t, v)); }
                }
            }
            string Fmt(int i) => time[i] == float.MaxValue ? "brak drogi" : $"{dist[i] / 1000f:0.0} km, ok. {time[i] / 60f:0.0} min";
            _report.AppendLine("Czasy przejazdu z mleczarni (asfalt 60, szuter 45, leśna 30 km/h):");
            int Closest(Vector2 p) { int b = 0; float bd = 1e9f; for (int i = 0; i < pts.Count; i++) { float d = (pts[i] - p).sqrMagnitude; if (d < bd && time[i] < float.MaxValue) { bd = d; b = i; } } return b; }
            foreach (var lot in _lots.Where(l => l.farm >= 0)) _report.AppendLine($"  {GameData.Farms[lot.farm].Name} ({(lot.v != null ? lot.v.name : "kolonia")}): {Fmt(Closest(lot.pos))}");
            _report.AppendLine($"  Rynek w Zabielsku: {Fmt(Closest(_town))}");
            _report.AppendLine($"  Mleczpol: {Fmt(Closest(_rival))}");
            int far = Enumerable.Range(0, pts.Count).Where(i => time[i] < float.MaxValue).OrderByDescending(i => time[i]).First();
            _report.AppendLine($"  Najdalszy punkt drogi ({pts[far].x:0}, {pts[far].y:0}): {Fmt(far)}");
            float total = _roads.Sum(r => { float s = 0; for (int i = 1; i < r.p.Count; i++) s += (r.p[i] - r.p[i - 1]).magnitude; return s; });
            _report.AppendLine($"Długość dróg: {total / 1000f:0.0} km (asfalt {_roads.Where(r => r.type == RoadType.Asfalt).Sum(r => Len(r)) / 1000f:0.0}, szuter {_roads.Where(r => r.type == RoadType.Szuter).Sum(r => Len(r)) / 1000f:0.0}, leśne {_roads.Where(r => r.type == RoadType.Lesna).Sum(r => Len(r)) / 1000f:0.0}).");
        }
        static float Len(Road r) { float s = 0; for (int i = 1; i < r.p.Count; i++) s += (r.p[i] - r.p[i - 1]).magnitude; return s; }

        // ---------- główne menu ----------
        [MenuItem("Mleczarka/5. Zbuduj gminę: Podlasie 5×5 km", priority = 5)]
        public static void BuildPodlasie()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _report = new StringBuilder("[Mleczarka] Gmina Podlasie\n");
            try
            {
                EditorUtility.DisplayProgressBar("Gmina", "Materiały", 0.02f);
                QuaterniusMaterials.SetupAll();
                Directory.CreateDirectory(PGEN); AssetDatabase.Refresh();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _rng = new System.Random(5);
                MeasureKits();

                EditorUtility.DisplayProgressBar("Gmina", "Drogi, rzeka, wsie", 0.08f);
                DefineLayout(); DefineRiver(); LayoutVillages();
                EditorUtility.DisplayProgressBar("Gmina", "Ukształtowanie terenu", 0.15f);
                ComputeTerrain();
                EditorUtility.DisplayProgressBar("Gmina", "Pola i lasy", 0.3f);
                LayoutFields(); ComputeForest();
                _report.AppendLine($"Pola: {_fields.Count} ({string.Join(", ", System.Enum.GetValues(typeof(Crop)).Cast<Crop>().Select(c => $"{c} {_fields.Count(f => f.crop == c)}"))}); domy: {_lots.Count}; mosty: {_bridges.Count}.");

                var env = new GameObject("Teren Gminy");
                LightingSetup.Apply(LightingSetup.Pora.Dzien);
                new GameObject("Odświeżanie otoczenia").AddComponent<Mleczarka.EnvironmentRefresh>();
                EditorUtility.DisplayProgressBar("Gmina", "Kafle terenu, uprawy, drzewa (to trwa)", 0.4f);
                var tiles = BuildTiles(env.transform);
                int treeCount = 0; foreach (var t in tiles) treeCount += t.terrainData.treeInstanceCount;
                _report.AppendLine($"Drzewa: {treeCount}.");
                EditorUtility.DisplayProgressBar("Gmina", "Drogi i rzeka", 0.75f);
                BuildRoadMeshes(env.transform); BuildRiverMesh(env.transform); BuildBridges(env.transform);
                EditorUtility.DisplayProgressBar("Gmina", "Wsie, mleczarnia, miasteczko", 0.82f);
                var places = new GameObject("Miejsca").transform;
                BuildVillages(places); BuildDairyAndTown(places);

                EditorUtility.DisplayProgressBar("Gmina", "Mapa i gracz", 0.9f);
                var info = new GameObject("Mapa gminy").AddComponent<MapInfo>();
                info.mapTexture = BakeMap(); info.worldMin = new Vector2(-Half, -Half); info.worldSize = 2 * Half;
                foreach (var v in _villages) info.labels.Add(new MapInfo.Label { name = v.name, pos = v.c, kind = 0 });
                foreach (var r in _roads)
                {
                    var pts = new List<Vector3>();
                    for (int i = 0; i < r.p.Count; i += 4) pts.Add(new Vector3(r.p[i].x, r.h[i], r.p[i].y));
                    pts.Add(new Vector3(r.p[r.p.Count - 1].x, r.h[r.p.Count - 1], r.p[r.p.Count - 1].y));
                    info.roads.Add(new MapInfo.RoadPath { name = r.name, type = (int)r.type, points = pts.ToArray() });
                }
                foreach (var hv in _hamlets) info.labels.Add(new MapInfo.Label { name = hv.name, pos = hv.c, kind = 4 });
                info.labels.Add(new MapInfo.Label { name = "Zabielsk", pos = _town, kind = 1 });
                info.labels.Add(new MapInfo.Label { name = "Mleczarnia", pos = _dairy, kind = 2 });
                info.labels.Add(new MapInfo.Label { name = "Mleczpol", pos = _rival, kind = 3 });

                VehicleFactory.BuildInScene(VehicleType.Cysterna, "Cysterna gracza", P3(_dairy + new Vector2(10, -6), 0.3f), 90f);
                VehicleFactory.BuildInScene(VehicleType.Dostawczak, "Dostawczak gracza", P3(_dairy + new Vector2(10, -16), 0.3f), 90f);
                var farmer = BuildFarmer(P3(_dairy + new Vector2(4, -10)), 90f);
                BuildCamera(farmer);
                var cam = Object.FindFirstObjectByType<Camera>(); if (cam) cam.farClipPlane = 1800f;
                BuildGarageUI(farmer);
                var garageDoc = Object.FindFirstObjectByType<Mleczarka.UI.GarageUI>().GetComponent<UnityEngine.UIElements.UIDocument>();
                garageDoc.sortingOrder = 10;
                var gc = new GameObject("Gra (symulacja)").AddComponent<GameController>();
                gc.player = farmer.GetComponent<FarmerController>();
                var hud = new GameObject("HUD (UI)");
                var hudDoc = hud.AddComponent<UnityEngine.UIElements.UIDocument>();
                hudDoc.panelSettings = garageDoc.panelSettings; hudDoc.sortingOrder = 0;
                hud.AddComponent<Mleczarka.UI.HudUI>();
                var mapUi = new GameObject("Mapa (UI)");
                var mapDoc = mapUi.AddComponent<UnityEngine.UIElements.UIDocument>();
                mapDoc.panelSettings = garageDoc.panelSettings; mapDoc.sortingOrder = 5;
                mapUi.AddComponent<Mleczarka.UI.MapUI>();
                LightingSetup.Apply(LightingSetup.Pora.Dzien);   // mgła w skali dużej mapy (teren już istnieje)
                var probe = Object.FindFirstObjectByType<ReflectionProbe>(); if (probe) probe.transform.position = P3(_dairy, 4f);

                MeasureTravel();
                EditorSceneManager.SaveScene(scene, PodlasieScenePath);
                var list = EditorBuildSettings.scenes.Where(s => s.path != PodlasieScenePath).ToList();
                list.Insert(0, new EditorBuildSettingsScene(PodlasieScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
                _report.AppendLine($"Zbudowano w {sw.Elapsed.TotalSeconds:0} s: {PodlasieScenePath}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Debug.Log(_report.ToString());
                Directory.CreateDirectory("Screenshots");
                File.WriteAllText("Screenshots/gmina_raport.txt", _report.ToString());
                _base = _h = _roadW = _roadH = _roadDist = _plazaW = _riverDist = _forest = null; _field = null; _roadKind = null;
            }
        }
    }
}
