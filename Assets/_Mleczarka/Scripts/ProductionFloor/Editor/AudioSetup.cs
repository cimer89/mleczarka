using System.IO;
using System.Linq;
using Mleczarka.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>Muzyka i efekty z Assets/_Mleczarka/Audio: ustawienia importu (muzyka strumieniowo, efekty w pamięci) i obiekt „Dźwięk gry” w scenie.</summary>
    public static class AudioSetup
    {
        const string Dir = "Assets/_Mleczarka/Audio/";

        [MenuItem("Mleczarka/Kariera/11. Muzyka i dźwięki")]
        public static void Run()
        {
            if (Application.isPlaying) return;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Dir.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as AudioImporter; if (!imp) continue;
                bool music = path.Contains("/Muzyka/"), amb = path.Contains("Ambient");
                var s = imp.defaultSampleSettings;
                s.loadType = music ? AudioClipLoadType.Streaming : amb ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = music ? 0.6f : 0.75f;
                s.preloadAudioData = !music;
                imp.defaultSampleSettings = s; imp.forceToMono = !music && !amb; imp.loadInBackground = music;
                imp.SaveAndReimport();
            }
            var ga = Object.FindFirstObjectByType<GameAudio>();
            if (!ga) ga = new GameObject("Dźwięk gry").AddComponent<GameAudio>();
            AudioClip L(string p) => AssetDatabase.LoadAssetAtPath<AudioClip>(Dir + p);
            ga.musicDay = L("Muzyka/Muzyka_Dzien_Morning_at_the_Dairy.mp3");
            ga.musicEvening = L("Muzyka/Muzyka_Wieczor_Sunset_Kitchen.mp3");
            ga.ambient = L("SFX/Ambient_Wies.wav");
            ga.effects = AssetDatabase.FindAssets("t:AudioClip", new[] { Dir + "SFX" }).Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).Where(c => c && c.name != "Ambient_Wies").ToArray();
            EditorUtility.SetDirty(ga);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            string log = $"Muzyka: dzień {(ga.musicDay ? ga.musicDay.length.ToString("0") + " s" : "BRAK")}, wieczór {(ga.musicEvening ? ga.musicEvening.length.ToString("0") + " s" : "BRAK")}; tło {(ga.ambient ? "jest" : "BRAK")}; efekty: {string.Join(", ", ga.effects.Select(c => c.name))}";
            Directory.CreateDirectory("Screenshots"); File.WriteAllText("Screenshots/dzwiek.txt", log);
            Debug.Log("[Mleczarka] " + log);
        }
    }
}
