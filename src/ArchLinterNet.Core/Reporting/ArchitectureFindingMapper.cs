using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

/// <summary>Builds the stable public finding envelope without inspecting display text.</summary>
public static class ArchitectureFindingMapper
{
    public static ArchitectureFinding FromViolation(ArchitectureViolation violation) =>
        FromViolation(violation, mode: null);

    public static ArchitectureFinding FromViolation(ArchitectureViolation violation, string? mode)
    {
        ArchitectureDiagnostic diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);
        ArchitectureViolationIdentity identity = violation.Identity ?? BuildIdentity(diagnostic);
        ArchitectureDiagnostic projected = violation.Identities.Count > 1
            ? ProjectDiagnosticForIdentity(diagnostic, identity, AttributedReference(violation, 0))
            : diagnostic;
        return Create(projected, identity, mode);
    }

    public static ArchitectureFinding FromDiagnostic(ArchitectureDiagnostic diagnostic) =>
        FromDiagnostic(diagnostic, mode: null);

    public static ArchitectureFinding FromDiagnostic(ArchitectureDiagnostic diagnostic, string? mode) =>
        Create(diagnostic, BuildIdentity(diagnostic), mode);

    /// <summary>Maps one Core applicability diagnostic through the existing finding envelope.</summary>
    public static ArchitectureFinding FromApplicabilityDiagnostic(
        ArchitectureApplicabilityDiagnostic diagnostic,
        string? mode = null) =>
        Create(diagnostic, BuildApplicabilityIdentity(diagnostic), mode);

    public static ArchitectureFinding FromBaseline(BaselineLifecycleEntry lifecycle)
    {
        ArchitectureBaselineComparisonEntry entry = lifecycle.Entry;
        string state = BaselineEntryLifecycleNames.WireName(lifecycle.Lifecycle);
        ArchitectureViolationIdentity identity = entry.Identity ?? BuildBaselineFallbackIdentity(entry);
        var diagnostic = new BaselineLifecycleDiagnostic(
            "baseline",
            entry.ContractId,
            entry.ContractGroup,
            entry.SourceType,
            entry.ForbiddenReference,
            entry.Reason,
            entry.Issue,
            lifecycle.Disposition,
            BaselineEntryLifecycleNames.Suppresses(lifecycle.Lifecycle),
            entry.Identity);
        return Create(diagnostic, identity, mode: null) with { BaselineState = state };
    }

    public static ArchitectureFinding FromPolicyError(
        string message,
        ArchitecturePolicyDiagnostic diagnostic,
        string? category = null)
    {
        var details = new ArchitecturePolicyErrorDiagnostic(
            message,
            diagnostic.Kind,
            category,
            diagnostic.ImportChain)
        {
            PolicyLocation = diagnostic.Location,
            RelatedPolicyLocations = diagnostic.RelatedLocations,
        };
        return FromDiagnostic(details);
    }

    /// <summary>
    /// Maps a batch without recomputing occurrence. Production violations carry identities assigned
    /// live by the execution context before ignore matching; aggregated legacy violations expand to
    /// one normalized finding per authoritative identity.
    /// </summary>
    public static IReadOnlyList<ArchitectureFinding> FromViolations(
        IEnumerable<ArchitectureViolation> violations,
        string? mode = null,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<ArchitectureFinding>();
        // Checked per violation AND per expanded identity — an aggregated legacy violation can
        // carry many Identities behind one ArchitectureViolation, and building each one's
        // ArchitectureFinding (ProjectDiagnosticForIdentity, Create) is real per-identity work, not
        // just the final serialization step callers loop over afterward.
        foreach (ArchitectureViolation violation in violations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArchitectureDiagnostic diagnostic = ArchitectureDiagnosticMapper.FromViolation(violation);
            IReadOnlyCollection<ArchitectureViolationIdentity> identities;
            if (violation.Identities.Count > 0)
            {
                identities = violation.Identities;
            }
            else if (violation.Identity is { } identity)
            {
                identities = new[] { identity };
            }
            else
            {
                identities = new[] { BuildIdentity(diagnostic) };
            }
            bool isAggregated = identities.Count > 1;
            int identityIndex = 0;
            foreach (ArchitectureViolationIdentity expandedIdentity in identities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                findings.Add(Create(
                    isAggregated
                        ? ProjectDiagnosticForIdentity(
                            diagnostic, expandedIdentity, AttributedReference(violation, identityIndex))
                        : diagnostic,
                    expandedIdentity,
                    mode));
                identityIndex++;
            }
        }

        return findings;
    }

    public static IReadOnlyList<ArchitectureFinding> Order(IEnumerable<ArchitectureFinding> findings) =>
        Order(findings, CancellationToken.None);

    // A single OrderBy(keySelector: identity, comparer) call with one comparer that replicates
    // every original ThenBy tiebreaker keeps LINQ's stable-sort guarantee (ties preserve source
    // order, so sequential/non-cancelled output is byte-for-byte unchanged) while making the whole
    // sort interruptible: the token is observed on every comparison, not just before/after the
    // call, so cancellation mid-sort of a large findings set stops before the remaining
    // comparisons instead of only being noticed once ToArray() has already finished. LINQ's sort
    // machinery wraps comparer exceptions in InvalidOperationException, so the comparer's
    // OperationCanceledException is unwrapped and rethrown as-is to preserve the cancellation
    // completion semantics the CLI and Testing API depend on.
    public static IReadOnlyList<ArchitectureFinding> Order(
        IEnumerable<ArchitectureFinding> findings, CancellationToken cancellationToken)
    {
        try
        {
            return findings.OrderBy(finding => finding, new FindingOrderComparer(cancellationToken)).ToArray();
        }
        catch (InvalidOperationException ex) when (ex.InnerException is OperationCanceledException)
        {
            throw ex.InnerException;
        }
    }

    private sealed class FindingOrderComparer : IComparer<ArchitectureFinding>
    {
        private readonly CancellationToken _cancellationToken;

        internal FindingOrderComparer(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public int Compare(ArchitectureFinding? x, ArchitectureFinding? y)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            int result = StringComparer.Ordinal.Compare(x!.ContractId ?? x.ContractName, y!.ContractId ?? y.ContractName);
            if (result != 0)
            {
                return result;
            }

            result = StringComparer.Ordinal.Compare(x.CanonicalIdentity, y.CanonicalIdentity);
            if (result != 0)
            {
                return result;
            }

            result = StringComparer.Ordinal.Compare(x.Kind, y.Kind);
            if (result != 0)
            {
                return result;
            }

            result = StringComparer.Ordinal.Compare(x.SourceLocation?.Path, y.SourceLocation?.Path);
            if (result != 0)
            {
                return result;
            }

            result = Comparer<int?>.Default.Compare(x.SourceLocation?.Line, y.SourceLocation?.Line);
            return result != 0 ? result : Comparer<int?>.Default.Compare(x.SourceLocation?.Column, y.SourceLocation?.Column);
        }
    }

    public static string KindToken(ArchitectureDiagnosticKind kind) => kind switch
    {
        ArchitectureDiagnosticKind.ExternalDependency => "external_dependency",
        ArchitectureDiagnosticKind.PackageDependency => "package_dependency",
        ArchitectureDiagnosticKind.TypePlacement => "type_placement",
        ArchitectureDiagnosticKind.PublicApiSurface => "public_api_surface",
        ArchitectureDiagnosticKind.AttributeUsage => "attribute_usage",
        ArchitectureDiagnosticKind.InterfaceImplementation => "interface_implementation",
        ArchitectureDiagnosticKind.ProjectMetadata => "project_metadata",
        ArchitectureDiagnosticKind.ContextDependency => "context_dependency",
        ArchitectureDiagnosticKind.ContextAllowOnly => "context_allow_only",
        ArchitectureDiagnosticKind.PortBoundary => "port_boundary",
        ArchitectureDiagnosticKind.LayoutConvention => "layout_convention",
        ArchitectureDiagnosticKind.PackageAllowOnly => "package_allow_only",
        ArchitectureDiagnosticKind.FrameworkReference => "framework_reference",
        ArchitectureDiagnosticKind.FrameworkReferenceAllowOnly => "framework_reference_allow_only",
        ArchitectureDiagnosticKind.BuildStatePreflight => "build_state_preflight",
        ArchitectureDiagnosticKind.PolicyConsistency => "policy_consistency",
        ArchitectureDiagnosticKind.UnmatchedIgnore => "unmatched_ignore",
        ArchitectureDiagnosticKind.Baseline => "baseline",
        ArchitectureDiagnosticKind.ArchitecturePolicyError => "architecture_policy_error",
        ArchitectureDiagnosticKind.Applicability => "applicability",
        _ => kind.ToString().ToLowerInvariant()
    };

    private static ArchitectureFinding Create(
        ArchitectureDiagnostic diagnostic,
        ArchitectureViolationIdentity identity,
        string? mode) =>
        new(
            ArchitectureFinding.CurrentSchemaVersion,
            KindToken(diagnostic.Kind),
            diagnostic.ContractName,
            diagnostic.ContractId,
            ArchitectureViolationIdentityJson.Serialize(identity),
            diagnostic)
        {
            Identity = identity,
            Mode = mode,
            Severity = SeverityFor(mode),
            MessageCode = KindToken(diagnostic.Kind),
            PolicyOrigin = diagnostic.PolicyLocation,
            RelatedPolicyOrigins = diagnostic.RelatedPolicyLocations,
            SourceLocation = SourceLocationOf(diagnostic),
            RemediationHint = ArchitectureRemediationHintFactory.Create(diagnostic, identity),
        };

    private static string? SeverityFor(string? mode)
    {
        if (mode is null)
        {
            return null;
        }

        return mode == "strict" ? "error" : "warning";
    }

    private static ArchitectureFindingSourceLocation? SourceLocationOf(ArchitectureDiagnostic diagnostic) => diagnostic switch
    {
        LayoutConventionDiagnostic { MatchedFilePath: { } path } => new ArchitectureFindingSourceLocation(path),
        FrameworkReferenceDiagnostic { Evidence: { Count: > 0 } evidence } =>
            new ArchitectureFindingSourceLocation(evidence.First().SourcePath),
        FrameworkReferenceAllowOnlyDiagnostic { Evidence: { Count: > 0 } evidence } =>
            new ArchitectureFindingSourceLocation(evidence.First().SourcePath),
        ProjectMetadataDiagnostic { ProjectMetadataSourcePath: { } path } => new ArchitectureFindingSourceLocation(path),
        BuildStatePreflightDiagnostic { Evidence.ProjectPath: var path } => new ArchitectureFindingSourceLocation(path),
        _ => null,
    };

    private static ArchitectureViolationIdentity BuildIdentity(ArchitectureDiagnostic diagnostic)
    {
        if (diagnostic is ArchitectureApplicabilityDiagnostic applicability)
        {
            return BuildApplicabilityIdentity(applicability);
        }

        // PolicyConsistencyDiagnostic is special-cased ahead of the generic IdentityParts/
        // SourceTypeOf switches below so PolicyConsistencyDistinguisher (an OrderBy+Join) runs once
        // per diagnostic instead of once per field.
        if (diagnostic is PolicyConsistencyDiagnostic policy)
        {
            string distinguisher = PolicyConsistencyDistinguisher(policy);
            return new ArchitectureViolationIdentity(
                ArchitectureViolationIdentity.CurrentVersion,
                KindToken(diagnostic.Kind),
                ArchitectureViolationIdentity.ResolveKind(KindToken(diagnostic.Kind)),
                diagnostic.ContractId ?? diagnostic.ContractName,
                null,
                distinguisher,
                null,
                null,
                null,
                distinguisher,
                0,
                policy.CheckKind);
        }

        (string? sourceAssembly, string? sourceMember, string? targetAssembly, string? targetType, string? targetMember,
            string? configuration) = IdentityParts(diagnostic);
        return new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion,
            KindToken(diagnostic.Kind),
            ArchitectureViolationIdentity.ResolveKind(KindToken(diagnostic.Kind)),
            diagnostic.ContractId ?? diagnostic.ContractName,
            sourceAssembly,
            SourceTypeOf(diagnostic),
            sourceMember,
            targetAssembly,
            targetType,
            targetMember,
            0,
            configuration);
    }

    private static ArchitectureViolationIdentity BuildApplicabilityIdentity(
        ArchitectureApplicabilityDiagnostic diagnostic)
    {
        // Applicability has no source/target dependency occurrence.  Existing structured identity
        // slots are used as labeled semantic dimensions: source_type is the canonical control,
        // source_member is the family, target_member is the reason, and configuration is the
        // policy identity.  None of these values are formatted display text.
        return new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion,
            diagnostic.Family,
            KindToken(diagnostic.Kind),
            diagnostic.ContractId ?? diagnostic.ContractName,
            null,
            diagnostic.ControlIdentity,
            diagnostic.Family,
            null,
            null,
            diagnostic.ReasonCode,
            0,
            diagnostic.PolicyIdentity);
    }

    private static ArchitectureViolationIdentity BuildBaselineFallbackIdentity(ArchitectureBaselineComparisonEntry entry)
    {
        string family = ArchitectureViolationIdentity.ResolveContractFamily(entry.ContractGroup);
        return new ArchitectureViolationIdentity(
            ArchitectureViolationIdentity.CurrentVersion,
            family,
            ArchitectureViolationIdentity.ResolveKind(family),
            entry.ContractId,
            null,
            entry.SourceType,
            null,
            null,
            null,
            entry.ForbiddenReference,
            0);
    }

    private static (string? SourceAssembly, string? SourceMember, string? TargetAssembly, string? TargetType,
        string? TargetMember, string? Configuration) IdentityParts(ArchitectureDiagnostic diagnostic)
    {
        return diagnostic switch
        {
            CompositionDiagnostic composition => (
                composition.SourceAssembly, composition.SourceMember, null, null,
                composition.MatchedForbiddenApi ?? string.Join("|", composition.ForbiddenReferences),
                composition.ExpectedCompositionBoundary),
            FrameworkReferenceDiagnostic framework => (
                null, null, null, null, string.Join("|", framework.ForbiddenReferences), framework.ForbiddenFrameworkGroup),
            FrameworkReferenceAllowOnlyDiagnostic framework => (
                null, null, null, null, string.Join("|", framework.ForbiddenReferences),
                string.Join("|", framework.AllowedFrameworkGroups)),
            PackageDependencyDiagnostic package => (
                null, null, null, null, string.Join("|", package.ForbiddenReferences), package.ForbiddenPackageGroup),
            PackageAllowOnlyDiagnostic package => (
                null, null, null, null, string.Join("|", package.ForbiddenReferences),
                string.Join("|", package.AllowedPackageGroups)),
            CycleDiagnostic cycle => (null, null, null, null, cycle.Path, null),
            BuildStatePreflightDiagnostic preflight =>
                (null, null, null, null, preflight.Evidence.ProjectPath, preflight.State.ToString()),
            UnmatchedIgnoreDiagnostic unmatched =>
                (null, null, null, null, unmatched.ForbiddenReference, unmatched.IgnoreIndex.ToString()),
            // PolicyConsistencyDiagnostic is handled directly in BuildIdentity, ahead of this switch.
            BaselineLifecycleDiagnostic baseline =>
                (null, null, null, null, baseline.ForbiddenReference, baseline.ContractGroup),
            ArchitecturePolicyErrorDiagnostic policyError =>
                (null, PolicyErrorImportPosition(policyError), null, null,
                    policyError.PolicyLocation?.YamlPath ?? policyError.DiagnosticKind.ToString(),
                    PolicyErrorConfiguration(policyError)),
            _ => (null, null, null, null, SourceIdentifier(diagnostic), diagnostic.Kind.ToString()),
        };
    }

    // Returns the reference identity attachment paired with the identity at <paramref name="index"/>,
    // or null when this violation carries no pairing.
    private static string? AttributedReference(ArchitectureViolation violation, int index)
    {
        IReadOnlyList<string> attributed = violation.IdentityReferences;
        return attributed.Count == violation.Identities.Count && index < attributed.Count
            ? attributed[index]
            : null;
    }

    private static ArchitectureDiagnostic ProjectDiagnosticForIdentity(
        ArchitectureDiagnostic diagnostic,
        ArchitectureViolationIdentity identity,
        string? attributedReference)
    {
        return diagnostic switch
        {
            DependencyDiagnostic dependency =>
                dependency with { ForbiddenReferences = ReferencesForIdentity(dependency.ForbiddenReferences, identity, attributedReference) },
            ExternalDependencyDiagnostic external =>
                external with { ForbiddenReferences = ReferencesForIdentity(external.ForbiddenReferences, identity, attributedReference) },
            PackageDependencyDiagnostic package =>
                package with { ForbiddenReferences = ReferencesForIdentity(package.ForbiddenReferences, identity, attributedReference) },
            PackageAllowOnlyDiagnostic package =>
                package with { ForbiddenReferences = ReferencesForIdentity(package.ForbiddenReferences, identity, attributedReference) },
            FrameworkReferenceDiagnostic framework => framework with
            {
                ForbiddenReferences = ReferencesForIdentity(framework.ForbiddenReferences, identity, attributedReference),
                Evidence = FrameworkEvidenceForIdentity(
                    framework.Evidence,
                    framework.ForbiddenReferences,
                    identity),
            },
            FrameworkReferenceAllowOnlyDiagnostic framework => framework with
            {
                ForbiddenReferences = ReferencesForIdentity(framework.ForbiddenReferences, identity, attributedReference),
                Evidence = FrameworkEvidenceForIdentity(
                    framework.Evidence,
                    framework.ForbiddenReferences,
                    identity),
            },
            CompositionDiagnostic composition =>
                composition with { ForbiddenReferences = ReferencesForIdentity(composition.ForbiddenReferences, identity, attributedReference) },
            _ => diagnostic,
        };
    }

    private static IReadOnlyCollection<string> ReferencesForIdentity(
        IReadOnlyCollection<string> references,
        ArchitectureViolationIdentity identity,
        string? attributedReference)
    {
        // Identity attachment already paired this identity with the reference it was selected for.
        // That pairing is authoritative: it is the only thing that separates two occurrences whose
        // identities differ solely by Occurrence and whose displays differ solely by IL offset.
        if (attributedReference is not null)
        {
            return new[] { attributedReference };
        }

        if (identity.TargetMember is not { Length: > 0 } targetMember)
        {
            return references;
        }

        // No pairing (a violation built outside identity attachment): fall back to matching the
        // display text. This still attributes the common shapes, but cannot split occurrences that
        // share a (source member, target member) pair — they keep every reference of that pair.
        string[] selected = references
            .Where(reference => ReferenceMatchesIdentity(reference, targetMember)
                || ReferenceMatchesSourceQualifiedIdentity(reference, identity.SourceMember, targetMember))
            .ToArray();
        return selected.Length == 0 ? references : selected;
    }

    private static IReadOnlyCollection<FrameworkReferenceEvidence> FrameworkEvidenceForIdentity(
        IReadOnlyCollection<FrameworkReferenceEvidence> evidence,
        IReadOnlyCollection<string> references,
        ArchitectureViolationIdentity identity)
    {
        if (identity.TargetMember is not { Length: > 0 } targetMember)
        {
            return evidence;
        }

        FrameworkReferenceEvidence[] selected = evidence
            .Where(item =>
                string.Equals(item.FrameworkName, targetMember, StringComparison.Ordinal)
                || string.Equals(
                    $"{item.FrameworkName} ({item.TargetFramework})",
                    targetMember,
                    StringComparison.Ordinal))
            .ToArray();
        return references.Any(reference => ReferenceMatchesIdentity(reference, targetMember))
            ? selected
            : evidence;
    }

    private static bool ReferenceMatchesIdentity(string reference, string targetMember) =>
        reference.Equals(targetMember, StringComparison.Ordinal)
        || reference.StartsWith(targetMember + "@", StringComparison.Ordinal)
        || reference.StartsWith(targetMember + " ", StringComparison.Ordinal);

    // The method-body families report one reference per (source member, target member) occurrence
    // and build its display from exactly those two parts, with the target member at the *end*:
    //
    //   "<source member>: <target member>"                       — external dependency IL scan
    //   "il <offset> (<source member>): <pattern> -> <target>"    — forbidden-call IL scan
    //
    // ReferenceMatchesIdentity above only anchors on the start of the reference, so it never
    // attributed those, and every identity of such a violation fell back to the violation's whole
    // reference list. A violation with N occurrences then serialized N x N references — on a broad
    // `audit_external` group that turned 17k real references into 2.4M and exhausted memory before
    // the report could be written (issue #419). Attributing them also fixes the content itself: a
    // finding no longer claims the references that belong to its siblings.
    private static bool ReferenceMatchesSourceQualifiedIdentity(
        string reference,
        string? sourceMember,
        string targetMember)
    {
        if (sourceMember is not { Length: > 0 })
        {
            return false;
        }

        // The target member terminates the display and is always preceded by a space (": " or "-> ").
        if (reference.Length <= targetMember.Length
            || reference[reference.Length - targetMember.Length - 1] != ' '
            || !reference.EndsWith(targetMember, StringComparison.Ordinal))
        {
            return false;
        }

        // The source member is named verbatim, closed by ':' or wrapped in parentheses. Anchoring on
        // that delimiter stops a member whose name is a prefix of another's (Convert vs ConvertNode)
        // from claiming the other's reference.
        int index = reference.IndexOf(sourceMember, StringComparison.Ordinal);
        while (index >= 0)
        {
            int after = index + sourceMember.Length;
            if (after < reference.Length
                && (reference[after] == ':'
                    || (reference[after] == ')' && index > 0 && reference[index - 1] == '(')))
            {
                return true;
            }

            index = reference.IndexOf(sourceMember, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static string? PolicyErrorImportPosition(ArchitecturePolicyErrorDiagnostic policyError) =>
        policyError.ImportChain.Count == 0
            ? null
            : $"{policyError.ImportChain.Count - 1}:{policyError.ImportChain[^1]}";

    private static string PolicyErrorConfiguration(ArchitecturePolicyErrorDiagnostic policyError)
    {
        string importChain = string.Join(" -> ", policyError.ImportChain);
        return $"kind={policyError.DiagnosticKind};category={policyError.ErrorCategory ?? "<none>"};"
            + $"import_chain={importChain}";
    }

    private static string SourceTypeOf(ArchitectureDiagnostic diagnostic) => diagnostic switch
    {
        CycleDiagnostic cycle => cycle.Path,
        BuildStatePreflightDiagnostic preflight => preflight.Evidence.ProjectPath,
        UnmatchedIgnoreDiagnostic unmatched => unmatched.SourceType,
        // PolicyConsistencyDiagnostic is handled directly in BuildIdentity, ahead of this switch.
        BaselineLifecycleDiagnostic baseline => baseline.SourceType,
        ArchitecturePolicyErrorDiagnostic policyError => policyError.PolicyLocation?.SourcePath ?? "<policy>",
        ArchitectureApplicabilityDiagnostic applicability => applicability.ControlIdentity,
        _ => SourceIdentifier(diagnostic),
    };

    private static string SourceIdentifier(ArchitectureDiagnostic diagnostic) => diagnostic switch
    {
        DependencyDiagnostic d => d.SourceType,
        ConfigurationDiagnostic d => d.SourceType,
        ExternalDependencyDiagnostic d => d.SourceType,
        PackageDependencyDiagnostic d => d.SourceType,
        PackageAllowOnlyDiagnostic d => d.SourceType,
        FrameworkReferenceDiagnostic d => d.SourceType,
        FrameworkReferenceAllowOnlyDiagnostic d => d.SourceType,
        TypePlacementDiagnostic d => d.SourceType,
        LayoutConventionDiagnostic d => d.SourceType,
        PublicApiSurfaceDiagnostic d => d.SourceType,
        AttributeUsageDiagnostic d => d.SourceType,
        InheritanceDiagnostic d => d.SourceType,
        InterfaceImplementationDiagnostic d => d.SourceType,
        CompositionDiagnostic d => $"{d.SourceAssembly}:{d.SourceType}:{d.SourceMember}",
        ProjectMetadataDiagnostic d => d.SourceType,
        ContextDependencyDiagnostic d => d.SourceType,
        ContextAllowOnlyDiagnostic d => d.SourceType,
        PortBoundaryDiagnostic d => d.SourceType,
        CycleDiagnostic d => d.Path,
        UnmatchedIgnoreDiagnostic d => $"{d.SourceType}:{d.IgnoreIndex}:{d.ForbiddenReference}",
        // PolicyConsistencyDiagnostic is handled directly in BuildIdentity, ahead of SourceTypeOf.
        BuildStatePreflightDiagnostic d => $"{d.State}:{d.Evidence.ProjectPath}",
        BaselineLifecycleDiagnostic d => d.SourceType,
        ArchitecturePolicyErrorDiagnostic d => d.PolicyLocation?.SourcePath ?? "<policy>",
        ArchitectureApplicabilityDiagnostic d => d.ControlIdentity,
        _ => string.Empty,
    };

    // Only "layer-overlap" sets RepresentativeType. Every other check kind (duplicate-id,
    // allow-forbid-conflict, independence-conflict, unreachable-contract, unmatched-layer-exclusion)
    // reports one finding per occurrence but shares the same ContractName/ContractId and CheckKind
    // across occurrences, so falling back to CheckKind alone collapses every occurrence of a check
    // kind under one contract into a single identity. Layers and ConflictingContractIds/Names are
    // exactly the fields each check populates to describe *which* occurrence this is; folding them
    // in keeps distinct occurrences from colliding into one identity.
    //
    // Ids and Names are kept as separate labeled segments, not one merged/either-or set: two
    // independence-conflict findings against contracts that share a duplicate id (a policy state
    // FindDuplicateContractIds explicitly anticipates) have identical ConflictingContractIds but
    // distinct ConflictingContractNames — discarding Names whenever an id is present (as an
    // either-or choice would) collapses that case (#686 PR review round 2).
    //
    // PolicyLocation.YamlPath is used only when layers/ids/names are ALL empty — it is a true last
    // resort, not an always-appended tiebreaker. ArchitecturePolicyProvenanceIndex.Enrich attaches
    // a PolicyLocation to essentially every policy-consistency diagnostic (derived from the
    // participating contract's position in its declaring YAML list), so unconditionally folding it
    // in would make identity depend on list position for every check kind: reordering an unrelated
    // contract earlier in `contracts.strict_independence` (etc.) would change an otherwise-unrelated
    // finding's identity even though layers/ids/names — its actual semantic content — are unchanged
    // (#686 PR review round 3). No currently-reachable check kind leaves layers/ids/names all empty
    // (unmatched-layer-exclusion, the one case that used to, now sets RepresentativeType instead),
    // so this fallback is defensive for future check kinds, not something today's callers rely on.
    private static string PolicyConsistencyDistinguisher(PolicyConsistencyDiagnostic policy)
    {
        if (policy.RepresentativeType is { Length: > 0 } representativeType)
        {
            return representativeType;
        }

        string layers = Sorted(policy.Layers);
        string ids = Sorted(policy.ConflictingContractIds);
        string names = Sorted(policy.ConflictingContractNames);
        if (layers.Length > 0 || ids.Length > 0 || names.Length > 0)
        {
            return $"layers:{layers}|ids:{ids}|names:{names}";
        }

        return policy.PolicyLocation?.YamlPath is { Length: > 0 } yamlPath ? yamlPath : policy.CheckKind;
    }

    private static string Sorted(IEnumerable<string> values) =>
        string.Join(",", values.OrderBy(static value => value, StringComparer.Ordinal));
}
