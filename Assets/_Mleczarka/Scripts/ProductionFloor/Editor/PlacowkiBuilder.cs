using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>
    /// Klienci na rynku i placówki z zamówień: nie stawia nowych budynków — wybiera istniejące domy wokół rynku,
    /// wiesza na nich szyld (szkoła, sklep, restauracja, przedszkole, kawiarnia, hotel), stawia odbiorcę przy wejściu
    /// i strefę dostawy. Można uruchamiać wielokrotnie (przebudowuje tylko swoje obiekty). Raport: Screenshots/placowki.txt
    /// </summary>
    public static class PlacowkiBuilder
    {
        const string RootName = "Sprzedaż z ręki — klienci i placówki";
        static readonly string[] Signs = { "Placowka_Szkola", "Placowka_Sklep", "Placowka_Restauracja", "Placowka_Przedszkole", "Placowka_Kawiarnia", "Placowka_Hotel" };
        static readonly string[] Skip = { "Mleczarnia", "Skrzydło", "Skrzydlo", "Szopa", "Hala", "Most", "Silos", "Teren", "Droga", "odcinek", "Rampa", "Stodo", "Garaż", "Garaz", "Pojazd", "Cysterna", "Dostawczak", "Ciągnik", "Kuchnia", "Dom_Letnia" };

        [MenuItem("Mleczarka/Kariera/4. Klienci na rynku i placówki")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("[Mleczarka] Zatrzymaj tryb gry."); return; }
            var log = new StringBuilder();
            var market = Object.FindObjectsByType<WorldPlace>(FindObjectsSortMode.None).FirstOrDefault(w => w.kind == PlaceKind.Rynek);
            if (!market) { Debug.LogError("[Mleczarka] Brak rynku w scenie."); return; }
            var old = GameObject.Find(RootName); if (old) Object.DestroyImmediate(old);
            var root = new GameObject(RootName).transform;
            var hs = root.gameObject.AddComponent<HandSales>(); hs.customerReach = 3.6f;
            hs.npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Mleczarka/Prefabs/Production/Pracownik.prefab");
            const string K = "Assets/_Mleczarka/Art/ThirdParty/Kenney/FoodKit/Models/FBX format/";
            hs.cartonModel = AssetDatabase.LoadAssetAtPath<GameObject>(K + "carton-small.fbx");
            hs.cupModel = AssetDatabase.LoadAssetAtPath<GameObject>(K + "cup.fbx");
            hs.cheeseModel = AssetDatabase.LoadAssetAtPath<GameObject>(K + "cheese.fbx");
            hs.cheeseCutModel = AssetDatabase.LoadAssetAtPath<GameObject>(K + "cheese-cut.fbx");
            Physics.SyncTransforms();

            // ---- klienci: za straganami na rynku (gracz podchodzi od strony placu) ----
            var c = market.transform.position; var spots = new List<Transform>(); float serveReach = 3.6f;
            var stalls = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Stragan_Rynek") && (t.parent == null || !t.parent.name.StartsWith("Stragan_Rynek")) && Flat(t.position - c) < market.radius + 10f)
                .OrderBy(t => Mathf.Atan2(t.position.x - c.x, t.position.z - c.z)).ToList();
            foreach (var st in stalls)
            {
                var rs = st.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var sb = rs[0].bounds; foreach (var r in rs) sb.Encapsulate(r.bounds);
                var outward = sb.center - c; outward.y = 0; outward.Normalize();
                float along = Mathf.Abs(Vector3.Dot(sb.extents, new Vector3(Mathf.Abs(outward.x), 0, Mathf.Abs(outward.z))));
                // lada zmierzona promieniami na wysokości 0,3–1,4 m (bez daszku): tył (strona sprzedawcy) i przód (strona placu)
                var cols = st.GetComponentsInChildren<Collider>();
                var mid = new Vector3(sb.center.x, 0, sb.center.z); float gy = Ground(mid).y;
                float back = along, front = along; bool measured = false;
                if (cols.Length > 0)
                {
                    back = 0; front = 0;
                    foreach (var h in new[] { 0.3f, 0.6f, 0.9f, 1.2f, 1.4f })
                        foreach (var side in new[] { -0.4f, 0f, 0.4f })
                        {
                            var lat = Vector3.Cross(Vector3.up, outward) * side;
                            var o = new Vector3(mid.x, gy + h, mid.z) + lat;
                            foreach (var col in cols)
                            {
                                if (col.Raycast(new Ray(o + outward * 12f, -outward), out var hb, 12f)) { back = Mathf.Max(back, 12f - hb.distance); measured = true; }
                                if (col.Raycast(new Ray(o - outward * 12f, outward), out var hf, 12f)) { front = Mathf.Max(front, 12f - hf.distance); measured = true; }
                            }
                        }
                    if (!measured) { back = along; front = along; }
                }
                var p = Ground(mid + outward * (back + 0.45f));
                var s = new GameObject("Miejsce klienta " + (spots.Count + 1)).transform; s.SetParent(root, false);
                s.position = p; s.rotation = Quaternion.LookRotation(-outward); spots.Add(s);
                serveReach = Mathf.Max(serveReach, back + 0.45f + front + 1.1f);
                log.AppendLine($"  stragan {st.name}: głębokość z daszkiem {along * 2:0.0} m, lada: tył {back:0.00} m, przód {front:0.00} m od środka (pomiar promieniem: {measured})");
            }
            if (spots.Count == 0)
                for (int a = 0; a < 24 && spots.Count < 3; a++)
                {
                    var dir = Quaternion.Euler(0, a * 47f, 0) * Vector3.forward;
                    var p = Ground(c + dir * 4.0f);
                    if (Blocked(p) || spots.Any(x => Vector3.Distance(x.position, p) < 2.5f)) continue;
                    var s = new GameObject("Miejsce klienta " + (spots.Count + 1)).transform; s.SetParent(root, false);
                    s.position = p; s.rotation = Quaternion.LookRotation(-dir); spots.Add(s);
                }
            hs.marketSpots = spots.ToArray(); hs.customerReach = serveReach;
            log.AppendLine($"Rynek: {market.displayName} ({c:F0}), promień {market.radius}; stragany: {stalls.Count}, miejsca klientów za straganami: {spots.Count}, zasięg podania {serveReach:0.0} m");

            // ---- placówki: istniejące budynki rozrzucone po całej gminie, przy drogach ----
            var zones = Object.FindObjectsByType<WorldPlace>(FindObjectsSortMode.None).Where(w => w.kind != PlaceKind.Placowka).ToList();
            var cands = new List<(Bounds b, string name, Vector3 road)>();
            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (col.isTrigger || col is TerrainCollider || col is WheelCollider) continue;
                var b = col.bounds;
                if (b.size.y < 3f || b.size.y > 16f) continue;
                float w = Mathf.Max(b.size.x, b.size.z), n = Mathf.Min(b.size.x, b.size.z);
                if (w < 6f || w > 26f || n < 4.5f) continue;
                string path = Path(col.transform);
                if (Skip.Any(k => path.Contains(k))) continue;
                // nie w gospodarstwie, mleczarni, domu gracza ani na samym rynku
                if (zones.Any(z => Flat(z.transform.position - b.center) < z.radius + (z.kind == PlaceKind.Rynek ? 9f : 6f))) continue;
                if (cands.Any(x => Flat(x.b.center - b.center) < 7f)) continue;
                // dojazd: najbliższy odcinek drogi w promieniu 35 m
                Vector3? road = null; float best = 35f;
                foreach (var rc in Physics.OverlapSphere(b.center, 35f))
                {
                    if (!rc.name.StartsWith("odcinek") || !(rc is MeshCollider)) continue;
                    var cp = rc.ClosestPoint(b.center); float d = Flat(cp - b.center);
                    if (d < best) { best = d; road = cp; }
                }
                if (road == null) continue;
                cands.Add((b, path, road.Value));
            }
            // rozrzut: najwyżej jedna placówka na miejscowość; pierwsza w miasteczku przy rynku,
            // każda kolejna w miejscowości najdalej od już wybranych (żeby dostawy prowadziły w różne strony gminy)
            string Village(string path) { var parts = path.Split('/'); return parts.Length > 2 && parts[1] == "Wsie" ? parts[2] : (parts.Length > 1 ? parts[1] : parts[0]); }
            var groups = cands.GroupBy(x => Village(x.name)).Select(gr => gr.OrderBy(x => Flat(x.road - x.b.center)).First()).ToList();
            log.AppendLine("Miejscowości z budynkami przy drodze: " + string.Join(", ", cands.GroupBy(x => Village(x.name)).Select(gr => $"{gr.Key} ({gr.Count()})")));
            var chosen = new List<(Bounds b, string name, Vector3 road)>();
            if (groups.Count > 0) chosen.Add(groups.OrderBy(x => Flat(x.b.center - c)).First());
            while (chosen.Count < GameData.Clients.Length && chosen.Count < groups.Count)
            {
                var next = groups.Where(x => !chosen.Contains(x))
                                 .OrderByDescending(x => Mathf.Min(Flat(x.b.center - c), chosen.Min(y => Flat(y.b.center - x.b.center)))).First();
                chosen.Add(next);
            }
            var recs = new List<HandSales.Receiver>();
            for (int i = 0; i < chosen.Count; i++)
            {
                var (b, name, road) = chosen[i];
                var toMarket = road - b.center; toMarket.y = 0;   // ściana od strony drogi
                // ściana zwrócona do rynku: oś budynku najbliższa kierunkowi do rynku
                Vector3 dir = Mathf.Abs(toMarket.x) / b.extents.x > Mathf.Abs(toMarket.z) / b.extents.z ? new Vector3(Mathf.Sign(toMarket.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(toMarket.z));
                float ext = Vector3.Scale(b.extents, new Vector3(Mathf.Abs(dir.x), 0, Mathf.Abs(dir.z))).magnitude;
                var wall = new Vector3(b.center.x, 0, b.center.z) + dir * ext;
                var door = Ground(wall + dir * 1.5f);
                // dach wystaje poza ściany — szukamy prawdziwej ściany promieniem od strony rynku na wysokości szyldu
                var probe = new Vector3(wall.x, door.y + 3.25f, wall.z) + dir * 3f;
                if (Physics.Raycast(probe, -dir, out var hitWall, 12f, ~0, QueryTriggerInteraction.Ignore))
                {
                    wall = new Vector3(hitWall.point.x, 0, hitWall.point.z);
                    door = Ground(wall + dir * 1.3f);
                }
                var g = new GameObject("Placówka: " + GameData.Clients[i].name).transform; g.SetParent(root, false); g.position = door;
                var wp = g.gameObject.AddComponent<WorldPlace>(); wp.kind = PlaceKind.Placowka; wp.farmId = i; wp.radius = 7f; wp.displayName = GameData.Clients[i].name;
                var point = new GameObject("Odbiorca").transform; point.SetParent(g, false); point.position = door; point.rotation = Quaternion.LookRotation(dir);
                // szyld na ścianie nad wejściem
                float groundY = door.y;
                // szyld na wysięgniku prostopadle do ściany (jak w dawnych sklepach) — widoczny z ulicy z obu stron, nie wchodzi w belki
                var along = Vector3.Cross(Vector3.up, dir);
                float topY = groundY + 3.35f;
                var plateWorld = new Vector3(wall.x, groundY + 2.95f, wall.z) + dir * 1.0f;
                WingDepartments.HangPlate(g, Signs[i], g.InverseTransformPoint(plateWorld), along, 1.5f, 0.375f, topY - g.position.y);
                var arm = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(arm.GetComponent<Collider>());
                arm.name = "Wysięgnik szyldu"; arm.transform.SetParent(g, true);
                arm.transform.position = new Vector3(wall.x, topY, wall.z) + dir * 0.9f;
                arm.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0);
                arm.transform.localScale = new Vector3(0.05f, 0.9f, 0.05f);
                var steel = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Mleczarka/Art/FactoryInterior/Stal_granatowa.mat");
                if (steel) arm.GetComponent<Renderer>().sharedMaterial = steel;
                recs.Add(new HandSales.Receiver { client = i, point = point, name = GameData.Clients[i].name });
                var nearZone = zones.OrderBy(z => Flat(z.transform.position - b.center)).FirstOrDefault();
                log.AppendLine($"{GameData.Clients[i].name}: budynek „{name}” {b.center:F0}, {Flat(b.center - c):F0} m od rynku, najbliżej: {(nearZone ? nearZone.displayName : "-")}, odbiorca {door:F0}");
            }
            hs.receivers = recs.ToArray();
            if (chosen.Count < GameData.Clients.Length) log.AppendLine($"UWAGA: znaleziono tylko {chosen.Count} budynków na placówki.");

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("Screenshots"); File.WriteAllText("Screenshots/placowki.txt", log.ToString());
            Debug.Log("[Mleczarka] Klienci i placówki: Screenshots/placowki.txt");
        }

        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
        // najniższe trafienie (teren albo droga), żeby nie postawić niczego na dachu ani koronie drzewa
        static Vector3 Ground(Vector3 p)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * 40f, Vector3.down, 90f, ~0, QueryTriggerInteraction.Ignore);
            return hits.Length > 0 ? hits.OrderBy(h => h.point.y).First().point : p;
        }
        static bool Blocked(Vector3 p) => Physics.CheckCapsule(p + Vector3.up * 0.6f, p + Vector3.up * 1.6f, 0.35f, ~0, QueryTriggerInteraction.Ignore);
        static string Path(Transform t) { var s = t.name; for (var p = t.parent; p; p = p.parent) s = p.name + "/" + s; return s; }
    }
}
