using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Scanning;

internal sealed class CanonicalAnnotationRoleResolver
{
    private readonly Dictionary<Assembly, CatalogIdentityValidation> _identityByAssembly = new();

    public ArchitectureAttributeClassificationCandidate Resolve(
        IReadOnlyList<CustomAttributeData> attributeData,
        Assembly assembly,
        ArchitectureClassificationSource source,
        string scope,
        string subject)
    {
        if (attributeData is ICollection<CustomAttributeData> { Count: 0 })
        {
            return ArchitectureAttributeClassificationCandidate.Empty;
        }

        CanonicalAnnotationCatalog catalog = CanonicalAnnotationCatalog.Current;
        ResolutionEvidence evidence = CollectEvidence(attributeData, catalog, source, scope, subject);
        if (!evidence.CanonicalEvidenceSeen)
        {
            return CreateNoCanonicalEvidenceCandidate(evidence);
        }

        CatalogIdentityValidation identity = GetIdentityValidation(assembly, catalog);
        AddIdentityDiagnostic(identity, scope, subject, evidence);
        if (!identity.IsCompatible)
        {
            return CreateUnassignedCandidate(evidence);
        }

        (Dictionary<string, object> resolvedContextMetadata, IReadOnlySet<string> blockedContextMetadataKeys) =
            ResolveContextMetadata(
                evidence.ContextEntries,
                evidence.InvalidContextMetadataKeys,
                scope,
                subject,
                evidence.Diagnostics);
        if (evidence.Matches.Count == 0)
        {
            return CreateContextOnlyCandidate(evidence, resolvedContextMetadata, blockedContextMetadataKeys);
        }

        return ResolveRoleMatches(evidence, resolvedContextMetadata, blockedContextMetadataKeys, scope, subject);
    }

    private static ResolutionEvidence CollectEvidence(
        IReadOnlyList<CustomAttributeData> attributeData,
        CanonicalAnnotationCatalog catalog,
        ArchitectureClassificationSource source,
        string scope,
        string subject)
    {
        ResolutionEvidence evidence = new(source, subject);
        foreach (CustomAttributeData data in attributeData)
        {
            ProcessAttribute(data, catalog, source, scope, subject, evidence);
        }

        return evidence;
    }

    private static void ProcessAttribute(
        CustomAttributeData data,
        CanonicalAnnotationCatalog catalog,
        ArchitectureClassificationSource source,
        string scope,
        string subject,
        ResolutionEvidence evidence)
    {
        string? attributeName = TryGetAttributeName(data);
        if (attributeName is null)
        {
            return;
        }

        if (catalog.IsReservedAttribute(attributeName) && !catalog.IsKnownAttribute(attributeName))
        {
            evidence.Diagnostics.Add(ArchitectureCanonicalAnnotationDiagnostic.Create(
                subject,
                "UnknownReservedAnnotation",
                $"'{attributeName}' is in the reserved '{catalog.ReservedNamespace}' namespace but is not present in the semantic annotation catalog.",
                attributeName));
            return;
        }

        if (catalog.TryGetRole(attributeName, out CanonicalAnnotationCatalog.CanonicalRole role))
        {
            ProcessRoleAttribute(data, role, catalog, source, scope, subject, evidence);
            return;
        }

        if (catalog.TryGetContext(attributeName, out CanonicalAnnotationCatalog.CanonicalContext context))
        {
            ProcessContextAttribute(data, context, scope, subject, evidence);
        }
    }

    private static void ProcessRoleAttribute(
        CustomAttributeData data,
        CanonicalAnnotationCatalog.CanonicalRole role,
        CanonicalAnnotationCatalog catalog,
        ArchitectureClassificationSource source,
        string scope,
        string subject,
        ResolutionEvidence evidence)
    {
        if (!role.Scopes.Contains(scope, StringComparer.Ordinal))
        {
            evidence.Diagnostics.Add(ArchitectureCanonicalAnnotationDiagnostic.Create(
                subject,
                "AnnotationScopeMismatch",
                $"'{role.AttributeFullName}' is catalogued for {string.Join(" and ", role.Scopes)} scope, not {scope} scope.",
                role.AttributeFullName));
            return;
        }

        evidence.CanonicalEvidenceSeen = true;
        evidence.CanonicalEvidenceNames.Add(role.AttributeFullName);
        IReadOnlyDictionary<string, object> metadata = ReadMetadata(
            data, catalog, source, subject, evidence.MetadataFailures, evidence.Diagnostics);
        evidence.Matches.Add((role, metadata));
    }

    private static void ProcessContextAttribute(
        CustomAttributeData data,
        CanonicalAnnotationCatalog.CanonicalContext context,
        string scope,
        string subject,
        ResolutionEvidence evidence)
    {
        if (!context.Scopes.Contains(scope, StringComparer.Ordinal))
        {
            evidence.Diagnostics.Add(ArchitectureCanonicalAnnotationDiagnostic.Create(
                subject,
                "AnnotationScopeMismatch",
                $"'{context.AttributeFullName}' is catalogued for {string.Join(" and ", context.Scopes)} scope, not {scope} scope.",
                context.AttributeFullName));
            return;
        }

        evidence.CanonicalEvidenceSeen = true;
        evidence.CanonicalEvidenceNames.Add(context.AttributeFullName);
        if (TryReadContextValue(data, context, evidence.ContextValueReadContext, out string value))
        {
            evidence.ContextEntries.Add(new ContextEvidence(context.Key, context.AttributeFullName, value));
        }
    }

    private static ArchitectureAttributeClassificationCandidate CreateNoCanonicalEvidenceCandidate(
        ResolutionEvidence evidence)
    {
        if (evidence.Diagnostics.Count == 0 && evidence.MetadataFailures.Count == 0)
        {
            return ArchitectureAttributeClassificationCandidate.Empty;
        }

        return CreateUnassignedCandidate(evidence);
    }

    private static ArchitectureAttributeClassificationCandidate CreateUnassignedCandidate(ResolutionEvidence evidence)
    {
        return new ArchitectureAttributeClassificationCandidate(null, new Dictionary<string, object>(), null,
            Array.Empty<ArchitectureClassificationConflict>(), evidence.MetadataFailures)
        {
            CanonicalAnnotationDiagnostics = evidence.Diagnostics,
        };
    }

    private static void AddIdentityDiagnostic(
        CatalogIdentityValidation identity,
        string scope,
        string subject,
        ResolutionEvidence evidence)
    {
        if (identity.Diagnostic is null)
        {
            return;
        }

        string subjectKind = string.Equals(scope, "assembly", StringComparison.Ordinal) ? "assembly" : "type";
        string[] annotationEvidence = evidence.CanonicalEvidenceNames
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        evidence.Diagnostics.Add(identity.Diagnostic with
        {
            Subject = subject,
            Message = $"{identity.Diagnostic.Message} The affected {subjectKind} is '{subject}' and carries canonical annotation(s): "
                + $"{string.Join(", ", annotationEvidence)}.",
            EvidenceSources = identity.Diagnostic.EvidenceSources.Concat(annotationEvidence)
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
        });
    }

    private static ArchitectureAttributeClassificationCandidate CreateContextOnlyCandidate(
        ResolutionEvidence evidence,
        Dictionary<string, object> resolvedContextMetadata,
        IReadOnlySet<string> blockedContextMetadataKeys)
    {
        return new ArchitectureAttributeClassificationCandidate(null, new Dictionary<string, object>(), null,
            Array.Empty<ArchitectureClassificationConflict>(), evidence.MetadataFailures)
        {
            ContextMetadata = resolvedContextMetadata,
            ContextMetadataBlockedKeys = blockedContextMetadataKeys,
            CanonicalAnnotationDiagnostics = evidence.Diagnostics,
        };
    }

    private static ArchitectureAttributeClassificationCandidate ResolveRoleMatches(
        ResolutionEvidence evidence,
        Dictionary<string, object> resolvedContextMetadata,
        IReadOnlySet<string> blockedContextMetadataKeys,
        string scope,
        string subject)
    {
        string[] evidenceSources = evidence.Matches.Select(match => match.Role.AttributeFullName)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        (CanonicalAnnotationCatalog.CanonicalRole Role, IReadOnlyDictionary<string, object> Metadata) first =
            evidence.Matches[0];
        bool coherent = evidence.Matches.All(match =>
            string.Equals(match.Role.Name, first.Role.Name, StringComparison.Ordinal)
            && MetadataEqual(match.Metadata, first.Metadata));
        if (!coherent)
        {
            AddRoleConflict(evidence, evidenceSources, scope, subject);
            return new ArchitectureAttributeClassificationCandidate(
                null, new Dictionary<string, object>(), null,
                Array.Empty<ArchitectureClassificationConflict>(), evidence.MetadataFailures)
            {
                EvidenceSources = evidenceSources,
                CanonicalAnnotationDiagnostics = evidence.Diagnostics,
                ContextMetadata = resolvedContextMetadata,
                ContextMetadataBlockedKeys = blockedContextMetadataKeys,
                Blocked = true,
            };
        }

        return new ArchitectureAttributeClassificationCandidate(
            first.Role.Name,
            first.Metadata,
            evidenceSources.FirstOrDefault(),
            Array.Empty<ArchitectureClassificationConflict>(),
            evidence.MetadataFailures)
        {
            EvidenceSources = evidenceSources,
            ObservedEvidence = evidence.Matches.Select(match => new ArchitectureAttributeRoleEvidence(
                match.Role.Name, match.Metadata, match.Role.AttributeFullName)).ToArray(),
            CanonicalAnnotationDiagnostics = evidence.Diagnostics,
            ContextMetadata = resolvedContextMetadata,
            ContextMetadataBlockedKeys = blockedContextMetadataKeys,
        };
    }

    private static void AddRoleConflict(
        ResolutionEvidence evidence,
        string[] evidenceSources,
        string scope,
        string subject)
    {
        string details = string.Join(
            "; ",
            evidence.Matches.Select(match =>
                    $"{match.Role.AttributeFullName} => {match.Role.Name} ({FormatMetadata(match.Metadata)})")
                .OrderBy(value => value, StringComparer.Ordinal));
        evidence.Diagnostics.Add(ArchitectureCanonicalAnnotationDiagnostic.Create(
            subject,
            "CanonicalRoleConflict",
            $"Canonical annotations at {scope} specificity disagree: {details}. No role is assigned at this specificity.",
            evidenceSources));
    }

    private CatalogIdentityValidation GetIdentityValidation(Assembly assembly, CanonicalAnnotationCatalog catalog)
    {
        if (_identityByAssembly.TryGetValue(assembly, out CatalogIdentityValidation? cached))
        {
            return cached;
        }

        string subject = SafeAssemblyName(assembly);
        IReadOnlyList<CustomAttributeData> attributes = GetAssemblyAttributes(assembly);
        CustomAttributeData[] markers = attributes
            .Where(data => string.Equals(TryGetAttributeName(data), catalog.CatalogIdentityMarker, StringComparison.Ordinal))
            .ToArray();

        CatalogIdentityValidation result;
        if (markers.Length == 0)
        {
            result = Invalid("MissingCatalogIdentity", $"Assembly '{subject}' uses a canonical annotation but has no '{catalog.CatalogIdentityMarker}' marker.");
        }
        else if (markers.Length != 1)
        {
            result = Invalid("MultipleCatalogIdentityMarkers", $"Assembly '{subject}' uses a canonical annotation but has {markers.Length} catalog identity markers; exactly one is required.");
        }
        else if (markers[0].ConstructorArguments.Count != 2
            || markers[0].ConstructorArguments[0].ArgumentType != typeof(int)
            || markers[0].ConstructorArguments[0].Value is not int generation
            || markers[0].ConstructorArguments[1].ArgumentType != typeof(string)
            || markers[0].ConstructorArguments[1].Value is not string packageVersion)
        {
            result = Invalid("InvalidCatalogIdentity", $"Assembly '{subject}' has a malformed canonical annotation catalog identity marker.");
        }
        else if (generation != catalog.Generation)
        {
            result = Invalid("UnsupportedCatalogGeneration", $"Assembly '{subject}' uses catalog generation {generation}; this tool supports generation {catalog.Generation}.");
        }
        else if (!catalog.IsSupportedPackageVersion(packageVersion))
        {
            result = Invalid("UnsupportedAnnotationPackageVersion", $"Assembly '{subject}' uses annotation package version '{packageVersion}', outside supported range {catalog.SupportedPackageRange}.");
        }
        else
        {
            result = new CatalogIdentityValidation(true, null);
        }

        _identityByAssembly.Add(assembly, result);
        return result;

        CatalogIdentityValidation Invalid(string code, string message) => new(
            false,
            ArchitectureCanonicalAnnotationDiagnostic.Create(subject, code, message, catalog.CatalogIdentityMarker));
    }

    private static Dictionary<string, object> ReadMetadata(
        CustomAttributeData data,
        CanonicalAnnotationCatalog catalog,
        ArchitectureClassificationSource source,
        string subject,
        List<ArchitectureClassificationMetadataFailure> metadataFailures,
        List<ArchitectureCanonicalAnnotationDiagnostic> diagnostics)
    {
        Dictionary<string, object> metadata = new(StringComparer.Ordinal);
        foreach (CustomAttributeNamedArgument argument in data.NamedArguments)
        {
            string? propertyName = argument.MemberName;
            if (propertyName is null || !catalog.TryGetMetadataKey(propertyName, out string metadataKey))
            {
                continue;
            }

            if (argument.TypedValue.ArgumentType == typeof(string)
                && argument.TypedValue.Value is string value
                && value.Length > 0)
            {
                metadata[metadataKey] = value;
                continue;
            }

            string fqn = TryGetAttributeName(data) ?? "<unknown>";
            const string FailureReason = "must contain an exact non-empty compile-time string value.";
            metadataFailures.Add(new ArchitectureClassificationMetadataFailure(
                subject, source, metadataKey, FailureReason));
            diagnostics.Add(ArchitectureCanonicalAnnotationDiagnostic.Create(
                subject,
                "InvalidCanonicalMetadata",
                $"'{fqn}.{propertyName}' {FailureReason}",
                fqn));
        }

        return metadata;
    }

    private static bool TryReadContextValue(
        CustomAttributeData data,
        CanonicalAnnotationCatalog.CanonicalContext context,
        ContextValueReadContext readContext,
        out string value)
    {
        value = string.Empty;
        if (data.ConstructorArguments.Count == 1
            && data.ConstructorArguments[0].ArgumentType == typeof(string)
            && data.ConstructorArguments[0].Value is string constructorValue
            && constructorValue.Length > 0)
        {
            value = constructorValue;
            return true;
        }

        const string FailureReason = "must contain an exact non-empty compile-time string value.";
        readContext.InvalidMetadataKeys.Add(context.Key);
        readContext.MetadataFailures.Add(new ArchitectureClassificationMetadataFailure(
            readContext.Subject, readContext.Source, context.Key, FailureReason));
        readContext.Diagnostics.Add(ArchitectureCanonicalAnnotationDiagnostic.Create(
            readContext.Subject,
            "InvalidCanonicalMetadata",
            $"'{context.AttributeFullName}' {FailureReason}",
            context.AttributeFullName));
        return false;
    }

    private static (Dictionary<string, object> Metadata, IReadOnlySet<string> BlockedKeys) ResolveContextMetadata(
        IReadOnlyList<ContextEvidence> evidence,
        IReadOnlySet<string> invalidKeys,
        string scope,
        string subject,
        List<ArchitectureCanonicalAnnotationDiagnostic> diagnostics)
    {
        Dictionary<string, object> metadata = new(StringComparer.Ordinal);
        HashSet<string> blockedKeys = new(invalidKeys, StringComparer.Ordinal);
        foreach (IGrouping<string, ContextEvidence> group in evidence
                     .GroupBy(item => item.Key, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            string[] values = group.Select(item => item.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (values.Length == 1)
            {
                if (!blockedKeys.Contains(group.Key))
                {
                    metadata[group.Key] = values[0];
                }
                continue;
            }

            blockedKeys.Add(group.Key);

            string[] evidenceSources = group.Select(item => item.AttributeFullName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string evidenceDescription = string.Join(
                "; ",
                group.Select(item => $"{item.AttributeFullName}='{item.Value}'")
                    .OrderBy(value => value, StringComparer.Ordinal));
            diagnostics.Add(ArchitectureCanonicalAnnotationDiagnostic.Create(
                subject,
                "CanonicalContextConflict",
                $"Canonical context metadata at {scope} specificity for key '{group.Key}' has conflicting values "
                    + $"[{string.Join(", ", values.Select(FormatContextValue))}] from [{evidenceDescription}]. "
                    + "The key is omitted from this context scope.",
                evidenceSources));
        }

        return (metadata, blockedKeys);
    }

    private static string FormatContextValue(string value) => $"'{value}'";

    private static IReadOnlyList<CustomAttributeData> GetAssemblyAttributes(Assembly assembly)
    {
        try
        {
            return assembly.GetCustomAttributesData().ToArray();
        }
        catch (TypeLoadException) { return Array.Empty<CustomAttributeData>(); }
        catch (FileNotFoundException) { return Array.Empty<CustomAttributeData>(); }
        catch (CustomAttributeFormatException) { return Array.Empty<CustomAttributeData>(); }
    }

    private static string? TryGetAttributeName(CustomAttributeData data)
    {
        try
        {
            return data.AttributeType.FullName;
        }
        catch (TypeLoadException) { return null; }
        catch (FileNotFoundException) { return null; }
        catch (CustomAttributeFormatException) { return null; }
    }

    private static string SafeAssemblyName(Assembly assembly)
    {
        try { return assembly.GetName().Name ?? assembly.FullName ?? "<unknown assembly>"; }
        catch (FileNotFoundException) { return assembly.FullName ?? "<unknown assembly>"; }
    }

    private static bool MetadataEqual(IReadOnlyDictionary<string, object> left, IReadOnlyDictionary<string, object> right) =>
        left.Count == right.Count && left.All(entry => right.TryGetValue(entry.Key, out object? other) && entry.Value.Equals(other));

    private static string FormatMetadata(IReadOnlyDictionary<string, object> metadata) =>
        metadata.Count == 0 ? "no metadata" : string.Join(", ", metadata.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{entry.Key}='{entry.Value}'"));

    private sealed class ResolutionEvidence
    {
        public ResolutionEvidence(ArchitectureClassificationSource source, string subject)
        {
            ContextValueReadContext = new ContextValueReadContext(
                source, subject, MetadataFailures, Diagnostics, InvalidContextMetadataKeys);
        }

        public List<ArchitectureCanonicalAnnotationDiagnostic> Diagnostics { get; } = new();

        public List<ArchitectureClassificationMetadataFailure> MetadataFailures { get; } = new();

        public List<(CanonicalAnnotationCatalog.CanonicalRole Role, IReadOnlyDictionary<string, object> Metadata)> Matches { get; } = new();

        public List<ContextEvidence> ContextEntries { get; } = new();

        public HashSet<string> InvalidContextMetadataKeys { get; } = new(StringComparer.Ordinal);

        public List<string> CanonicalEvidenceNames { get; } = new();

        public bool CanonicalEvidenceSeen { get; set; }

        public ContextValueReadContext ContextValueReadContext { get; }
    }

    private sealed record ContextEvidence(string Key, string AttributeFullName, string Value);

    private sealed record ContextValueReadContext(
        ArchitectureClassificationSource Source,
        string Subject,
        List<ArchitectureClassificationMetadataFailure> MetadataFailures,
        List<ArchitectureCanonicalAnnotationDiagnostic> Diagnostics,
        HashSet<string> InvalidMetadataKeys);

    private sealed record CatalogIdentityValidation(bool IsCompatible, ArchitectureCanonicalAnnotationDiagnostic? Diagnostic);
}

internal static class CanonicalAnnotationCandidateComposer
{
    public static ArchitectureAttributeClassificationCandidate Merge(
        ArchitectureAttributeClassificationCandidate configured,
        ArchitectureAttributeClassificationCandidate canonical,
        string subject)
    {
        if (ReferenceEquals(configured, ArchitectureAttributeClassificationCandidate.Empty)
            && ReferenceEquals(canonical, ArchitectureAttributeClassificationCandidate.Empty))
        {
            return ArchitectureAttributeClassificationCandidate.Empty;
        }

        List<ArchitectureCanonicalAnnotationDiagnostic> diagnostics = configured.CanonicalAnnotationDiagnostics
            .Concat(canonical.CanonicalAnnotationDiagnostics)
            .Distinct()
            .OrderBy(diagnostic => diagnostic.Subject, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ToList();
        IReadOnlyList<ArchitectureClassificationConflict> conflicts = configured.Conflicts.Concat(canonical.Conflicts).ToList();
        IReadOnlyList<ArchitectureClassificationMetadataFailure> failures = configured.MetadataFailures
            .Concat(canonical.MetadataFailures).ToList();

        if (configured.Blocked || canonical.Blocked)
        {
            return new ArchitectureAttributeClassificationCandidate(
                null, new Dictionary<string, object>(), null, conflicts, failures)
            {
                EvidenceSources = MergeEvidence(configured, canonical),
                CanonicalAnnotationDiagnostics = diagnostics,
                ContextMetadata = MergeContextMetadata(configured, canonical),
                ContextMetadataBlockedKeys = MergeContextMetadataBlockedKeys(configured, canonical),
                Blocked = true
            };
        }

        if (configured.Role is null)
        {
            return canonical with
            {
                Conflicts = conflicts,
                MetadataFailures = failures,
                CanonicalAnnotationDiagnostics = diagnostics,
                ContextMetadata = MergeContextMetadata(configured, canonical),
                ContextMetadataBlockedKeys = MergeContextMetadataBlockedKeys(configured, canonical)
            };
        }

        if (canonical.Role is null)
        {
            return configured with
            {
                EvidenceSources = MergeEvidence(configured, canonical),
                Conflicts = conflicts,
                MetadataFailures = failures,
                CanonicalAnnotationDiagnostics = diagnostics,
                ContextMetadata = MergeContextMetadata(configured, canonical),
                ContextMetadataBlockedKeys = MergeContextMetadataBlockedKeys(configured, canonical)
            };
        }

        IReadOnlyList<ArchitectureAttributeRoleEvidence> observedEvidence = configured.ObservedEvidence
            .Concat(canonical.ObservedEvidence).ToArray();
        ArchitectureAttributeRoleEvidence? first = observedEvidence.FirstOrDefault();
        bool coherent = first is not null && observedEvidence.All(candidate =>
            string.Equals(candidate.Role, first.Role, StringComparison.Ordinal)
            && MetadataEqual(candidate.Metadata, first.Metadata));
        if (coherent)
        {
            return configured with
            {
                EvidenceSources = MergeEvidence(configured, canonical, observedEvidence),
                ObservedEvidence = observedEvidence,
                Conflicts = conflicts,
                MetadataFailures = failures,
                CanonicalAnnotationDiagnostics = diagnostics,
                ContextMetadata = MergeContextMetadata(configured, canonical),
                ContextMetadataBlockedKeys = MergeContextMetadataBlockedKeys(configured, canonical)
            };
        }

        IReadOnlyList<string> evidenceSources = MergeEvidence(configured, canonical, observedEvidence);
        string details = string.Join("; ", observedEvidence
            .Select(candidate => $"{candidate.Evidence} => {candidate.Role} ({FormatMetadata(candidate.Metadata)})")
            .OrderBy(value => value, StringComparer.Ordinal));
        string message = $"Canonical annotation evidence on '{subject}' conflicts with configured attribute evidence: "
            + $"{details}. No role is assigned at this specificity.";
        diagnostics.Add(ArchitectureCanonicalAnnotationDiagnostic.Create(
            subject, "CanonicalRoleConflict", message, evidenceSources.ToArray()));
        return new ArchitectureAttributeClassificationCandidate(
            null, new Dictionary<string, object>(), null, conflicts, failures)
        {
            EvidenceSources = evidenceSources,
            ObservedEvidence = observedEvidence,
            CanonicalAnnotationDiagnostics = diagnostics,
            ContextMetadata = MergeContextMetadata(configured, canonical),
            ContextMetadataBlockedKeys = MergeContextMetadataBlockedKeys(configured, canonical),
            Blocked = true
        };
    }

    private static IReadOnlyDictionary<string, object> MergeContextMetadata(
        ArchitectureAttributeClassificationCandidate configured,
        ArchitectureAttributeClassificationCandidate canonical)
    {
        if (configured.ContextMetadata.Count == 0)
        {
            return canonical.ContextMetadata;
        }

        if (canonical.ContextMetadata.Count == 0)
        {
            return configured.ContextMetadata;
        }

        Dictionary<string, object> merged = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object> entry in configured.ContextMetadata)
        {
            merged[entry.Key] = entry.Value;
        }

        foreach (KeyValuePair<string, object> entry in canonical.ContextMetadata)
        {
            merged.TryAdd(entry.Key, entry.Value);
        }

        return merged;
    }

    private static IReadOnlySet<string> MergeContextMetadataBlockedKeys(
        ArchitectureAttributeClassificationCandidate configured,
        ArchitectureAttributeClassificationCandidate canonical)
    {
        if (configured.ContextMetadataBlockedKeys.Count == 0)
        {
            return canonical.ContextMetadataBlockedKeys;
        }

        if (canonical.ContextMetadataBlockedKeys.Count == 0)
        {
            return configured.ContextMetadataBlockedKeys;
        }

        HashSet<string> merged = new(configured.ContextMetadataBlockedKeys, StringComparer.Ordinal);
        merged.UnionWith(canonical.ContextMetadataBlockedKeys);
        return merged;
    }

    private static IReadOnlyList<string> MergeEvidence(
        ArchitectureAttributeClassificationCandidate configured,
        ArchitectureAttributeClassificationCandidate canonical,
        IEnumerable<ArchitectureAttributeRoleEvidence>? observedEvidence = null) =>
        configured.EvidenceSources.Concat(canonical.EvidenceSources)
            .Concat(configured.ObservedEvidence.Select(evidence => evidence.Evidence))
            .Concat(canonical.ObservedEvidence.Select(evidence => evidence.Evidence))
            .Concat(observedEvidence?.Select(evidence => evidence.Evidence) ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private static bool MetadataEqual(IReadOnlyDictionary<string, object> left, IReadOnlyDictionary<string, object> right) =>
        left.Count == right.Count && left.All(entry => right.TryGetValue(entry.Key, out object? other) && entry.Value.Equals(other));

    private static string FormatMetadata(IReadOnlyDictionary<string, object> metadata) =>
        metadata.Count == 0 ? "no metadata" : string.Join(", ", metadata.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{entry.Key}='{entry.Value}'"));
}
