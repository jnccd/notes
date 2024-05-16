﻿using Newtonsoft.Json;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Configuration
{
    public static class Config
    {
        static readonly object lockject = new object();
        static readonly string exePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar;
        static readonly string configPath = exePath + "config.json";
        static readonly string configBackupPath = exePath + "config_backup.json";
        public static bool UnsavedChanges = false;
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
                File.WriteAllText(configPath, JsonConvert.SerializeObject(Data, Formatting.Indented));

                UnsavedChanges = false;
            }
        }
        public static void Load()
        {
            lock (lockject)
            {
                if (Exists())
                    Data = JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(configPath));
                else
                    Data = new ConfigData();
            }
        }
        public static void LoadFrom(string JSON)
        {
            lock (lockject)
            {
                Data = JsonConvert.DeserializeObject<ConfigData>(JSON);
            }
        }
        public static new string ToString()
        {
            string output = "";

            FieldInfo[] Infos = typeof(ConfigData).GetFields();
            foreach (FieldInfo info in Infos)
            {
                output += "\n" + info.Name + ": ";

                if (info.FieldType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(info.FieldType))
                {
                    output += "\n";
                    IEnumerable a = (IEnumerable)info.GetValue(Data);
                    IEnumerator e = a.GetEnumerator();
                    e.Reset();
                    while (e.MoveNext())
                    {
                        output += e.Current + ", ";
                    }
                }
                else
                {
                    output += info.GetValue(Data) + "\n";
                }
            }

            return output;
        }
    }
}
