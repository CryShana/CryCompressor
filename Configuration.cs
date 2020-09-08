using System.Text.Json;
using System.IO;

namespace CryCompressor
{
    public class Configuration
    {
        public string InputDirectory { get; init; }
        public string OutputDirectory { get; init; }

        public VideoConfiguration VideoCompression { get; init; } = new VideoConfiguration();
        public ImageConfiguration ImageCompression { get; init; } = new ImageConfiguration();

        public string[] VideoExtensions { get; init; } = new string[] {
            "mp4", "mpg", "mts", "mov", "avi", "wmv", "webm", "flv", "mpeg", "mpv"
        };
        public string[] ImageExtensions { get; init; } = new string[] {
            "jpg", "jpeg", "png", "bmp"
        };
        public string[] IgnoredVideoCodecs { get; init; } = new string[] {
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
        public bool CompressVideos { get; init; } = true;
        public long MinSize { get; init; } = 1000 * 100; // 100kB
        public int MaxConcurrentWorkers { get; init; } = 1;

        public ParametersObject[] ParametersPriorityList { get; init; } = new ParametersObject[] {
            // first concurrent worker will use this
            new ParametersObject
            {
                Parameters = "-c:v hevc_nvenc -rc:v vbr_hq -cq:v 26 -preset slow -c:a copy", // cq goes from 0 - 51 (worst)
                Extension = "mp4"
            },
            
            // second concurrent worker will use this (and all others)
            new ParametersObject
            {
                Parameters = "-c:v libx265 -crf 26 -preset medium -c:a copy", // crf goes from 0 - 51 (worst)
                Extension = "mp4"
            }
        };
    }

    public class ImageConfiguration
    {
        public bool CompressImages { get; init; } = true;
        public long MinSize { get; init; } = 1000 * 30; // 30kB
        public int MaxConcurrentWorkers { get; init; } = 2;

        public ParametersObject[] ParametersPriorityList { get; init; } = new ParametersObject[] {
            new ParametersObject
            {
                Parameters = "-c:v libwebp -qscale 90",
                Extension = "webp"
            }
        };
    }

    public class ParametersObject
    {
        public string Parameters { get; set; }
        public string Extension { get; set; }
    }
}