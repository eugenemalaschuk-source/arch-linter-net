using SharpFuzz;

namespace ArchLinterNet.GitFuzz;

internal static class Program
{
    private const int UsageError = 2;
    private const string ReplayWorkerArgument = "--replay-worker";

    private static int Main(string[] args)
        => RunMain(args, Environment.ProcessPath);

    internal static int RunMain(string[] args, string? processPath)
    {
        if (args.Length == 2 && args[0] == "--materialize-corpus")
        {
            return MaterializeCorpus(args[1]);
        }

        if (args.Length == 2 && args[0] == "--replay")
        {
            return RunReplay(args[1], processPath);
        }

        if (args.Length == 2
            && args[0] == ReplayWorkerArgument
            && Environment.GetEnvironmentVariable(BoundedReplayRunner.WorkerEnvironmentVariable) == "1")
        {
            return RunReplayWorker(args[1]);
        }

        if (args.Length <= 1)
        {
            Fuzzer.OutOfProcess.Run(stream => _ = FuzzInputProcessor.Execute(stream));
            return 0;
        }

        Console.Error.WriteLine("Usage: ArchLinterNet.GitFuzz [--materialize-corpus <output-dir>|--replay <input-file>]");
        return UsageError;
    }

    private static int MaterializeCorpus(string outputDirectory)
    {
        foreach (string path in FuzzCorpus.Materialize(outputDirectory))
        {
            Console.WriteLine(path);
        }

        return 0;
    }

    private static int RunReplay(string inputPath, string? processPath)
    {
        try
        {
            return BoundedReplayRunner.Run(inputPath, processPath);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or PlatformNotSupportedException)
        {
            Console.Error.WriteLine(exception.Message);
            return BoundedReplayRunner.ReplayLimitSetupExitCode;
        }
    }

    private static int RunReplayWorker(string inputPath)
    {
        Console.WriteLine(BoundedReplayRunner.WorkerReadyMarker);
        Console.Out.Flush();
        if (!string.Equals(
                Console.ReadLine(),
                BoundedReplayRunner.WorkerWarmupMarker,
                StringComparison.Ordinal))
        {
            return UsageError;
        }

        WarmUpParserSeams();
        Console.WriteLine(BoundedReplayRunner.WorkerCaseReadyMarker);
        Console.Out.Flush();
        if (!string.Equals(
                Console.ReadLine(),
                BoundedReplayRunner.WorkerStartMarker,
                StringComparison.Ordinal))
        {
            return UsageError;
        }

        using FileStream stream = File.OpenRead(inputPath);
        Console.WriteLine(FuzzInputProcessor.Execute(stream));
        return 0;
    }

    private static void WarmUpParserSeams()
    {
        string warmupDirectory = Path.Combine(
            Path.GetTempPath(),
            $"arch-linter-git-fuzz-warmup-{Guid.NewGuid():N}");
        try
        {
            foreach (string path in FuzzCorpus.Materialize(warmupDirectory))
            {
                _ = FuzzInputProcessor.Execute(File.ReadAllBytes(path));
            }
        }
        finally
        {
            if (Directory.Exists(warmupDirectory))
            {
                Directory.Delete(warmupDirectory, recursive: true);
            }
        }
    }
}
