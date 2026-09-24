using System.Text;
using ArchLinterNet.Cli.Abstractions;

namespace ArchLinterNet.Cli.Tests;

internal sealed class FailOnWriteFileSystem(IFileSystem inner, string failingTargetPath) : IFileSystem
{
    public bool FileExists(string path) => inner.FileExists(path);

    public string ReadAllText(string path) => inner.ReadAllText(path);

    public void WriteAllText(string path, string contents) => inner.WriteAllText(path, contents);

    public string WriteAllTextToTemp(string targetPath, string contents)
    {
        if (string.Equals(targetPath, failingTargetPath, StringComparison.Ordinal))
        {
            throw new IOException($"Cannot write to {targetPath}");
        }

        return inner.WriteAllTextToTemp(targetPath, contents);
    }

    public void RenameTempToTarget(string tempPath, string targetPath) => inner.RenameTempToTarget(tempPath, targetPath);

    public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => inner.TryRenameTempToNewTarget(tempPath, targetPath);

    public void DeleteFile(string path) => inner.DeleteFile(path);

    public bool TryCreateNewFile(string path) => inner.TryCreateNewFile(path);

    public bool DirectoryExists(string path) => inner.DirectoryExists(path);

    public void DeleteDirectoryIfEmpty(string path) => inner.DeleteDirectoryIfEmpty(path);

    public bool CanWriteToDirectory(string path) => inner.CanWriteToDirectory(path);
}

internal sealed class SamePhysicalFileFileSystem(
    IFileSystem inner,
    string aliasPath,
    string policyPath) : IFileSystem
{
    public bool FileExists(string path) => inner.FileExists(path);

    public bool AreSameExistingFile(string firstPath, string secondPath) =>
        (string.Equals(firstPath, aliasPath, StringComparison.Ordinal)
            && string.Equals(secondPath, policyPath, StringComparison.Ordinal))
        || (string.Equals(firstPath, policyPath, StringComparison.Ordinal)
            && string.Equals(secondPath, aliasPath, StringComparison.Ordinal))
        || inner.AreSameExistingFile(firstPath, secondPath);

    public string ReadAllText(string path) => inner.ReadAllText(path);

    public void WriteAllText(string path, string contents) => inner.WriteAllText(path, contents);

    public string WriteAllTextToTemp(string targetPath, string contents) => inner.WriteAllTextToTemp(targetPath, contents);

    public void RenameTempToTarget(string tempPath, string targetPath) => inner.RenameTempToTarget(tempPath, targetPath);

    public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => inner.TryRenameTempToNewTarget(tempPath, targetPath);

    public void DeleteFile(string path) => inner.DeleteFile(path);

    public bool TryCreateNewFile(string path) => inner.TryCreateNewFile(path);

    public bool DirectoryExists(string path) => inner.DirectoryExists(path);

    public void DeleteDirectoryIfEmpty(string path) => inner.DeleteDirectoryIfEmpty(path);

    public bool CanWriteToDirectory(string path) => inner.CanWriteToDirectory(path);
}

internal sealed class FailOnRenameFileSystem(IFileSystem inner, string failingTargetPath) : IFileSystem
{
    public bool FileExists(string path) => inner.FileExists(path);

    public string ReadAllText(string path) => inner.ReadAllText(path);

    public void WriteAllText(string path, string contents) => inner.WriteAllText(path, contents);

    public string WriteAllTextToTemp(string targetPath, string contents) => inner.WriteAllTextToTemp(targetPath, contents);

    public void RenameTempToTarget(string tempPath, string targetPath)
    {
        if (string.Equals(targetPath, failingTargetPath, StringComparison.Ordinal))
        {
            throw new IOException($"Cannot rename to {targetPath}");
        }

        inner.RenameTempToTarget(tempPath, targetPath);
    }

    public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => inner.TryRenameTempToNewTarget(tempPath, targetPath);

    public void DeleteFile(string path) => inner.DeleteFile(path);

    public bool TryCreateNewFile(string path) => inner.TryCreateNewFile(path);

    public bool DirectoryExists(string path) => inner.DirectoryExists(path);

    public void DeleteDirectoryIfEmpty(string path) => inner.DeleteDirectoryIfEmpty(path);

    public bool CanWriteToDirectory(string path) => inner.CanWriteToDirectory(path);
}

internal sealed class InvalidJsonTempFileSystem(IFileSystem inner) : IFileSystem
{
    public string? CorruptedTempPath { get; private set; }

    public bool FileExists(string path) => inner.FileExists(path);

    public bool AreSameExistingFile(string firstPath, string secondPath) => inner.AreSameExistingFile(firstPath, secondPath);

    public string ReadAllText(string path) => inner.ReadAllText(path);

    public void WriteAllText(string path, string contents) => inner.WriteAllText(path, contents);

    public string WriteAllTextToTemp(string targetPath, string contents)
    {
        string tempPath = inner.WriteAllTextToTemp(targetPath, contents);
        if (targetPath.EndsWith(".json", StringComparison.Ordinal))
        {
            CorruptedTempPath = tempPath;
            inner.WriteAllText(tempPath, "{ not valid JSON");
        }

        return tempPath;
    }

    public void RenameTempToTarget(string tempPath, string targetPath) => inner.RenameTempToTarget(tempPath, targetPath);

    public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => inner.TryRenameTempToNewTarget(tempPath, targetPath);

    public void DeleteFile(string path) => inner.DeleteFile(path);

    public bool TryCreateNewFile(string path) => inner.TryCreateNewFile(path);

    public bool DirectoryExists(string path) => inner.DirectoryExists(path);

    public void DeleteDirectoryIfEmpty(string path) => inner.DeleteDirectoryIfEmpty(path);

    public bool CanWriteToDirectory(string path) => inner.CanWriteToDirectory(path);
}

// Simulates a broken stdout pipe: WriteCanonicalJson's default implementation and Out.Write both
// go through this writer, so it exercises the stream-write failure path regardless of the format.
internal sealed class BrokenOutConsole : ICliConsole
{
    private readonly StringBuilder _error = new();

    public TextWriter Out => new ThrowingWriter();

    public TextWriter Error => new StringWriter(_error);

    public string ErrorOutput => _error.ToString();

    private sealed class ThrowingWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(string? value) => throw new IOException("broken pipe");
    }
}

internal sealed class StreamThenBrokenErrorConsole : ICliConsole
{
    private readonly StringBuilder _output = new();
    private readonly StringBuilder _error = new();
    private int _errorWriteAttempts;

    public TextWriter Out => new StringWriter(_output);

    public TextWriter Error => new FailingOnceWriter(_error, () => _errorWriteAttempts++ == 0);

    public string Output => _output.ToString();

    public string ErrorOutput => _error.ToString();

    private sealed class FailingOnceWriter(StringBuilder destination, Func<bool> shouldFail) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(string? value)
        {
            if (shouldFail())
            {
                throw new IOException("broken stderr pipe");
            }

            destination.Append(value);
        }
    }
}

internal sealed class FakeConsole : ICliConsole
{
    private readonly StringBuilder _output = new();
    private readonly StringBuilder _error = new();

    public TextWriter Out => new StringWriter(_output);

    public TextWriter Error => new StringWriter(_error);

    public string Output => _output.ToString();

    public string ErrorOutput => _error.ToString();
}
