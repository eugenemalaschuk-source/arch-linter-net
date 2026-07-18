using System.Text.Json;
using System.Text.RegularExpressions;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public interface IArchitectureSarifFormatter
{
    string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        string toolVersion);
}

public sealed partial class ArchitectureSarifFormatter : IArchitectureSarifFormatter
{
    private const string SchemaUri =
        "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json";

    private const string ToolName = "arch-linter-net";
    private const string SarifVersion = "2.1.0";
    private const string VersionPropertyName = "version";
    private const string MessagePropertyName = "message";
    private const string MethodBodyCategory = "method-body";
    private const string MethodBodyIlCategory = "method-body-il";
    private const string CycleRuleFallback = "dependency-cycle";

    [GeneratedRegex(@"^line (?<line>\d+):", RegexOptions.CultureInvariant)]
    private static partial Regex MethodBodyLinePattern();
    [GeneratedRegex(@"^\[(?<id>[^\]]+)\] ", RegexOptions.CultureInvariant)]
    private static partial Regex CycleIdPrefixPattern();

    public string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        string toolVersion)
    {
        return FormatResultAsSarifCore(
            mode,
            violations,
            cycles.Select(cycle => (Func<string, ResultEntry>)(level => BuildCycleEntry(cycle, level))),
            toolVersion);
    }

    public static string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<ArchitectureCycleFinding> cycles,
        string toolVersion)
    {
        return FormatResultAsSarifCore(
            mode,
            violations,
            cycles.Select(cycle => (Func<string, ResultEntry>)(level =>
                BuildCycleEntry(ArchitectureDiagnosticMapper.FromCycle(cycle), level))),
            toolVersion);
    }

    private static string FormatResultAsSarifCore(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IEnumerable<Func<string, ResultEntry>> cycleEntryFactories,
        string toolVersion)
    {
        string level = mode == "strict" ? "error" : "warning";

        List<ResultEntry> entries = violations
            .Select(ArchitectureDiagnosticMapper.FromViolation)
            .Select(diagnostic => BuildViolationEntry(diagnostic, level))
            .Concat(cycleEntryFactories.Select(factory => factory(level)))
            .OrderBy(e => e.RuleId, StringComparer.Ordinal)
            .ThenBy(e => e.SourceIdentifier, StringComparer.Ordinal)
            .ThenBy(e => e.Category, StringComparer.Ordinal)
            .ToList();

        object[] rules = entries
            .GroupBy(e => e.RuleId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (object)new Dictionary<string, object?>
            {
                ["id"] = g.Key,
                ["shortDescription"] = new Dictionary<string, object?> { ["text"] = g.First().ContractName },
            })
            .ToArray();

        object[] results = entries.Select(e => (object)e.Json).ToArray();

        var payload = new Dictionary<string, object?>
        {
            ["$schema"] = SchemaUri,
            [VersionPropertyName] = SarifVersion,
            ["runs"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["tool"] = new Dictionary<string, object?>
                    {
                        ["driver"] = new Dictionary<string, object?>
                        {
                            ["name"] = ToolName,
                            ["version"] = toolVersion,
                            ["rules"] = rules,
                        },
                    },
                    ["results"] = results,
                },
            },
        };

        return JsonSerializer.Serialize(payload);
    }

    private static ResultEntry BuildViolationEntry(ArchitectureDiagnostic diagnostic, string level)
    {
        (string sourceType, string forbiddenNamespace, IReadOnlyCollection<string> references) = ExtractFields(diagnostic);
        string ruleId = diagnostic.ContractId ?? ArchitecturePolicyDocumentLoader.NormalizeToContractId(diagnostic.ContractName);

        var json = new Dictionary<string, object?>
        {
            ["ruleId"] = ruleId,
            ["level"] = level,
            [MessagePropertyName] = new Dictionary<string, object?>
            {
                ["text"] = $"[{diagnostic.ContractName}] {sourceType} -> {forbiddenNamespace}: {string.Join(", ", references)}",
            },
        };

        if (forbiddenNamespace == MethodBodyCategory)
        {
            json["locations"] = BuildPhysicalLocations(sourceType, references);
        }
        else
        {
            json["logicalLocations"] = BuildLogicalLocations(sourceType, LogicalLocationKindFor(diagnostic, forbiddenNamespace));
        }

        object[] relatedPolicyLocations = FormatPolicyLocationsForSarif(
            diagnostic.PolicyLocation,
            diagnostic.RelatedPolicyLocations);
        if (relatedPolicyLocations.Length > 0)
        {
            json["relatedLocations"] = relatedPolicyLocations;
        }

        return new ResultEntry(ruleId, diagnostic.ContractName, sourceType, forbiddenNamespace, json);
    }

    public static object[] FormatPolicyLocationsForSarif(
        ArchitecturePolicySourceLocation? primaryLocation,
        IEnumerable<ArchitecturePolicySourceLocation> relatedLocations)
    {
        IEnumerable<ArchitecturePolicySourceLocation> locations =
            primaryLocation is null
                ? relatedLocations
                : new[] { primaryLocation }.Concat(relatedLocations);

        return locations
            .Distinct()
            .OrderBy(location => location.SourceOrdinal)
            .ThenBy(location => location.EncounterOrdinal)
            .Select((location, index) => (object)new Dictionary<string, object?>
            {
                ["id"] = index + 1,
                [MessagePropertyName] = new Dictionary<string, object?>
                {
                    ["text"] = $"Policy {location.Role.ToString().ToLowerInvariant()} definition at {location.YamlPath}"
                },
                ["physicalLocation"] = new Dictionary<string, object?>
                {
                    ["artifactLocation"] = new Dictionary<string, object?> { ["uri"] = location.SourcePath },
                    ["region"] = new Dictionary<string, object?>
                    {
                        ["startLine"] = location.Line,
                        ["startColumn"] = location.Column
                    }
                }
            })
            .ToArray();
    }

    private static ResultEntry BuildCycleEntry(string cycle, string level)
    {
        Match match = CycleIdPrefixPattern().Match(cycle);
        string ruleId = match.Success ? match.Groups["id"].Value : CycleRuleFallback;
        string path = match.Success ? cycle[match.Length..] : cycle;

        var json = new Dictionary<string, object?>
        {
            ["ruleId"] = ruleId,
            ["level"] = level,
            [MessagePropertyName] = new Dictionary<string, object?> { ["text"] = $"Dependency cycle detected: {path}" },
            ["logicalLocations"] = BuildLogicalLocations(path, "namespace"),
        };

        return new ResultEntry(ruleId, ruleId, path, "cycle", json);
    }

    private static ResultEntry BuildCycleEntry(CycleDiagnostic diagnostic, string level)
    {
        string ruleId = diagnostic.ContractId ?? CycleRuleFallback;

        var json = new Dictionary<string, object?>
        {
            ["ruleId"] = ruleId,
            ["level"] = level,
            [MessagePropertyName] = new Dictionary<string, object?> { ["text"] = $"Dependency cycle detected: {diagnostic.Path}" },
            ["logicalLocations"] = BuildLogicalLocations(diagnostic.Path, "namespace"),
        };

        object[] relatedPolicyLocations = FormatPolicyLocationsForSarif(
            diagnostic.PolicyLocation,
            diagnostic.RelatedPolicyLocations);
        if (relatedPolicyLocations.Length > 0)
        {
            json["relatedLocations"] = relatedPolicyLocations;
        }

        return new ResultEntry(ruleId, diagnostic.ContractName, diagnostic.Path, "cycle", json);
    }

    private static object[] BuildPhysicalLocations(string filePath, IReadOnlyCollection<string> references)
    {
        if (references.Count == 0)
        {
            return new object[]
            {
                new Dictionary<string, object?>
                {
                    ["physicalLocation"] = new Dictionary<string, object?>
                    {
                        ["artifactLocation"] = new Dictionary<string, object?> { ["uri"] = filePath },
                    },
                },
            };
        }

        return references.Select(reference =>
        {
            var physicalLocation = new Dictionary<string, object?>
            {
                ["artifactLocation"] = new Dictionary<string, object?> { ["uri"] = filePath },
            };

            Match match = MethodBodyLinePattern().Match(reference);
            if (match.Success && int.TryParse(match.Groups["line"].Value, out int line))
            {
                physicalLocation["region"] = new Dictionary<string, object?> { ["startLine"] = line };
            }

            return (object)new Dictionary<string, object?> { ["physicalLocation"] = physicalLocation };
        }).ToArray();
    }

    private static object[] BuildLogicalLocations(string fullyQualifiedName, string kind)
    {
        return new object[]
        {
            new Dictionary<string, object?>
            {
                ["fullyQualifiedName"] = fullyQualifiedName,
                ["kind"] = kind,
            },
        };
    }

    // Best-effort hint: no diagnostic kind carries an explicit "this identifier is a
    // namespace/type/package" flag, so the kind is inferred from the diagnostic's concrete subtype.
    // IL-scanned method-body violations are a special case: they map to the generic
    // DependencyDiagnostic subtype like namespace/layer violations do, but SourceType is a
    // type's fully-qualified name (see ArchitectureIlMethodBodyScanner), not a namespace.
    private static string LogicalLocationKindFor(ArchitectureDiagnostic diagnostic, string forbiddenNamespace)
    {
        if (forbiddenNamespace == MethodBodyIlCategory)
        {
            return "type";
        }

        return diagnostic switch
        {
            DependencyDiagnostic or ConfigurationDiagnostic => "namespace",
            PackageDependencyDiagnostic => "package",
            _ => "type",
        };
    }

    private static (string SourceType, string ForbiddenNamespace, IReadOnlyCollection<string> References) ExtractFields(
        ArchitectureDiagnostic diagnostic) => diagnostic switch
        {
            DependencyDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            ConfigurationDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            ExternalDependencyDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            PackageDependencyDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            TypePlacementDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            LayoutConventionDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            PublicApiSurfaceDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            AttributeUsageDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            InheritanceDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            InterfaceImplementationDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            CompositionDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            ProjectMetadataDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            _ => (string.Empty, string.Empty, Array.Empty<string>()),
        };

    private sealed record ResultEntry(
        string RuleId,
        string ContractName,
        string SourceIdentifier,
        string Category,
        Dictionary<string, object?> Json);
}
