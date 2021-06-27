using System;
using System.IO;
using System.Threading;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using static CryCompressor.ColorConsole;
using CryMediaAPI;

namespace CryCompressor
{
    class Program
    {
        const string CONFIG_PATH = "compressor-config.jsonc";

        static async Task Main()
        {
            if (!File.Exists(CONFIG_PATH))
            {
                Configuration.Create(CONFIG_PATH);
                Console.WriteLine("No configuration file found. Created new one.");
                return;
            }

            try
            {
                var config = Configuration.Load(CONFIG_PATH); 

                // VALIDATE CONFIGURATION
                if (string.IsNullOrEmpty(config.InputDirectory) || !Directory.Exists(config.InputDirectory)) throw new Exception("Invalid input directory!");
                if (string.IsNullOrEmpty(config.OutputDirectory) || !Directory.Exists(config.OutputDirectory)) throw new Exception("Invalid output directory!");
                
                if (config.ImageExtensions == null || config.VideoExtensions == null || config.AudioExtensions == null) throw new Exception("Extensions can not be null!");
                if (config.VideoCompression == null || config.ImageCompression == null || config.AudioCompression == null) throw new Exception("Compression configurations can not be null!");

                if (config.VideoCompression.ParametersPriorityList == null || config.VideoCompression.ParametersPriorityList.Length == 0) throw new Exception("Video parameters priority list can not be empty!");
                if (config.ImageCompression.ParametersPriorityList == null || config.ImageCompression.ParametersPriorityList.Length == 0) throw new Exception("Image parameters priority list can not be empty!");
                if (config.AudioCompression.ParametersPriorityList == null || config.AudioCompression.ParametersPriorityList.Length == 0) throw new Exception("Audio parameters priority list can not be empty!");
                
                if (config.VideoCompression.MaxConcurrentWorkers <= 0) throw new Exception("Video max. concurrent workers can not be 0 or less!");
                if (config.ImageCompression.MaxConcurrentWorkers <= 0) throw new Exception("Image max. concurrent workers can not be 0 or less!");
                if (config.AudioCompression.MaxConcurrentWorkers <= 0) throw new Exception("Image max. concurrent workers can not be 0 or less!");

                try
                {
                    // CHECK IF FFMPEG PRESENT
                    var encoders = FFmpegWrapper.GetEncoders();
                }
                catch (Exception e)
                {
                    throw new Exception("Failed to use FFmpeg! Make sure it's accessible! " + e.Message);
                }

                // HANDLE CANCEL EVENT
                var csc = new CancellationTokenSource();
                Console.CancelKeyPress += (a, b) =>
                {
                    csc.Cancel();
                    b.Cancel = true;
                };

                // START WORK
                var sw = Stopwatch.StartNew();
                var c = new Compressor(config, csc.Token);
                await c.Start();

                sw.Stop();
                Console.WriteLine();
                WriteInfo($"Done ({sw.Elapsed.TotalMilliseconds.ToTimeString()})");
            }
            catch (JsonException)
            {
                WriteError("ERROR: Failed to parse configuration file!");
            }
            catch (Exception ex)
            {
                WriteError("ERROR: " + ex.Message);
            }
        }
    }
}
