using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using UnityEngine;

namespace VehiclePreSelection
{
    internal static class PersistedRouteSelectionStore
    {
        [DataContract]
        internal sealed class PersistedSelectionFile
        {
            [DataMember]
            public List<PersistedRouteSelection> routes = new List<PersistedRouteSelection>();

            [DataMember]
            public List<PersistedColorPreference> colors = new List<PersistedColorPreference>();
        }

        [DataContract]
        internal sealed class PersistedRouteSelection
        {
            [DataMember]
            public string routeKey;

            [DataMember]
            public List<string> primary = new List<string>();

            [DataMember]
            public List<string> secondary = new List<string>();
        }

        [DataContract]
        internal sealed class PersistedColorPreference
        {
            [DataMember]
            public string key;

            [DataMember]
            public bool enabled;
        }

        private static readonly string s_Path =
            Path.Combine(Application.persistentDataPath, "ModsData", "VehiclePreSelection", "vehicle-selections.json");

        internal static string PathValue => s_Path;

        internal static PersistedSelectionFile Load()
        {
            try
            {
                if (!File.Exists(s_Path))
                {
                    return new PersistedSelectionFile();
                }

                var json = File.ReadAllText(s_Path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new PersistedSelectionFile();
                }

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var serializer = new DataContractJsonSerializer(typeof(PersistedSelectionFile));
                var file = serializer.ReadObject(stream) as PersistedSelectionFile ?? new PersistedSelectionFile();
                file.routes ??= new List<PersistedRouteSelection>();
                file.colors ??= new List<PersistedColorPreference>();
                return file;
            }
            catch
            {
                return new PersistedSelectionFile();
            }
        }

        internal static void Save(PersistedSelectionFile file)
        {
            file.routes ??= new List<PersistedRouteSelection>();
            var directory = System.IO.Path.GetDirectoryName(s_Path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(PersistedSelectionFile));
            serializer.WriteObject(stream, file);
            File.WriteAllText(s_Path, Encoding.UTF8.GetString(stream.ToArray()));
        }

        internal static PersistedRouteSelection Find(PersistedSelectionFile file, string routeKey)
        {
            if (file?.routes == null || string.IsNullOrEmpty(routeKey))
            {
                return null;
            }

            for (var i = 0; i < file.routes.Count; i++)
            {
                if (file.routes[i].routeKey == routeKey)
                {
                    return file.routes[i];
                }
            }

            return null;
        }

        internal static PersistedRouteSelection FindOrCreate(PersistedSelectionFile file, string routeKey)
        {
            var existing = Find(file, routeKey);
            if (existing != null)
            {
                return existing;
            }

            var created = new PersistedRouteSelection { routeKey = routeKey };
            file.routes.Add(created);
            return created;
        }

        internal static PersistedColorPreference FindColorPreference(PersistedSelectionFile file, string key)
        {
            if (file?.colors == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            for (var i = 0; i < file.colors.Count; i++)
            {
                if (file.colors[i].key == key)
                {
                    return file.colors[i];
                }
            }

            return null;
        }

        internal static PersistedColorPreference FindOrCreateColorPreference(PersistedSelectionFile file, string key)
        {
            var existing = FindColorPreference(file, key);
            if (existing != null)
            {
                return existing;
            }

            var created = new PersistedColorPreference { key = key };
            file.colors.Add(created);
            return created;
        }
    }
}
