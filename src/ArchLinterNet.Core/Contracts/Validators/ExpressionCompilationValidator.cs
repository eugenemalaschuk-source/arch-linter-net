using ArchLinterNet.CEL;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.Core.Contracts.Expressions;
using ArchLinterNet.Core.Contracts.Families;

namespace ArchLinterNet.Core.Contracts.Validators;

// Compiles every declared `when` field through the public ArchLinterNet.CEL API at policy-load
// time, per openspec/specs/cel-policy-model/spec.md: compilation happens once here (never during
// contract execution), forces a boolean predicate result, and fails the policy load on any
// CelDiagnostic. A selector with no `when` never touches the CEL engine (literal-only fast path).
// See openspec/changes/core-cel-integration/design.md.
internal sealed class ExpressionCompilationValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        foreach ((string layerName, ArchitectureLayer layer) in document.Layers)
        {
            document.Provenance.SetValidationSubject(layer);
            CompileLayerSelector(layer.Selector, layerName);
        }

        foreach (ArchitectureContextDependencyContract contract in document.Provenance.Track(
                     document.Contracts.StrictContextDependencies.Concat(document.Contracts.AuditContextDependencies)))
        {
            CompileContextualSource(contract.Source, contract.Name);
            CompileContextualTargets(contract.Forbidden, contract.Name, "forbidden");
            CompileContextualTargets(contract.Exclude, contract.Name, "exclude");
        }

        foreach (ArchitectureContextAllowOnlyContract contract in document.Provenance.Track(
                     document.Contracts.StrictContextAllowOnly.Concat(document.Contracts.AuditContextAllowOnly)))
        {
            CompileContextualSource(contract.Source, contract.Name);
            CompileContextualTargets(contract.Allowed, contract.Name, "allowed");
            CompileContextualTargets(contract.Exclude, contract.Name, "exclude");
        }
    }

    private static void CompileLayerSelector(ArchitectureLayerSelector? selector, string layerName)
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
    }

    private static void CompileContextualSource(ArchitectureContextSelector? source, string contractName)
    {
        if (source is null || string.IsNullOrEmpty(source.When))
        {
            return;
        }

        CelCompilationResult<CelCompiledPredicate> result =
            ArchitectureExpressionSchemas.ContextualSourceEnvironment.CompilePredicate(source.When);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Contextual contract '{contractName}' declares a 'source.when' expression that failed to compile: " +
                $"{Describe(result.Diagnostics)}");
        }

        source.CompiledWhen = result.Program;
    }

    private static void CompileContextualTargets(
        List<ArchitectureContextSelector>? selectors, string contractName, string fieldName)
    {
        if (selectors is null)
        {
            return;
        }

        foreach (ArchitectureContextSelector selector in selectors)
        {
            if (selector is null || string.IsNullOrEmpty(selector.When))
            {
                continue;
            }

            CelCompilationResult<CelCompiledPredicate> result =
                ArchitectureExpressionSchemas.ContextualTargetEnvironment.CompilePredicate(selector.When);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Contextual contract '{contractName}' declares a '{fieldName}.when' expression that failed to " +
                    $"compile: {Describe(result.Diagnostics)}");
            }

            selector.CompiledWhen = result.Program;
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
