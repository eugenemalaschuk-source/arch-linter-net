using System.Diagnostics;
using System.Globalization;

namespace ArchLinterNet.ProcessTreeProbe;

/// <summary>
/// Deterministic Windows-only test helper: a "root" process that starts a "child" process
/// inheriting the root's own redirected stdout/stderr handles, publishes the child's process id,
/// and exits immediately. This reproduces the inherited-redirected-handle regression without
/// depending on a shell's own exit-signaling timing.
/// </summary>
internal static class Program
{
    private const int UsageError = 2;
    private const int DefaultSleepSeconds = 30;

    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine(
                "Usage: ArchLinterNet.ProcessTreeProbe <root|child> <pidFilePath> [sleepSeconds]");
            return UsageError;
        }

        string mode = args[0];
        string pidFilePath = args[1];
        int sleepSeconds = args.Length > 2
            ? int.Parse(args[2], CultureInfo.InvariantCulture)
            : DefaultSleepSeconds;

        return mode switch
        {
            "root" => RunRoot(pidFilePath, sleepSeconds),
            "child" => RunChild(sleepSeconds),
            _ => UnknownMode(mode),
        };
    }

    private static int RunRoot(string pidFilePath, int sleepSeconds)
    {
        string exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not resolve the probe executable path.");

        var startInfo = new ProcessStartInfo(exePath) { UseShellExecute = false };
        startInfo.ArgumentList.Add("child");
        startInfo.ArgumentList.Add(pidFilePath);
        startInfo.ArgumentList.Add(sleepSeconds.ToString(CultureInfo.InvariantCulture));

        using Process child = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the descendant probe process.");
        File.WriteAllText(pidFilePath, child.Id.ToString(CultureInfo.InvariantCulture));
        return 0;
    }

    private static int RunChild(int sleepSeconds)
    {
        Console.Out.WriteLine("descendant-output");
        Console.Error.WriteLine("descendant-error");
        Console.Out.Flush();
        Console.Error.Flush();
        Thread.Sleep(TimeSpan.FromSeconds(sleepSeconds));
        return 0;
    }

    private static int UnknownMode(string mode)
    {
        Console.Error.WriteLine($"Unknown mode '{mode}'.");
        return UsageError;
    }
}
