using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using static CryCompressor.ColorConsole;

namespace CryCompressor
{
    public class Compressor
    {
        Configuration configuration;

        bool started;
        int fileCount;
        int filesProcessed;
        CancellationToken tkn;
        TaskCompletionSource taskState;

        ConcurrentQueue<string> videoQueue = new ConcurrentQueue<string>();
        ConcurrentQueue<string> imageQueue = new ConcurrentQueue<string>();
        ConcurrentQueue<string> errorQueue = new ConcurrentQueue<string>();

        public Compressor(Configuration config, CancellationToken token)
        {
            tkn = token;
            configuration = config;
        }

        public Task Start()
        {
            if (started) throw new InvalidOperationException("Compressor already started!");
            started = true;

            taskState = new TaskCompletionSource();

            // get all files
            WriteInfo("Getting files");
            var files = Directory.GetFiles(configuration.InputDirectory, "*.*", SearchOption.AllDirectories);
            WriteInfo($"Found {files.Length} files.");
            fileCount = files.Length;

            // Start up workers
            for (int i = 0; i < configuration.VideoCompression.MaxConcurrentWorkers; i++) _ = Task.Run(VideoWorker, tkn);
            for (int i = 0; i < configuration.ImageCompression.MaxConcurrentWorkers; i++) _ = Task.Run(ImageWorker, tkn);

            // Start up progress logger
            _ = Task.Run(Logger, tkn);

            // Start filtering files and queueing them for work
            var vidExtensions = configuration.VideoExtensions.Select(x => "." + x.ToLower().Trim()).ToArray();
            var imgExtensions = configuration.ImageExtensions.Select(x => "." + x.ToLower().Trim()).ToArray();
            foreach (var f in files)
            {
                var extension = Path.GetExtension(f).ToLower().Trim();
                var size = new FileInfo(f).Length;

                if (configuration.VideoCompression.CompressVideos && vidExtensions.Contains(extension) && size >= configuration.VideoCompression.MinSize)
                {
                    // VIDEO
                    videoQueue.Enqueue(f);
                }
                else if (configuration.ImageCompression.CompressImages && imgExtensions.Contains(extension) && size >= configuration.ImageCompression.MinSize)
                {
                    // IMAGE
                    imageQueue.Enqueue(f);
                }
                else
                {
                    // ignore these files
                    Interlocked.Increment(ref filesProcessed);
                }
            }

            return taskState.Task;
        }

        async Task Logger()
        {
            int lastCount = -1;
            while (!tkn.IsCancellationRequested)
            {
                if (errorQueue.TryDequeue(out string err))
                {
                    WriteEmpty();
                    WriteError(err);
                }

                if (lastCount != filesProcessed)
                {
                    lastCount = filesProcessed;

                    WriteUpdate(fileCount, filesProcessed);
                    if (filesProcessed == fileCount) break;
                }

                await Task.Delay(100);
            }

            taskState.SetResult();
        }

        async Task VideoWorker()
        {
            while (!tkn.IsCancellationRequested)
            {
                // TODO: handle videos

                await Task.Delay(100);
            }
        }

        async Task ImageWorker()
        {
            while (!tkn.IsCancellationRequested)
            {
                // TODO: handle images

                await Task.Delay(100);
            }
        }
    }
}