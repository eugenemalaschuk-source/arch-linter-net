using SharpFuzz;

namespace ArchLinterNet.GitFuzz;

internal static class Program
{
    private const int UsageError = 2;
    private const string ReplayWorkerArgument = "--replay-worker";

    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--materialize-corpus")
        {
            foreach (string path in FuzzCorpus.Materialize(args[1]))
            {
                Console.WriteLine(path);
            }

            return 0;
        }

        if (args.Length == 2 && args[0] == "--replay")
        {
            try
            {
                return BoundedReplayRunner.Run(args[1]);
            }
            catch (InvalidOperationException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return BoundedReplayRunner.ReplayLimitSetupExitCode;
            }
        }

        if (args.Length == 2
            && args[0] == ReplayWorkerArgument
            && Environment.GetEnvironmentVariable(BoundedReplayRunner.WorkerEnvironmentVariable) == "1")
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

            using FileStream stream = File.OpenRead(args[1]);
            Console.WriteLine(FuzzInputProcessor.Execute(stream));
            return 0;
        }

        if (args.Length <= 1)
        {
            Fuzzer.OutOfProcess.Run(stream => _ = FuzzInputProcessor.Execute(stream));
            return 0;
        }

        Console.Error.WriteLine("Usage: ArchLinterNet.GitFuzz [--materialize-corpus <output-dir>|--replay <input-file>]");
        return UsageError;
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
