using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ArchLinterNet.Core.Tests.History;

// Fixtures are produced by Git itself so the canonical reader is verified against real object
// encoding — loose objects, packfiles, deltas, and ref storage included — rather than against a
// hand-written approximation of it.
internal sealed class GitTestRepository : IDisposable
{
    private long _nextEpochSecond = 1_700_000_000;

    private GitTestRepository(string path) => Path = path;

    public string Path { get; }

    public static GitTestRepository Create() => Create(objectFormat: null);

    public static GitTestRepository CreateWithObjectFormat(string objectFormat) => Create(objectFormat);

    private static GitTestRepository Create(string? objectFormat)
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arch-linter-history-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        GitTestRepository repository = new(path);
        List<string> initArguments = ["init", "-b", "main"];
        if (objectFormat is not null)
        {
            initArguments.Add($"--object-format={objectFormat}");
        }

        repository.Git([.. initArguments]);
        repository.Git("config", "user.name", "Fixture Author");
        repository.Git("config", "user.email", "Fixture@Example.COM");
        repository.Git("config", "commit.gpgsign", "false");
        return repository;
    }

    public void Write(string relativePath, string content)
    {
        string absolute = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, Encoding.UTF8.GetBytes(content));
    }

    public void WriteBytes(string relativePath, byte[] content)
    {
        string absolute = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, content);
    }

    public void Remove(string relativePath) => Git("rm", "-q", "--", relativePath);

    public void Move(string fromRelativePath, string toRelativePath)
    {
        string absolute = System.IO.Path.Combine(Path, toRelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(absolute)!);
        Git("mv", "--", fromRelativePath, toRelativePath);
    }

    // Fixed, monotonically increasing timestamps keep canonical commit order deterministic without
    // depending on how fast the test runs.
    public string Commit(string message)
    {
        string stamp = _nextEpochSecond.ToString(CultureInfo.InvariantCulture) + " +0000";
        _nextEpochSecond += 3600;
        Git(["add", "-A"]);
        GitWithEnvironment(["commit", "--allow-empty", "-q", "-m", message], stamp);
        return Head();
    }

    public string Head() => Git("rev-parse", "HEAD").Trim();

    // Git writes loose objects read-only. Deleting one to simulate a missing/corrupt object needs
    // the read-only attribute cleared first, or Windows refuses the delete outright.
    public void DeleteLooseObject(string objectId)
    {
        string path = System.IO.Path.Combine(Path, ".git", "objects", objectId[..2], objectId[2..]);
        File.SetAttributes(path, FileAttributes.Normal);
        File.Delete(path);
    }

    public string WriteRawObject(string type, byte[] payload)
    {
        // --literally: several fixtures write deliberately malformed commit objects (missing
        // author, non-UTF8 bytes) to exercise fail-closed parsing. Git's own fsck-style validation
        // on `hash-object -w` would otherwise refuse to write them before the reader ever sees them.
        ProcessStartInfo startInfo = NewStartInfo(["hash-object", "-w", "-t", type, "--stdin", "--literally"]);
        startInfo.RedirectStandardInput = true;
        using Process process = Process.Start(startInfo)!;
        using (Stream input = process.StandardInput.BaseStream)
        {
            input.Write(payload, 0, payload.Length);
        }

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0
            ? output.Trim()
            : throw new InvalidOperationException($"git hash-object failed: {error}");
    }

    public string Git(params string[] arguments) => GitWithEnvironment(arguments, null);

    public void Dispose() => DeleteDirectoryRecursively(Path);

    // Git writes loose objects, packfiles, and pack indexes read-only, which Windows refuses to
    // delete without the attribute cleared first. Any fixture directory a real `git` command wrote
    // into — not just the primary repository path — needs this instead of a plain recursive delete.
    public static void DeleteDirectoryRecursively(string path)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // A leaked temporary fixture must never fail an otherwise passing test.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string GitWithEnvironment(string[] arguments, string? commitStamp)
    {
        ProcessStartInfo startInfo = NewStartInfo(arguments);
        if (commitStamp is not null)
        {
            startInfo.Environment["GIT_AUTHOR_DATE"] = commitStamp;
            startInfo.Environment["GIT_COMMITTER_DATE"] = commitStamp;
        }

        using Process process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0
            ? output
            : throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}{output}");
    }

    private ProcessStartInfo NewStartInfo(string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
