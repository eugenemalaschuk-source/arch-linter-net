using SharpFuzz;

namespace ArchLinterNet.GitFuzz;

internal static class Program
{
    private const int UsageError = 2;

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
}
