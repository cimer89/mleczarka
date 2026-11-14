using System.IO;
using System.Linq;
using Mleczarka.Core;
using Mleczarka.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>Zrzuty kontrolne w grze (bez zapisu): postać na asfalcie, rower, most z balustradą, rampa z poręczami, las, maszyny w skrzydle.</summary>
    [InitializeOnLoad]
    public static class WorldTourPlayTest
    {
        const string TestKey = "mleczarka_world_tour", Dir = "Screenshots/kontrola_swiata/";
        static double _t0, _next; static int _step;

        static WorldTourPlayTest()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(TestKey, false))
                {
                    SessionState.SetBool(TestKey, false);
                    _t0 = EditorApplication.timeSinceStartup; _step = 0; _next = 3;
                    if (Directory.Exists(Dir)) foreach (var f in Directory.GetFiles(Dir)) File.Delete(f);
                    Directory.CreateDirectory(Dir);
                    EditorApplication.update += Tick;
                }
            };
        }

        [MenuItem("Mleczarka/Testy/Zrzuty kontrolne świata (Play, bez zapisu)")]
        static void Run()
        {
            if (Application.isPlaying) return;
            PlayerPrefs.SetInt("mleczarka_test_mode", (int)GameMode.Swobodna); PlayerPrefs.Save();
            SessionState.SetBool(TestKey, true);
            EditorApplication.isPlaying = true;
        }

        static void Teleport(Vector3 p, float yaw)
        {
            var pl = GameController.Instance.player; var cc = pl.GetComponent<CharacterController>();
            if (cc) cc.enabled = false; pl.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0)); if (cc) cc.enabled = true;
            if (pl.orbital) { pl.orbital.HorizontalAxis.Value = yaw; pl.orbital.VerticalAxis.Value = 12f; }
        }

        static Vector3 Ground(Vector3 p) => Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var h, 60f, ~0, QueryTriggerInteraction.Ignore) ? h.point + Vector3.up * 0.05f : p;
        static float TerrainY(Vector3 p)
        {
            foreach (var t in Terrain.activeTerrains) { var o = t.transform.position; var s = t.terrainData.size; if (p.x >= o.x && p.z >= o.z && p.x <= o.x + s.x && p.z <= o.z + s.z) return t.SampleHeight(p) + o.y; }
            return p.y;
        }
        static void Shot(string n) => ScreenCapture.CaptureScreenshot(Dir + n + ".png");

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - _t0; if (t < _next) return;
            var g = GameController.Instance; if (!g) return;
            var home = Object.FindFirstObjectByType<CareerHome>();
            var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            switch (_step++)
            {
                case 0: { var m = Object.FindFirstObjectByType<Mleczarka.UI.StartMenuUI>(); if (m) m.CloseMenu(); _next = t + 1; break; }
                case 1: { // asfalt: najbliższa droga od domu
                    var road = all.Where(x => x.name.StartsWith("odcinek") && x.GetComponent<MeshCollider>()).OrderBy(x => Vector3.Distance(x.GetComponent<Renderer>().bounds.center, home.transform.position)).First();
                    var mesh = road.GetComponent<MeshFilter>().sharedMesh; var v = road.TransformPoint(mesh.vertices[mesh.vertexCount / 2 / 3 * 3 + 1]);
                    Teleport(v + Vector3.up * 0.3f, 90); g.player.cameraRadiusOnFoot = 2.6f; _next = t + 2; break; }
                case 2: Shot("01_nogi_na_asfalcie"); _next = t + 0.5; break;
                case 3: { var bp = g.player.transform.position; home.bike.position = bp + Vector3.right * 1.2f; home.bike.rotation = Quaternion.Euler(0, 90, 0); Teleport(bp + Vector3.right * 1.2f - Vector3.forward * 0.8f, 90); home.ToggleBike(); _next = t + 2; break; }
                case 4: Shot("02_rower_na_asfalcie"); home.ToggleBike(); g.player.cameraRadiusOnFoot = 5.5f; _next = t + 0.5; break;
                case 5: { var most = all.Where(x => x.name == "Most").OrderBy(x => x.position.x).Last(); Teleport(most.position + most.forward * -16f + Vector3.up * 0.5f, most.eulerAngles.y); _next = t + 2; break; }
                case 6: Shot("03_most"); _next = t + 0.5; break;
                case 7: { var most = all.Where(x => x.name == "Most").OrderBy(x => x.position.x).Last(); Teleport(Ground(most.position + most.right * 14f + most.forward * -6f), most.eulerAngles.y - 70); _next = t + 2; break; }
                case 8: Shot("04_most_z_boku"); _next = t + 0.5; break;
                case 9: { var ramp = all.First(x => x.name == "Rampa wejściowa"); var q = ramp.position - ramp.forward * 7f + ramp.right * 4f; q.y = TerrainY(q) + 0.1f; Teleport(q, ramp.eulerAngles.y - 30); _next = t + 2; break; }
                case 10: Shot("05_rampa_porecze"); _next = t + 0.5; break;
                case 11: { var v = all.First(x => x.name.StartsWith("homogenizator — ")).GetComponent<PlantMachineView>(); var d = Vector3.ProjectOnPlane(v.transform.position - v.station.position, Vector3.up); Teleport(v.station.position + Vector3.up * 0.1f - d.normalized * 1.5f, Quaternion.LookRotation(d).eulerAngles.y); g.player.cameraRadiusOnFoot = 3.2f; _next = t + 2; break; }
                case 12: Shot("06_maszyny"); g.player.cameraRadiusOnFoot = 5.5f; _next = t + 0.5; break;
                case 113: { var tr = Terrain.activeTerrains.First(x => x.terrainData.treeInstances.Length > 15000); var ti = tr.terrainData.treeInstances[tr.terrainData.treeInstances.Length / 3];
                    var p = Vector3.Scale(ti.position, tr.terrainData.size) + tr.transform.position; Teleport(Ground(p + Vector3.right * 25f), 270); if (g.player.orbital) g.player.orbital.VerticalAxis.Value = 4f; g.player.cameraRadiusOnFoot = 9f; _next = t + 3; break; }
                case 114: Shot("07_las"); g.player.cameraRadiusOnFoot = 5.5f; _next = t + 0.5; break;
                default: EditorApplication.update -= Tick; Debug.Log("[Mleczarka] Zrzuty kontrolne: " + Dir); break;
            }
        }
    }
}
