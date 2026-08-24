using ArchLinterNet.Core.PolicyContext;
using static ArchLinterNet.Core.PolicyWeakening.ArchitecturePolicyWeakeningComparisonSupport;

namespace ArchLinterNet.Core.PolicyWeakening;

internal static class ArchitecturePolicyWeakeningExceptionEvaluator
{
    private static readonly StringComparer _comparer = StringComparer.Ordinal;

    internal static void Evaluate(
        ArchitecturePolicyContextExport baseline,
        ArchitecturePolicyContextExport current,
        string severity,
        ICollection<ArchitecturePolicyWeakeningFinding> findings)
    {
        HashSet<string> baseExceptions = baseline.Exceptions.Select(ExceptionKey).ToHashSet(_comparer);
        foreach (ArchitecturePolicyContextException exceptionItem in current.Exceptions
                     .Where(item => !baseExceptions.Contains(ExceptionKey(item)))
                     .OrderBy(ExceptionKey, _comparer))
        {
            if (IsUniversalException(exceptionItem))
            {
                findings.Add(CreateFinding(
                    "universal_exception_added",
                    ExceptionControl(exceptionItem),
                    "semantic",
                    severity,
                    Array.Empty<string>(),
                    [exceptionItem.Details],
                    null,
                    null,
                    Array.Empty<string>(),
                    exceptionItem.Reason));
            }
            else if (IsBroadExceptionName(exceptionItem))
            {
                findings.Add(CreateFinding(
                    "broad_exception_impact_not_proven",
                    ExceptionControl(exceptionItem),
                    "impact_not_proven",
                    severity,
                    Array.Empty<string>(),
                    [exceptionItem.Details],
                    null,
                    null,
                    Array.Empty<string>(),
                    exceptionItem.Reason));
            }
        }
    }
}
