using System.Text.RegularExpressions;

namespace ArchLinterNet.Core.Contracts.Validators;

// A contextual contract's `source` selector has no other source to compare metadata against, so
// the not-equal-to-source operator ('!{source.metadata.<key>}') is only meaningful on
// forbidden/allowed/exclude selectors (see
// openspec/changes/add-contextual-dependency-contracts/design.md Decision 2/3). Without this check,
// declaring it on `source` deserializes and matches zero source types silently -
// ArchitectureContextSelectorMatcher.Matches always resolves "not-equal-to-source" against a null
// sourceDescriptor as a non-match when resolving the contract's own source selector - turning a
// strict contract into a false-negative no-op instead of a build failure.
//
// Mirrors (rather than references) Execution.ArchitectureContextSelectorMatcher's pattern, because
// Contracts must not depend on Execution (see ArchitecturePolicyDocumentValidatorPipeline's note on
// the dependency direction).
internal sealed partial class ContextualSourceSelectorValidator : IArchitecturePolicyDocumentValidator
{
    [GeneratedRegex(@"^!\{source\.metadata\.[A-Za-z0-9_]+\}$", RegexOptions.CultureInvariant)]
    private static partial Regex NotEqualToSourcePattern();

    public void Validate(ArchitectureContractDocument document)
    {
        foreach (ArchitectureContextDependencyContract contract in document.Contracts.StrictContextDependencies
                     .Concat(document.Contracts.AuditContextDependencies))
        {
            ValidateSourceSelector(contract.Name, contract.Source);
        }

        foreach (ArchitectureContextAllowOnlyContract contract in document.Contracts.StrictContextAllowOnly
                     .Concat(document.Contracts.AuditContextAllowOnly))
        {
            ValidateSourceSelector(contract.Name, contract.Source);
        }
    }

    private static void ValidateSourceSelector(string contractName, ArchitectureContextSelector source)
    {
        string? invalidKey = source.Metadata
            .Where(entry => entry.Value is string stringValue && NotEqualToSourcePattern().IsMatch(stringValue))
            .Select(entry => entry.Key)
            .FirstOrDefault();

        if (invalidKey == null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Contextual contract '{contractName}' declares a not-equal-to-source ('!{{source.metadata.<key>}}') " +
            $"value on its 'source' selector's '{invalidKey}' metadata key. This operator compares a candidate " +
            "against the current source type's own metadata and is only meaningful on 'forbidden'/'allowed'/" +
            "'exclude' selectors — declaring it on 'source' itself has no other source to compare against and " +
            "would silently match zero source types.");
    }
}
