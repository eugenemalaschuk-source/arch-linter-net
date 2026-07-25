namespace ArchLinterNet.Core.Model;

// Wraps any exception thrown while evaluating an already-built ArchitectureAnalysisSnapshot
// (contract execution, expression evaluation, etc. — see
// ArchitectureAnalysisSnapshot.Evaluate) with the policy-import and resolved-assembly paths this
// invocation had already loaded by that point. Hosts that report this exception via a --report
// file sink need that inventory to avoid overwriting one of those inputs with the error document,
// the same way a policy-load failure's own diagnostic already protects its inputs.
public sealed class ArchitectureAnalysisEvaluationException : InvalidOperationException
{
    public ArchitectureAnalysisEvaluationException(
        string message,
        Exception innerException,
        IReadOnlyList<string> policyImportPaths,
        IReadOnlyList<string> resolvedAssemblyPaths)
        : base(message, innerException)
    {
        PolicyImportPaths = policyImportPaths;
        ResolvedAssemblyPaths = resolvedAssemblyPaths;
    }

    public IReadOnlyList<string> PolicyImportPaths { get; }

    public IReadOnlyList<string> ResolvedAssemblyPaths { get; }
}
