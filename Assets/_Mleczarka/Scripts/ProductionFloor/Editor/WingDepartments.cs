using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>
    /// Działy produkcyjne w skrzydle maszyn i tabliczki.
    /// Działy oddzielają przeszklone ścianki z drzwiami od korytarza i pełne ścianki między działami
    /// (górą idą rurociągi). Każdy dział ma swój kolor posadzki i tablicę nad drzwiami.
    /// Nad maszynami wiszą tabliczki na linkach, a w starej hali tabliczki zastępują unoszące się napisy.
    /// Napisy są obrazkami (Art/Signs, czcionka DejaVu Sans): ostre, zasłaniane przez ściany jak zwykłe obiekty.
    /// </summary>
    public static class WingDepartments
    {
        const string Signs = "Assets/_Mleczarka/Art/Signs/";
        public const string Tag = "Skrzydło: ";
        const float FrontN = 0.3f, FrontS = -4.5f, WallH = 3.0f, DoorW = 2.6f, DoorH = 2.4f;

        public class Dept { public int num; public string name; public Color color; public float x0, x1; public bool north; public float door; }
        public static readonly Dept[] Depts =
        {
            new Dept { num = 1, name = "Przyjęcie i laboratorium", color = Hex("#3C7DD9"), x0 = -12f, x1 = -7.2f, north = true, door = -9.5f },
            new Dept { num = 2, name = "Mleko płynne", color = Hex("#2AA6C9"), x0 = -7.2f, x1 = 1.8f, north = true, door = -4.6f },
            new Dept { num = 3, name = "Fermentacja", color = Hex("#4CB772"), x0 = 1.8f, x1 = 6.6f, north = true, door = 4.3f },
            new Dept { num = 6, name = "Chłodnia", color = Hex("#7FBDE8"), x0 = 6.6f, x1 = 12f, north = true, door = 9.3f },
            new Dept { num = 4, name = "Masło i sery", color = Hex("#E2A93B"), x0 = -12f, x1 = 1.7f, north = false, door = -4.4f },
            new Dept { num = 5, name = "Rozlew i pakowanie", color = Hex("#2BB3A3"), x0 = 1.7f, x1 = 12f, north = false, door = 6.0f },
        };

        static Transform root; static List<Renderer> shell; static Material cream, navy, teal, glass, floorMat, cable;
        static readonly Dictionary<string, Material> signMats = new Dictionary<string, Material>();

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        /// <summary>Buduje działy w skrzydle; zwraca środki drzwi (świat) dla ścieżek pracowników.</summary>
        public static Vector3[] Build(Transform wing, List<Renderer> wingShell, Material creamM, Material navyM, Material tealM, Material glassM, Material floorM, Material steel)
        {
            root = wing; shell = wingShell; cream = creamM; navy = navyM; teal = tealM; floorMat = floorM; cable = steel;
            glass = ClearGlass();
            signMats.Clear();
            var doors = new List<Vector3>();
            var holder = new GameObject("Działy produkcyjne").transform; holder.SetParent(wing, false);
            Transform keep = root; root = holder;
            try
            {
                foreach (var d in Depts)
                {
                    float zf = d.north ? FrontN : FrontS, zb = d.north ? 13f : -13f, zc = (zf + zb) / 2, depth = Mathf.Abs(zb - zf);
                    // posadzka w kolorze działu
                    var tint = Color.Lerp(new Color(0.72f, 0.78f, 0.77f), d.color, 0.28f);
                    Box($"Posadzka działu {d.num}", new Vector3((d.x0 + d.x1) / 2, 0.006f, zc), new Vector3(d.x1 - d.x0 - 0.1f, 0.012f, depth - 0.1f), FloorMat(d), false, false);
                    // ścianka od korytarza: cokół, szkło, górna belka, drzwi
                    float dl = d.door - DoorW / 2, dr = d.door + DoorW / 2;
                    FrontWall(d, zf, d.x0, dl); FrontWall(d, zf, dr, d.x1);
                    Box("Nadproże drzwi", new Vector3(d.door, (DoorH + WallH) / 2, zf), new Vector3(DoorW, WallH - DoorH, 0.14f), navy, true);
                    foreach (float x in new[] { dl, dr }) Box("Ościeżnica", new Vector3(x, DoorH / 2, zf), new Vector3(0.1f, DoorH, 0.18f), teal);
                    Box("Próg działu", new Vector3(d.door, 0.008f, zf), new Vector3(DoorW, 0.016f, 0.35f), teal, false, false);
                    // tablica działu nad drzwiami — na nadprożu, od korytarza i od środka
                    var dir = d.north ? Vector3.back : Vector3.forward;
                    Plate($"Dzial_{d.num}", new Vector3(d.door, DoorH + 0.42f, zf), dir, 2.5f, 0.5f, true, 0.09f);
                    doors.Add(wing.TransformPoint(new Vector3(d.door, 0, zf)));
                }
                // pełne ścianki między działami (od ścianki frontowej do ściany zewnętrznej)
                Side(-7.2f, FrontN, 13f); Side(1.8f, FrontN, 13f); Side(6.6f, FrontN, 13f);
                // między masłem i serami a rozlewem: otwór na taśmę zbiorczą
                Side(1.7f, FrontS, -8.1f); Side(1.7f, -9.1f, -13f);
                Box("Ścianka nad taśmą", new Vector3(1.7f, (1.2f + WallH) / 2, -8.6f), new Vector3(0.14f, WallH - 1.2f, 1.0f), cream, true);
                Box("Rama otworu taśmy", new Vector3(1.7f, 1.2f, -8.6f), new Vector3(0.2f, 0.08f, 1.05f), teal, false, false);
                // laboratorium stoi przy korytarzu na końcu działu 1
                Box("Posadzka laboratorium", new Vector3(-11.1f, 0.006f, -2.1f), new Vector3(1.7f, 0.012f, 4.6f), FloorMat(Depts[0]), false, false);
            }
            finally { root = keep; }
            return doors.ToArray();
        }

        static Material FloorMat(Dept d)
        {
            string key = "Posadzka_dzial_" + d.num;
            if (signMats.TryGetValue(key, out var m)) return m;
            m = Mat(key, Color.Lerp(new Color(0.72f, 0.78f, 0.77f), d.color, 0.3f), 0.35f);
            signMats[key] = m; return m;
        }

        static void FrontWall(Dept d, float z, float x0, float x1)
        {
            if (x1 - x0 < 0.05f) return;
            float cx = (x0 + x1) / 2, w = x1 - x0;
            Box("Cokół ścianki", new Vector3(cx, 0.5f, z), new Vector3(w, 1.0f, 0.14f), cream, true);
            Box("Szyba ścianki", new Vector3(cx, 1.75f, z), new Vector3(w, 1.5f, 0.05f), glass, true);
            Box("Belka ścianki", new Vector3(cx, 2.75f, z), new Vector3(w, 0.5f, 0.14f), navy, true);
            Box("Pas działu", new Vector3(cx, 1.0f, z), new Vector3(w, 0.08f, 0.16f), Mat("Pas_dzial_" + d.num, d.color, 0.4f), false, false);
            for (float x = x0 + 1.5f; x < x1 - 0.5f; x += 1.5f) Box("Słupek szklenia", new Vector3(x, 1.75f, z), new Vector3(0.06f, 1.5f, 0.1f), navy, false, false);
        }

        static void Side(float x, float z0, float z1)
        {
            float cz = (z0 + z1) / 2, l = Mathf.Abs(z1 - z0);
            Box("Ścianka działu", new Vector3(x, WallH / 2, cz), new Vector3(0.14f, WallH, l), cream, true);
            Box("Belka ścianki działu", new Vector3(x, WallH - 0.05f, cz), new Vector3(0.18f, 0.1f, l), navy, false, false);
        }

        // ---------------- tabliczki ----------------

        /// <summary>Tabliczka z obrazkiem: rama, lico z obu stron (gdy twoSided), opcjonalnie linki do dźwigara.</summary>
        public static Transform Plate(string sign, Vector3 localPos, Vector3 faceDir, float w, float h, bool twoSided, float frame = 0.05f, float hangTo = -1)
        {
            var g = new GameObject("Tabliczka: " + sign).transform; g.SetParent(root, false);
            g.localPosition = localPos; g.localRotation = Quaternion.LookRotation(faceDir, Vector3.up);
            var back = Prim(PrimitiveType.Cube, g, "Rama tabliczki", Vector3.zero, new Vector3(w + frame * 2, h + frame * 2, 0.04f), navy);
            var mat = SignMat(sign);
            var front = Prim(PrimitiveType.Quad, g, "Lico", new Vector3(0, 0, 0.022f), new Vector3(w, h, 1), mat);
            front.localRotation = Quaternion.LookRotation(Vector3.back);   // quad patrzy w -Z; obracamy, żeby lico patrzyło w stronę faceDir
            if (twoSided)
            {
                var rear = Prim(PrimitiveType.Quad, g, "Lico (tył)", new Vector3(0, 0, -0.022f), new Vector3(w, h, 1), mat);
                rear.localRotation = Quaternion.identity;
            }
            if (hangTo > 0)
                foreach (float s in new[] { -1f, 1f })
                {
                    float top = h / 2 + frame, len = hangTo - (localPos.y + top);
                    if (len <= 0.05f) continue;
                    Prim(PrimitiveType.Cylinder, g, "Linka", new Vector3(s * w * 0.4f, top + len / 2, 0), new Vector3(0.012f, len / 2, 0.012f), cable);
                }
            return g;
        }

        static Material SignMat(string sign)
        {
            if (signMats.TryGetValue(sign, out var m)) return m;
            string tex = Signs + sign + ".png";
            var imp = AssetImporter.GetAtPath(tex) as TextureImporter;
            if (imp && (imp.mipmapEnabled == false || imp.anisoLevel < 4 || imp.textureCompression != TextureImporterCompression.CompressedHQ))
            {
                imp.mipmapEnabled = true; imp.anisoLevel = 8; imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(tex);
            string path = Signs + "Mat_" + sign + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", t); m.mainTexture = t; m.color = Color.white; m.SetFloat("_Smoothness", 0.25f); m.SetFloat("_Metallic", 0);
            if (!t) { m.color = new Color(0.1f, 0.2f, 0.25f); Debug.LogWarning("[Mleczarka] Brak obrazka tabliczki " + tex); }
            EditorUtility.SetDirty(m); signMats[sign] = m; return m;
        }

        /// <summary>Jasne, przejrzyste szkło ścianek działów — z korytarza widać maszyny.</summary>
        static Material ClearGlass()
        {
            var m = Mat("Szyba_dzialu", new Color(0.78f, 0.92f, 0.96f, 0.22f), 0.92f);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(m); return m;
        }

        static Material Mat(string name, Color c, float smooth = 0.3f)
        {
            string path = "Assets/_Mleczarka/Art/FactoryInterior/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.color = c; m.SetFloat("_Smoothness", smooth); EditorUtility.SetDirty(m); return m;
        }

        static Transform Prim(PrimitiveType t, Transform parent, string name, Vector3 lp, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(t); g.name = name; Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        static GameObject Box(string name, Vector3 p, Vector3 size, Material m, bool cut = false, bool collide = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(root, false);
            g.transform.localPosition = p; g.transform.localScale = size;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m;
            if (!collide) Object.DestroyImmediate(g.GetComponent<Collider>());
            if (cut) shell.Add(r);
            return g;
        }

        // ---------------- tabliczki maszyn i starej hali ----------------

        /// <summary>Tabliczka na linkach nad stanowiskiem maszyny (między maszyną a operatorem).</summary>
        public static void MachinePlate(Transform wing, string sign, Vector3 machineLocal, Vector3 face, float ahead, float yCenter = 2.75f)
        {
            Transform keep = root; root = wing;
            try { Plate(sign, machineLocal + face * ahead + Vector3.up * yCenter, face, 1.2f, 0.3f, true, 0.035f, 5.3f); }
            finally { root = keep; }
        }

        /// <summary>Stara hala: unoszące się napisy wyłączone (obiekty zostają), zamiast nich tabliczki na linkach i szyld na fasadzie.</summary>
        public static int OldHall(Transform hall, System.Text.StringBuilder log)
        {
            Transform keep = root; root = hall;
            int n = 0;
            try
            {
                var names = new Dictionary<string, string>
                {
                    { "PRZYJĘCIE / START PARTII", "Hala_Przyjecie" }, { "LINIA UHT", "Hala_UHT" }, { "PAKOWANIE MLEKA UHT", "Hala_PakUHT" },
                    { "PASTERYZACJA JOGURTU", "Hala_Past" }, { "FERMENTACJA", "Hala_Ferm" }, { "PAKOWANIE JOGURTU", "Hala_PakJog" },
                    { "MAGAZYN WYROBÓW GOTOWYCH", "Hala_Magazyn" }, { "WYSYŁKA / WYJŚCIE NA PLAC", "Hala_Wysylka" },
                };
                foreach (var st in hall.GetComponentsInChildren<FloorStation>(true))
                {
                    foreach (var tm in st.GetComponentsInChildren<TextMesh>(true)) tm.gameObject.SetActive(false);
                    if (!names.TryGetValue(st.title ?? st.name, out var sign)) continue;
                    var lp = hall.InverseTransformPoint(st.transform.position);
                    var face = lp.z < -0.8f ? Vector3.forward : Vector3.back;
                    var p = Plate(sign, new Vector3(lp.x, 2.9f, lp.z), face, 1.5f, 0.375f, true, 0.04f, 5.3f);
                    p.name = Tag + p.name; n++;
                }
                var label = hall.GetComponentsInChildren<TextMesh>(true).FirstOrDefault(t => t.text == "MLECZARNIA");
                if (label) label.gameObject.SetActive(false);
                var szyld = hall.Find("Szyld fasadowy");
                if (szyld)
                {
                    var lp = szyld.localPosition; var s = szyld.localScale;
                    var g = new GameObject(Tag + "Szyld fasadowy — napis").transform; g.SetParent(hall, false);
                    g.localPosition = new Vector3(lp.x, lp.y, lp.z - s.z / 2 - 0.006f); g.localRotation = Quaternion.LookRotation(Vector3.back);
                    var q = Prim(PrimitiveType.Quad, g, "Lico", Vector3.zero, new Vector3(s.x - 0.2f, s.y - 0.12f, 1), SignMat("Fasada"));
                    q.localRotation = Quaternion.LookRotation(Vector3.back);
                    n++;
                }
                log?.AppendLine($"Stara hala: {n} tabliczek, unoszące się napisy wyłączone.");
            }
            finally { root = keep; }
            return n;
        }

        /// <summary>Tabliczka na linkach w dowolnym miejscu (lokalnie względem parent).</summary>
        public static void HangPlate(Transform parent, string sign, Vector3 local, Vector3 face, float w, float h, float hangTo)
        {
            Transform keep = root; root = parent;
            if (!navy) navy = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Mleczarka/Art/FactoryInterior/Stal_granatowa.mat");
            if (!cable) cable = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Mleczarka/Art/FactoryInterior/Stal_nierdzewna.mat");
            try { Plate(sign, local, face, w, h, true, 0.04f, hangTo); }
            finally { root = keep; }
        }

        /// <summary>Szyld na zewnętrznej ścianie skrzydła (od strony placu).</summary>
        public static void WingFacade(Transform wing, float halfZ)
        {
            Transform keep = root; root = wing;
            try { Plate("Skrzydlo", new Vector3(0, 4.75f, -halfZ - 0.2f), Vector3.back, 8f, 0.89f, false, 0.06f); }
            finally { root = keep; }
        }
    }
}
