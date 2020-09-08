using System;
using System.IO;
using static CryCompressor.ColorConsole;

namespace CryCompressor
{
    public class Compressor
    {
        Configuration configuration;

        public Compressor(Configuration config)
        {
            configuration = config;
        }

        public void Start()
        {
            WriteInfo("Getting files");
            var files = Directory.GetFiles(configuration.InputDirectory, "*.*", SearchOption.AllDirectories);
            WriteInfo($"Found {files.Length} files.");
        }
    }
}