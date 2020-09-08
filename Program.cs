﻿using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using static CryCompressor.ColorConsole;

namespace CryCompressor
{
    class Program
    {
        const string CONFIG_PATH = "compressor-config.jsonc";

        static void Main(string[] args)
        {
            if (!File.Exists(CONFIG_PATH))
            {
                Configuration.Create(CONFIG_PATH);
                WriteLine("No configuration file found. Created new one.");
                return;
            }

            try
            {
                var config = Configuration.Load(CONFIG_PATH);

                // VALIDATE CONFIGURATION
                if (string.IsNullOrEmpty(config.InputDirectory) || !Directory.Exists(config.InputDirectory)) throw new Exception("Invalid input directory!");
                if (string.IsNullOrEmpty(config.OutputDirectory) || !Directory.Exists(config.OutputDirectory)) throw new Exception("Invalid output directory!");
                if (config.ImageExtensions == null || config.VideoExtensions == null) throw new Exception("Image/video extensions can not be null!");
                if (config.VideoCompression == null || config.ImageCompression == null) throw new Exception("Image/video compression configurations can not be null!");
                if (config.VideoCompression.ParametersPriorityList == null || config.VideoCompression.ParametersPriorityList.Length == 0) throw new Exception("Video parameters priority list can not be empty!");
                if (config.ImageCompression.ParametersPriorityList == null || config.ImageCompression.ParametersPriorityList.Length == 0) throw new Exception("Image parameters priority list can not be empty!");
                if (config.VideoCompression.MaxConcurrentWorkers <= 0) throw new Exception("Video max. concurrent workers can not be 0 or less!");
                if (config.ImageCompression.MaxConcurrentWorkers <= 0) throw new Exception("Video max. concurrent workers can not be 0 or less!");
     
                // START WORK
                var sw = Stopwatch.StartNew();
                var c = new Compressor(config);
                c.Start();

                sw.Stop();
                WriteLine($"Done [{sw.Elapsed.TotalMilliseconds.ToTimeString()}]");
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