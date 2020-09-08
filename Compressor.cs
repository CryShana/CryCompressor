using CryMediaAPI;
using CryMediaAPI.Video;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using static CryCompressor.ColorConsole;

namespace CryCompressor
{
    public class Compressor
    {
        readonly Configuration configuration;
     
        int fileCount;
        int filesProcessed;
        bool started, queuedone;
        readonly CancellationToken tkn;
        TaskCompletionSource taskState;

        readonly ConcurrentQueue<string> fileQueue = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> videoQueue = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> imageQueue = new ConcurrentQueue<string>();    
        
        readonly ConcurrentQueue<string> errorQueue = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> warnQueue = new ConcurrentQueue<string>();

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
            _ = Task.Run(FileWorker, tkn);

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
                    // just copy these files
                    fileQueue.Enqueue(f);
                }
            }

            queuedone = true;

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
                if (warnQueue.TryDequeue(out string wrn))
                {
                    WriteEmpty();
                    WriteInfo(wrn);
                }

                if (lastCount != filesProcessed)
                {
                    lastCount = filesProcessed;

                    var fp = filesProcessed;
                    WriteUpdate(fileCount, fp);
                    if (fp == fileCount) break;
                }

                await Task.Delay(50);
            }

            taskState.SetResult();
        }

        string getPath(string filename)
        {
            var directory = Path.GetRelativePath(configuration.InputDirectory, Path.GetDirectoryName(filename));
            if (directory == ".") directory = "";

            var destinationDirectory = Path.Combine(configuration.OutputDirectory, directory);
            Directory.CreateDirectory(destinationDirectory);

            var destination = Path.Combine(destinationDirectory, Path.GetFileName(filename));

            return destination;
        }

        async Task VideoWorker()
        {
            var codecs = configuration.IgnoredVideoCodecs.Select(x => x.ToLower().Trim()).ToArray();

            while (!tkn.IsCancellationRequested)
            {
                if (videoQueue.TryDequeue(out string f))
                {
                    var dst = getPath(f);
                    Process p = null;

                    try
                    {
                        // convert
                        using var reader = new VideoReader(f);

                        await reader.LoadMetadataAsync();
                        var codec = reader.Metadata.Codec.ToLower();
                        if (codecs.Contains(codec))
                        {
                            // ignore and copy file to destination
                            File.Copy(f, dst, true);
                            continue;
                        }

                        var interlacefilter = "";
                        if (configuration.VideoCompression.InterlaceChecking)
                        {
                            // TODO: check if interlaced...

                            interlacefilter = configuration.VideoCompression.InterlaceFilter;
                        }

                        reader.Dispose();

                        // TODO: choose parameters based on priority list - try to take highermost parameters if not taken - if all taken, use last one
                        var parameters = "";

                        p = FFmpegWrapper.ExecuteCommand("ffmpeg", $"-i \"{f}\" {interlacefilter} {parameters} \"{dst}\"");
                        await p.WaitForExitAsync(tkn);

                        var result = new FileInfo(dst);
                        if (result.Length < 1000)
                        {
                            File.Delete(dst);
                            File.Copy(f, dst, true);
                            throw new Exception("Video conversion failed.");
                        }

                        if (configuration.DeleteResultIfBigger && result.Length > new FileInfo(f).Length)
                        {
                            File.Delete(dst);
                            File.Copy(f, dst, true);
                            warnQueue.Enqueue($"Converted video was larger than original, overwriting it. ('{f}')");
                        }
                    }
                    catch (Exception ex)
                    {
                        errorQueue.Enqueue($"Failed to convert video '{f}': {ex.Message}");
                    }
                    finally
                    {
                        if (p != null && !p.HasExited) p.Kill();

                        Interlocked.Increment(ref filesProcessed);
                    }
                }
                else if (queuedone) break;

                await Task.Delay(10);
            }
        }

        async Task ImageWorker()
        {
            while (!tkn.IsCancellationRequested)
            {
                if (imageQueue.TryDequeue(out string f))
                {
                    try
                    {
                        // convert
                    }
                    catch (Exception ex)
                    {
                        errorQueue.Enqueue($"Failed to convert image '{f}': {ex.Message}");
                    }
                    finally
                    {
                        Interlocked.Increment(ref filesProcessed);
                    }
                }
                else if (queuedone) break;

                await Task.Delay(10);
            }
        }

        async Task FileWorker()
        {
            while (!tkn.IsCancellationRequested)
            {
                if (fileQueue.TryDequeue(out string f))
                {
                    try
                    {
                        var dst = getPath(f);
                        File.Copy(f, dst, true);
                    }
                    catch (Exception ex)
                    {
                        errorQueue.Enqueue($"Failed to copy file '{f}': {ex.Message}");
                    }
                    finally
                    {
                        Interlocked.Increment(ref filesProcessed);
                    }
                }
                else if (queuedone) break;

                await Task.Delay(10);
            }
        }
    }
}