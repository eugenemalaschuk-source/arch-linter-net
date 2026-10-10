using System.Runtime.InteropServices;
using ArchLinterNet.Cli.Infrastructure;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class FileIdentityComparerTests
{
    [Test]
    public void TryAreDifferentFiles_OnAppleSiliconUsesDarwinStatLayout()
    {
        if (!OperatingSystem.IsMacOS() || RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
        {
            Assert.Ignore("This regression test targets the Apple Silicon Darwin fstat ABI.");
        }

        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-file-identity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string firstPath = Path.Combine(directory, "first.txt");
        string secondPath = Path.Combine(directory, "second.txt");

        try
        {
            File.WriteAllText(firstPath, "first");
            File.WriteAllText(secondPath, "second");

            Assert.Multiple(() =>
            {
                Assert.That(FileIdentityComparer.TryAreDifferentFiles(firstPath, secondPath), Is.True);
                Assert.That(FileIdentityComparer.TryAreDifferentFiles(firstPath, firstPath), Is.False);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
