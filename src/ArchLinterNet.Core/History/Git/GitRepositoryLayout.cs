using System.IO;

namespace ArchLinterNet.Core.History.Git;

// Repository discovery and object-format detection. The format is read from the repository's own
// configuration rather than inferred from an object ID length, so an empty repository still reports
// a canonical hash format.
internal sealed class GitRepositoryLayout
{
    private const string ObjectFormatKey = "objectformat";

    private GitRepositoryLayout(string gitDirectory, string objectFormatName, int digestLength)
    {
        GitDirectory = gitDirectory;
        ObjectFormatName = objectFormatName;
        DigestLength = digestLength;
    }

    public string GitDirectory { get; }

    public string ObjectFormatName { get; }

    public int DigestLength { get; }

    public string ObjectsDirectory => Path.Combine(GitDirectory, "objects");

    public static GitRepositoryLayout Discover(string startDirectory)
    {
        string gitDirectory = FindGitDirectory(startDirectory);
        string objectFormatName = ReadObjectFormat(Path.Combine(gitDirectory, "config"));
        int digestLength = objectFormatName switch
        {
            "sha1" => 20,
            "sha256" => 32,
            _ => throw HistoryFailures.Fail(
                HistoryDiagnosticKind.UnsupportedObjectFormat,
                $"Unsupported repository object format '{objectFormatName}'."),
        };

        return new GitRepositoryLayout(gitDirectory, objectFormatName, digestLength);
    }

    private static string FindGitDirectory(string startDirectory)
    {
        string? current;
        try
        {
            current = Path.GetFullPath(startDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw HistoryFailures.Fail(
                HistoryDiagnosticKind.RepositoryNotFound,
                $"The repository path '{startDirectory}' is not a usable filesystem path.",
                path: startDirectory);
        }

        while (!string.IsNullOrEmpty(current))
        {
            string candidate = Path.Combine(current, ".git");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            if (File.Exists(candidate))
            {
                return ResolveGitDirectoryFile(candidate, current);
            }

            // A bare repository is its own Git directory.
            if (File.Exists(Path.Combine(current, "HEAD")) && Directory.Exists(Path.Combine(current, "objects")))
            {
                return current;
            }

            current = Path.GetDirectoryName(current);
        }

        throw HistoryFailures.Fail(
            HistoryDiagnosticKind.RepositoryNotFound,
            $"No Git repository was found at or above '{startDirectory}'.",
            path: startDirectory);
    }

    private static string ResolveGitDirectoryFile(string gitFile, string workingDirectory)
    {
        const string Prefix = "gitdir:";
        foreach (string line in File.ReadAllLines(gitFile))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string target = trimmed[Prefix.Length..].Trim();
            string resolved = Path.IsPathRooted(target) ? target : Path.GetFullPath(Path.Combine(workingDirectory, target));
            if (Directory.Exists(resolved))
            {
                return resolved;
            }

            break;
        }

        throw HistoryFailures.Fail(
            HistoryDiagnosticKind.RepositoryNotFound,
            $"The Git directory pointer '{gitFile}' does not name an existing Git directory.",
            path: gitFile);
    }

    // Minimal INI reading limited to what canonical ingestion needs: the [extensions] objectformat
    // value. Anything else in the config file is irrelevant to canonical evidence.
    private static string ReadObjectFormat(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return "sha1";
        }

        string section = string.Empty;
        foreach (string rawLine in File.ReadAllLines(configPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] is '#' or ';')
            {
                continue;
            }

            if (line[0] == '[')
            {
                int end = line.IndexOf(']');
                section = end > 1 ? line[1..end].Trim().ToLowerInvariant() : string.Empty;
                continue;
            }

            if (section != "extensions")
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            if (line[..separator].Trim().Equals(ObjectFormatKey, StringComparison.OrdinalIgnoreCase))
            {
                return line[(separator + 1)..].Trim().ToLowerInvariant();
            }
        }

        return "sha1";
    }
}
