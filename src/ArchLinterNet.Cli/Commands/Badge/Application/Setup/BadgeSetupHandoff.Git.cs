using System.ComponentModel;
using System.Diagnostics;

namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal static class BadgeSetupHandoffGit
{
    internal static void ValidateLocalReviewBranch(string outputDirectory, string expectedCommit, string expectedTree)
    {
        string root = Path.GetFullPath(outputDirectory);
        if (!Directory.Exists(root))
        {
            throw new IOException("The normal review branch directory does not exist.");
        }

        string prefix = RunGit(root, true, "rev-parse", "--show-prefix");
        if (!string.IsNullOrEmpty(prefix))
        {
            throw new IOException("Apply-handoff must target the root of the normal review branch.");
        }

        string head = ReadSha(RunGit(root, "rev-parse", "HEAD"), "local review commit");
        string tree = ReadSha(RunGit(root, "rev-parse", "HEAD^{tree}"), "local review tree");
        if (!string.Equals(head, expectedCommit, StringComparison.Ordinal)
            || !string.Equals(tree, expectedTree, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The normal review branch is not at the bootstrap handoff's exact base commit and tree.");
        }
    }

    private static string RunGit(string workingDirectory, params string[] arguments) => RunGit(workingDirectory, false, arguments);

    private static string RunGit(string workingDirectory, bool allowEmptyOutput, params string[] arguments)
    {
        try
        {
            ProcessStartInfo startInfo = new(FindGitExecutable())
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new IOException("Could not start Git for the normal review branch.");
            string output = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new IOException("The apply-handoff output directory is not a readable Git review branch.");
            }

            string value = output.Trim();
            if (!allowEmptyOutput && string.IsNullOrWhiteSpace(value))
            {
                throw new IOException("Git returned an empty identity for the normal review branch.");
            }

            return value;
        }
        catch (Win32Exception exception)
        {
            throw new IOException("Git is required to verify the normal review branch.", exception);
        }
    }

    private static string ReadSha(string value, string name)
    {
        if (value.Length != 40 || !value.All(static character => char.IsAsciiHexDigit(character)))
        {
            throw new IOException($"Git returned an invalid {name} SHA.");
        }

        return value.ToLowerInvariant();
    }

    private static string FindGitExecutable()
    {
        string executableName = OperatingSystem.IsWindows() ? "git.exe" : "git";
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new IOException("Git is required to verify the normal review branch.");
        }

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.GetFullPath(Path.Combine(directory, executableName));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Git is required to verify the normal review branch.");
    }
}
