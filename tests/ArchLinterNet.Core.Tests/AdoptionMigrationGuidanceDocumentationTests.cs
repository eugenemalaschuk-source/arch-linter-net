using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AdoptionMigrationGuidanceDocumentationTests
{
    [Test]
    public void MigrationGuide_SeparatesGreenfieldAndUpgradePathsWithSafeReleaseBoundary()
    {
        string guide = ReadDocumentation("guides/migration-to-0-5-1.md");

        Assert.Multiple(() =>
        {
            Assert.That(guide, Does.Contain("# Adopt or Upgrade to 0.5.1"));
            Assert.That(guide, Does.Contain("## Greenfield adoption"));
            Assert.That(guide, Does.Contain("## Upgrade from 0.5.0"));
            Assert.That(guide, Does.Contain("Checkpoint A is internal integration evidence"));
            Assert.That(guide, Does.Contain("not a package release or a support promise"));
            Assert.That(guide, Does.Contain("dotnet arch-linter-net policy check"));
            Assert.That(guide, Does.Contain("--ensure-built --no-restore"));
            Assert.That(guide, Does.Contain("changed`, `stale`, or `ambiguous`"));
            Assert.That(guide, Does.Contain("CI uses read-only `baseline verify`"));
            Assert.That(guide, Does.Contain("must never regenerate, update, or commit"));
        });
    }

    [Test]
    public void MigrationGuide_DocumentsInstalledSchemasReportsAndExecutionControls()
    {
        string guide = ReadDocumentation("guides/migration-to-0-5-1.md");

        Assert.Multiple(() =>
        {
            Assert.That(guide, Does.Contain("schema list"));
            Assert.That(guide, Does.Contain("schema print analysis-profile"));
            Assert.That(guide, Does.Contain("--report <format>=<destination>"));
            Assert.That(guide, Does.Contain("`partial-output`"));
            Assert.That(guide, Does.Contain("`analysis-cache/v1`"));
            Assert.That(guide, Does.Contain("`analysis-profile/v1`"));
            Assert.That(guide, Does.Contain("--max-parallelism 1"));
            Assert.That(guide, Does.Contain("typed `cancelled` completion"));
            Assert.That(guide, Does.Contain("Human output is complete without color or a TTY"));
        });
    }

    [Test]
    public void ReferenceEntrypoints_PreserveArgumentsStreamsAndExitStatus()
    {
        string entrypoints = ReadDocumentation("guides/reference-entrypoints.md");

        Assert.Multiple(() =>
        {
            Assert.That(entrypoints, Does.Contain("## Direct pinned .NET tool"));
            Assert.That(entrypoints, Does.Contain("## POSIX shell"));
            Assert.That(entrypoints, Does.Contain("## PowerShell"));
            Assert.That(entrypoints, Does.Contain("## Make"));
            Assert.That(entrypoints, Does.Contain("## Taskfile"));
            Assert.That(entrypoints, Does.Contain("## Tilt"));
            Assert.That(entrypoints, Does.Contain("## Generic CI contract"));
            Assert.That(entrypoints, Does.Contain("## GitHub Actions example"));
            Assert.That(entrypoints, Does.Contain("\"${tool[@]}\" \"${args[@]}\""));
            Assert.That(entrypoints, Does.Contain("exit \"$?\""));
            Assert.That(entrypoints, Does.Contain("& dotnet @arguments"));
            Assert.That(entrypoints, Does.Contain("$LASTEXITCODE"));
            Assert.That(entrypoints, Does.Contain("Do not use `Invoke-Expression`"));
            Assert.That(entrypoints, Does.Contain("do not interpolate untrusted values"));
        });
    }

    [Test]
    public void NavigationAndPublicReferences_LinkToCanonicalGuidance()
    {
        string nav = File.ReadAllText(Path.Combine(RepositoryRoot(), "mkdocs.yml"));
        string readme = File.ReadAllText(Path.Combine(RepositoryRoot(), "README.md"));
        string releaseNotes = ReadDocumentation("reference/release-notes-0-5-1.md");

        Assert.Multiple(() =>
        {
            Assert.That(nav, Does.Contain("guides/migration-to-0-5-1.md"));
            Assert.That(nav, Does.Contain("guides/reference-entrypoints.md"));
            Assert.That(nav, Does.Contain("reference/release-notes-0-5-1.md"));
            Assert.That(readme, Does.Contain("0.5.1 is the single public adoption-stabilization release target"));
            Assert.That(readme, Does.Contain("Checkpoint A is internal evidence only"));
            Assert.That(releaseNotes, Does.Contain("0.5.1 is the single public adoption-stabilization release"));
            Assert.That(releaseNotes, Does.Contain("schema list"));
        });
    }

    private static string ReadDocumentation(string relativePath)
    {
        return File.ReadAllText(Path.Combine(
            RepositoryRoot(), "docs", relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string RepositoryRoot()
    {
        return new ArchitectureRepositoryRootResolver().Resolve();
    }
}
