using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Audion_WPF
{
    [DataContract]
    public sealed class AppSettings
    {
        [DataMember(Name = "shuffle")]
        public bool Shuffle { get; set; }

        [DataMember(Name = "repeat")]
        public string Repeat { get; set; }

        [DataMember(Name = "volume")]
        public double Volume { get; set; }

        [DataMember(Name = "muted")]
        public bool Muted { get; set; }

        [DataMember(Name = "lang")]
        public string Language { get; set; }

        [DataMember(Name = "theme")]
        public string Theme { get; set; }

        [DataMember(Name = "speed")]
        public double Speed { get; set; }

        [DataMember(Name = "restoreSession")]
        public bool RestoreSession { get; set; }

        [DataMember(Name = "showLyrics")]
        public bool ShowLyrics { get; set; }

        [DataMember(Name = "sidebarWidth")]
        public double SidebarWidth { get; set; }

        [DataMember(Name = "alwaysOnTop")]
        public bool AlwaysOnTop { get; set; }

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                Shuffle = false,
                Repeat = "none",
                Volume = 80,
                Muted = false,
                Language = "ja",
                Theme = "system",
                Speed = 1.0,
                RestoreSession = true,
                ShowLyrics = false,
                SidebarWidth = 340,
                AlwaysOnTop = false
            };
        }

        public static AppSettings Load(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return CreateDefault();
                }

                using (var stream = File.OpenRead(path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    var loaded = serializer.ReadObject(stream) as AppSettings;
                    return loaded ?? CreateDefault();
                }
            }
            catch
            {
                return CreateDefault();
            }
        }

        public void Save(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var stream = File.Create(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                serializer.WriteObject(stream, this);
            }
        }
    }

    [DataContract]
    public sealed class PlaylistFile
    {
        [DataMember(Name = "tracks")]
        public List<PlaylistTrack> Tracks { get; set; }
    }

    [DataContract]
    public sealed class PlaylistTrack
    {
        [DataMember(Name = "filePath")]
        public string FilePath { get; set; }
    }
}
