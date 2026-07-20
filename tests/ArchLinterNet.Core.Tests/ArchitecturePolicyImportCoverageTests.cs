using System.Runtime.InteropServices;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed partial class ArchitecturePolicyImportCoverageTests
{
    [Test]
    public void Load_MissingVirtualPolicy_ExposesRootMissingFileDiagnostic()
    {
        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => new ArchitecturePolicyDocumentLoader(new FakeArchitectureFileSystem())
                .Load("/virtual/architecture/missing.yml"))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.MissingFile));
            Assert.That(exception.Diagnostic!.Location!.Role, Is.EqualTo(ArchitecturePolicyDocumentRole.Root));
            Assert.That(exception.Diagnostic.Location.SourcePath, Is.EqualTo("architecture/missing.yml"));
            Assert.That(exception.Message, Is.EqualTo("Root policy file not found: architecture/missing.yml"));
            Assert.That(exception.Message, Does.Not.Contain("/virtual/"));
        });
    }

    [Test]
    public void ImportGraphResolver_RootResolutionFailure_ExposesRootDiagnostic()
    {
        var resolver = new ThrowingPolicyPathResolver();
        var graphResolver = new ArchitecturePolicyImportGraphResolver(
            new FakeArchitectureFileSystem(),
            resolver,
            new ArchitecturePolicySourceParser());

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
            () => graphResolver.Resolve("/virtual/architecture/root.yml", "imports: []\n"))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.PlatformFailure));
            Assert.That(exception.Diagnostic!.Location!.Role, Is.EqualTo(ArchitecturePolicyDocumentRole.Root));
            Assert.That(exception.Diagnostic.Location.SourcePath, Is.EqualTo("architecture/root.yml"));
            Assert.That(exception.Message, Is.EqualTo("Root policy 'architecture/root.yml' could not be inspected (native error)."));
        });
    }

    [Test]
    public void ReadVerifiedAllText_ReplacedRegularFileWithNamedPipe_ExposesSourceShape()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("The regression fixture uses POSIX mkfifo.");
        }

        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-secure-reader-{Guid.NewGuid():N}", "architecture");
        Directory.CreateDirectory(directory);
        string policyPath = Path.Combine(directory, "root.yml");
        File.WriteAllText(policyPath, "version: 1\n");
        ArchitecturePolicyRootPath root = new ArchitecturePolicyPathResolver().ResolveRoot(policyPath);
        File.Delete(policyPath);
        Assert.That(CreateNamedPipe(policyPath, 0x180), Is.EqualTo(0));

        try
        {
            ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(
                () => ArchitecturePolicyPathResolver.ReadVerifiedAllText(
                    root.PhysicalPath,
                    "architecture/root.yml",
                    root.FileIdentity))!;

            Assert.That(exception.Category, Is.EqualTo(ArchitecturePolicyImportErrorCategory.SourceShape));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(directory)!, recursive: true);
        }
    }

    [LibraryImport("libc", EntryPoint = "mkfifo", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int CreateNamedPipe(string pathName, uint mode);

    private sealed class ThrowingPolicyPathResolver : IArchitecturePolicyPathResolver
    {
        public ArchitecturePolicyRootPath ResolveRoot(string rootPath)
        {
            throw new ArchitecturePolicyImportException(
                ArchitecturePolicyImportErrorCategory.PlatformFailure,
                "Test-only root resolution failure.");
        }

        public ArchitecturePolicyResolvedPath ResolveImport(
            ArchitecturePolicyRootPath root,
            string declaringPath,
            string importPath)
        {
            throw new NotSupportedException();
        }
    }
}
