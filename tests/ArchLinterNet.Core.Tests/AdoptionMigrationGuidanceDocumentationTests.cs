using System.Text.RegularExpressions;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Schema;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed partial class AdoptionMigrationGuidanceDocumentationTests
{
    [Test]
    public void UpgradeGuide_SeparatesGreenfieldAndExistingPolicyPathsWithoutReleaseIdentity()
    {
        string guide = ReadNormalizedDocumentation("guides/upgrading.md");

        Assert.Multiple(() =>
        {
            Assert.That(guide, Does.Contain("# Adopt or Upgrade ArchLinterNet"));
            Assert.That(guide, Does.Contain("## Greenfield adoption"));
            Assert.That(guide, Does.Contain("## Upgrade an existing policy"));
            Assert.That(guide, Does.Contain("package releases and persisted document/schema versions have separate lifecycles"));
            Assert.That(guide, Does.Contain("dotnet arch-linter-net policy check"));
            Assert.That(guide, Does.Contain("solution: Example.Product.slnx"));
            Assert.That(guide, Does.Contain("dotnet build Example.Product.slnx --no-restore"));
            Assert.That(guide, Does.Contain("--ensure-built --no-restore"));
            Assert.That(guide, Does.Contain("changed`, `stale`, or `ambiguous`"));
            Assert.That(guide, Does.Contain("CI uses read-only `baseline verify`"));
            Assert.That(guide, Does.Contain("must never regenerate, update, or commit accepted debt"));
            Assert.That(guide, Does.Not.Match(@"Adopt or Upgrade (?:to )?v?\d+\.\d+\.\d+"));
        });
    }

    [Test]
    public void UpgradeGuide_DocumentsInstalledSchemasReportsAndExecutionControls()
    {
        string guide = ReadNormalizedDocumentation("guides/upgrading.md");

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
            Assert.That(guide, Does.Contain("`.config/dotnet-tools.json`"));
        });
    }

    [Test]
    public void ReferenceEntrypoints_PreserveArgumentsStreamsAndExitStatusWithoutDocsOwnedPackagePin()
    {
        string entrypoints = ReadNormalizedDocumentation("guides/reference-entrypoints.md");

        Assert.Multiple(() =>
        {
            Assert.That(entrypoints, Does.Contain("# Reference Entrypoints"));
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
            Assert.That(entrypoints, Does.Contain("task --exit-code architecture"));
            Assert.That(entrypoints, Does.Contain("does not preserve the product exit code"));
            Assert.That(entrypoints, Does.Contain("`.config/dotnet-tools.json`"));
            Assert.That(entrypoints, Does.Not.Match(@"ArchLinterNet\.Cli --version v?\d+\.\d+\.\d+"));
        });
    }

    [Test]
    public void NavigationAndPublicReferences_UseEvergreenCanonicalGuidance()
    {
        string root = RepositoryRoot();
        string nav = File.ReadAllText(Path.Combine(root, "mkdocs.yml"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.Multiple(() =>
        {
            Assert.That(nav, Does.Contain("guides/upgrading.md"));
            Assert.That(nav, Does.Contain("guides/reference-entrypoints.md"));
            Assert.That(nav, Does.Not.Contain("migration-to-0-5-1.md"));
            Assert.That(nav, Does.Not.Contain("release-notes-0-5-1.md"));
            Assert.That(readme, Does.Contain("/guides/upgrading/"));
            Assert.That(readme, Does.Contain("Reference entrypoints"));
            Assert.That(readme, Does.Not.Contain("public adoption package line"));
            Assert.That(File.Exists(Path.Combine(root, "docs", "guides", "migration-to-0-5-1.md")), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "docs", "reference", "release-notes-0-5-1.md")), Is.False);
        });
    }

    [Test]
    public void PublicSchemaGuidance_UsesInstalledRegistryWithoutTreatingPackageSemVerAsSchemaIdentity()
    {
        string root = RepositoryRoot();
        string schemaReference = ReadDocumentation("reference/yaml-schema.md");
        string cliReference = ReadDocumentation("cli/index.md");
        string releaseProcess = ReadDocumentation("reference/release-process.md");
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        string guidance = string.Join(Environment.NewLine, readme, schemaReference, cliReference, releaseProcess);
        string normalizedGuidance = WhitespaceRunRegex().Replace(guidance, " ");
        var registry = new PackagedSchemaRegistry();
        var supportedIds = registry.List()
            .Select(static schema => schema.SchemaId)
            .ToHashSet(StringComparer.Ordinal);
        string[] documentedIds = SchemaUrlRegex().Matches(guidance)
            .Select(static match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(normalizedGuidance, Does.Contain("independently from package SemVer"));
            Assert.That(normalizedGuidance, Does.Contain("schema list"));
            Assert.That(normalizedGuidance, Does.Not.Contain("public adoption package line"));
            Assert.That(normalizedGuidance, Does.Not.Match(@"current public .*package line"));
            Assert.That(documentedIds, Is.Not.Empty);
            Assert.That(documentedIds, Is.SubsetOf(supportedIds));
        });
    }

    private static string ReadDocumentation(string relativePath)
    {
        return File.ReadAllText(Path.Combine(
            RepositoryRoot(), "docs", relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string ReadNormalizedDocumentation(string relativePath)
    {
        return string.Join(' ', ReadDocumentation(relativePath)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string RepositoryRoot()
    {
        return new ArchitectureRepositoryRootResolver().Resolve();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRunRegex();

    [GeneratedRegex(@"https://archlinternet\.dev/schema/[^\s]+?\.schema\.json")]
    private static partial Regex SchemaUrlRegex();
}
