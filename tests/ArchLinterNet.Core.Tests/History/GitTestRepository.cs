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

    public static GitTestRepository Create()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arch-linter-history-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        GitTestRepository repository = new(path);
        repository.Git("init", "-b", "main");
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

    public string WriteRawObject(string type, byte[] payload)
    {
        ProcessStartInfo startInfo = NewStartInfo(["hash-object", "-w", "-t", type, "--stdin"]);
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

    public void Dispose()
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Path, recursive: true);
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
