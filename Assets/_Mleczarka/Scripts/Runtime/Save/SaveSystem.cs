using System;
using System.Collections.Generic;
using System.IO;
using Mleczarka.Core;
using Mleczarka.Vehicles.Model;
using UnityEngine;

namespace Mleczarka.Save
{
    /// <summary>Plik zapisu: stan symulacji + konfiguracje pojazdów + wygląd postaci. Wersjonowany, z migracjami.</summary>
    [Serializable]
    public class SaveFile
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public string savedAtUtc;
        public GameState game;
        public List<VehicleLoadout> vehicles = new List<VehicleLoadout>();
        public string characterJson;      // wygląd postaci (kreator), osobny format modułu postaci
    }

    /// <summary>Zapis i odczyt JSON w Application.persistentDataPath (PC, Android, iOS).</summary>
    public static class SaveSystem
    {
        public static string Folder => Application.persistentDataPath;
        public static string PathFor(string slot) => System.IO.Path.Combine(Folder, $"mleczarka_{slot}.json");

        public static void Save(string slot, SaveFile data)
        {
            data.version = SaveFile.CurrentVersion;
            data.savedAtUtc = DateTime.UtcNow.ToString("o");
            string json = JsonUtility.ToJson(data, true);
            string path = PathFor(slot), tmp = path + ".tmp";
            File.WriteAllText(tmp, json);                 // najpierw plik tymczasowy, żeby awaria nie zniszczyła zapisu
            if (File.Exists(path)) File.Replace(tmp, path, path + ".bak"); else File.Move(tmp, path);
        }

        public static SaveFile Load(string slot)
        {
            string path = PathFor(slot);
            if (!File.Exists(path)) return null;
            var data = JsonUtility.FromJson<SaveFile>(File.ReadAllText(path));
            return Migrate(data);
        }

        public static bool Exists(string slot) => File.Exists(PathFor(slot));

        /// <summary>Kolejne wersje formatu dopisują tu swoje kroki (v1 → v2 → …).</summary>
        static SaveFile Migrate(SaveFile d)
        {
            if (d == null) return null;
            // if (d.version < 2) { ... d.version = 2; }
            d.version = SaveFile.CurrentVersion;
            return d;
        }

        public static string ToJson(GameState s) => JsonUtility.ToJson(s);
        public static GameState FromJson(string json) => JsonUtility.FromJson<GameState>(json);
    }
}
