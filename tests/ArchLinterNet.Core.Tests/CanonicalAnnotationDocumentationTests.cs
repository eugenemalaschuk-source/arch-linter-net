using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class CanonicalAnnotationDocumentationTests
{
    [Test]
    public void PublicCatalog_MatchesCanonicalManifest()
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(repositoryRoot, "architecture", "semantic-annotations.v1.json")));
        string catalog = File.ReadAllText(Path.Combine(
            repositoryRoot, "docs", "reference", "semantic-annotation-catalog.md"));

        JsonElement root = manifest.RootElement;
        IReadOnlyList<IReadOnlyDictionary<string, string>> roleRows = ReadTable(
            catalog, "## Role attribute contract");
        IReadOnlyList<IReadOnlyDictionary<string, string>> contextRows = ReadTable(
            catalog, "## Context metadata attributes");
        IReadOnlyDictionary<string, string> compatibility = ReadKeyValueTable(
            ReadTable(catalog, "## Package and compatibility"), "Manifest field", "Value");
        IReadOnlyDictionary<string, string> marker = ReadKeyValueTable(
            ReadTable(catalog, "## Assembly compatibility marker"), "Marker field", "Manifest value");

        JsonElement[] manifestRoles = root.GetProperty("roles").EnumerateArray().ToArray();
        JsonElement[] manifestContexts = root.GetProperty("contextAttributes").EnumerateArray().ToArray();
        var documentedRoleFqns = roleRows
            .Select(row => CodeTokens(row["Exact FQN"]).Single())
            .ToArray();
        var documentedContextFqns = contextRows
            .Select(row => CodeTokens(row["Exact FQN"]).Single())
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(roleRows.Count, Is.EqualTo(manifestRoles.Length),
                "The role table must contain one row for every manifest role.");
            Assert.That(documentedRoleFqns, Is.Unique,
                "The role table must not contain duplicate FQNs.");
            Assert.That(documentedRoleFqns, Is.EquivalentTo(manifestRoles
                .Select(role => role.GetProperty("fqn").GetString()!)),
                "The role table must cover exactly the manifest role FQNs.");
            Assert.That(contextRows.Count, Is.EqualTo(manifestContexts.Length),
                "The context table must contain one row for every manifest context attribute.");
            Assert.That(documentedContextFqns, Is.Unique,
                "The context table must not contain duplicate FQNs.");
            Assert.That(documentedContextFqns, Is.EquivalentTo(manifestContexts
                .Select(context => context.GetProperty("fqn").GetString()!)),
                "The context table must cover exactly the manifest context FQNs.");
        });

        var roleByFqn = roleRows.ToDictionary(
            row => CodeTokens(row["Exact FQN"]).Single(), StringComparer.Ordinal);
        var contextByFqn = contextRows.ToDictionary(
            row => CodeTokens(row["Exact FQN"]).Single(), StringComparer.Ordinal);

        foreach (JsonElement role in manifestRoles)
        {
            string fqn = role.GetProperty("fqn").GetString()!;
            IReadOnlyDictionary<string, string> row = roleByFqn[fqn];
            string roleName = role.GetProperty("role").GetString()!;
            string[] expectedTargets = role.GetProperty("attributeTargets")
                .EnumerateArray().Select(value => value.GetString()!).ToArray();
            string[] expectedProperties = root.GetProperty("roleAttributeContract")
                .GetProperty("metadataProperties")
                .EnumerateArray()
                .Select(value => value.GetProperty("property").GetString()!)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(CodeValue(row["Canonical role"]), Is.EqualTo(roleName), fqn);
                Assert.That(row["Manifest family"], Is.EqualTo(role.GetProperty("family").GetString()), fqn);
                Assert.That(CodeTokens(row["Valid attributeTargets"]), Is.EqualTo(expectedTargets), fqn);
                Assert.That(CodeTokens(row["Supported role properties"]), Is.EqualTo(expectedProperties), fqn);
                Assert.That(row["Assembly use supported"].Trim().ToLowerInvariant(),
                    Is.EqualTo(expectedTargets.Contains("assembly") ? "yes" : "no"), fqn);
                Assert.That(row["Example selector or policy use"], Does.Contain($"role: {roleName}"), fqn);
            });
        }

        foreach (JsonElement context in manifestContexts)
        {
            string fqn = context.GetProperty("fqn").GetString()!;
            IReadOnlyDictionary<string, string> row = contextByFqn[fqn];
            string key = context.GetProperty("key").GetString()!;

            Assert.Multiple(() =>
            {
                Assert.That(CodeValue(row["Metadata key"]), Is.EqualTo(key), fqn);
                Assert.That(CodeTokens(row["Valid attributeTargets"]), Is.EqualTo(
                    context.GetProperty("attributeTargets").EnumerateArray()
                        .Select(value => value.GetString()!).ToArray()), fqn);
                Assert.That(row["Constructor"], Does.Contain("value: string").And.Contain("required"), fqn);
                Assert.That(CodeValue(row["Accessibility"]), Is.EqualTo(context.GetProperty("accessibility").GetString()), fqn);
                Assert.That(row["allowMultiple"].Trim().ToLowerInvariant(), Is.EqualTo(
                    context.GetProperty("allowMultiple").GetBoolean().ToString().ToLowerInvariant()), fqn);
                Assert.That(row["inherited"].Trim().ToLowerInvariant(), Is.EqualTo(
                    context.GetProperty("inherited").GetBoolean().ToString().ToLowerInvariant()), fqn);
                Assert.That(row["Constructor accessibility"], Is.EqualTo(
                    context.GetProperty("constructorAccessibility").GetString()), fqn);
                Assert.That(row["Metadata value rule"], Is.EqualTo(context.GetProperty("metadataValueRule").GetString()), fqn);
                Assert.That(row["Assembly use supported"].Trim().ToLowerInvariant(), Is.EqualTo("yes"), fqn);
                Assert.That(row["Example (metadata alongside a primary role selector)"],
                    Does.Contain("Metadata alongside a primary role selector")
                        .And.Contain($"metadata: {{ {key}:"), fqn);
            });
        }

        JsonElement package = root.GetProperty("package");
        JsonElement contentFiles = package.GetProperty("contentFiles");
        var expectedCompatibility = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["manifestFormatVersion"] = root.GetProperty("manifestFormatVersion").GetInt32()
                .ToString(CultureInfo.InvariantCulture),
            ["catalogGeneration"] = root.GetProperty("catalogGeneration").GetInt32()
                .ToString(CultureInfo.InvariantCulture),
            ["productRelease"] = root.GetProperty("productRelease").GetString()!,
            ["package.id"] = package.GetProperty("id").GetString()!,
            ["package.version"] = package.GetProperty("version").GetString()!,
            ["package.distribution"] = package.GetProperty("distribution").GetString()!,
            ["package.prohibitedAssets"] = string.Join(", ", package.GetProperty("prohibitedAssets")
                .EnumerateArray().Select(value => value.GetString()!)),
            ["package.minimumCSharpLanguageVersion"] = package.GetProperty("minimumCSharpLanguageVersion").GetString()!,
            ["package.contentFiles.pattern"] = contentFiles.GetProperty("pattern").GetString()!,
            ["package.contentFiles.buildAction"] = contentFiles.GetProperty("buildAction").GetString()!,
            ["package.contentFiles.copyToOutput"] = contentFiles.GetProperty("copyToOutput").GetBoolean()
                .ToString().ToLowerInvariant(),
            ["package.contentFiles.directReferenceRequiredPerAuthoringProject"] = contentFiles
                .GetProperty("directReferenceRequiredPerAuthoringProject").GetBoolean().ToString().ToLowerInvariant(),
            ["package.contentFiles.transitiveSourceInjection"] = contentFiles
                .GetProperty("transitiveSourceInjection").GetBoolean().ToString().ToLowerInvariant(),
            ["package.minimumAnnotationAwareToolVersion"] = package
                .GetProperty("minimumAnnotationAwareToolVersion").GetString()!,
            ["package.compatiblePackageRangeForGeneration1"] = package
                .GetProperty("compatiblePackageRangeForGeneration1").GetString()!
        };

        Assert.Multiple(() =>
        {
            Assert.That(compatibility.Keys, Is.EquivalentTo(expectedCompatibility.Keys));
            foreach ((string key, string value) in expectedCompatibility)
            {
                Assert.That(compatibility[key].Replace("`", string.Empty, StringComparison.Ordinal),
                    Is.EqualTo(value), $"Compatibility field {key}");
            }
        });

        JsonElement identity = root.GetProperty("catalogIdentityMarker");
        Assert.Multiple(() =>
        {
            Assert.That(CodeValue(marker["Exact FQN"]), Is.EqualTo(identity.GetProperty("fqn").GetString()));
            Assert.That(CodeValue(marker["Accessibility"]), Is.EqualTo(identity.GetProperty("accessibility").GetString()));
            Assert.That(CodeTokens(marker["Valid attributeTargets"]), Is.EqualTo(
                identity.GetProperty("attributeTargets").EnumerateArray()
                    .Select(value => value.GetString()!).ToArray()));
            Assert.That(marker["Constructor"], Does.Contain("catalogGeneration: int").And.Contain("packageVersion: string"));
            Assert.That(marker["Marker value"], Does.Contain($"catalogGeneration: {identity.GetProperty("value").GetProperty("catalogGeneration").GetInt32()}"));
            Assert.That(marker["Marker value"], Does.Contain($"packageVersion: {identity.GetProperty("value").GetProperty("packageVersion").GetString()}"));
            Assert.That(marker["Value rule"], Is.EqualTo(identity.GetProperty("valueRule").GetString()));
            Assert.That(marker["allowMultiple"].Trim().ToLowerInvariant(), Is.EqualTo(
                identity.GetProperty("allowMultiple").GetBoolean().ToString().ToLowerInvariant()));
            Assert.That(marker["inherited"].Trim().ToLowerInvariant(), Is.EqualTo(
                identity.GetProperty("inherited").GetBoolean().ToString().ToLowerInvariant()));
            Assert.That(marker["Constructor accessibility"], Is.EqualTo(identity.GetProperty("constructorAccessibility").GetString()));
            Assert.That(marker["Assembly use supported"], Does.Contain("yes"));
            Assert.That(marker["Selectable semantic evidence"], Does.Contain("no"));
        });
    }

    private static IReadOnlyDictionary<string, string> ReadKeyValueTable(
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        string keyColumn,
        string valueColumn)
    {
        return rows.ToDictionary(
            row => row[keyColumn].Replace("`", string.Empty, StringComparison.Ordinal),
            row => row[valueColumn],
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadTable(string markdown, string heading)
    {
        string[] lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int headingIndex = Array.FindIndex(lines, line => line.Trim() == heading);
        if (headingIndex < 0)
        {
            throw new InvalidOperationException($"Documentation heading '{heading}' was not found.");
        }

        int headerIndex = Array.FindIndex(lines, headingIndex + 1, lines.Length - headingIndex - 1,
            line => line.TrimStart().StartsWith('|'));
        if (headerIndex < 0)
        {
            throw new InvalidOperationException($"Documentation table for '{heading}' was not found.");
        }

        string[] headers = SplitTableRow(lines[headerIndex])
            .Select(header => header.Replace("`", string.Empty, StringComparison.Ordinal))
            .ToArray();
        var rows = new List<IReadOnlyDictionary<string, string>>();
        for (int index = headerIndex + 1; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            if (!line.StartsWith('|'))
            {
                break;
            }

            if (line.Replace("|", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(":", string.Empty, StringComparison.Ordinal).Trim().Length == 0)
            {
                continue;
            }

            string[] cells = SplitTableRow(line);
            if (cells.Length != headers.Length)
            {
                throw new InvalidOperationException(
                    $"Documentation table for '{heading}' has {cells.Length} cells, expected {headers.Length}.");
            }

            rows.Add(headers.Select((header, cellIndex) => new { header, value = cells[cellIndex] })
                .ToDictionary(pair => pair.header, pair => pair.value, StringComparer.Ordinal));
        }

        return rows;
    }

    private static string[] SplitTableRow(string line)
    {
        return line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
    }

    private static string CodeValue(string value)
    {
        return CodeTokens(value).FirstOrDefault() ?? value.Trim();
    }

    private static string[] CodeTokens(string value)
    {
        return value.Split('`', StringSplitOptions.None)
            .Where((_, index) => index % 2 == 1)
            .ToArray();
    }
}
