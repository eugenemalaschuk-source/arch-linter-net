using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli;

internal static class ExternalEvidenceCommandSupport
{
    public static SarifEvidenceAssessmentContext? ResolveAssessmentContext(
        string? repository,
        string? revision,
        string? scope) =>
        repository is null && revision is null && scope is null
            ? null
            : new SarifEvidenceAssessmentContext(repository, revision, scope);

    // One occurrence = one binding: id=<id>,path=<path>[,repository=<v>][,revision=<v>][,scope=<v>].
    // Bindings are matched to declared external_evidence requirements by id, not position, so
    // multiple occurrences remain order-independent (see ArchitectureExternalEvidenceBinder).
    public static IReadOnlyList<SarifEvidenceArtifactReference> ParseBindings(string[]? rawValues)
    {
        if (rawValues is null || rawValues.Length == 0)
        {
            return Array.Empty<SarifEvidenceArtifactReference>();
        }

        List<SarifEvidenceArtifactReference> artifacts = new(rawValues.Length);
        HashSet<string> seenIds = new(StringComparer.Ordinal);
        foreach (string raw in rawValues)
        {
            Dictionary<string, string> fields = ParseFields(raw);
            if (!fields.TryGetValue("id", out string? id) || string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException(
                    $"Invalid --external-evidence value: '{raw}'. Missing required 'id'.");
            }

            if (!fields.TryGetValue("path", out string? path) || string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException(
                    $"Invalid --external-evidence value: '{raw}'. Missing required 'path'.");
            }

            if (!seenIds.Add(id))
            {
                throw new InvalidOperationException(
                    $"Duplicate --external-evidence binding for id '{id}'.");
            }

            fields.TryGetValue("repository", out string? repository);
            fields.TryGetValue("revision", out string? revision);
            fields.TryGetValue("scope", out string? scope);
            SarifEvidenceProducerContext? producer = repository is null && revision is null && scope is null
                ? null
                : new SarifEvidenceProducerContext(repository, revision, scope);
            artifacts.Add(new SarifEvidenceArtifactReference(path, id, producer));
        }

        return artifacts;
    }

    private static Dictionary<string, string> ParseFields(string raw)
    {
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (string segment in raw.Split(','))
        {
            int eqIndex = segment.IndexOf('=');
            if (eqIndex <= 0 || eqIndex >= segment.Length - 1)
            {
                throw new InvalidOperationException(
                    $"Invalid --external-evidence value: '{raw}'. Use " +
                    "id=<id>,path=<path>[,repository=<value>][,revision=<value>][,scope=<value>].");
            }

            string key = segment[..eqIndex];
            string value = segment[(eqIndex + 1)..];
            if (key is not ("id" or "path" or "repository" or "revision" or "scope"))
            {
                throw new InvalidOperationException(
                    $"Invalid --external-evidence key '{key}' in '{raw}'. " +
                    "Supported keys: id, path, repository, revision, scope.");
            }

            if (!fields.TryAdd(key, value))
            {
                throw new InvalidOperationException(
                    $"Duplicate key '{key}' in --external-evidence value '{raw}'.");
            }
        }

        return fields;
    }
}
