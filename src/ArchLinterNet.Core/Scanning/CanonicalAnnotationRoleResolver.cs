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
        CanonicalAnnotationCatalog catalog = CanonicalAnnotationCatalog.Current;
        List<ArchitectureCanonicalAnnotationDiagnostic> diagnostics = new();
        List<(CanonicalAnnotationCatalog.CanonicalRole Role, IReadOnlyDictionary<string, object> Metadata)> matches = new();

        foreach (CustomAttributeData data in attributeData)
        {
            string? attributeName = TryGetAttributeName(data);
            if (attributeName is null)
            {
                continue;
            }

            if (catalog.IsReservedAttribute(attributeName) && !catalog.IsKnownAttribute(attributeName))
            {
                diagnostics.Add(ArchitectureCanonicalAnnotationDiagnostic.Create(
                    subject,
                    "UnknownReservedAnnotation",
                    $"'{attributeName}' is in the reserved '{catalog.ReservedNamespace}' namespace but is not present in the semantic annotation catalog.",
                    attributeName));
                continue;
            }

            if (!catalog.TryGetRole(attributeName, out CanonicalAnnotationCatalog.CanonicalRole role))
            {
                continue;
            }

            if (!role.Scopes.Contains(scope, StringComparer.Ordinal))
            {
                diagnostics.Add(ArchitectureCanonicalAnnotationDiagnostic.Create(
                    subject,
                    "AnnotationScopeMismatch",
                    $"'{attributeName}' is catalogued for {string.Join(" and ", role.Scopes)} scope, not {scope} scope.",
                    attributeName));
                continue;
            }

            IReadOnlyDictionary<string, object> metadata = ReadMetadata(data, catalog, subject, diagnostics);
            matches.Add((role, metadata));
        }

        if (matches.Count == 0)
        {
            return new ArchitectureAttributeClassificationCandidate(
                null, new Dictionary<string, object>(), null,
                Array.Empty<ArchitectureClassificationConflict>(),
                Array.Empty<ArchitectureClassificationMetadataFailure>())
            {
                CanonicalAnnotationDiagnostics = diagnostics
            };
        }

        CatalogIdentityValidation identity = GetIdentityValidation(assembly, catalog);
        if (identity.Diagnostic is not null)
        {
            string subjectKind = string.Equals(scope, "assembly", StringComparison.Ordinal) ? "assembly" : "type";
            string[] annotationEvidence = matches.Select(match => match.Role.AttributeFullName)
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            diagnostics.Add(identity.Diagnostic with
            {
                Subject = subject,
                Message = $"{identity.Diagnostic.Message} The affected {subjectKind} is '{subject}' and carries canonical annotation(s): "
                    + $"{string.Join(", ", annotationEvidence)}.",
                EvidenceSources = identity.Diagnostic.EvidenceSources.Concat(annotationEvidence)
                    .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
            });
        }

        if (!identity.IsCompatible)
        {
            return new ArchitectureAttributeClassificationCandidate(
                null, new Dictionary<string, object>(), null,
                Array.Empty<ArchitectureClassificationConflict>(),
                Array.Empty<ArchitectureClassificationMetadataFailure>())
            {
                CanonicalAnnotationDiagnostics = diagnostics
            };
        }

        string[] evidenceSources = matches.Select(match => match.Role.AttributeFullName)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        (CanonicalAnnotationCatalog.CanonicalRole Role, IReadOnlyDictionary<string, object> Metadata) first = matches[0];
        bool coherent = matches.All(match => string.Equals(match.Role.Name, first.Role.Name, StringComparison.Ordinal)
            && MetadataEqual(match.Metadata, first.Metadata));
        if (!coherent)
        {
            string details = string.Join(
                "; ",
                matches.Select(match => $"{match.Role.AttributeFullName} => {match.Role.Name} ({FormatMetadata(match.Metadata)})")
                    .OrderBy(value => value, StringComparer.Ordinal));
            diagnostics.Add(ArchitectureCanonicalAnnotationDiagnostic.Create(
                subject,
                "CanonicalRoleConflict",
                $"Canonical annotations at {scope} specificity disagree: {details}. No role is assigned at this specificity.",
                evidenceSources));
            return new ArchitectureAttributeClassificationCandidate(
                null, new Dictionary<string, object>(), null,
                Array.Empty<ArchitectureClassificationConflict>(),
                Array.Empty<ArchitectureClassificationMetadataFailure>())
            {
                EvidenceSources = evidenceSources,
                CanonicalAnnotationDiagnostics = diagnostics,
                Blocked = true
            };
        }

        return new ArchitectureAttributeClassificationCandidate(
            first.Role.Name,
            first.Metadata,
            evidenceSources.FirstOrDefault(),
            Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            EvidenceSources = evidenceSources,
            ObservedEvidence = matches.Select(match => new ArchitectureAttributeRoleEvidence(
                match.Role.Name, match.Metadata, match.Role.AttributeFullName)).ToArray(),
            CanonicalAnnotationDiagnostics = diagnostics
        };
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
        string subject,
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
            diagnostics.Add(ArchitectureCanonicalAnnotationDiagnostic.Create(
                subject,
                "InvalidCanonicalMetadata",
                $"'{fqn}.{propertyName}' must contain an exact non-empty compile-time string value.",
                fqn));
        }

        return metadata;
    }

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

    private sealed record CatalogIdentityValidation(bool IsCompatible, ArchitectureCanonicalAnnotationDiagnostic? Diagnostic);
}

internal static class CanonicalAnnotationCandidateComposer
{
    public static ArchitectureAttributeClassificationCandidate Merge(
        ArchitectureAttributeClassificationCandidate configured,
        ArchitectureAttributeClassificationCandidate canonical,
        string subject)
    {
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
                Blocked = true
            };
        }

        if (configured.Role is null)
        {
            return canonical with
            {
                Conflicts = conflicts,
                MetadataFailures = failures,
                CanonicalAnnotationDiagnostics = diagnostics
            };
        }

        if (canonical.Role is null)
        {
            return configured with
            {
                EvidenceSources = MergeEvidence(configured, canonical),
                CanonicalAnnotationDiagnostics = diagnostics
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
                CanonicalAnnotationDiagnostics = diagnostics
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
            Blocked = true
        };
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
