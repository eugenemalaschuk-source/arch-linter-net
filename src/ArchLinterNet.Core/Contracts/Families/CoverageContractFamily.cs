using ArchLinterNet.Core.Model;
using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed class ArchitectureCoverageRoot
{
    [YamlMember(Alias = "namespace")] public string Namespace { get; set; } = string.Empty;

    [YamlMember(Alias = "namespace_suffix")] public string NamespaceSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "include")] public List<string> Include { get; set; } = new();

    [YamlMember(Alias = "exclude")] public List<string> Exclude { get; set; } = new();
}

public sealed class ArchitectureCoverageExclusion
{
    [YamlMember(Alias = "namespace")] public string Namespace { get; set; } = string.Empty;

    [YamlMember(Alias = "namespace_suffix")] public string NamespaceSuffix { get; set; } = string.Empty;

    [YamlMember(Alias = "project")] public string Project { get; set; } = string.Empty;

    [YamlMember(Alias = "assembly")] public string Assembly { get; set; } = string.Empty;

    [YamlMember(Alias = "contract_id")] public string ContractId { get; set; } = string.Empty;

    [YamlMember(Alias = "role")] public string Role { get; set; } = string.Empty;

    [YamlMember(Alias = "metadata")] public Dictionary<string, object> Metadata { get; set; } = new();

    [YamlMember(Alias = "between")] public List<string> Between { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class ArchitectureOptionalRuleInput
{
    [YamlMember(Alias = "contract_id")] public string ContractId { get; set; } = string.Empty;

    [YamlMember(Alias = "input")] public string Input { get; set; } = string.Empty;

    [YamlMember(Alias = "layer")] public string Layer { get; set; } = string.Empty;

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;

    [YamlIgnore] internal ArchitecturePolicySourceLocation? PolicyLocation { get; set; }
}

internal sealed record ArchitectureRuleInputReference(string Input, string Layer);

internal static class ArchitectureRuleInputReferences
{
    private const string SourceInput = "source";

    public static IEnumerable<ArchitectureRuleInputReference> For(IArchitectureContract contract)
    {
        return contract switch
        {
            ArchitectureDependencyContract c => One(SourceInput, c.Source).Concat(Many("forbidden", c.Forbidden)),
            ArchitectureAllowOnlyContract c => One(SourceInput, c.Source).Concat(Many("allowed", c.Allowed)),
            ArchitectureCycleContract c => Many("layers", c.Layers),
            ArchitectureMethodBodyContract c => One(SourceInput, c.Source),
            ArchitectureIndependenceContract c => Many("layers", c.Layers),
            ArchitectureLayerContract c => Many("layers", c.Layers),
            ArchitectureProtectedContract c => Many("protected", c.Protected).Concat(Many("allowed_importers", c.AllowedImporters)),
            ArchitectureExternalDependencyContract c => One(SourceInput, c.Source),
            ArchitectureExternalAllowOnlyContract c => One(SourceInput, c.Source),
            ArchitectureTypePlacementContract c => One("types_matching.layer", c.TypesMatching.Layer)
                .Concat(Many("must_reside_in_layers", c.MustResideInLayers)),
            ArchitectureAttributeUsageContract c => Many("allowed_only_in_layers", c.AllowedOnlyInLayers)
                .Concat(Many("forbidden_in_layers", c.ForbiddenInLayers)),
            ArchitectureInheritanceContract c => Many("source_layers", c.SourceLayers),
            ArchitectureInterfaceImplementationContract c => Many("allowed_only_in_layers", c.AllowedOnlyInLayers)
                .Concat(Many("forbidden_in_layers", c.ForbiddenInLayers)),
            ArchitectureCompositionContract c => Many("allowed_only_in_layers", c.AllowedOnlyInLayers),
            _ => Array.Empty<ArchitectureRuleInputReference>()
        };
    }

    private static ArchitectureRuleInputReference[] One(string input, string layer) =>
        string.IsNullOrWhiteSpace(layer)
            ? Array.Empty<ArchitectureRuleInputReference>()
            : new[] { new ArchitectureRuleInputReference(input, layer) };

    private static IEnumerable<ArchitectureRuleInputReference> Many(string input, IEnumerable<string> layers) =>
        layers.Where(layer => !string.IsNullOrWhiteSpace(layer))
            .Select(layer => new ArchitectureRuleInputReference(input, layer));
}

public sealed class ArchitectureCoverageContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "scope")] public string Scope { get; set; } = string.Empty;

    [YamlMember(Alias = "roots")] public List<ArchitectureCoverageRoot> Roots { get; set; } = new();

    [YamlMember(Alias = "between")] public List<List<string>> Between { get; set; } = new();

    [YamlMember(Alias = "contract_ids")] public List<string> ContractIds { get; set; } = new();

    [YamlMember(Alias = "exclude")] public List<ArchitectureCoverageExclusion> Exclude { get; set; } = new();

    [YamlMember(Alias = "optional_inputs")] public List<ArchitectureOptionalRuleInput> OptionalInputs { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;

    [YamlMember(Alias = "ignored_violations")]
    public List<ArchitectureIgnoredViolation> IgnoredViolations { get; set; } = new();
}
