using System.Collections;
using System.Text.RegularExpressions;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

// Structural validation for the context_dependencies/context_allow_only families, beyond what
// IgnoreUnmatchedProperties() deserialization alone guarantees. The production loading path
// (ArchitecturePolicyDocumentLoader.Load) never runs the public JSON schema (schema/dependencies.
// arch.schema.json) — schema validation only happens in tests/IDE tooling — and every contextual
// selector field has a YAML-safe empty-value default (Role: "", Metadata: new(), Forbidden/Allowed:
// new List<>()). Without this validator, a typo'd key (e.g. "forbiden") or an omitted `source`/
// `forbidden`/`allowed` deserializes cleanly into a contract whose checker then matches zero source
// types or zero forbidden/allowed selectors, so a strict contract silently reports zero violations
// instead of failing to load — a false-negative enforcement gap, not just a confusing error.
//
// Mirrors (rather than references) Execution.ArchitectureContextSelectorMatcher's not-equal-to-source
// pattern, because Contracts must not depend on Execution (see
// ArchitecturePolicyDocumentValidatorPipeline's note on the dependency direction).
internal sealed partial class ContextualContractValidator : IArchitecturePolicyDocumentValidator
{
    private const string ForbiddenFieldName = "forbidden";

    [GeneratedRegex(@"^!\{source\.metadata\.[A-Za-z0-9_]+\}$", RegexOptions.CultureInvariant)]
    private static partial Regex NotEqualToSourcePattern();

    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureContextDependencyContract contract in document.Contracts.StrictContextDependencies
                     .Concat(document.Contracts.AuditContextDependencies))
        {
            ValidateSource(contract.Name, contract.Source);
            ValidateNonEmptySelectorList(contract.Name, ForbiddenFieldName, contract.Forbidden);
            ValidateTargetSelectors(contract.Name, ForbiddenFieldName, contract.Forbidden);
            ValidateTargetSelectors(contract.Name, "exclude", contract.Exclude);
        }

        foreach (ArchitectureContextAllowOnlyContract contract in document.Contracts.StrictContextAllowOnly
                     .Concat(document.Contracts.AuditContextAllowOnly))
        {
            ValidateSource(contract.Name, contract.Source);
            ValidateNonEmptySelectorList(contract.Name, "allowed", contract.Allowed);
            ValidateTargetSelectors(contract.Name, "allowed", contract.Allowed);
            ValidateTargetSelectors(contract.Name, "exclude", contract.Exclude);
        }

        foreach (ArchitecturePortBoundaryContract contract in document.Contracts.StrictPortBoundaries
                     .Concat(document.Contracts.AuditPortBoundaries))
        {
            ValidateSource(contract.Name, contract.Source);
            if (contract.TargetContext.Metadata.Count == 0)
                throw new InvalidOperationException($"Port-boundary contract '{contract.Name}' must declare non-empty 'target_context.metadata'.");
            ValidateMetadataValues(contract.Name, "target_context", contract.TargetContext.Metadata);
            if (string.IsNullOrWhiteSpace(contract.Reason))
                throw new InvalidOperationException($"Port-boundary contract '{contract.Name}' must declare a non-empty 'reason'.");
            ValidateNonEmptySelectorList(contract.Name, "allowed_seams", contract.AllowedSeams);
            ValidateNonEmptySelectorList(contract.Name, ForbiddenFieldName, contract.Forbidden);
            ValidateTargetSelectors(contract.Name, "allowed_seams", contract.AllowedSeams);
            ValidateTargetSelectors(contract.Name, ForbiddenFieldName, contract.Forbidden);
            ValidateTargetSelectors(contract.Name, "exclude", contract.Exclude);
            foreach (ArchitectureAdapterPortBinding binding in contract.AdapterBindings)
            {
                ValidateSource(contract.Name, binding.Adapter);
                ValidateSource(contract.Name, binding.ExpectedPort);
                ValidateTargetSelectors(contract.Name, "adapter_bindings.allowed_contexts", binding.AllowedContexts);
            }
        }
    }

    private static void ValidateSource(string contractName, ArchitectureContextSelector? source)
    {
        if (source == null)
        {
            throw new InvalidOperationException(
                $"Contextual contract '{contractName}' must declare a 'source' selector.");
        }

        if (string.IsNullOrWhiteSpace(source.Role))
        {
            throw new InvalidOperationException(
                $"Contextual contract '{contractName}' must declare a non-empty 'source.role'.");
        }

        ValidateMetadata(contractName, "source", source);

        string? invalidKey = source.Metadata
            .Where(entry => entry.Value is string stringValue && NotEqualToSourcePattern().IsMatch(stringValue))
            .Select(entry => entry.Key)
            .FirstOrDefault();

        if (invalidKey != null)
        {
            throw new InvalidOperationException(
                $"Contextual contract '{contractName}' declares a not-equal-to-source ('!{{source.metadata.<key>}}') " +
                $"value on its 'source' selector's '{invalidKey}' metadata key. This operator compares a candidate " +
                "against the current source type's own metadata and is only meaningful on 'forbidden'/'allowed'/" +
                "'exclude' selectors — declaring it on 'source' itself has no other source to compare against and " +
                "would silently match zero source types.");
        }
    }

    private static void ValidateNonEmptySelectorList(
        string contractName, string fieldName, List<ArchitectureContextSelector>? selectors)
    {
        if (selectors == null || selectors.Count == 0)
        {
            throw new InvalidOperationException(
                $"Contextual contract '{contractName}' must declare at least one '{fieldName}' selector.");
        }
    }

    private static void ValidateTargetSelectors(
        string contractName, string fieldName, List<ArchitectureContextSelector>? selectors)
    {
        if (selectors == null)
        {
            return;
        }

        foreach (ArchitectureContextSelector selector in selectors)
        {
            if (selector == null)
            {
                throw new InvalidOperationException(
                    $"Contextual contract '{contractName}' declares a null entry in '{fieldName}'.");
            }

            if (string.IsNullOrWhiteSpace(selector.Role))
            {
                throw new InvalidOperationException(
                    $"Contextual contract '{contractName}' declares a '{fieldName}' selector with no non-empty 'role'.");
            }

            ValidateMetadata(contractName, fieldName, selector);
        }
    }

    private static void ValidateMetadata(string contractName, string fieldName, ArchitectureContextSelector selector)
    {
        if (selector.Metadata == null)
        {
            throw new InvalidOperationException(
                $"Contextual contract '{contractName}' declares a '{fieldName}' selector whose 'metadata' must be an object when declared.");
        }

        ValidateMetadataValues(contractName, fieldName, selector.Metadata);
    }

    // The public JSON schema's contextMetadataValue union requires each metadata value to be either
    // a non-empty scalar or a non-empty list of scalars (schema/dependencies.arch.schema.json's
    // scalarValue + contextMetadataValue $defs), but the production loader never runs that schema and
    // Dictionary<string, object> deserializes a YAML `domain: []` or `domain: {}` cleanly. The
    // evaluator's "in" operator (MatchesTargetContext/ArchitectureContextSelectorMatcher) then calls
    // Any() over an empty/malformed sequence, which is unconditionally false for every candidate - so
    // an empty list or a nested mapping silently makes a strict selector match nothing rather than
    // failing to load.
    private static void ValidateMetadataValues(string contractName, string fieldName, IDictionary<string, object> metadata)
    {
        foreach (KeyValuePair<string, object> entry in metadata)
        {
            ValidateMetadataValue(contractName, fieldName, entry.Key, entry.Value);
        }
    }

    private static void ValidateMetadataValue(string contractName, string fieldName, string key, object? value)
    {
        // String is checked ahead of the generic IEnumerable branch below - string implements
        // IEnumerable<char>, so an empty string would otherwise be miscategorized as an "empty list"
        // instead of reported as an empty scalar.
        if (value is string text)
        {
            if (text.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Contextual contract '{contractName}' declares a '{fieldName}' selector whose metadata key " +
                    $"'{key}' has an empty string value; metadata values must be a non-empty scalar, or a " +
                    "non-empty list of scalars.");
            }

            return;
        }

        if (IsScalarMetadataValue(value))
        {
            return;
        }

        if (value is IEnumerable sequence and not IDictionary)
        {
            List<object?> items = sequence.Cast<object?>().ToList();
            if (items.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Contextual contract '{contractName}' declares a '{fieldName}' selector whose metadata key " +
                    $"'{key}' is an empty list. An empty list never matches any candidate, silently turning the " +
                    "contract into a no-op.");
            }

            if (items.Any(item => !(item is string s ? s.Length > 0 : IsScalarMetadataValue(item))))
            {
                throw new InvalidOperationException(
                    $"Contextual contract '{contractName}' declares a '{fieldName}' selector whose metadata key " +
                    $"'{key}' list contains a non-scalar entry (null, list, or mapping); list entries must be " +
                    "non-empty scalar values.");
            }

            return;
        }

        throw new InvalidOperationException(
            $"Contextual contract '{contractName}' declares a '{fieldName}' selector whose metadata key '{key}' " +
            "has an unsupported value (null or a nested mapping); metadata values must be a non-empty scalar, " +
            "or a non-empty list of scalars.");
    }

    private static bool IsScalarMetadataValue(object? value) =>
        value switch
        {
            bool => true,
            long => true,
            int => true,
            decimal => true,
            double => true,
            _ => false
        };
}
