namespace ArchLinterNet.Core.Execution;

/// <summary>
/// Auditable baseline-identity classification for one registered contract family. The family
/// registry owns this metadata so adding a checker cannot silently opt out of the baseline
/// inventory.
/// </summary>
internal sealed record ArchitectureBaselineIdentityDescriptor(
    bool IsBaselineCapable,
    IReadOnlyList<string> SemanticDimensions,
    string? NonBaselineReason = null)
{
    private const string SourceAssembly = "source-assembly";
    private const string TargetAssembly = "target-assembly";

    public static ArchitectureBaselineIdentityDescriptor For(string familyId, bool isBaselineCapable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);

        if (!isBaselineCapable)
        {
            return familyId switch
            {
                "asmdef" => new(false, Array.Empty<string>(), "Asmdef validation does not support ignored violations."),
                "layer_template" => new(false, Array.Empty<string>(), "Templates expand into layer instances before findings are evaluated."),
                _ => throw new InvalidOperationException($"Non-baseline-capable family '{familyId}' requires an explicit reason."),
            };
        }

        return new(true, DimensionsFor(familyId));
    }

    private static string[] DimensionsFor(string familyId)
    {
        // Every baseline-capable family starts with these semantic dimensions. Family-specific
        // dimensions below are intentionally named as data, never inferred from diagnostic prose.
        string[] common = ["contract-family", "contract-id", "source", "target", "occurrence"];
        return familyId switch
        {
            "method_body" or "composition" => common.Concat([SourceAssembly, "source-member", "target-member"]).ToArray(),
            "package_dependency" or "package_allow_only" => common.Concat(["source-project", "package-id"]).ToArray(),
            "framework_dependency" or "framework_allow_only" => common.Concat(["source-project", "framework-reference", "target-framework", "condition"]).ToArray(),
            "public_api_surface" => common.Concat([SourceAssembly, "api-symbol"]).ToArray(),
            "contract_surface_exposure" => common.Concat([SourceAssembly, "source-type", "source-member", "exposure-path", TargetAssembly, "target-type"]).ToArray(),
            "coverage" => common.Concat(["coverage-subject", "coverage-kind"]).ToArray(),
            "metric_budgets" => common.Concat(["metric-id", "metric-kind", "native-subject", "effective-scope", "bound", "limit"]).ToArray(),
            "project_metadata" => common.Concat(["source-project", "metadata-key", "configuration", "target-framework"]).ToArray(),
            "assembly_independence" or "assembly_dependency" or "assembly_allow_only" => common.Concat([SourceAssembly, TargetAssembly]).ToArray(),
            "external" or "external_allow_only" => common.Concat([SourceAssembly, TargetAssembly, "target-type"]).ToArray(),
            "dependency" or "layer" or "allow_only" or "cycle" or "acyclic_sibling" or "module_container"
                or "independence" or "protected" or "context_dependency" or "context_allow_only"
                or "port_boundary" or "type_placement" or "layout_conventions" or "attribute_usage"
                or "inheritance" or "interface_implementation" => common.Concat([SourceAssembly, "source-type", TargetAssembly, "target-type"]).ToArray(),
            _ => throw new InvalidOperationException($"Baseline-capable family '{familyId}' requires an identity-dimension classification."),
        };
    }
}
