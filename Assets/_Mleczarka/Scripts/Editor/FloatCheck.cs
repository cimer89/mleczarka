using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.EditorTools
{
    /// <summary>
    /// Pomiar (nic nie zmienia): drogi bez kolizji, płoty/balustrady/poręcze wiszące nad terenem lub zakopane,
    /// przerwy między przęsłami płotu, maszyny i przedmioty w halach unoszące się nad podłogą, drzewa w terenie.
    /// Raport: Screenshots/pomiar_lewitowanie.txt
    /// </summary>
    public static class FloatCheck
    {
        static readonly string[] Hanging = { "Rura", "Wieszak", "Oprawa", "Tabliczka", "Tablica", "Linka", "Światło", "Dach", "Kalenica", "Dźwigar", "Belka", "Szkl", "Szyba", "Okn", "Rama", "Szpros", "Nadproże", "Lampka", "Kamera", "CameraTarget", "Napis", "Szyld", "Rurociąg", "Kolano", "Rąbek", "Pas ", "Spoina", "Znacznik", "Pole obsługi", "Lico", "Ościeżnica", "Wejście", "Wyjście", "Próg", "Posadzka", "Kanał", "Panel", "Wyświetlacz", "Ekran", "screen", "window", "Sufit", "Strop" };

        public static float TerrainY(Vector3 p)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var o = t.transform.position; var s = t.terrainData.size;
                if (p.x >= o.x && p.z >= o.z && p.x <= o.x + s.x && p.z <= o.z + s.z) return t.SampleHeight(p) + o.y;
            }
            return float.NaN;
        }

        static Bounds? RBounds(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>().Where(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)).ToArray();
            if (rs.Length == 0) return null;
            var b = rs[0].bounds; foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds); return b;
        }

        [MenuItem("Mleczarka/Testy/Pomiar: lewitowanie i zapadanie (bez zmian)")]
        public static void Run()
        {
            var log = new StringBuilder();
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            // 1. drogi
            var roads = all.Where(t => t.name.StartsWith("odcinek") && t.GetComponent<MeshFilter>()).ToArray();
            log.AppendLine($"DROGI: {roads.Length} odcinków, z kolizją: {roads.Count(t => t.GetComponent<Collider>())}");
            float maxLift = 0, sumLift = 0; int nl = 0;
            foreach (var r in roads.Take(400))
            {
                var m = r.GetComponent<MeshFilter>().sharedMesh; if (!m) continue; var v = m.vertices;
                for (int i = 1; i < v.Length; i += 3 * 7) { var w = r.TransformPoint(v[i]); float ty = TerrainY(w); if (float.IsNaN(ty)) continue; float d = w.y - ty; sumLift += d; nl++; if (d > maxLift) maxLift = d; }
            }
            if (nl > 0) log.AppendLine($"  środek drogi nad terenem: średnio {sumLift / nl:0.000} m, maks. {maxLift:0.000} m");

            // 2. płoty i balustrady
            log.AppendLine();
            foreach (var key in new[] { "Plot_Sztachety", "Most_Balustrada", "Poręcz", "Bariera", "Barierka", "Prop_WoodenFence", "Prop_MetalFence" })
            {
                var items = all.Where(t => t.name.StartsWith(key) && (t.parent == null || !t.parent.name.StartsWith(key))).ToArray();
                if (items.Length == 0) continue;
                int floatN = 0, buryN = 0; float worst = 0, worstBury = 0; string worstName = "";
                float lenSum = 0; var gaps = new List<float>();
                foreach (var t in items)
                {
                    var b = RBounds(t.gameObject); if (b == null) continue;
                    var mf = t.GetComponentInChildren<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                    var lb = mf.sharedMesh.bounds;
                    // dłuższa oś przęsła w poziomie
                    Vector3 axis = lb.size.x >= lb.size.z ? Vector3.right * lb.extents.x : Vector3.forward * lb.extents.z;
                    Vector3 e1 = mf.transform.TransformPoint(lb.center + axis), e2 = mf.transform.TransformPoint(lb.center - axis);
                    lenSum += Vector3.Distance(e1, e2);
                    float bottom = b.Value.min.y;
                    float t1 = TerrainY(e1), t2 = TerrainY(e2), tc = TerrainY(b.Value.center);
                    if (float.IsNaN(t1) || float.IsNaN(t2)) continue;
                    float lowGround = Mathf.Min(t1, t2, tc), highGround = Mathf.Max(t1, t2, tc);
                    float gap = bottom - lowGround;           // wisi nad najniższym miejscem
                    float bury = highGround - bottom;          // zakopane w najwyższym miejscu
                    if (gap > 0.08f) { floatN++; if (gap > worst) { worst = gap; worstName = t.name + " @ " + t.position.ToString("F0"); } }
                    if (bury > 0.35f) { buryN++; worstBury = Mathf.Max(worstBury, bury); }
                }
                log.AppendLine($"{key}: {items.Length} szt., średnia długość przęsła {lenSum / Mathf.Max(1, items.Length):0.00} m; wiszące > 8 cm: {floatN} (najgorsze {worst:0.00} m: {worstName}); zakopane > 35 cm: {buryN} (maks. {worstBury:0.00} m)");
            }
            // przerwy między sąsiednimi przęsłami sztachet (na tej samej działce, co 2 m)
            {
                var pickets = all.Where(t => t.name.StartsWith("Plot_Sztachety")).ToArray();
                var mf = pickets.Select(p => p.GetComponentInChildren<MeshFilter>()).FirstOrDefault(m => m && m.sharedMesh);
                if (mf)
                {
                    var lb = mf.sharedMesh.bounds; float len = Mathf.Max(lb.size.x * mf.transform.lossyScale.x, lb.size.z * mf.transform.lossyScale.z);
                    log.AppendLine($"  przęsło sztachet: {len:0.00} m przy rozstawie 2,00 m → przerwa {2f - len:0.00} m");
                }
            }

            // 3. maszyny i przedmioty w halach
            log.AppendLine();
            log.AppendLine("HALE (dół obiektu nad podłogą pod nim, > 5 cm):");
            Physics.SyncTransforms();
            var halls = all.Where(t => t.parent == null && (t.name.Contains("Mleczarnia") || t.name.Contains("Skrzydło") || t.name.Contains("Hala") || t.name.Contains("hala") || t.name.Contains("Produkcja"))).ToArray();
            log.AppendLine("  korzenie: " + string.Join(", ", halls.Select(h => h.name)));
            var cands = new List<Transform>();
            foreach (var h in halls) foreach (Transform c in h) { cands.Add(c); foreach (Transform cc in c) cands.Add(cc); }
            cands.AddRange(all.Where(t => t.GetComponents<MonoBehaviour>().Any(m => m && (m.GetType().Name == "PlantMachineView" || m.GetType().Name.Contains("Station")))));
            var seen = new HashSet<Transform>(); int reported = 0;
            foreach (var t in cands.Distinct())
            {
                if (!seen.Add(t) || Hanging.Any(k => t.name.Contains(k))) continue;
                var b = RBounds(t.gameObject); if (b == null) continue;
                if (b.Value.size.y > 6f || b.Value.size.x > 12f || b.Value.size.z > 12f) continue; // ściany, posadzki
                var own = new HashSet<Collider>(t.GetComponentsInChildren<Collider>());
                var start = new Vector3(b.Value.center.x, b.Value.min.y + 0.02f, b.Value.center.z);
                var hits = Physics.RaycastAll(start, Vector3.down, 6f).Where(hh => !own.Contains(hh.collider) && !hh.collider.isTrigger).OrderBy(hh => hh.distance).ToArray();
                float floor = hits.Length > 0 ? hits[0].point.y : TerrainY(start);
                if (float.IsNaN(floor)) continue;
                float gap = b.Value.min.y - floor;
                if (gap > 0.05f && gap < 3f)
                {
                    reported++;
                    log.AppendLine($"  {Path(t)}: {gap:0.00} m nad „{(hits.Length > 0 ? hits[0].collider.name : "teren")}”");
                }
            }
            if (reported == 0) log.AppendLine("  brak");
            foreach (var t in all.Where(t => t.name.StartsWith("Chlodnia — ") || t.name.StartsWith("uht — ") || t.name.StartsWith("homogenizator — ")))
            {
                log.AppendLine($"  części {t.name} (y korzenia {t.position.y:0.000}):");
                foreach (var r in t.GetComponentsInChildren<Renderer>(true)) log.AppendLine($"    {r.name}: dół {r.bounds.min.y - t.position.y:0.000}, góra {r.bounds.max.y - t.position.y:0.000}, włączony {r.enabled && r.gameObject.activeInHierarchy}");
            }

            // 4. drzewa
            log.AppendLine();
            foreach (var t in Terrain.activeTerrains)
            {
                var td = t.terrainData;
                var counts = td.treeInstances.GroupBy(i => i.prototypeIndex).OrderBy(g => g.Key).Select(g => $"{(g.Key < td.treePrototypes.Length && td.treePrototypes[g.Key].prefab ? td.treePrototypes[g.Key].prefab.name : "?")}:{g.Count()}");
                log.AppendLine($"DRZEWA {t.name}: {td.treeInstances.Length} ({string.Join(", ", counts)})");
            }

            Directory.CreateDirectory("Screenshots");
            File.WriteAllText("Screenshots/pomiar_lewitowanie.txt", log.ToString());
            Debug.Log("[Mleczarka] Pomiar lewitowania: Screenshots/pomiar_lewitowanie.txt");
        }

        static string Path(Transform t) { var s = t.name; for (var p = t.parent; p; p = p.parent) s = p.name + "/" + s; return s; }
    }
}
