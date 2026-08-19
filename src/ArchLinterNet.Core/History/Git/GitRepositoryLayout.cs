using System.IO;

namespace ArchLinterNet.Core.History.Git;

// Repository discovery and object-format detection. The format is read from the repository's own
// configuration rather than inferred from an object ID length, so an empty repository still reports
// a canonical hash format.
internal sealed class GitRepositoryLayout
{
    private const string ObjectFormatKey = "objectformat";

    private GitRepositoryLayout(string gitDirectory, string commonDirectory, string objectFormatName, int digestLength)
    {
        GitDirectory = gitDirectory;
        CommonDirectory = commonDirectory;
        ObjectFormatName = objectFormatName;
        DigestLength = digestLength;
    }

    // Per-worktree: only HEAD (and other worktree-private state outside canonical ingestion's scope,
    // such as the index) lives here.
    public string GitDirectory { get; }

    // Shared across every worktree of the repository: objects, repository config, branch/tag refs,
    // and packed-refs live here. Equal to GitDirectory except inside a linked worktree (`git worktree
    // add`), where a linked worktree's private GitDirectory names its common directory through a
    // `commondir` file rather than containing these itself.
    public string CommonDirectory { get; }

    public string ObjectFormatName { get; }

    public int DigestLength { get; }

    public string ObjectsDirectory => Path.Combine(CommonDirectory, "objects");

    public static GitRepositoryLayout Discover(string startDirectory)
    {
        string gitDirectory = FindGitDirectory(startDirectory);
        string commonDirectory = ResolveCommonDirectory(gitDirectory);
        string objectFormatName = ReadObjectFormat(Path.Combine(commonDirectory, "config"));
        int digestLength = objectFormatName switch
        {
            "sha1" => 20,
            "sha256" => 32,
            _ => throw HistoryFailures.Fail(
                HistoryDiagnosticKind.UnsupportedObjectFormat,
                $"Unsupported repository object format '{objectFormatName}'."),
        };

        return new GitRepositoryLayout(gitDirectory, commonDirectory, objectFormatName, digestLength);
    }

    private static string ResolveCommonDirectory(string gitDirectory)
    {
        string commondirFile = Path.Combine(gitDirectory, "commondir");
        if (!File.Exists(commondirFile))
        {
            return gitDirectory;
        }

        string pointer = File.ReadAllText(commondirFile).Trim();
        string resolved = Path.IsPathRooted(pointer) ? pointer : Path.GetFullPath(Path.Combine(gitDirectory, pointer));
        return Directory.Exists(resolved)
            ? resolved
            : throw HistoryFailures.Fail(
                HistoryDiagnosticKind.RepositoryNotFound,
                $"The worktree common-directory pointer '{commondirFile}' does not name an existing directory.",
                path: commondirFile);
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
            if (IsCommentOrBlank(line))
            {
                continue;
            }

            if (TryParseSectionName(line, out string sectionName))
            {
                section = sectionName;
                continue;
            }

            if (section == "extensions" && TryParseObjectFormatValue(line, out string value))
            {
                return value;
            }
        }

        return "sha1";
    }

    private static bool IsCommentOrBlank(string line) => line.Length == 0 || line[0] is '#' or ';';

    private static bool TryParseSectionName(string line, out string sectionName)
    {
        if (line[0] != '[')
        {
            sectionName = string.Empty;
            return false;
        }

        int end = line.IndexOf(']');
        sectionName = end > 1 ? line[1..end].Trim().ToLowerInvariant() : string.Empty;
        return true;
    }

    private static bool TryParseObjectFormatValue(string line, out string value)
    {
        int separator = line.IndexOf('=');
        if (separator >= 0 && line[..separator].Trim().Equals(ObjectFormatKey, StringComparison.OrdinalIgnoreCase))
        {
            value = line[(separator + 1)..].Trim().ToLowerInvariant();
            return true;
        }

        value = string.Empty;
        return false;
    }
}
