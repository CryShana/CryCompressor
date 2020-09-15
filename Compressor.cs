using CryMediaAPI;
using CryMediaAPI.Video;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        readonly ConcurrentQueue<string> audioQueue = new ConcurrentQueue<string>();

        readonly ConcurrentQueue<string> errorQueue = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> warnQueue = new ConcurrentQueue<string>();

        readonly SemaphoreSlim videoParamsSemaphore = new SemaphoreSlim(1);
        readonly Dictionary<int, bool> videoParams = new Dictionary<int, bool>();
        readonly SemaphoreSlim imageParamsSemaphore = new SemaphoreSlim(1);
        readonly Dictionary<int, bool> imageParams = new Dictionary<int, bool>();
        readonly SemaphoreSlim audioParamsSemaphore = new SemaphoreSlim(1);
        readonly Dictionary<int, bool> audioParams = new Dictionary<int, bool>();

        public Compressor(Configuration config, CancellationToken token)
        {
            tkn = token;
            configuration = config;

            for (int i = 0; i < config.VideoCompression.ParametersPriorityList.Length; i++) videoParams.Add(i, false);
            for (int i = 0; i < config.ImageCompression.ParametersPriorityList.Length; i++) imageParams.Add(i, false);
            for (int i = 0; i < config.AudioCompression.ParametersPriorityList.Length; i++) audioParams.Add(i, false);
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
            for (int i = 0; i < configuration.AudioCompression.MaxConcurrentWorkers; i++) _ = Task.Run(AudioWorker, tkn);
            _ = Task.Run(FileWorker, tkn);

            // Start up progress logger
            _ = Task.Run(Logger, tkn);

            // Start filtering files and queueing them for work
            var vidExtensions = configuration.VideoExtensions.Select(x => "." + x.ToLower().Trim().GetExtensionWithoutDot()).ToArray();
            var imgExtensions = configuration.ImageExtensions.Select(x => "." + x.ToLower().Trim().GetExtensionWithoutDot()).ToArray();
            var audExtensions = configuration.AudioExtensions.Select(x => "." + x.ToLower().Trim().GetExtensionWithoutDot()).ToArray();
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
                else if (configuration.AudioCompression.CompressAudio && audExtensions.Contains(extension) && size >= configuration.AudioCompression.MinSize)
                {
                    // AUDIO
                    audioQueue.Enqueue(f);
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
                    Console.Write("\r");
                    WriteError(err);
                }
                if (warnQueue.TryDequeue(out string wrn))
                {
                    WriteEmpty();
                    Console.Write("\r");
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

        readonly static Random rng = new();
        (string destination_unchanged, string destination_modified) GetPath(string filename, string extension = null, bool generateSuffix = true)
        {
            extension = extension?.GetExtensionWithoutDot();

            var directory = Path.GetRelativePath(configuration.InputDirectory, Path.GetDirectoryName(filename));
            if (directory == ".") directory = "";

            var destinationDirectory = Path.Combine(configuration.OutputDirectory, directory);
            Directory.CreateDirectory(destinationDirectory);

            var fileNameNoext = Path.GetFileNameWithoutExtension(filename);
            var fileExtension = Path.GetExtension(filename).GetExtensionWithoutDot();

            if (extension == null) extension = fileExtension;

            // if defined extension is different from original, add a unique suffix to avoid
            // rare cases of multiple files with same names but different extensions merging into one
            if (generateSuffix && fileExtension.ToLower() != extension.ToLower()) fileNameNoext += $"({rng.Next(0, 1000)}-{fileExtension})";

            var destination_unchanged = Path.Combine(destinationDirectory, Path.GetFileName(filename));
            var destination_modified = Path.Combine(destinationDirectory, fileNameNoext + "." + extension);

            return (destination_unchanged, destination_modified);
        }

        async Task VideoWorker()
        {
            var codecs = configuration.IgnoredVideoCodecs.Select(x => x.ToLower().Trim()).ToArray();

            while (!tkn.IsCancellationRequested)
            {
                if (videoQueue.TryDequeue(out string f))
                {
                    Process p = null;

                    // choose parameters based on priority list
                    var (pIndex, parameters) = await TakeVideoParameters();
                    var (dst_orig, dst) = GetPath(f, parameters.Extension, configuration.VideoCompression.RandomSuffixOnDifferentExtension);

                    string output = "";
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

                        reader.Dispose();

                        p = FFmpegWrapper.ExecuteCommand("ffmpeg", $"-hide_banner -i \"{f}\" {parameters.Parameters} \"{dst}\" -y");
                        p.ErrorDataReceived += (s, data) =>
                        {
                            if (data.Data == null) return;
                            output += data.Data + "\n";
                        };

                        await p.WaitForExitAsync(tkn);
                        var code = p.ExitCode;

                        // validate result
                        var result = new FileInfo(dst);
                        if (result.Length < 1000 || code != 0)
                        {
                            File.Delete(dst);
                            File.Copy(f, dst, true);
                            throw new Exception($"Video conversion failed. (Code: {code}). Output:\n{output}");
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
                        // in rare case it hasn't been copied over, copy it (use original filename for destination now instead)
                        if (!File.Exists(dst)) File.Copy(f, dst_orig, true);

                        errorQueue.Enqueue($"Failed to convert video '{f}': {ex.Message}" + (output.Length > 0 ? $"\n\nFFmpeg output:\n{output}" : ""));
                    }
                    finally
                    {
                        await ReleaseVideoParameters(pIndex);

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
                    Process p = null;

                    // choose parameters based on priority list
                    var (pIndex, parameters) = await TakeImageParameters();

                    var (dst_orig, dst) = GetPath(f, parameters.Extension, configuration.ImageCompression.RandomSuffixOnDifferentExtension);

                    string output = "";
                    try
                    {
                        // convert
                        using var reader = new VideoReader(f);

                        await reader.LoadMetadataAsync();

                        reader.Dispose();

                        p = FFmpegWrapper.ExecuteCommand("ffmpeg", $"-hide_banner -i \"{f}\" {parameters.Parameters} \"{dst}\" -y");
                        p.ErrorDataReceived += (s, data) =>
                        {
                            if (data.Data == null) return;
                            output += data.Data + "\n";
                        };

                        await p.WaitForExitAsync(tkn);
                        var code = p.ExitCode;

                        // validate result
                        var result = new FileInfo(dst);
                        if (result.Length < 1000 || code != 0)
                        {
                            File.Delete(dst);
                            File.Copy(f, dst, true);
                            throw new Exception($"Image conversion failed. (Code: {code}). Output:\n{output}");
                        }
                        if (configuration.DeleteResultIfBigger && result.Length > new FileInfo(f).Length)
                        {
                            File.Delete(dst);
                            File.Copy(f, dst, true);
                            warnQueue.Enqueue($"Converted image was larger than original, overwriting it. ('{f}')");
                        }
                    }
                    catch (Exception ex)
                    {
                        // in rare case it hasn't been copied over, copy it (use original filename for destination now instead)
                        if (!File.Exists(dst)) File.Copy(f, dst_orig, true);

                        errorQueue.Enqueue($"Failed to convert image '{f}': {ex.Message}" + (output.Length > 0 ? $"\n\nFFmpeg output:\n{output}" : ""));
                    }
                    finally
                    {
                        await ReleaseImageParameters(pIndex);

                        if (p != null && !p.HasExited) p.Kill();

                        Interlocked.Increment(ref filesProcessed);
                    }
                }
                else if (queuedone) break;

                await Task.Delay(10);
            }
        }

        async Task AudioWorker()
        {
            while (!tkn.IsCancellationRequested)
            {
                if (audioQueue.TryDequeue(out string f))
                {
                    Process p = null;

                    // choose parameters based on priority list
                    var (pIndex, parameters) = await TakeAudioParameters();

                    var (dst_orig, dst) = GetPath(f, parameters.Extension, configuration.AudioCompression.RandomSuffixOnDifferentExtension);

                    string output = "";
                    try
                    {
                        // convert
                        using var reader = new VideoReader(f);

                        await reader.LoadMetadataAsync();

                        reader.Dispose();

                        p = FFmpegWrapper.ExecuteCommand("ffmpeg", $"-hide_banner -i \"{f}\" {parameters.Parameters} \"{dst}\" -y");
                        p.ErrorDataReceived += (s, data) =>
                        {
                            if (data.Data == null) return;
                            output += data.Data + "\n";
                        };

                        await p.WaitForExitAsync(tkn);
                        var code = p.ExitCode;

                        // validate result
                        var result = new FileInfo(dst);
                        if (result.Length < 1000 || code != 0)
                        {
                            File.Delete(dst);
                            File.Copy(f, dst, true);
                            throw new Exception($"Audio conversion failed. (Code: {code}). Output:\n{output}");
                        }
                        if (configuration.DeleteResultIfBigger && result.Length > new FileInfo(f).Length)
                        {
                            File.Delete(dst);
                            File.Copy(f, dst, true);
                            warnQueue.Enqueue($"Converted audio was larger than original, overwriting it. ('{f}')");
                        }
                    }
                    catch (Exception ex)
                    {
                        // in rare case it hasn't been copied over, copy it (use original filename for destination now instead)
                        if (!File.Exists(dst)) File.Copy(f, dst_orig, true);

                        errorQueue.Enqueue($"Failed to convert audio '{f}': {ex.Message}" + (output.Length > 0 ? $"\n\nFFmpeg output:\n{output}" : ""));
                    }
                    finally
                    {
                        await ReleaseAudioParameters(pIndex);

                        if (p != null && !p.HasExited) p.Kill();

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
                        var (_, dst) = GetPath(f);
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

        #region Getting Parameters
        async Task<(int Index, ParametersObject Parameters)> TakeVideoParameters()
        {
            // pick the first parameters available - if last, take last one.

            await videoParamsSemaphore.WaitAsync();

            try
            {
                for (int i = 0; i < configuration.VideoCompression.ParametersPriorityList.Length; i++)
                {
                    if (!videoParams[i])
                    {
                        videoParams[i] = true;
                        return (i, configuration.VideoCompression.ParametersPriorityList[i]);
                    }
                }

                // always return last one if all else is taken
                return (
                    configuration.VideoCompression.ParametersPriorityList.Length - 1,
                    configuration.VideoCompression.ParametersPriorityList.Last());
            }
            finally
            {
                videoParamsSemaphore.Release();
            }
        }

        async Task ReleaseVideoParameters(int index)
        {
            await videoParamsSemaphore.WaitAsync();

            try
            {
                videoParams[index] = false;
            }
            finally
            {
                videoParamsSemaphore.Release();
            }
        }

        async Task<(int Index, ParametersObject Parameters)> TakeImageParameters()
        {
            // pick the first parameters available - if last, take last one.

            await imageParamsSemaphore.WaitAsync();

            try
            {
                for (int i = 0; i < configuration.ImageCompression.ParametersPriorityList.Length; i++)
                {
                    if (!imageParams[i])
                    {
                        imageParams[i] = true;
                        return (i, configuration.ImageCompression.ParametersPriorityList[i]);
                    }
                }

                // always return last one if all else is taken
                return (
                    configuration.ImageCompression.ParametersPriorityList.Length - 1,
                    configuration.ImageCompression.ParametersPriorityList.Last());
            }
            finally
            {
                imageParamsSemaphore.Release();
            }
        }

        async Task ReleaseImageParameters(int index)
        {
            await imageParamsSemaphore.WaitAsync();

            try
            {
                imageParams[index] = false;
            }
            finally
            {
                imageParamsSemaphore.Release();
            }
        }

        async Task<(int Index, ParametersObject Parameters)> TakeAudioParameters()
        {
            // pick the first parameters available - if last, take last one.

            await audioParamsSemaphore.WaitAsync();

            try
            {
                for (int i = 0; i < configuration.AudioCompression.ParametersPriorityList.Length; i++)
                {
                    if (!audioParams[i])
                    {
                        audioParams[i] = true;
                        return (i, configuration.AudioCompression.ParametersPriorityList[i]);
                    }
                }

                // always return last one if all else is taken
                return (
                    configuration.AudioCompression.ParametersPriorityList.Length - 1,
                    configuration.AudioCompression.ParametersPriorityList.Last());
            }
            finally
            {
                audioParamsSemaphore.Release();
            }
        }

        async Task ReleaseAudioParameters(int index)
        {
            await audioParamsSemaphore.WaitAsync();

            try
            {
                audioParams[index] = false;
            }
            finally
            {
                audioParamsSemaphore.Release();
            }
        }
        #endregion
    }
}