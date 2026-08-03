using ArchLinterNet.Core.Caching;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AnalysisCacheLocationResolverTests
{
    [Test]
    public void Resolve_Disabled_ReturnsNull()
    {
        Assert.That(AnalysisCacheLocationResolver.Resolve(AnalysisCacheOptions.Disabled), Is.Null);
    }

    [Test]
    public void Resolve_Auto_ResolvesUnderProductAndSchemaVersion()
    {
        AnalysisCacheLocation? location = AnalysisCacheLocationResolver.Resolve(AnalysisCacheOptions.Auto);

        Assert.That(location, Is.Not.Null);
        Assert.That(location!.Mode, Is.EqualTo(AnalysisCacheMode.Auto));
        string normalized = location.RootPath.Replace('\\', '/');
        Assert.That(normalized, Does.Contain("ArchLinterNet/0.5.1/analysis-cache/v1"));
    }

    [Test]
    public void Resolve_ExplicitPath_ReturnsCanonicalFullPath()
    {
        string relative = Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-resolver-tests", Guid.NewGuid().ToString("N"));

        AnalysisCacheLocation? location = AnalysisCacheLocationResolver.Resolve(AnalysisCacheOptions.AtPath(relative));

        Assert.That(location, Is.Not.Null);
        Assert.That(location!.RootPath, Is.EqualTo(Path.GetFullPath(relative)));
        Assert.That(location.Mode, Is.EqualTo(AnalysisCacheMode.ExplicitPath));
    }

    [Test]
    public void Resolve_ExplicitPath_Empty_Throws()
    {
        Assert.Throws<AnalysisCacheLocationRejectedException>(() => AnalysisCacheLocationResolver.Resolve(AnalysisCacheOptions.AtPath("   ")));
    }

    [Test]
    public void Resolve_ExplicitPath_FilesystemRoot_Throws()
    {
        string root = OperatingSystem.IsWindows() ? Path.GetPathRoot(Environment.SystemDirectory)! : "/";

        Assert.Throws<AnalysisCacheLocationRejectedException>(() => AnalysisCacheLocationResolver.Resolve(AnalysisCacheOptions.AtPath(root)));
    }

    [Test]
    public void Resolve_ExplicitPath_ExistingFile_Throws()
    {
        string file = Path.Combine(Path.GetTempPath(), $"arch-linter-net-cache-resolver-{Guid.NewGuid():N}.txt");
        File.WriteAllText(file, "not a directory");
        try
        {
            Assert.Throws<AnalysisCacheLocationRejectedException>(() => AnalysisCacheLocationResolver.Resolve(AnalysisCacheOptions.AtPath(file)));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Test]
    public void Resolve_ExplicitPath_SymlinkDirectory_Throws()
    {
        string targetDir = Path.Combine(Path.GetTempPath(), $"arch-linter-net-cache-target-{Guid.NewGuid():N}");
        string linkDir = Path.Combine(Path.GetTempPath(), $"arch-linter-net-cache-link-{Guid.NewGuid():N}");
        Directory.CreateDirectory(targetDir);
        try
        {
            Directory.CreateSymbolicLink(linkDir, targetDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Ignore("Symbolic link creation is not permitted/supported in this environment.");
            return;
        }

        try
        {
            Assert.Throws<AnalysisCacheLocationRejectedException>(() => AnalysisCacheLocationResolver.Resolve(AnalysisCacheOptions.AtPath(linkDir)));
        }
        finally
        {
            try { Directory.Delete(linkDir); } catch (IOException) { }
            Directory.Delete(targetDir, recursive: true);
        }
    }
}
