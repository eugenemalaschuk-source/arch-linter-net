using System.Text.RegularExpressions;
using ArchLinterNet.CEL;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.Core.Contracts.Expressions;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts.Validators;

// Compiles every declared `when` field through the public ArchLinterNet.CEL API at policy-load
// time, per openspec/specs/cel-policy-model/spec.md: compilation happens once here (never during
// contract execution), forces a boolean predicate result, and fails the policy load on any
// CelDiagnostic. A selector with no `when` never touches the CEL engine (literal-only fast path).
// See openspec/changes/core-cel-integration/design.md.
//
// Each compile call sets the validation subject to the exact effective YAML path of the selector
// declaring `when` (e.g. `contracts.strict_context_dependencies[0].forbidden[2]`), not just the
// owning layer/contract, so ArchitecturePolicyDocumentLoader.Load's EnrichValidationException
// attaches the precise fragment/path for composed (imported) policies rather than the whole
// contract's location. Strict and audit groups are walked separately (not concatenated) because
// each has its own independent YAML index sequence.
internal sealed partial class ExpressionCompilationValidator : IArchitecturePolicyDocumentValidator
{
    private const string ContractsProperty = "contracts";
    private const string LayersProperty = "layers";
    private const string SelectorProperty = "selector";
    private const string SourceProperty = "source";

    // ArchLinterNet.CEL exposes no public API to introspect which identifiers/members a compiled
    // predicate references (its bound-expression tree is deliberately internal — see
    // docs/internal/cel-engine-architecture.md's "Prohibited shortcuts" table), so Core cannot ask
    // "does this expression read dependency.viaMethodBody" after compilation. `dependency` facts
    // are populated with fixed, non-per-edge constants in this release (see
    // ArchitectureExpressionFactService.BuildDependencyFacts and
    // openspec/changes/cel-selector-contextual-integration/design.md Decision D6) — a `when` that
    // reads them would compile successfully and then always evaluate the same way regardless of the
    // real edge, silently weakening the contract instead of failing closed. Reject any occurrence of
    // the `dependency` identifier at policy-load time until real per-edge facts exist.
    //
    // Deliberately an unconditional whole-string bare-word match, with no attempt to skip string
    // literals or comments. Two earlier review rounds each found a real bypass in a "smarter"
    // version of this check: a first pass required the literal substring "dependency." and missed
    // CEL's postfix-member-access-after-parentheses grammar ((dependency).viaMethodBody); a second
    // pass added hand-rolled quote-tracking to avoid false-positiving on the word "dependency"
    // inside string literals, and that scanner didn't know CEL raw strings (`r'...'`) don't treat
    // backslash as an escape character — `r'\' == "x" || dependency.viaMethodBody` fooled it into
    // treating the real reference as still "inside" the raw string. CEL also supports `//` line
    // comments, which a quote-only scanner never accounts for either. Correctly replicating CEL's
    // full lexical grammar (raw/byte-string prefixes, escape rules, comments, triple-quoted forms)
    // by hand in Core — without access to the real tokenizer, which is deliberately internal to
    // ArchLinterNet.CEL — has now proven to be exactly the kind of thing that's easy to get subtly
    // wrong twice in a row. The unconditional bare-word match is the one form of this check that is
    // provably impossible to bypass, at the accepted cost of also rejecting the word "dependency"
    // inside an unrelated string literal or comment. That trade-off is minor (rename the metadata
    // value, or phrase the comparison differently) next to what a bypass would mean: a predicate
    // that looks like it checks per-edge facts but silently never does.
    [GeneratedRegex(@"\bdependency\b", RegexOptions.CultureInvariant)]
    private static partial Regex DependencyIdentifierPattern();

    private static bool ReferencesDependencyIdentifier(string expression)
    {
        return DependencyIdentifierPattern().IsMatch(expression);
    }

    public void Validate(ArchitectureContractDocument document)
    {
        foreach ((string layerName, ArchitectureLayer layer) in document.Layers)
        {
            string path = ArchitecturePolicyProvenancePath.AppendProperty(
                ArchitecturePolicyProvenancePath.AppendProperty(
                    ArchitecturePolicyProvenancePath.Property(LayersProperty), layerName),
                SelectorProperty);
            document.Provenance.SetValidationSubject(path);
            CompileLayerSelector(document, layer.Selector, layerName, path);
        }

        CompileContextDependencyGroup(document, document.Contracts.StrictContextDependencies, "strict_context_dependencies");
        CompileContextDependencyGroup(document, document.Contracts.AuditContextDependencies, "audit_context_dependencies");
        CompileContextAllowOnlyGroup(document, document.Contracts.StrictContextAllowOnly, "strict_context_allow_only");
        CompileContextAllowOnlyGroup(document, document.Contracts.AuditContextAllowOnly, "audit_context_allow_only");
    }

    private static void CompileContextDependencyGroup(
        ArchitectureContractDocument document, List<ArchitectureContextDependencyContract> contracts, string groupKey)
    {
        for (int index = 0; index < contracts.Count; index++)
        {
            ArchitectureContextDependencyContract contract = contracts[index];
            string contractPath = ContractPath(groupKey, index);
            CompileContextualSource(document, contract.Source, contract.Name, contractPath);
            CompileContextualTargets(document, contract.Forbidden, contract.Name, "forbidden", contractPath);
            CompileContextualTargets(document, contract.Exclude, contract.Name, "exclude", contractPath);
        }
    }

    private static void CompileContextAllowOnlyGroup(
        ArchitectureContractDocument document, List<ArchitectureContextAllowOnlyContract> contracts, string groupKey)
    {
        for (int index = 0; index < contracts.Count; index++)
        {
            ArchitectureContextAllowOnlyContract contract = contracts[index];
            string contractPath = ContractPath(groupKey, index);
            CompileContextualSource(document, contract.Source, contract.Name, contractPath);
            CompileContextualTargets(document, contract.Allowed, contract.Name, "allowed", contractPath);
            CompileContextualTargets(document, contract.Exclude, contract.Name, "exclude", contractPath);
        }
    }

    private static string ContractPath(string groupKey, int index) =>
        ArchitecturePolicyProvenancePath.AppendIndex(
            ArchitecturePolicyProvenancePath.AppendProperty(
                ArchitecturePolicyProvenancePath.Property(ContractsProperty), groupKey),
            index);

    private static void CompileLayerSelector(
        ArchitectureContractDocument document, ArchitectureLayerSelector? selector, string layerName, string path)
    {
        if (selector is null || string.IsNullOrEmpty(selector.When))
        {
            return;
        }

        CelCompilationResult<CelCompiledPredicate> result =
            ArchitectureExpressionSchemas.SelectorEnvironment.CompilePredicate(selector.When);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Layer '{layerName}' selector 'when' expression failed to compile: {Describe(result.Diagnostics)}");
        }

        selector.CompiledWhen = result.Program;
        document.Provenance.TryGetLocation(path, out ArchitecturePolicySourceLocation? location);
        selector.WhenLocation = location;
    }

    private static void CompileContextualSource(
        ArchitectureContractDocument document, ArchitectureContextSelector? source, string contractName, string contractPath)
    {
        if (source is null || string.IsNullOrEmpty(source.When))
        {
            return;
        }

        string path = ArchitecturePolicyProvenancePath.AppendProperty(contractPath, SourceProperty);
        document.Provenance.SetValidationSubject(path);

        CelCompilationResult<CelCompiledPredicate> result =
            ArchitectureExpressionSchemas.ContextualSourceEnvironment.CompilePredicate(source.When);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Contextual contract '{contractName}' declares a 'source.when' expression that failed to compile: " +
                $"{Describe(result.Diagnostics)}");
        }

        source.CompiledWhen = result.Program;
        document.Provenance.TryGetLocation(path, out ArchitecturePolicySourceLocation? location);
        source.WhenLocation = location;
        source.WhenContractName = contractName;
    }

    private static void CompileContextualTargets(
        ArchitectureContractDocument document,
        List<ArchitectureContextSelector>? selectors,
        string contractName,
        string fieldName,
        string contractPath)
    {
        if (selectors is null)
        {
            return;
        }

        for (int index = 0; index < selectors.Count; index++)
        {
            ArchitectureContextSelector selector = selectors[index];
            if (selector is null || string.IsNullOrEmpty(selector.When))
            {
                continue;
            }

            string path = ArchitecturePolicyProvenancePath.AppendIndex(
                ArchitecturePolicyProvenancePath.AppendProperty(contractPath, fieldName), index);
            document.Provenance.SetValidationSubject(path);

            if (ReferencesDependencyIdentifier(selector.When))
            {
                throw new InvalidOperationException(
                    $"Contextual contract '{contractName}' declares a '{fieldName}[{index}].when' expression that " +
                    "references 'dependency'. Dependency-edge facts (kind, viaMethodBody, sourceMemberName, " +
                    "targetMemberName) are not populated with real per-edge data in this release — every candidate " +
                    "would see the same fixed values, so a predicate reading them would silently never behave as " +
                    "intended. Remove the 'dependency' reference and express the constraint using 'source'/'target' " +
                    "facts instead.");
            }

            CelCompilationResult<CelCompiledPredicate> result =
                ArchitectureExpressionSchemas.ContextualTargetEnvironment.CompilePredicate(selector.When);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Contextual contract '{contractName}' declares a '{fieldName}[{index}].when' expression that " +
                    $"failed to compile: {Describe(result.Diagnostics)}");
            }

            selector.CompiledWhen = result.Program;
            document.Provenance.TryGetLocation(path, out ArchitecturePolicySourceLocation? location);
            selector.WhenLocation = location;
            selector.WhenContractName = contractName;
        }
    }

    private static string Describe(IReadOnlyList<CelDiagnostic> diagnostics)
    {
        return string.Join("; ", diagnostics.Select(Describe));
    }

    private static string Describe(CelDiagnostic diagnostic)
    {
        return diagnostic.Span is { } span
            ? $"{diagnostic.Code} at [{span.Start}, {span.End}): {diagnostic.Message}"
            : $"{diagnostic.Code}: {diagnostic.Message}";
    }
}
