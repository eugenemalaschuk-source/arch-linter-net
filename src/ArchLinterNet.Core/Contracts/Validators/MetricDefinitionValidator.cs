using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class MetricDefinitionValidator : IArchitecturePolicyDocumentValidator
{
    private static readonly HashSet<string> _kinds =
        ArchitectureMetricKinds.All.ToHashSet(StringComparer.Ordinal);

    public void Validate(ArchitectureContractDocument document)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (ArchitectureMetricDefinition definition in document.Metrics)
        {
            document.Provenance.SetValidationSubject(definition);
            ValidateDefinition(document, definition, ids);
        }
    }

    private static void ValidateDefinition(ArchitectureContractDocument document, ArchitectureMetricDefinition definition, HashSet<string> ids)
    {
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            throw new InvalidOperationException("Every metric definition must declare a non-empty id.");
        }

        if (!ids.Add(definition.Id))
        {
            throw new InvalidOperationException($"Duplicate metric id '{definition.Id}'.");
        }

        if (!_kinds.Contains(definition.Kind))
        {
            throw new InvalidOperationException(
                $"Metric '{definition.Id}' declares unsupported kind '{definition.Kind}'. " +
                $"Supported values are {string.Join(", ", ArchitectureMetricKinds.All.Select(kind => $"'{kind}'"))}.");
        }

        bool isPublic = definition.Kind == ArchitectureMetricKinds.PublicContractSurfaceCount;
        bool isTopology = !isPublic;

        ValidateTargetShape(definition, isPublic, isTopology);

        if (isTopology)
        {
            ValidateTopologyTarget(document, definition);
        }

        if (isPublic)
        {
            ValidatePublicApiSurfaceTarget(document, definition);
        }
    }

    private static void ValidateTargetShape(ArchitectureMetricDefinition definition, bool isPublic, bool isTopology)
    {
        bool hasTopologyTarget = definition.TopologyNode is not null;
        bool hasPublicTarget = definition.PublicApiSurface is not null;
        bool hasUnitTarget = definition.Unit is not null;
        bool topologyTarget = !string.IsNullOrWhiteSpace(definition.TopologyNode);
        bool publicTarget = !string.IsNullOrWhiteSpace(definition.PublicApiSurface);
        bool isFootprint = definition.Kind == ArchitectureMetricKinds.ComponentFootprintCount;

        if (isTopology && (!topologyTarget || hasPublicTarget))
        {
            throw new InvalidOperationException(
                $"Metric '{definition.Id}' kind '{definition.Kind}' requires exactly one 'topology_node' target.");
        }

        if (isPublic && (hasTopologyTarget || hasUnitTarget || !publicTarget))
        {
            throw new InvalidOperationException(
                $"Metric '{definition.Id}' kind '{definition.Kind}' requires exactly one 'public_api_surface' target.");
        }

        if (isFootprint && definition.Unit is not ("project" or "assembly"))
        {
            throw new InvalidOperationException(
                $"Metric '{definition.Id}' footprint must select unit 'project' or 'assembly'.");
        }

        if (!isFootprint && hasUnitTarget)
        {
            throw new InvalidOperationException(
                $"Metric '{definition.Id}' kind '{definition.Kind}' does not accept 'unit'.");
        }
    }

    private static void ValidateTopologyTarget(ArchitectureContractDocument document, ArchitectureMetricDefinition definition)
    {
        if (document.Topology is null)
        {
            throw new InvalidOperationException(
                $"Metric '{definition.Id}' targets topology node '{definition.TopologyNode}', but no topology is declared.");
        }

        if (!document.Topology.Nodes.Any(node => string.Equals(node.Id, definition.TopologyNode, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Metric '{definition.Id}' references undeclared topology node '{definition.TopologyNode}'.");
        }

        if (definition.Kind == ArchitectureMetricKinds.TopologyTypeCount
            && document.Topology is { SubjectKind: not "type" })
        {
            throw new InvalidOperationException(
                $"Metric '{definition.Id}' topology_type_count requires a topology with subject_kind 'type'.");
        }
    }

    private static void ValidatePublicApiSurfaceTarget(ArchitectureContractDocument document, ArchitectureMetricDefinition definition)
    {
        bool strictMatch = document.Contracts.StrictPublicApiSurface
            .Any(contract => string.Equals(contract.Id, definition.PublicApiSurface, StringComparison.OrdinalIgnoreCase));
        bool auditMatch = document.Contracts.AuditPublicApiSurface
            .Any(contract => string.Equals(contract.Id, definition.PublicApiSurface, StringComparison.OrdinalIgnoreCase));
        if (!strictMatch && !auditMatch)
        {
            throw new InvalidOperationException(
                $"Metric '{definition.Id}' references unknown public API surface '{definition.PublicApiSurface}'.");
        }

        if (strictMatch && auditMatch)
        {
            throw new InvalidOperationException(
                $"Metric '{definition.Id}' references ambiguous public API surface '{definition.PublicApiSurface}' " +
                "declared in both strict and audit modes.");
        }
    }
}
