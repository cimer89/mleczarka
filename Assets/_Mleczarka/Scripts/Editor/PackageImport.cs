using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mleczarka.EditorTools
{
    /// <summary>Importuje paczki .unitypackage z folderu Import/ obok Assets (zasoby przesłane w kilku częściach).</summary>
    public static class PackageImport
    {
        [MenuItem("Mleczarka/0. Importuj paczki z folderu Import", priority = 0)]
        public static void ImportAll()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Import"));
            if (!Directory.Exists(dir)) { Debug.LogWarning("[Mleczarka] Brak folderu " + dir); return; }
            var files = Directory.GetFiles(dir, "*.unitypackage");
            System.Array.Sort(files);
            foreach (var f in files)
            {
                Debug.Log("[Mleczarka] Import paczki " + Path.GetFileName(f));
                AssetDatabase.ImportPackage(f, false);
            }
        }
    }
}
