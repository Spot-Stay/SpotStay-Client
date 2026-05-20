using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace 코빚노_프로젝트
{
    public static class FavoriteNameStore
    {
        private static readonly string FolderPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spot Stay");

        private static readonly string FilePath =
            Path.Combine(FolderPath, "favorite_names.json");

        private static Dictionary<string, string> LoadAll()
        {
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);

            if (!File.Exists(FilePath))
                return new Dictionary<string, string>();

            string json = File.ReadAllText(FilePath);

            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, string>();

            Dictionary<string, string> dict =
                JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

            return dict ?? new Dictionary<string, string>();
        }

        private static void SaveAll(Dictionary<string, string> dict)
        {
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);

            string json = JsonConvert.SerializeObject(dict, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }

        private static string MakeKey(string targetType, int targetId)
        {
            return targetType + "_" + targetId;
        }

        public static void SaveName(string targetType, int targetId, string name)
        {
            if (targetId <= 0)
                return;

            if (string.IsNullOrWhiteSpace(name))
                return;

            Dictionary<string, string> dict = LoadAll();

            string key = MakeKey(targetType, targetId);

            if (dict.ContainsKey(key))
                dict[key] = name;
            else
                dict.Add(key, name);

            SaveAll(dict);
        }

        public static string GetName(string targetType, int targetId)
        {
            Dictionary<string, string> dict = LoadAll();

            string key = MakeKey(targetType, targetId);

            if (dict.ContainsKey(key))
                return dict[key];

            return "";
        }
    }
}