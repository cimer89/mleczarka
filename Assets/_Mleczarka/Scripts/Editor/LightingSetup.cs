using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mleczarka.EditorTools
{
    /// <summary>
    /// Oświetlenie sceny: niebo HDRI z Poly Haven (CC0) zgrane ze słońcem, światło otoczenia i odbicia z nieba,
    /// mgła w kolorze horyzontu, postprocess oraz ustawienia URP (cienie, SSAO, MSAA, HDR).
    /// Trzy pory dnia do przełączania z menu.
    /// </summary>
    public static class LightingSetup
    {
        public enum Pora { Dzien, Popoludnie, Zmierzch }

        const string HDRI = "Assets/_Mleczarka/Art/ThirdParty/PolyHaven/HDRI/";
        const string GEN = "Assets/_Mleczarka/Art/Generated/Settings/";

        struct Preset
        {
            public string hdri; public float u, v;          // położenie słońca na panoramie (z analizy pliku HDR)
            public Color sun; public float sunIntensity; public float exposure;
            public Color fog; public float fogStart, fogEnd;
            public float temperature, tint, saturation, postExposure;
        }

        static Preset Get(Pora p) => p switch
        {
            Pora.Popoludnie => new Preset { hdri = "qwantani_late_afternoon_puresky_2k.hdr", u = 0.6003f, v = 0.6060f,
                sun = new Color(1f, 0.82f, 0.62f), sunIntensity = 2.1f, exposure = 0.9f, fog = new Color(0.86f, 0.84f, 0.76f), fogStart = 50, fogEnd = 210,
                temperature = 8, tint = 2, saturation = 4, postExposure = 0f },
            Pora.Zmierzch => new Preset { hdri = "qwantani_dusk_1_puresky_2k.hdr", u = 0.5969f, v = 0.6118f,
                sun = new Color(1f, 0.62f, 0.42f), sunIntensity = 1.2f, exposure = 1.25f, fog = new Color(0.62f, 0.66f, 0.80f), fogStart = 40, fogEnd = 180,
                temperature = 4, tint = 6, saturation = 8, postExposure = 0.25f },
            _ => new Preset { hdri = "kloofendal_48d_partly_cloudy_puresky_2k.hdr", u = 0.5950f, v = 0.7661f,
                sun = new Color(1f, 0.95f, 0.86f), sunIntensity = 2.0f, exposure = 1.0f, fog = new Color(0.70f, 0.75f, 0.84f), fogStart = 60, fogEnd = 230,
                temperature = 4, tint = 0, saturation = 2, postExposure = 0f },
        };

        /// <summary>Kierunek DO słońca z panoramy (zgodny z shaderem Skybox/Panoramic przy obrocie 0).</summary>
        static Vector3 SunDirection(float u, float v)
        {
            float lon = (0.5f - u) * 2f * Mathf.PI;          // atan2(z, x)
            float y = Mathf.Cos((1f - v) * Mathf.PI);          // v = 1 - acos(y)/PI
            float c = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            return new Vector3(Mathf.Cos(lon) * c, y, Mathf.Sin(lon) * c).normalized;
        }

        [MenuItem("Mleczarka/Oświetlenie/Dzień", priority = 20)] static void MDzien() => ApplyToOpenScene(Pora.Dzien);
        [MenuItem("Mleczarka/Oświetlenie/Popołudnie (złota godzina)", priority = 21)] static void MPop() => ApplyToOpenScene(Pora.Popoludnie);
        [MenuItem("Mleczarka/Oświetlenie/Zmierzch", priority = 22)] static void MZm() => ApplyToOpenScene(Pora.Zmierzch);

        static void ApplyToOpenScene(Pora p)
        {
            Apply(p);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        /// <summary>Ustawia (albo tworzy) słońce, niebo, mgłę, sondę odbić i postprocess w otwartej scenie.</summary>
        public static void Apply(Pora pora)
        {
            System.IO.Directory.CreateDirectory(GEN);
            QuaterniusMaterials.SetupAll();
            ConfigureURP();
            var pr = Get(pora);

            // --- niebo HDRI
            var hdr = AssetDatabase.LoadAssetAtPath<Texture>(HDRI + pr.hdri);
            string skyPath = GEN + "Niebo_HDRI.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (!sky) { sky = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(sky, skyPath); }
            sky.shader = Shader.Find("Skybox/Panoramic");
            sky.SetTexture("_MainTex", hdr);
            sky.SetFloat("_Mapping", 1f);      // latitude-longitude
            sky.SetFloat("_ImageType", 0f);    // 360°
            sky.SetFloat("_Rotation", 0f);
            sky.SetFloat("_Exposure", pr.exposure);
            sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            // --- słońce zgodne z HDRI
            var sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (!sun)
            {
                var go = new GameObject("Słońce"); sun = go.AddComponent<Light>(); sun.type = LightType.Directional;
                go.AddComponent<UniversalAdditionalLightData>();
            }
            sun.name = "Słońce";
            var toSun = SunDirection(pr.u, pr.v);
            sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
            sun.color = pr.sun;
            sun.intensity = pr.sunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.92f;
            sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.35f;
            RenderSettings.sun = sun;

            // --- światło otoczenia i odbicia z nieba
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1.0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = pr.fog;
            float fk = GameObject.Find("Teren Gminy") ? 6f : 1f;   // duża mapa: mgła dalej
            RenderSettings.fogStartDistance = pr.fogStart * fk;
            RenderSettings.fogEndDistance = pr.fogEnd * fk;

            // --- sonda odbić nad zagrodą (lakier, chrom, szyby)
            var probe = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).FirstOrDefault();
            if (!probe) { var pg = new GameObject("Sonda odbić"); probe = pg.AddComponent<ReflectionProbe>(); }
            probe.transform.position = new Vector3(0f, 3f, 8f);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(90f, 30f, 90f);
            probe.resolution = 256;
            probe.boxProjection = false;
            probe.hdr = true;

            // --- postprocess
            var vol = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).FirstOrDefault(v => v.isGlobal);
            if (!vol) { var vg = new GameObject("Postprocess"); vol = vg.AddComponent<Volume>(); vol.isGlobal = true; }
            string profPath = GEN + "Postprocess_" + pora + ".asset";
            AssetDatabase.DeleteAsset(profPath);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, profPath);
            var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var bloom = prof.Add<Bloom>(true); bloom.intensity.Override(0.3f); bloom.threshold.Override(1.1f); bloom.scatter.Override(0.65f);
            var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(pr.postExposure); ca.contrast.Override(8f); ca.saturation.Override(pr.saturation);
            var wb = prof.Add<WhiteBalance>(true); wb.temperature.Override(pr.temperature); wb.tint.Override(pr.tint);
            var smh = prof.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.92f, 0.96f, 1.08f, 0f));   // chłodniejsze cienie, cieplejsze światła — bajkowy kontrast
            smh.highlights.Override(new Vector4(1.04f, 1.0f, 0.94f, 0f));
            var vig = prof.Add<Vignette>(true); vig.intensity.Override(0.16f); vig.smoothness.Override(0.5f);
            foreach (var c in prof.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, prof); }
            EditorUtility.SetDirty(prof);
            vol.sharedProfile = prof;

            // odświeżenie światła otoczenia z nieba (w edytorze; w grze robi to EnvironmentRefresh)
            DynamicGI.UpdateEnvironment();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Mleczarka] Oświetlenie: {pora}, HDRI {pr.hdri}, słońce {Mathf.Asin(toSun.y) * Mathf.Rad2Deg:0}° nad horyzontem.");
        }

        /// <summary>Jakość URP: PC — cienie 4 kaskady 4096 px do 110 m, miękkie, MSAA 4×, SSAO; telefon — lżej.</summary>
        public static void ConfigureURP()
        {
            Tune("Assets/Settings/PC_RPAsset.asset", 110f, 4, 4096, 4);
            Tune("Assets/Settings/Mobile_RPAsset.asset", 55f, 2, 2048, 2);
            AddRendererFeature("Assets/Settings/PC_Renderer.asset", "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion", "SSAO");
        }

        static void Tune(string path, float dist, int cascades, int res, int msaa)
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (!rp) return;
            var so = new SerializedObject(rp);
            void Set(string n, Action<SerializedProperty> a) { var p = so.FindProperty(n); if (p != null) a(p); }
            Set("m_ShadowDistance", p => p.floatValue = dist);
            Set("m_ShadowCascadeCount", p => p.intValue = cascades);
            Set("m_MainLightShadowmapResolution", p => p.intValue = res);
            Set("m_SoftShadowsSupported", p => p.boolValue = true);
            Set("m_SoftShadowQuality", p => p.intValue = cascades >= 4 ? 3 : 1);
            Set("m_MSAA", p => p.intValue = msaa);
            Set("m_SupportsHDR", p => p.boolValue = true);
            Set("m_MainLightShadowsSupported", p => p.boolValue = true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);
        }

        static void AddRendererFeature(string rendererPath, string typeName, string name)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            if (!data) return;
            if (data.rendererFeatures.Any(f => f && f.GetType().FullName == typeName)) return;
            var type = typeof(ScriptableRendererFeature).Assembly.GetType(typeName);
            if (type == null) { Debug.LogWarning("[Mleczarka] Brak typu " + typeName); return; }
            var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
            feature.name = name;
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            var so = new SerializedObject(data);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Mleczarka] Dodano efekt renderera: {name}");
        }
    }
}
