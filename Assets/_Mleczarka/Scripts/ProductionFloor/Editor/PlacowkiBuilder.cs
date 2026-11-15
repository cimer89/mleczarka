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
            var hs = root.gameObject.AddComponent<HandSales>();
            hs.npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Mleczarka/Prefabs/Production/Pracownik.prefab");
            const string K = "Assets/_Mleczarka/Art/ThirdParty/Kenney/FoodKit/Models/FBX format/";
            hs.cartonModel = AssetDatabase.LoadAssetAtPath<GameObject>(K + "carton-small.fbx");
            hs.cupModel = AssetDatabase.LoadAssetAtPath<GameObject>(K + "cup.fbx");
            hs.cheeseModel = AssetDatabase.LoadAssetAtPath<GameObject>(K + "cheese.fbx");
            hs.cheeseCutModel = AssetDatabase.LoadAssetAtPath<GameObject>(K + "cheese-cut.fbx");
            Physics.SyncTransforms();

            // ---- klienci: 3 miejsca przy rynku, z dala od ścian ----
            var c = market.transform.position; var spots = new List<Transform>();
            for (int a = 0; a < 24 && spots.Count < 3; a++)
            {
                float ang = a * 47f;
                var dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                var p = Ground(c + dir * 4.0f);
                if (Blocked(p) || spots.Any(s => Vector3.Distance(s.position, p) < 2.5f)) continue;
                var s = new GameObject("Miejsce klienta " + (spots.Count + 1)).transform; s.SetParent(root, false);
                s.position = p; s.rotation = Quaternion.LookRotation(-dir); spots.Add(s);
            }
            hs.marketSpots = spots.ToArray();
            log.AppendLine($"Rynek: {market.displayName} ({c:F0}), promień {market.radius}; miejsca klientów: {spots.Count}");

            // ---- placówki: istniejące budynki wokół rynku ----
            var cands = new List<(Bounds b, string name)>();
            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (col.isTrigger || col is TerrainCollider || col is WheelCollider) continue;
                var b = col.bounds; float d = Flat(b.center - c);
                if (d < market.radius + 9f || d > 150f) continue;
                if (b.size.y < 3f || b.size.y > 16f) continue;
                float w = Mathf.Max(b.size.x, b.size.z), n = Mathf.Min(b.size.x, b.size.z);
                if (w < 6f || w > 26f || n < 4.5f) continue;
                string path = Path(col.transform);
                if (Skip.Any(k => path.Contains(k))) continue;
                if (cands.Any(x => Flat(x.b.center - b.center) < 7f)) continue;
                cands.Add((b, path));
            }
            var chosen = new List<(Bounds b, string name)>();
            foreach (var x in cands.OrderBy(x => Flat(x.b.center - c)))
            {
                if (chosen.Count >= GameData.Clients.Length) break;
                if (chosen.Any(y => Flat(y.b.center - x.b.center) < 16f)) continue;
                chosen.Add(x);
            }
            var recs = new List<HandSales.Receiver>();
            for (int i = 0; i < chosen.Count; i++)
            {
                var (b, name) = chosen[i];
                var toMarket = c - b.center; toMarket.y = 0;
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
                log.AppendLine($"{GameData.Clients[i].name}: budynek „{name}” {b.center:F0}, rozmiar {b.size:F0}, odbiorca {door:F0}");
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
