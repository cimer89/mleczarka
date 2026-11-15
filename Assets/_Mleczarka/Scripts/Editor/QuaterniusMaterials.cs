using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mleczarka.EditorTools
{
    /// <summary>
    /// Tworzy materiały URP Lit dla paczek Quaternius i Kenney oraz przypina je do modeli (remap po nazwie).
    /// Ustawia też klipy animacji UAL (nazwy bez "Armature|", zapętlenie, animacje w miejscu).
    /// </summary>
    public static class QuaterniusMaterials
    {
        public const string Art = "Assets/_Mleczarka/Art/";
        public const string TP = Art + "ThirdParty/";
        const string Q = TP + "Quaternius/";
        const string MatDir = Art + "Materials/Quaternius/";
        const string Maps = MatDir + "Maps/";

        enum Mode { Opaque, Cutout, Transparent }

        class Spec
        {
            public string name, baseMap, normal, ms, ao;
            public Color color = Color.white;
            public Mode mode = Mode.Opaque;
            public bool twoSided;
            public float smoothness = 0.25f, metallic = 0f;
            public Color emission = Color.black;
        }

        static Spec S(string name, string baseMap, string normal = null, string ms = null, string ao = null) =>
            new Spec { name = name, baseMap = baseMap, normal = normal, ms = ms, ao = ao };

        static List<Spec> Table()
        {
            const string N = Q + "StylizedNatureMegaKit/Textures/";
            const string V = Q + "MedievalVillageMegaKit/Textures/";
            const string U = Q + "UniversalBaseCharacters/Textures/";
            const string O = Q + "ModularOutfitsFantasy/Textures/";
            var l = new List<Spec>
            {
                // Natura
                S("Bark_NormalTree", N + "Bark_NormalTree.png", N + "Bark_NormalTree_Normal.png"),
                S("Bark_DeadTree", N + "Bark_DeadTree.png", N + "Bark_DeadTree_Normal.png"),
                S("Bark_TwistedTree", N + "Bark_TwistedTree.png", N + "Bark_TwistedTree_Normal.png"),
                new Spec { name = "Leaves_NormalTree", baseMap = N + "Leaves_NormalTree_C.png", mode = Mode.Cutout, twoSided = true, smoothness = 0.1f },
                new Spec { name = "Leaves_TwistedTree", baseMap = N + "Leaves_TwistedTree_C.png", mode = Mode.Cutout, twoSided = true, smoothness = 0.1f },
                new Spec { name = "Leaves_Pine", baseMap = N + "Leaf_Pine_C.png", mode = Mode.Cutout, twoSided = true, smoothness = 0.1f },
                new Spec { name = "Leaves", baseMap = N + "Leaves.png", mode = Mode.Cutout, twoSided = true, smoothness = 0.1f },
                new Spec { name = "Flowers", baseMap = N + "Flowers.png", mode = Mode.Cutout, twoSided = true, smoothness = 0.1f },
                new Spec { name = "Grass", baseMap = N + "Grass.png", mode = Mode.Cutout, twoSided = true, smoothness = 0.05f },
                S("PathRocks", N + "PathRocks_Diffuse.png"),
                S("Rocks", N + "Rocks_Diffuse.png"),
                S("Mushrooms", N + "Mushrooms.png"),
                // Wieś
                S("MI_WoodTrim", V + "T_WoodTrim_BaseColor.png", V + "T_WoodTrim_Normal.png", Maps + "WoodTrim_MS.png"),
                S("MI_WoodTrim_Wear", V + "T_WoodTrim_BaseColor.png", V + "T_WoodTrim_Normal.png", Maps + "WoodTrim_MS.png"),
                S("MI_Plaster", V + "T_Plaster_BaseColor.png", V + "T_Plaster_Normal.png", Maps + "Plaster_MS.png", Maps + "Plaster_AO.png"),
                S("MI_RockTrim", V + "T_RockTrim_BaseColor.png", V + "T_RockTrim_Normal.png", Maps + "RockTrim_MS.png", Maps + "RockTrim_AO.png"),
                S("MI_Brick", V + "T_Brick_BaseColor.png", V + "T_Brick_Normal.png", Maps + "Brick_MS.png"),
                S("MI_RedBrick", V + "T_RedBrick_BaseColor.png", V + "T_Brick_Normal.png", Maps + "Brick_MS.png"),
                S("MI_UnevenBrick", V + "T_UnevenBrick_BaseColor.png", V + "T_UnevenBrick_Normal.png", Maps + "UnevenBrick_MS.png"),
                S("MI_RoundTiles", V + "T_RoundTiles_BaseColor.png", V + "T_RoundTiles_Normal.png", Maps + "RoundTiles_MS.png"),
                new Spec { name = "MI_MetalOrnaments", color = new Color(0.16f, 0.15f, 0.14f), metallic = 0.8f, smoothness = 0.45f },
                new Spec { name = "MI_WindowGlass", color = new Color(0.55f, 0.72f, 0.85f, 0.35f), mode = Mode.Transparent, smoothness = 0.92f },
                new Spec { name = "MI_Vine", baseMap = V + "T_VineLeaf.png", mode = Mode.Cutout, twoSided = true, smoothness = 0.1f },
                // Postacie
                S("MI_Superhero_Male", U + "T_Superhero_Male_Ligh.png", U + "T_Superhero_Male_Normal.png"),
                S("MI_Superhero_Female", U + "T_Superhero_Female_Light_BaseColor.png", U + "T_Superhero_Female_Normal.png"),
                new Spec { name = "MI_Eyes", baseMap = U + "T_Eye_Brown.png", normal = U + "T_Eye_Normal.png", smoothness = 0.85f },
                new Spec { name = "MI_Hair_1", baseMap = U + "T_Hair_1_BaseColor.png", normal = U + "T_Hair_1_Normal.png", color = new Color(0.45f, 0.30f, 0.18f), smoothness = 0.35f },
                new Spec { name = "MI_Hair_2", baseMap = U + "T_Hair_2_BaseColor.png", normal = U + "T_Hair_2_Normal.png", color = new Color(0.45f, 0.30f, 0.18f), smoothness = 0.35f },
                S("MI_Regular_Male", O + "T_Regular_Male_Dark_BaseColor.png", O + "T_Regular_Male_Normal.png"),
                S("MI_Regular_Female", O + "T_Regular_Female_Dark_BaseColor.png", O + "T_Regular_Female_Normal.png"),
                S("MI_Peasant", O + "T_Peasant_BaseColor.png", O + "T_Peasant_Normal.png", Maps + "Peasant_MS.png", Maps + "Peasant_AO.png"),
                S("MI_Ranger", O + "T_Ranger_BaseColor.png", O + "T_Ranger_Normal.png", Maps + "Ranger_MS.png", Maps + "Ranger_AO.png"),
                // Pojazdy mleczarni (własne modele z Blendera). Kolory lakieru nadpisuje VehicleAssembler.
                new Spec { name = "Lakier_Glowny", color = new Color(0.93f, 0.93f, 0.90f), smoothness = 0.62f, metallic = 0.1f },
                new Spec { name = "Lakier_Dodatkowy", color = new Color(0.15f, 0.40f, 0.85f), smoothness = 0.62f, metallic = 0.1f },
                new Spec { name = "Lakier_Akcent", color = new Color(0.90f, 0.20f, 0.16f), smoothness = 0.62f, metallic = 0.1f },
                new Spec { name = "Guma", color = new Color(0.07f, 0.07f, 0.075f), smoothness = 0.15f },
                new Spec { name = "Chrom", color = new Color(0.85f, 0.86f, 0.88f), smoothness = 0.85f, metallic = 1f },
                new Spec { name = "Stal_Zbiornik", color = new Color(0.80f, 0.82f, 0.84f), smoothness = 0.75f, metallic = 0.85f },
                new Spec { name = "Plastik_Czarny", color = new Color(0.10f, 0.10f, 0.11f), smoothness = 0.35f },
                new Spec { name = "Szyba", color = new Color(0.35f, 0.55f, 0.70f, 0.55f), mode = Mode.Transparent, smoothness = 0.95f },
                new Spec { name = "Swiatla", color = new Color(1f, 0.95f, 0.75f), smoothness = 0.8f, emission = new Color(1f, 0.95f, 0.75f) * 1.5f },
                new Spec { name = "Kierunkowskaz", color = new Color(1f, 0.55f, 0.1f), smoothness = 0.7f, emission = new Color(1f, 0.55f, 0.1f) * 0.8f },
                new Spec { name = "Swiatla_Tyl", color = new Color(0.9f, 0.08f, 0.06f), smoothness = 0.7f, emission = new Color(0.9f, 0.08f, 0.06f) * 0.8f },
                new Spec { name = "Naklejka_Logo", baseMap = Art + "Vehicles/Textures/T_Naklejka_Logo.png", mode = Mode.Cutout, smoothness = 0.55f },
                // Wieś podlaska i uprawy (własne modele z Blendera, kolory jednolite)
                new Spec { name = "Drewno_Bale", color = new Color(0.45f, 0.30f, 0.18f), smoothness = 0.2f },
                new Spec { name = "Drewno_Stare", color = new Color(0.42f, 0.38f, 0.33f), smoothness = 0.15f },
                new Spec { name = "Drewno_Ciemne", color = new Color(0.25f, 0.17f, 0.10f), smoothness = 0.2f },
                new Spec { name = "Okno_Niebieskie", color = new Color(0.20f, 0.42f, 0.75f), smoothness = 0.45f },
                new Spec { name = "Okno_Zielone", color = new Color(0.22f, 0.55f, 0.38f), smoothness = 0.45f },
                new Spec { name = "Tynk_Bialy", color = new Color(0.92f, 0.91f, 0.86f), smoothness = 0.2f },
                new Spec { name = "Blacha_Zielona", color = new Color(0.22f, 0.45f, 0.30f), smoothness = 0.55f, metallic = 0.3f },
                new Spec { name = "Blacha_Czerwona", color = new Color(0.62f, 0.16f, 0.12f), smoothness = 0.55f, metallic = 0.3f },
                new Spec { name = "Eternit", color = new Color(0.55f, 0.56f, 0.55f), smoothness = 0.2f },
                new Spec { name = "Podmurowka", color = new Color(0.55f, 0.55f, 0.52f), smoothness = 0.15f },
                new Spec { name = "Cegla", color = new Color(0.60f, 0.28f, 0.20f), smoothness = 0.15f },
                new Spec { name = "Metal_Szary", color = new Color(0.50f, 0.52f, 0.55f), smoothness = 0.5f, metallic = 0.7f },
                new Spec { name = "Tablica_Zolta", color = new Color(0.98f, 0.80f, 0.10f), smoothness = 0.5f },
                new Spec { name = "Tablica_Czarna", color = new Color(0.05f, 0.05f, 0.05f), smoothness = 0.4f },
                new Spec { name = "Kukurydza_Lisc", color = new Color(0.35f, 0.62f, 0.20f), smoothness = 0.3f, twoSided = true },
                new Spec { name = "Kukurydza_Kolba", color = new Color(0.95f, 0.80f, 0.30f), smoothness = 0.3f },
                new Spec { name = "Pszenica", color = new Color(0.88f, 0.72f, 0.35f), smoothness = 0.2f, twoSided = true },
                new Spec { name = "Rzepak_Lodyga", color = new Color(0.40f, 0.60f, 0.22f), smoothness = 0.25f, twoSided = true },
                new Spec { name = "Rzepak_Kwiat", color = new Color(1.0f, 0.88f, 0.10f), smoothness = 0.25f },
                new Spec { name = "Ziemniak_Lisc", color = new Color(0.28f, 0.50f, 0.20f), smoothness = 0.25f },
                // Kariera: letnia kuchnia, sprzęt domowy, rower (własne modele z Blendera)
                new Spec { name = "Drewno_Deski", color = new Color(0.45f, 0.30f, 0.17f), smoothness = 0.15f },
                new Spec { name = "Drewno_Ciemne", color = new Color(0.27f, 0.17f, 0.10f), smoothness = 0.15f },
                new Spec { name = "Drewno_Jasne", color = new Color(0.70f, 0.54f, 0.35f), smoothness = 0.2f },
                new Spec { name = "Emalia_Biala", color = new Color(0.92f, 0.92f, 0.89f), smoothness = 0.65f },
                new Spec { name = "Emalia_Kremowa", color = new Color(0.93f, 0.87f, 0.72f), smoothness = 0.7f },
                new Spec { name = "Zeliwo", color = new Color(0.13f, 0.13f, 0.14f), smoothness = 0.4f, metallic = 0.6f },
                new Spec { name = "Wiklina", color = new Color(0.66f, 0.49f, 0.27f), smoothness = 0.15f },
                new Spec { name = "Wiklina_Ciemna", color = new Color(0.48f, 0.33f, 0.16f), smoothness = 0.12f },
                new Spec { name = "Lakier_Rower", color = new Color(0.14f, 0.45f, 0.30f), smoothness = 0.65f, metallic = 0.3f },
                new Spec { name = "Plotno", color = new Color(0.93f, 0.92f, 0.86f), smoothness = 0.1f },
                new Spec { name = "Szklo_Sloik", color = new Color(0.80f, 0.92f, 0.94f, 0.45f), mode = Mode.Transparent, smoothness = 0.95f },
                new Spec { name = "Blacha_Dach", color = new Color(0.35f, 0.38f, 0.42f), smoothness = 0.5f, metallic = 0.6f },
                new Spec { name = "Aluminium", color = new Color(0.80f, 0.82f, 0.84f), smoothness = 0.7f, metallic = 0.9f },
                new Spec { name = "Mleko_Bialy", color = new Color(0.97f, 0.97f, 0.95f), smoothness = 0.5f },
                new Spec { name = "Twarog_Bialy", color = new Color(0.96f, 0.95f, 0.90f), smoothness = 0.2f },
                new Spec { name = "Maslo_Zolte", color = new Color(0.98f, 0.88f, 0.48f), smoothness = 0.45f },
                // Maszyny mleczarni (własne modele z Blendera)
                new Spec { name = "Stal_Nierdzewna", color = new Color(0.80f, 0.82f, 0.85f), smoothness = 0.72f, metallic = 0.85f },
                new Spec { name = "Stal_Rama", color = new Color(0.30f, 0.36f, 0.44f), smoothness = 0.5f, metallic = 0.6f },
                new Spec { name = "Panel_Ciemny", color = new Color(0.12f, 0.14f, 0.18f), smoothness = 0.5f },
                new Spec { name = "Ekran_Panelu", color = new Color(0.15f, 0.55f, 0.75f), smoothness = 0.8f, emission = new Color(0.1f, 0.45f, 0.65f) },
                new Spec { name = "Tasma_Guma", color = new Color(0.08f, 0.08f, 0.09f), smoothness = 0.2f },
                new Spec { name = "Akcent_Niebieski", color = new Color(0.16f, 0.42f, 0.80f), smoothness = 0.6f, metallic = 0.3f },
                new Spec { name = "Akcent_Pomaranczowy", color = new Color(0.95f, 0.58f, 0.15f), smoothness = 0.5f, metallic = 0.2f },
                new Spec { name = "Izolacja_Biala", color = new Color(0.92f, 0.93f, 0.94f), smoothness = 0.55f },
                new Spec { name = "Ser_Zolty", color = new Color(0.96f, 0.78f, 0.30f), smoothness = 0.35f },
                new Spec { name = "Szklo_Lab", color = new Color(0.75f, 0.9f, 0.95f, 0.45f), mode = Mode.Transparent, smoothness = 0.95f },
                new Spec { name = "Lampka_Stanu", color = new Color(0.35f, 0.36f, 0.38f), smoothness = 0.6f, emission = new Color(0.02f, 0.02f, 0.02f) },
                new Spec { name = "Drewno_Polka", color = new Color(0.55f, 0.38f, 0.22f), smoothness = 0.25f },
                // Kenney (zastępcze pojazdy)
                new Spec { name = "colormap", baseMap = TP + "Kenney/CarKit/Textures/colormap.png", smoothness = 0.45f },
            };
            return l;
        }

        [MenuItem("Mleczarka/1. Przygotuj materiały, rigi i animacje", priority = 1)]
        public static void SetupAll()
        {
            FixTextureShapes();
            var mats = CreateMaterials();
            int remapped = RemapModels(mats);
            int clips = SetupAnimationClips();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Mleczarka] Materiały: {mats.Count}, modele z podmienionymi materiałami: {remapped}, pliki animacji: {clips}.");
        }

        /// <summary>Paczki zaimportowane z samym GUID w .meta dostawały kształt Cube; wymuszamy Texture2D.</summary>
        static void FixTextureShapes()
        {
            int n = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var g in AssetDatabase.FindAssets("t:Texture", new[] { Art.TrimEnd('/') }))
                {
                    string tp = AssetDatabase.GUIDToAssetPath(g);
                    var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
                    if (ti == null) continue;
                    bool wrongNormal = tp.EndsWith("_Normal.png") && ti.textureType != TextureImporterType.NormalMap;
                    bool wrongHdri = tp.Contains("/HDRI/") && ti.mipmapEnabled;
                    if (ti.textureShape == TextureImporterShape.Texture2D && !wrongNormal && !wrongHdri) continue;
                    ti.SaveAndReimport();   // ponowny import przechodzi przez ThirdPartyImportRules
                    n++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            if (n > 0) Debug.Log($"[Mleczarka] Poprawiono kształt {n} tekstur (Cube -> 2D).");
        }

        public static Dictionary<string, Material> CreateMaterials()
        {
            Directory.CreateDirectory(MatDir);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var result = new Dictionary<string, Material>();
            foreach (var s in Table())
            {
                string path = MatDir + s.name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
                m.shader = shader;
                Configure(m, s);
                EditorUtility.SetDirty(m);
                result[s.name] = m;
            }
            return result;
        }

        static Texture2D Tex(string p) => string.IsNullOrEmpty(p) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(p);

        static void Configure(Material m, Spec s)
        {
            m.enableInstancing = true;
            m.SetColor("_BaseColor", s.color);
            var bm = Tex(s.baseMap);
            m.SetTexture("_BaseMap", bm);
            m.SetTexture("_MainTex", bm);

            var nm = Tex(s.normal);
            m.SetTexture("_BumpMap", nm);
            SetKeyword(m, "_NORMALMAP", nm);

            var ms = Tex(s.ms);
            m.SetTexture("_MetallicGlossMap", ms);
            SetKeyword(m, "_METALLICSPECGLOSSMAP", ms);
            m.SetFloat("_Smoothness", ms ? 0.9f : s.smoothness);
            m.SetFloat("_Metallic", s.metallic);

            var ao = Tex(s.ao);
            m.SetTexture("_OcclusionMap", ao);
            SetKeyword(m, "_OCCLUSIONMAP", ao);

            m.SetColor("_EmissionColor", s.emission);
            SetKeyword(m, "_EMISSION", s.emission.maxColorComponent > 0f);
            m.globalIlluminationFlags = s.emission.maxColorComponent > 0f ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            m.SetFloat("_Cull", s.twoSided ? (float)CullMode.Off : (float)CullMode.Back);
            m.doubleSidedGI = s.twoSided;

            m.SetFloat("_AlphaClip", s.mode == Mode.Cutout ? 1f : 0f);
            m.SetFloat("_Cutoff", 0.45f);
            SetKeyword(m, "_ALPHATEST_ON", s.mode == Mode.Cutout);

            if (s.mode == Mode.Transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("DepthOnly", false);
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
            {
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", s.mode == Mode.Cutout ? "TransparentCutout" : "Opaque");
                m.renderQueue = s.mode == Mode.Cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
                m.SetShaderPassEnabled("DepthOnly", true);
                m.SetShaderPassEnabled("ShadowCaster", true);
            }
        }

        static void SetKeyword(Material m, string k, bool on)
        {
            if (on) m.EnableKeyword(k); else m.DisableKeyword(k);
        }

        static int RemapModels(Dictionary<string, Material> mats)
        {
            int count = 0;
            var guids = AssetDatabase.FindAssets("t:Model", new[] { TP.TrimEnd('/'), Art + "Vehicles", Art + "World", Art + "Machines", Art + "Career" });
            // Faza 1: nazwy materiałów jak w pliku źródłowym (np. "MI_WoodTrim", "Leaves_NormalTree").
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var g in guids)
                {
                    var mi = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(g)) as ModelImporter;
                    if (mi == null || (mi.materialName == ModelImporterMaterialName.BasedOnMaterialName && mi.materialLocation == ModelImporterMaterialLocation.InPrefab)) continue;
                    mi.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                    mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
                    mi.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            // Faza 2: podpięcie naszych materiałów URP.
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var g in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (mi == null) continue;
                    bool changed = false;
                    var embedded = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(x => x.name)
                        .Concat(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m).Select(m => m.name))
                        .Distinct().ToList();
                    var existing = mi.GetExternalObjectMap();
                    foreach (var name in embedded)
                    {
                        if (!mats.TryGetValue(name, out var mat)) continue;
                        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                        if (existing.TryGetValue(id, out var cur) && cur == mat) continue;
                        mi.AddRemap(id, mat);
                        changed = true;
                    }
                    if (changed) { mi.SaveAndReimport(); count++; }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            return count;
        }

        static int SetupAnimationClips()
        {
            int count = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { Q + "UniversalAnimationLibrary", Q + "UniversalAnimationLibrary2" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (!path.StartsWith(Q + "UniversalAnimationLibrary")) continue;
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) continue;
                var clips = mi.defaultClipAnimations;
                foreach (var c in clips)
                {
                    c.name = c.name.Replace("Armature|", "");
                    bool loop = c.name.Contains("Loop") || c.name == "Sword_Idle";
                    c.loopTime = loop;
                    c.loopPose = loop;
                    c.lockRootRotation = true;
                    c.keepOriginalOrientation = true;
                    c.lockRootHeightY = true;
                    c.keepOriginalPositionY = true;
                    c.lockRootPositionXZ = true;
                    c.keepOriginalPositionXZ = true;
                }
                mi.clipAnimations = clips;
                mi.SaveAndReimport();
                count++;
            }
            return count;
        }
    }
}
