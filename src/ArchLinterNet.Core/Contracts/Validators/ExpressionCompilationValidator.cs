using System.Text.RegularExpressions;
using ArchLinterNet.CEL;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.Core.Contracts.Expressions;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Contracts.PolicyImports;

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
internal sealed class ExpressionCompilationValidator : IArchitecturePolicyDocumentValidator
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
    // Deliberately a bare `\bdependency\b`, not `\bdependency\.` — CEL's grammar allows postfix
    // member access after any parenthesized expression, so `(dependency).viaMethodBody` still
    // reaches the same root variable while never producing the literal substring `dependency.`.
    // Since CEL has no reflection, string-based dynamic member access, or way to alias an
    // identifier, the token `dependency` can only ever appear in valid CEL source text by
    // literally naming this root variable — so matching the bare identifier, with no assumption
    // about what (if anything) follows it, is what actually closes the bypass. The trade-off is
    // reduced precision: this also rejects a `when` that happens to reference the word
    // "dependency" inside a string literal (e.g. a metadata value check). That is an acceptable,
    // deliberately fail-closed trade-off for a rejection whose entire purpose is to prevent a
    // predicate from silently reading facts that don't vary per edge.
    private static readonly Regex _dependencyMemberReferencePattern =
        new(@"\bdependency\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public void Validate(ArchitectureContractDocument document)
    {
        foreach ((string layerName, ArchitectureLayer layer) in document.Layers)
        {
            string path = ArchitecturePolicyProvenancePath.AppendProperty(
                ArchitecturePolicyProvenancePath.AppendProperty(
                    ArchitecturePolicyProvenancePath.Property(LayersProperty), layerName),
                SelectorProperty);
            document.Provenance.SetValidationSubject(path);
            CompileLayerSelector(layer.Selector, layerName, path);
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

    private static void CompileLayerSelector(ArchitectureLayerSelector? selector, string layerName, string path)
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
        selector.WhenLocation = path;
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
        source.WhenLocation = path;
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

            if (_dependencyMemberReferencePattern.IsMatch(selector.When))
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
            selector.WhenLocation = path;
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
