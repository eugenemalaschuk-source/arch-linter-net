using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicyImportCoverageTests
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
