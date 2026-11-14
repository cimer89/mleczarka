using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Mleczarka.EditorTools
{
    /// <summary>Renderuje kilka ujęć kontrolnych sceny demo do folderu Screenshots/ (weryfikacja wyglądu bez ręcznego kadrowania).</summary>
    public static class SceneShots
    {
        [MenuItem("Mleczarka/3. Zrzuty kontrolne sceny", priority = 3)]
        public static void Capture()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
            Directory.CreateDirectory(dir);
            if (Application.isPlaying)
            {
                // w trybie gry: zrzut widoku gry razem z interfejsem
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "00_widok_gry.png"));
            }
            var farmer = GameObject.Find("Rolnik");
            var tractor = GameObject.Find("Dostawczak mleczarni (zastępczy)");
            var shots = new (string name, Vector3 pos, Vector3 look, float fov)[]
            {
                ("01_zagroda", new Vector3(14f, 9f, 22f), new Vector3(0f, 1.5f, 0f), 50f),
                ("02_dom", new Vector3(5.5f, 2.2f, 11f), new Vector3(0f, 2.2f, 2.5f), 45f),
                ("03_rolnik", farmer ? farmer.transform.position + farmer.transform.forward * 2.6f + Vector3.up * 1.5f : Vector3.one, farmer ? farmer.transform.position + Vector3.up * 1.15f : Vector3.zero, 35f),
                ("04_dostawczak", tractor ? tractor.transform.position + new Vector3(-4.5f, 2.2f, 4.5f) : Vector3.one, tractor ? tractor.transform.position + Vector3.up * 1f : Vector3.zero, 45f),
                ("06_cysterna", GameObject.Find("Cysterna mleczarni") ? GameObject.Find("Cysterna mleczarni").transform.position + new Vector3(6f, 2.6f, 6f) : Vector3.one, GameObject.Find("Cysterna mleczarni") ? GameObject.Find("Cysterna mleczarni").transform.position + Vector3.up * 1.4f : Vector3.zero, 45f),
                ("07_trzy_cysterny", new Vector3(0.5f, 4.5f, 34f), new Vector3(0.5f, 1.3f, 19f), 55f),
                ("05_las", new Vector3(-6f, 1.7f, 16f), new Vector3(-20f, 4f, -10f), 55f),
            };
            // ujęcia pojazdów po nazwie (z ukosa z przodu)
            var list = new System.Collections.Generic.List<(string, Vector3, Vector3, float)>(shots);
            int n = 8;
            foreach (var vn in new[] { "Dostawczak mleczarni", "Dostawczak z bańkami", "Dostawczak wyścigowy" })
            {
                var v = GameObject.Find(vn);
                if (!v) continue;
                var t = v.transform;
                list.Add(($"{n++:00}_{vn.Replace(' ', '_')}", t.position + t.forward * 5.5f + t.right * 4.0f + Vector3.up * 2.2f, t.position + Vector3.up * 1.1f, 45f));
            }
            // świat „Gmina”: ujęcia z lotu ptaka i miejsc
            if (GameObject.Find("Mleczarnia"))
            {
                list.Clear();
                list.Add(("20_gmina_z_gory", new Vector3(-150f, 120f, -150f), new Vector3(0f, 0f, 30f), 55f));
                list.Add(("21_mleczarnia", new Vector3(14f, 14f, 10f), new Vector3(-22f, 2f, -22f), 55f));
                list.Add(("22_rynek", new Vector3(0f, 12f, 132f), new Vector3(0f, 2f, 172f), 55f));
                var farm = GameObject.Find("Gospodarstwo Nowakowie");
                if (farm) { var z = farm.GetComponentInChildren<Mleczarka.Gameplay.WorldPlace>().transform.position; list.Add(("23_gospodarstwo", z + new Vector3(18f, 12f, 22f), z + new Vector3(-12f, 1f, -6f), 55f)); }
                var tk = GameObject.Find("Cysterna gracza");
                if (tk) list.Add(("24_pojazdy_gracza", tk.transform.position + new Vector3(12f, 5f, 4f), tk.transform.position + new Vector3(0f, 1f, -9f), 50f));
            }
            // gmina Podlasie: ujęcia z lotu ptaka, wsi, pól, mostu i mleczarni
            if (GameObject.Find("Teren Gminy"))
            {
                list.Clear();
                Vector3 Gr(float x, float z) { var o = new Vector3(x, 500f, z); return Physics.Raycast(o, Vector3.down, out var h, 1000f) ? h.point : new Vector3(x, 0, z); }
                list.Add(("30_gmina_z_gory", new Vector3(0f, 3400f, -3700f), new Vector3(0f, 0f, 300f), 60f));
                var d = Gr(0, 0);
                list.Add(("31_mleczarnia", d + new Vector3(45f, 28f, -40f), d + new Vector3(-10f, 2f, 5f), 55f));
                var v = GameObject.Find("Wieś Olszanka");
                if (v && v.transform.childCount > 0) { var c = v.transform.GetChild(v.transform.childCount / 2).position; list.Add(("32_wies", c + new Vector3(-30f, 14f, -35f), c + new Vector3(0f, 2f, 0f), 55f)); list.Add(("33_ulica", c + new Vector3(0f, 2.2f, 0f) + (Quaternion.Euler(0, 80, 0) * new Vector3(0, 0, -12)) + Vector3.up * 0, c + new Vector3(0, 2f, 0) + (Quaternion.Euler(0, 80, 0) * new Vector3(0, 0, 60)), 60f)); }
                if (v && v.transform.childCount > 0) { var c = v.transform.GetChild(0).position; list.Add(("38_dom_z_gory", c + new Vector3(0f, 70f, -1f), c, 40f)); }
                var b = GameObject.Find("Mosty");
                if (b && b.transform.childCount > 0) { var c = b.transform.GetChild(0).position; list.Add(("34_most", c + new Vector3(30f, 10f, -30f), c, 55f)); }
                var t = GameObject.Find("Zabielsk");
                if (t) { var c = Gr(t.transform.position.x, t.transform.position.z); list.Add(("35_zabielsk", c + new Vector3(-50f, 30f, -60f), c, 55f)); }
                var p = Gr(-500f, 750f); list.Add(("36_pola", p + new Vector3(0f, 60f, -120f), p + new Vector3(0f, 0f, 60f), 60f));
                var f = Gr(-1690f, -800f); list.Add(("37_las", f + new Vector3(0f, 3f, 0f), f + new Vector3(60f, 4f, 40f), 60f));
            }
            shots = list.ToArray();
            var go = new GameObject("__ShotCam");
            try
            {
                var cam = go.AddComponent<Camera>();
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 6000f;
                const int W = 1600, H = 900;
                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;
                foreach (var s in shots)
                {
                    go.transform.position = s.pos;
                    go.transform.LookAt(s.look);
                    cam.fieldOfView = s.fov;
                    bool fog = RenderSettings.fog; if (s.name.Contains("z_gory")) RenderSettings.fog = false;
                    cam.Render(); cam.Render();   // drugi przebieg: zasłanianie GPU (GPU Resident Drawer) liczy się z poprzedniej klatki
                    RenderSettings.fog = fog;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(Path.Combine(dir, s.name + ".png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Debug.Log("[Mleczarka] Zrzuty zapisane w " + dir);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
