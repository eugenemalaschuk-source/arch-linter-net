using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureDiagnosticFormatter
{
    // Dispatches to the family-owned projector registered in DiagnosticDetailProjectionRegistry
    // (see ArchitectureDiagnosticFormatter.DetailProjectionRegistry.cs) instead of a central switch
    // enumerating every diagnostic kind - see #453. The throw is unreachable for any of the 24
    // supported diagnostic kinds today; it exists as defense in depth so a future diagnostic type
    // added without a registry entry fails loudly at runtime, not just in the completeness test.
    private static void ApplyDiagnosticSpecificCiFields(ArchitectureDiagnostic diagnostic, Dictionary<string, object?> obj)
    {
        if (!DiagnosticDetailProjectionRegistry.ByType.TryGetValue(diagnostic.GetType(), out DiagnosticDetailProjector? projector))
        {
            throw new InvalidOperationException(
                $"No diagnostic detail projector registered for diagnostic type '{diagnostic.GetType().Name}'.");
        }

        projector(diagnostic, obj);
    }

    private static void ApplyExternalDependencyCiFields(ExternalDependencyDiagnostic external, Dictionary<string, object?> obj)
    {
        obj["forbidden_external_group"] = external.ForbiddenExternalGroup;
    }

    private static void ApplyPackageDependencyCiFields(PackageDependencyDiagnostic package, Dictionary<string, object?> obj)
    {
        obj["forbidden_package_group"] = package.ForbiddenPackageGroup;
    }

    private static void ApplyPackageAllowOnlyCiFields(PackageAllowOnlyDiagnostic package, Dictionary<string, object?> obj)
    {
        obj["allowed_package_groups"] = package.AllowedPackageGroups.ToArray();
    }

    private static void ApplyMetricBudgetCiFields(MetricBudgetDiagnostic budget, Dictionary<string, object?> obj)
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

    private static void ApplyContractSurfaceExposureCiFields(
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

    private static void ApplyCycleCiFields(CycleDiagnostic cycle, Dictionary<string, object?> obj)
    {
        obj["path"] = cycle.Path;
    }

    private static void ApplyUnmatchedIgnoreCiFields(UnmatchedIgnoreDiagnostic unmatched, Dictionary<string, object?> obj)
    {
        obj["ignore_index"] = unmatched.IgnoreIndex;
        obj["source_type"] = unmatched.SourceType;
        obj["forbidden_reference"] = unmatched.ForbiddenReference;
        obj["reason"] = unmatched.Reason;
    }

    private static void ApplyPolicyConsistencyCiFields(PolicyConsistencyDiagnostic policy, Dictionary<string, object?> obj)
    {
        obj["check_kind"] = policy.CheckKind;
        obj["reason"] = policy.Reason;
        obj["conflicting_contract_ids"] = policy.ConflictingContractIds.ToArray();
        obj["conflicting_contract_names"] = policy.ConflictingContractNames.ToArray();
        obj["layers"] = policy.Layers.ToArray();
        obj["representative_type"] = policy.RepresentativeType;
    }

    private static void ApplyBaselineLifecycleCiFields(BaselineLifecycleDiagnostic baseline, Dictionary<string, object?> obj)
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

    private static void ApplyArchitecturePolicyErrorCiFields(ArchitecturePolicyErrorDiagnostic policyError, Dictionary<string, object?> obj)
    {
        obj["diagnostic_kind"] = policyError.DiagnosticKind.ToString().ToLowerInvariant();
        obj["error_category"] = policyError.ErrorCategory;
        obj["import_chain"] = policyError.ImportChain;
        obj["message"] = policyError.Message;
    }

    private static void ApplyArchitectureApplicabilityCiFields(
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
}
