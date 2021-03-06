using System.Text.Json;
using System.IO;

namespace CryCompressor
{
    public class Configuration
    {
        public string InputDirectory { get; set; }
        public string OutputDirectory { get; set; }

        public VideoConfiguration VideoCompression { get; set; } = new VideoConfiguration();
        public ImageConfiguration ImageCompression { get; set; } = new ImageConfiguration();
        public AudioConfiguration AudioCompression { get; set; } = new AudioConfiguration();

        public string[] VideoExtensions { get; set; } = new string[] {
            "mp4", "mpg", "mts", "mov", "avi", "wmv", "webm", "flv", "mpeg", "mpv"
        };
        public string[] ImageExtensions { get; set; } = new string[] {
            "jpg", "jpeg", "png", "bmp"
        };
        public string[] AudioExtensions { get; set; } = new string[] {
            "wav", "ogg", "oga", "wma", "mp3", "aac", "flac", "m4a"
        };

        public string[] IgnoredVideoCodecs { get; set; } = new string[] {
            "h265", "hevc", "vp9", "av1"
        };

        public bool DeleteResultIfBigger { get; set; } = true;

        public static Configuration Load(string path) => JsonSerializer.Deserialize<Configuration>(File.ReadAllBytes(path), new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        });

        public static void Create(string path)
        {
            var config = new Configuration();
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                IgnoreNullValues = false,
                IgnoreReadOnlyProperties = true
            });

            File.WriteAllText(path, json);
        }
    }

    public class VideoConfiguration
    {
        public bool CompressVideos { get; set; } = true;
        public long MinSize { get; set; } = 1000 * 100;
        public int MaxConcurrentWorkers { get; set; } = 1;
        public bool RandomSuffixOnDifferentExtension { get; set; } = true;      

        public ParametersObject[] ParametersPriorityList { get; set; } = new ParametersObject[] {
            // first concurrent worker will use this
            new ParametersObject
            {
                Parameters = "-c:v hevc_nvenc -rc:v vbr_hq -cq:v 26 -preset slow -c:a aac -b:a 256k -f mp4", // cq goes from 0 - 51 (worst)
                Extension = "mp4"
            },
            
            // second concurrent worker will use this (and all others)
            new ParametersObject
            {
                Parameters = "-c:v libx265 -crf 26 -preset medium -c:a aac -b:a 256k -f mp4", // crf goes from 0 - 51 (worst)
                Extension = "mp4"
            }
        };
    }

    public class ImageConfiguration
    {
        public bool CompressImages { get; set; } = true;
        public long MinSize { get; set; } = 1000 * 30;
        public int MaxConcurrentWorkers { get; set; } = 4;
        public bool RandomSuffixOnDifferentExtension { get; set; } = true;

        public ParametersObject[] ParametersPriorityList { get; set; } = new ParametersObject[] {
            new ParametersObject
            {
                Parameters = "-c:v libwebp -qscale 83",
                Extension = "webp"
            }
        };
    }

    public class AudioConfiguration
    {
        public bool CompressAudio { get; set; } = true;
        public long MinSize { get; set; } = 1000 * 30;
        public int MaxConcurrentWorkers { get; set; } = 4;
        public bool RandomSuffixOnDifferentExtension { get; set; } = true;  

        public ParametersObject[] ParametersPriorityList { get; set; } = new ParametersObject[] {
            new ParametersObject
            {
                Parameters = "-c:a libopus -b:a 256k -vn",
                Extension = "ogg"
            }
        };
    }

    public class ParametersObject
    {
        public string Parameters { get; set; }
        public string Extension { get; set; }
    }
}