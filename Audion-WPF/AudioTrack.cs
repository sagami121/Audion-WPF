using System.IO;

namespace Audion_WPF
{
    public sealed class AudioTrack
    {
        public AudioTrack(string filePath)
        {
            FilePath = filePath;
            Title = Path.GetFileNameWithoutExtension(filePath);
            Artist = string.Empty;
            Album = string.Empty;
            Subtitle = Path.GetDirectoryName(filePath) ?? "Local file";
            Extension = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
        }

        public string FilePath { get; private set; }

        public string Title { get; private set; }

        public string Artist { get; set; }

        public string Album { get; set; }

        public string Subtitle { get; set; }

        public string Extension { get; private set; }

        public string CoverPath { get; set; }

        public double DurationSeconds { get; set; }

        public string DisplaySubtitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(Album))
                {
                    return Artist + " - " + Album;
                }

                if (!string.IsNullOrWhiteSpace(Artist))
                {
                    return Artist;
                }

                if (!string.IsNullOrWhiteSpace(Album))
                {
                    return Album;
                }

                return Subtitle;
            }
        }
    }
}
