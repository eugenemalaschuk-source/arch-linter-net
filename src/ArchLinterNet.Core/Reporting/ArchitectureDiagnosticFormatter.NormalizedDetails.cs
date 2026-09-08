using ArchLinterNet.Core.Model;
using static ArchLinterNet.Core.Reporting.ArchitectureDiagnosticFormatter;

namespace ArchLinterNet.Core.Reporting;

internal static class ArchitectureNormalizedDetailsProjector
{
    // Dispatches to the family-owned projector registered in DiagnosticDetailProjectionRegistry
    // (see ArchitectureDiagnosticFormatter.DetailProjectionRegistry.cs) instead of a central switch
    // enumerating every diagnostic kind - see #453. The throw is unreachable for any of the 24
    // supported diagnostic kinds today; it exists as defense in depth so a future diagnostic type
    // added without a registry entry fails loudly at runtime, not just in the completeness test.
    internal static void ApplyDiagnosticSpecificCiFields(ArchitectureDiagnostic diagnostic, Dictionary<string, object?> obj)
    {
        if (!ArchitectureDiagnosticDetailProjectionRegistry.ByType.TryGetValue(diagnostic.GetType(), out DiagnosticDetailProjector? projector))
        {
            throw new InvalidOperationException(
                $"No diagnostic detail projector registered for diagnostic type '{diagnostic.GetType().Name}'.");
        }

        projector!(diagnostic, obj);
    }

    internal static void ApplyExternalDependencyCiFields(ExternalDependencyDiagnostic external, Dictionary<string, object?> obj)
    {
        obj["forbidden_external_group"] = external.ForbiddenExternalGroup;
    }

    internal static void ApplyPackageDependencyCiFields(PackageDependencyDiagnostic package, Dictionary<string, object?> obj)
    {
        obj["forbidden_package_group"] = package.ForbiddenPackageGroup;
    }

    internal static void ApplyPackageAllowOnlyCiFields(PackageAllowOnlyDiagnostic package, Dictionary<string, object?> obj)
    {
        obj["allowed_package_groups"] = package.AllowedPackageGroups.ToArray();
    }

    internal static void ApplyMetricBudgetCiFields(MetricBudgetDiagnostic budget, Dictionary<string, object?> obj)
    {
        obj["budget_id"] = budget.BudgetId;
        obj["metric_id"] = budget.MetricId;
        obj["metric_kind"] = budget.MetricKind;
        obj["native_subject"] = budget.NativeSubject;
        obj["effective_scope"] = budget.EffectiveScope;
        obj["measured_value"] = budget.MeasuredValue;
        obj["breached_bound"] = budget.BreachedBound;
        obj["configured_limit"] = budget.ConfiguredLimit;
        obj["contributor_count"] = budget.ContributorCount;
        obj["contributors"] = budget.Contributors.ToArray();
        obj["baseline_mode"] = budget.BaselineMode;
        obj["baseline_value"] = budget.BaselineValue;
        obj["delta"] = budget.Delta;
        obj["allowed_delta"] = budget.AllowedDelta;
        obj["effective_threshold"] = budget.EffectiveThreshold;
        obj["absolute_cap"] = budget.AbsoluteCap;
    }

    internal static void ApplyContractSurfaceExposureCiFields(
        ContractSurfaceExposureDiagnostic exposure, Dictionary<string, object?> obj)
    {
        obj["source_assembly"] = exposure.SourceAssemblyName;
        obj["declaring_source_type"] = exposure.DeclaringSourceType;
        obj["exposure_path"] = exposure.ExposurePath;
        obj["canonical_exposure_path"] = exposure.CanonicalExposurePath;
        obj["target_assembly"] = exposure.TargetAssemblyName;
        obj["target_type"] = exposure.TargetTypeName;
        obj["source_surface"] = exposure.SourceSurface;
        obj["member_or_metadata_site"] = exposure.MemberOrMetadataSite;
        obj["reviewed_public_api_surface"] = exposure.ReviewedPublicApiSurface;
        obj["matching_forbidden_selectors"] = exposure.MatchingForbiddenSelectors?.ToArray();
    }

    internal static void ApplyCycleCiFields(CycleDiagnostic cycle, Dictionary<string, object?> obj)
    {
        obj["path"] = cycle.Path;
    }

    internal static void ApplyUnmatchedIgnoreCiFields(UnmatchedIgnoreDiagnostic unmatched, Dictionary<string, object?> obj)
    {
        obj["ignore_index"] = unmatched.IgnoreIndex;
        obj["source_type"] = unmatched.SourceType;
        obj["forbidden_reference"] = unmatched.ForbiddenReference;
        obj["reason"] = unmatched.Reason;
    }

    internal static void ApplyPolicyConsistencyCiFields(PolicyConsistencyDiagnostic policy, Dictionary<string, object?> obj)
    {
        obj["check_kind"] = policy.CheckKind;
        obj["reason"] = policy.Reason;
        obj["conflicting_contract_ids"] = policy.ConflictingContractIds.ToArray();
        obj["conflicting_contract_names"] = policy.ConflictingContractNames.ToArray();
        obj["layers"] = policy.Layers.ToArray();
        obj["representative_type"] = policy.RepresentativeType;
    }

    internal static void ApplyBaselineLifecycleCiFields(BaselineLifecycleDiagnostic baseline, Dictionary<string, object?> obj)
    {
        obj["contract_group"] = baseline.ContractGroup;
        obj["source_type"] = baseline.SourceType;
        obj["forbidden_reference"] = baseline.ForbiddenReference;
        obj["reason"] = baseline.Reason;
        obj["issue"] = baseline.Issue;
        obj["disposition"] = BaselineEntryDispositionNames.WireName(baseline.Disposition);
        obj["suppresses"] = baseline.Suppresses;
        obj["identity"] = baseline.StructuredIdentity is null
            ? null
            : ArchitectureViolationIdentityJson.ToWireObject(baseline.StructuredIdentity);
    }

    internal static void ApplyArchitecturePolicyErrorCiFields(ArchitecturePolicyErrorDiagnostic policyError, Dictionary<string, object?> obj)
    {
        obj["diagnostic_kind"] = policyError.DiagnosticKind.ToString().ToLowerInvariant();
        obj["error_category"] = policyError.ErrorCategory;
        obj["import_chain"] = policyError.ImportChain;
        obj["message"] = policyError.Message;
    }

    internal static void ApplyArchitectureApplicabilityCiFields(
        ArchitectureApplicabilityDiagnostic applicability,
        Dictionary<string, object?> obj)
    {
        obj["control_identity"] = applicability.ControlIdentity;
        obj["family"] = applicability.Family;
        obj["membership"] = applicability.Membership is { } membership
            ? ArchitectureApplicabilityWireNames.MembershipToken(membership)
            : null;
        obj["state"] = applicability.State is { } state
            ? ArchitectureApplicabilityWireNames.StateToken(state)
            : null;
        obj["validated_state"] = applicability.ValidatedState is { } validatedState
            ? ArchitectureApplicabilityWireNames.StateToken(validatedState)
            : null;
        obj["reason_code"] = applicability.ReasonCode;
        obj["policy_identity"] = applicability.PolicyIdentity;
        obj["provenance"] = new Dictionary<string, object?>
        {
            ["family"] = applicability.Provenance.Family,
            ["control_identity"] = applicability.Provenance.ControlIdentity,
            ["policy_identity"] = applicability.Provenance.PolicyIdentity,
        };
    }

    internal static void ApplyDependencyCiFields(DependencyDiagnostic dependency, Dictionary<string, object?> obj)
    {
        if (dependency.SourceLayer != null) obj["source_layer"] = dependency.SourceLayer;
        if (dependency.TargetLayer != null) obj["target_layer"] = dependency.TargetLayer;
        if (dependency.AllowedImporters != null) obj["allowed_importers"] = dependency.AllowedImporters.ToArray();
    }

    internal static void ApplyTypePlacementCiFields(TypePlacementDiagnostic typePlacement, Dictionary<string, object?> obj)
    {
        if (typePlacement.ExpectedTypeLocation != null) obj["expected_type_location"] = typePlacement.ExpectedTypeLocation;
        if (typePlacement.ActualTypeLocation != null) obj["actual_type_location"] = typePlacement.ActualTypeLocation;
        if (typePlacement.ExpectedTypeName != null) obj["expected_type_name"] = typePlacement.ExpectedTypeName;
        if (typePlacement.ActualTypeName != null) obj["actual_type_name"] = typePlacement.ActualTypeName;
    }

    internal static void ApplyAttributeUsageCiFields(AttributeUsageDiagnostic attributeUsage, Dictionary<string, object?> obj)
    {
        if (attributeUsage.MatchedAttribute != null) obj["matched_attribute"] = attributeUsage.MatchedAttribute;
        if (attributeUsage.AttributeUsageKind != null) obj["attribute_usage_kind"] = attributeUsage.AttributeUsageKind;
        if (attributeUsage.ExpectedAttributeLocation != null) obj["expected_attribute_location"] = attributeUsage.ExpectedAttributeLocation;
        if (attributeUsage.ActualAttributeLocation != null) obj["actual_attribute_location"] = attributeUsage.ActualAttributeLocation;
    }

    internal static void ApplyInheritanceCiFields(InheritanceDiagnostic inheritance, Dictionary<string, object?> obj)
    {
        if (inheritance.ForbiddenBaseType != null) obj["forbidden_base_type"] = inheritance.ForbiddenBaseType;
        if (inheritance.InheritanceSourceSurface != null) obj["source_surface"] = inheritance.InheritanceSourceSurface;
    }

    internal static void ApplyInterfaceImplementationCiFields(InterfaceImplementationDiagnostic diagnostic, Dictionary<string, object?> obj)
    {
        if (diagnostic.MatchedInterface != null) obj["matched_interface"] = diagnostic.MatchedInterface;
        if (diagnostic.ImplementationKind != null) obj["implementation_kind"] = diagnostic.ImplementationKind;
        if (diagnostic.ExpectedImplementationLocation != null) obj["expected_implementation_location"] = diagnostic.ExpectedImplementationLocation;
        if (diagnostic.ActualImplementationLocation != null) obj["actual_implementation_location"] = diagnostic.ActualImplementationLocation;
    }

    internal static void ApplyCompositionCiFields(CompositionDiagnostic diagnostic, Dictionary<string, object?> obj)
    {
        if (diagnostic.SourceMember != null) obj["source_member"] = diagnostic.SourceMember;
        if (diagnostic.MatchedForbiddenApi != null) obj["matched_forbidden_api"] = diagnostic.MatchedForbiddenApi;
        if (diagnostic.SourceAssembly != null) obj["source_assembly"] = diagnostic.SourceAssembly;
        if (diagnostic.ExpectedCompositionBoundary != null) obj["expected_composition_boundary"] = diagnostic.ExpectedCompositionBoundary;
    }

    internal static void ApplyProjectMetadataCiFields(ProjectMetadataDiagnostic diagnostic, Dictionary<string, object?> obj)
    {
        if (diagnostic.ProjectMetadataKind != null) obj["project_metadata_kind"] = diagnostic.ProjectMetadataKind;
        if (diagnostic.ProjectMetadataKey != null) obj["project_metadata_key"] = diagnostic.ProjectMetadataKey;
        if (diagnostic.ProjectMetadataExpectedValue != null) obj["project_metadata_expected_value"] = diagnostic.ProjectMetadataExpectedValue;
        if (diagnostic.ProjectMetadataActualValue != null) obj["project_metadata_actual_value"] = diagnostic.ProjectMetadataActualValue;
        if (diagnostic.ProjectMetadataSourcePath != null) obj["project_metadata_source_path"] = diagnostic.ProjectMetadataSourcePath;
    }

    internal static void ApplyConfigurationCiFields(ConfigurationDiagnostic diagnostic, Dictionary<string, object?> obj)
    {
        if (diagnostic.TemplateName != null) obj["template_name"] = diagnostic.TemplateName;
        if (diagnostic.ContainerNamespace != null) obj["container_namespace"] = diagnostic.ContainerNamespace;
        if (diagnostic.DependencyPaths != null) obj["dependency_paths"] = diagnostic.DependencyPaths.Select(path => path.ToArray()).ToArray();
    }
}
