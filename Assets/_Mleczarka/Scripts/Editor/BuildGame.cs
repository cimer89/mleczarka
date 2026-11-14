using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mleczarka.EditorTools
{
    /// <summary>Wersja gry do uruchomienia bez edytora (Windows): Builds/Windows/Mleczarka.exe, scena startowa Gmina_Podlasie.</summary>
    public static class BuildGame
    {
        const string Scene = "Assets/_Mleczarka/Scenes/Gmina_Podlasie.unity";

        [MenuItem("Mleczarka/Zbuduj grę dla Windows (.exe)", priority = 100)]
        public static void BuildWindows()
        {
            if (!File.Exists(Scene)) { Debug.LogError("[Mleczarka] Brak sceny " + Scene); return; }
            string dir = Path.GetFullPath("Builds/Windows");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = Path.Combine(dir, "Mleczarka.exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            PlayerSettings.productName = "Mleczarka";
            PlayerSettings.companyName = "Mleczarka";
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            string msg = $"[Mleczarka] Budowanie: {s.result}, {s.totalSize / (1024f * 1024f):0} MB, {s.totalTime.TotalMinutes:0.0} min, błędy: {s.totalErrors}. Plik: {options.locationPathName}";
            Directory.CreateDirectory("Screenshots");
            File.WriteAllText("Screenshots/budowanie.txt", msg + "\n" + string.Join("\n", report.steps.SelectMany(st => st.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Take(30)));
            if (s.result == BuildResult.Succeeded)
            {
                // Paczka do przeniesienia na inny komputer: bez folderów debugowania.
                string zip = Path.GetFullPath("Builds/Mleczarka_Windows.zip");
                if (File.Exists(zip)) File.Delete(zip);
                foreach (var d in Directory.GetDirectories(dir, "*DoNotShip*")) Directory.Delete(d, true);
                System.IO.Compression.ZipFile.CreateFromDirectory(dir, zip, System.IO.Compression.CompressionLevel.Optimal, true);
                msg += $" · ZIP: {new FileInfo(zip).Length / (1024f * 1024f):0} MB ({zip})";
                File.WriteAllText("Screenshots/budowanie.txt", msg);
                Debug.Log(msg);
            }
            else Debug.LogError(msg);
        }
    }
}
