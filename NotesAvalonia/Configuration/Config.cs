using Avalonia.Logging;
using Notes.Interface;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace NotesAvalonia.Configuration
{
    public static class Config
    {
        static readonly object lockject = new object();
        public static readonly string PersonalPath = Notes.Interface.Logger.PersonalPath;
        static readonly string configPath = PersonalPath + "config.json";
        static readonly string configBackupPath = PersonalPath + "config_backup.json";
        public static bool UnsavedChanges = false;

        // Same shape as NoteJson.Default plus converters for Avalonia structs (PixelPoint/Color)
        // that STJ cannot assign member-by-member. Uses the source-generated resolver so the
        // config also loads/stores under NativeAOT (reflection-based STJ disabled).
        static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = ConfigJsonContext.Default,
            Converters = { new PixelPointJsonConverter(), new ColorJsonConverter() }
        };
        public static ConfigData Data
        {
            get
            {
                lock (lockject)
                {
                    UnsavedChanges = true;
                    return data;
                }
            }
            set
            {
                UnsavedChanges = true;
                data = value;
            }
        }
        private static ConfigData data = new ConfigData();

        static Config()
        {
            if (Config.Exists())
                Config.Load();
            else
                Config.Data = new ConfigData();
        }

        public static string GetConfigPath()
        {
            return configPath;
        }
        public static bool Exists()
        {
            return File.Exists(configPath);
        }
        public static void Save()
        {
            lock (lockject)
            {
                if (File.Exists(configPath))
                    File.Copy(configPath, configBackupPath, true);
                File.WriteAllText(configPath, JsonSerializer.Serialize(Data, JsonOptions));

                UnsavedChanges = false;
            }
        }
        public static void Load()
        {
            lock (lockject)
            {
                if (Exists())
                    Data = JsonSerializer.Deserialize<ConfigData>(File.ReadAllText(configPath), JsonOptions) ?? Data;
                else
                    Data = new ConfigData();

                // Collapse redundant queued updates (per-keystroke duplicates etc.) that may have
                // accumulated while changes could not be delivered. The pruned file is written on
                // the next Save().
                Data.CompactUnsyncedChanges();
            }
        }
        public static void LoadFrom(string JSON)
        {
            lock (lockject)
            {
                Data = JsonSerializer.Deserialize<ConfigData>(JSON, JsonOptions) ?? Data;
            }
        }
        public static new string ToString()
        {
            string output = "";

            // ConfigData members are public properties now (a few remaining fields carry
            // [JsonInclude]); list both so the debug output stays useful.
            var members = typeof(ConfigData)
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Cast<System.Reflection.MemberInfo>()
                .Concat(typeof(ConfigData)
                    .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Where(f => f.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIncludeAttribute), false).Length > 0));
            foreach (var member in members)
            {
                object? value = member switch
                {
                    System.Reflection.PropertyInfo p => p.GetValue(Data),
                    System.Reflection.FieldInfo f => f.GetValue(Data),
                    _ => null
                };
                output += "\n" + member.Name + ": ";

                var valueType = (member as System.Reflection.PropertyInfo)?.PropertyType ?? (member as System.Reflection.FieldInfo)?.FieldType;
                if (valueType != typeof(string) && value is IEnumerable a)
                {
                    output += "\n";
                    foreach (var item in a)
                        output += item + ", ";
                }
                else
                {
                    output += value + "\n";
                }
            }

            return output;
        }
    }
}
