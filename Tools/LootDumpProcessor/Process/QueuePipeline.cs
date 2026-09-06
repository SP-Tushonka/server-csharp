using System.Collections.Concurrent;
using LootDumpProcessor.Logger;
using LootDumpProcessor.Model.Input;
using LootDumpProcessor.Process.Collector;
using LootDumpProcessor.Process.Processor;
using LootDumpProcessor.Process.Processor.DumpProcessor;
using LootDumpProcessor.Process.Processor.FileProcessor;
using LootDumpProcessor.Process.Reader;
using LootDumpProcessor.Process.Reader.Filters;
using LootDumpProcessor.Process.Reader.Intake;
using LootDumpProcessor.Process.Writer;
using LootDumpProcessor.Serializers.Json;
using LootDumpProcessor.Storage;
using LootDumpProcessor.Utils;

namespace LootDumpProcessor.Process;

public class QueuePipeline : IPipeline
{
    private static readonly BlockingCollection<string> _filesToRename = new();
    private static readonly BlockingCollection<string> _filesToProcess = new();
    private static readonly List<Task> Runners = new();
    private static readonly List<Task> Renamers = new();

    private readonly List<string> _mapNames = LootDumpProcessorContext.GetConfig().MapsToProcess;

    public void DoProcess()
    {
        // Single collector instance to collect results
        var collector = CollectorFactory.GetInstance();
        collector.Setup();

        // We add 2 more threads to the total count to account for subprocesses and others
        int threads = LootDumpProcessorContext.GetConfig().Threads;
        ThreadPool.SetMaxThreads(threads + 2, threads + 2);

        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
        {
            LoggerFactory.GetInstance().Log("Gathering files to begin processing", LogLevel.Info);
        }

        FixFilesFromDumps(threads);
        foreach (var mapName in _mapNames)
        {
            ProcessFilesFromDumpsPerMap(threads, collector, mapName);
        }
    }

    private List<string> GatherFiles()
    {
        // Read locations
        var inputPath = new List<string> { LootDumpProcessorContext.DumpInputDirectory };

        // We gather up all files into a queue
        var queuedFilesToProcess = GetFileQueue(inputPath);
        // Then we preprocess the files in the queue and get them ready for processing
        return PreProcessQueuedFiles(queuedFilesToProcess);
    }

    private List<string> PreProcessQueuedFiles(Queue<string> queuedFilesToProcess)
    {
        var gatheredFiles = new List<string>();

        if (queuedFilesToProcess.Count == 0)
        {
            throw new Exception("No files matched accepted extension types in configs");
        }

        var fileFilters = GetFileFilters() ?? new Dictionary<string, IFileFilter>();

        while (queuedFilesToProcess.TryDequeue(out var file))
        {
            var extension = Path.GetExtension(file)[1..].ToLower();
            if (fileFilters.TryGetValue(extension, out var filter))
            {
                if (filter.Accept(file))
                {
                    gatheredFiles.Add(file);
                }
            }
            else
            {
                gatheredFiles.Add(file);
            }
        }

        return gatheredFiles;
    }

    private Queue<string> GetFileQueue(List<string> inputPath)
    {
        var queuedPathsToProcess = new Queue<string>();
        var queuedFilesToProcess = new Queue<string>();

        // Accepted file extensions on raw files
        var acceptedFileExtension = LootDumpProcessorContext
            .GetConfig()
            .ReaderConfig.AcceptedFileExtensions.Select(ex => ex.ToLower())
            .ToHashSet();
        inputPath.ForEach(p => queuedPathsToProcess.Enqueue(p));

        while (queuedPathsToProcess.TryDequeue(out var path))
        {
            // Check the input file to be sure its going to have data on it.
            if (!Directory.Exists(path))
            {
                throw new Exception($"Input directory \"{path}\" could not be found");
            }

            //Todo: Create dir and if check if dir is empty, if dir is empty close out.

            // If we should process subfolder, queue them up as well
            if (LootDumpProcessorContext.GetConfig().ReaderConfig.ProcessSubFolders)
            {
                foreach (var directory in Directory.GetDirectories(path))
                {
                    queuedPathsToProcess.Enqueue(directory);
                }
            }

            var files = Directory.GetFiles(path);

            foreach (var file in files)
            {
                if (acceptedFileExtension.Contains(Path.GetExtension(file)[1..].ToLower()))
                {
                    queuedFilesToProcess.Enqueue(file);
                }
            }
        }

        return queuedFilesToProcess;
    }

    private Dictionary<string, IFileFilter>? GetFileFilters()
    {
        return LootDumpProcessorContext
            .GetConfig()
            .ReaderConfig.FileFilters?.ToDictionary(t => FileFilterFactory.GetInstance(t).GetExtension(), FileFilterFactory.GetInstance);
    }

    /// <summary>
    /// Gets all files and adds them to the processor.
    /// use per map version now
    /// </summary>
    /// <param name="threads"></param>
    /// <param name="collector"></param>
    private void ProcessFilesFromDumps(int threads, ICollector collector)
    {
        // Gather all files, sort them by date descending and then add them into the processing queue
        GatherFiles()
            .OrderByDescending(f =>
            {
                FileDateParser.TryParseFileDate(f, out var date);
                return date;
            })
            .ToList()
            .ForEach(f => _filesToProcess.Add(f));

        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
        {
            LoggerFactory.GetInstance().Log("Files sorted and ready to begin pre-processing", LogLevel.Info);
        }

        // We startup all the threads and collect them into a runners list
        for (int i = 0; i < threads; i++)
        {
            if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            {
                LoggerFactory.GetInstance().Log("Creating pre-processing threads", LogLevel.Info);
            }

            Runners.Add(
                Task.Factory.StartNew(
                    () =>
                    {
                        while (_filesToProcess.TryTake(out var file, TimeSpan.FromMilliseconds(5000)))
                        {
                            try
                            {
                                var reader = IntakeReaderFactory.GetInstance();
                                var processor = FileProcessorFactory.GetInstance();
                                if (reader.Read(file, out var basicInfo))
                                {
                                    collector.Hold(processor.Process(basicInfo));
                                }
                            }
                            catch (Exception e)
                            {
                                if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Error))
                                {
                                    LoggerFactory
                                        .GetInstance()
                                        .Log($"Error occurred while processing file {file}\n{e.Message}\n{e.StackTrace}", LogLevel.Error);
                                }
                            }
                        }
                    },
                    TaskCreationOptions.LongRunning
                )
            );
        }

        // Wait until all runners are done processing
        while (!Runners.All(r => r.IsCompleted))
        {
            if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            {
                LoggerFactory
                    .GetInstance()
                    .Log(
                        $"One or more file processors are still processing files. Waiting {LootDumpProcessorContext.GetConfig().ThreadPoolingTimeoutMs}ms before checking again",
                        LogLevel.Info
                    );
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(LootDumpProcessorContext.GetConfig().ThreadPoolingTimeoutMs));
        }

        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
        {
            LoggerFactory.GetInstance().Log("Pre-processing finished", LogLevel.Info);
        }

        // Single writer instance to collect results
        var writer = WriterFactory.GetInstance();
        // Single collector instance to collect results
        var dumpProcessor = DumpProcessorFactory.GetInstance();
        writer.WriteAll(dumpProcessor.ProcessDumps(collector.Retrieve()));
    }

    private void ProcessFilesFromDumpsPerMap(int threads, ICollector collector, string mapName)
    {
        // Gather all files, sort them by date descending and then add them into the processing queue
        GatherFiles()
            .FindAll(f => f.ToLower().Contains($"{mapName}--"))
            .OrderByDescending(f =>
            {
                FileDateParser.TryParseFileDate(f, out var date);
                return date;
            })
            .ToList()
            .ForEach(f => _filesToProcess.Add(f));

        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
        {
            LoggerFactory.GetInstance().Log("Files sorted and ready to begin pre-processing", LogLevel.Info);
        }

        // We startup all the threads and collect them into a runners list
        for (int i = 0; i < threads; i++)
        {
            if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            {
                LoggerFactory.GetInstance().Log("Creating pre-processing threads", LogLevel.Info);
            }

            Runners.Add(
                Task.Factory.StartNew(
                    () =>
                    {
                        while (_filesToProcess.TryTake(out var file, TimeSpan.FromMilliseconds(5000)))
                        {
                            try
                            {
                                var reader = IntakeReaderFactory.GetInstance();
                                var processor = FileProcessorFactory.GetInstance();
                                if (reader.Read(file, out var basicInfo))
                                {
                                    collector.Hold(processor.Process(basicInfo));
                                }
                            }
                            catch (Exception e)
                            {
                                if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Error))
                                {
                                    LoggerFactory
                                        .GetInstance()
                                        .Log($"Error occurred while processing file {file}\n{e.Message}\n{e.StackTrace}", LogLevel.Error);
                                }
                            }
                        }
                    },
                    TaskCreationOptions.LongRunning
                )
            );
        }

        // Wait until all runners are done processing
        while (!Runners.All(r => r.IsCompleted))
        {
            if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            {
                LoggerFactory
                    .GetInstance()
                    .Log(
                        $"One or more file processors are still processing files. Waiting {LootDumpProcessorContext.GetConfig().ThreadPoolingTimeoutMs}ms before checking again",
                        LogLevel.Info
                    );
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(LootDumpProcessorContext.GetConfig().ThreadPoolingTimeoutMs));
        }

        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
        {
            LoggerFactory.GetInstance().Log("Pre-processing finished", LogLevel.Info);
        }

        // Single writer instance to collect results
        var writer = WriterFactory.GetInstance();
        // Single collector instance to collect results
        var dumpProcessor = DumpProcessorFactory.GetInstance();
        writer.WriteAll(dumpProcessor.ProcessDumps(collector.Retrieve()));

        // clear collector and datastorage as we process per map now
        collector.Clear();
        DataStorageFactory.GetInstance().Clear();
    }

    /// <summary>
    /// Adds map name to file if they dont have it already.
    /// </summary>
    /// <param name="threads">Number of threads to use</param>
    private void FixFilesFromDumps(int threads)
    {
        var inputPath = new List<string> { LootDumpProcessorContext.DumpInputDirectory };

        GetFileQueue(inputPath)
            .ToList()
            .ForEach(f =>
            {
                // if the file name already has a map name in it, dont add it to be "fixed"
                // we only use -- to split the map and the rest in the file names, so we can safely assume if it can split,
                // the map name it there.
                var test = f.Split("--");
                if (test.Length >= 2)
                {
                    return;
                }

                _filesToRename.Add(f);
            });

        var jsonUtil = new NetJsonSerializer();

        for (var i = 0; i < threads; i++)
        {
            if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            {
                LoggerFactory.GetInstance().Log("Creating file-processing threads", LogLevel.Info);
            }

            Renamers.Add(
                Task.Factory.StartNew(
                    () =>
                    {
                        while (_filesToRename.TryTake(out var file, TimeSpan.FromMilliseconds(5000)))
                        {
                            if (_mapNames.Any(x => file.Contains(x)))
                            {
                                continue;
                            }

                            try
                            {
                                var data = File.ReadAllText(file);
                                var fileData = jsonUtil.Deserialize<Data>(data);
                                if (fileData?.LocationLoot?.Id is null)
                                {
                                    if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Warning))
                                    {
                                        LoggerFactory.GetInstance().Log($"No location loot in {file}, skipped", LogLevel.Warning);
                                    }

                                    continue;
                                }

                                var newpath = file.Replace("resp", $"{fileData.LocationLoot.Id.ToLower()}--resp");
                                File.Move(file, newpath, overwrite: true);
                            }
                            catch (Exception e)
                            {
                                if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Error))
                                {
                                    LoggerFactory
                                        .GetInstance()
                                        .Log($"Error occurred while processing file {file}\n{e.Message}\n{e.StackTrace}", LogLevel.Error);
                                }
                            }
                        }
                    },
                    TaskCreationOptions.LongRunning
                )
            );
        }

        while (!Renamers.All(r => r.IsCompleted))
        {
            if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
            {
                LoggerFactory
                    .GetInstance()
                    .Log(
                        $"one or more files are being processed. Waiting {LootDumpProcessorContext.GetConfig().ThreadPoolingTimeoutMs} ms",
                        LogLevel.Info
                    );
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(LootDumpProcessorContext.GetConfig().ThreadPoolingTimeoutMs));
        }

        if (LoggerFactory.GetInstance().CanBeLogged(LogLevel.Info))
        {
            LoggerFactory.GetInstance().Log("File-processing finished", LogLevel.Info);
        }
    }
}
