using ArchLinterNet.CEL.Values;

namespace ArchLinterNet.CEL.Binding;

/// <summary>
/// Pure, side-effect-free execution of the closed Profile v1 built-in function catalog
/// (<see cref="CelFunctionCatalog"/>), keyed by <see cref="CelFunctionOperationId"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every catalog overload is total given a receiver/argument shape the binder has already proven
/// correct (<see cref="CelBinder"/> resolves a <c>CelCallSyntax</c> against exactly one
/// <see cref="CelFunctionOverload"/> before this type is ever reached) — none of the seven Profile
/// v1 overloads has a runtime failure case, so <see cref="Invoke"/> returns a <see cref="CelValue"/>
/// directly with no failure/diagnostic channel.
/// </para>
/// <para>
/// Stateless and holds no mutable data — the evaluator (#328) calls <see cref="Invoke"/> and
/// <see cref="ComputeCost"/> using the operation identifier carried on the bound call's resolved
/// overload, never by re-dispatching on the function name string. Keeping both the pure
/// implementation and its cost model in this one file — one <c>case</c> per operation id in each
/// method — is what makes "add one standard function" a single reviewed change instead of also
/// requiring the evaluator to invent its own per-function cost logic.
/// </para>
/// </remarks>
internal static class CelBuiltinFunctionInvoker
{
    /// <summary>
    /// Executes the built-in function identified by <paramref name="operationId"/> against
    /// <paramref name="receiver"/> and <paramref name="arguments"/>.
    /// </summary>
    /// <param name="operationId">Which catalog overload to execute.</param>
    /// <param name="receiver">
    /// The receiver value. Non-null for every current Profile v1 overload, since the catalog
    /// declares no free-function overloads.
    /// </param>
    /// <param name="arguments">The call's argument values, already type-checked by the binder.</param>
    public static CelValue Invoke(CelFunctionOperationId operationId, CelValue? receiver, IReadOnlyList<CelValue> arguments) =>
        operationId switch
        {
            CelFunctionOperationId.StartsWith => CelValue.Bool(
                Receiver(receiver).StartsWith(arguments[0].AsString(), StringComparison.Ordinal)),
            CelFunctionOperationId.EndsWith => CelValue.Bool(
                Receiver(receiver).EndsWith(arguments[0].AsString(), StringComparison.Ordinal)),
            CelFunctionOperationId.Contains => CelValue.Bool(
                Receiver(receiver).Contains(arguments[0].AsString(), StringComparison.Ordinal)),
            CelFunctionOperationId.SizeString => CelValue.Int(Receiver(receiver).EnumerateRunes().Count()),
            CelFunctionOperationId.SizeList => CelValue.Int(NonNull(receiver).AsList().Count),
            CelFunctionOperationId.SizeMap => CelValue.Int(NonNull(receiver).AsMap().Count),
            CelFunctionOperationId.ContainsKey => CelValue.Bool(NonNull(receiver).AsMap().ContainsKey(arguments[0].AsString())),
            _ => throw new InvalidOperationException($"Unhandled built-in function operation '{operationId}'."),
        };

    /// <summary>
    /// Computes the abstract cost (in <see cref="ArchLinterNet.CEL.Evaluation.CelEvaluationLimits.MaxCostUnits"/>
    /// units) that executing this operation against this exact receiver/argument shape will incur,
    /// so the evaluator (#328) can charge a call's true, input-size-proportional cost against its
    /// budget before or after invoking it, instead of treating every built-in call as a fixed unit
    /// cost regardless of how much data it scans.
    /// </summary>
    /// <remarks>
    /// A linear, input-size-proportional heuristic: cost is 1 (a fixed per-call floor) plus the
    /// UTF-16 length of every string operand scanned by the operation. This is a deliberate
    /// approximation, not each operation's exact worst-case algorithmic complexity (e.g. naive
    /// substring search is technically <c>O(n*m)</c>) — a linear model is the same choice CEL's
    /// reference cost estimation makes for string functions, and it is enough to stop a single call
    /// against an oversized string from being charged as if it were free, which is the actual
    /// budget-safety property <see cref="ArchLinterNet.CEL.Evaluation.CelEvaluationLimits.MaxCostUnits"/> exists to
    /// provide. <see cref="CelFunctionOperationId.SizeList"/>, <see cref="CelFunctionOperationId.SizeMap"/>,
    /// and <see cref="CelFunctionOperationId.ContainsKey"/> are O(1) (backed by a count field / hash
    /// lookup) and cost only the fixed floor.
    /// </remarks>
    /// <param name="operationId">Which catalog overload's cost to compute.</param>
    /// <param name="receiver">The receiver value that would be passed to <see cref="Invoke"/>.</param>
    /// <param name="arguments">The argument values that would be passed to <see cref="Invoke"/>.</param>
    public static long ComputeCost(CelFunctionOperationId operationId, CelValue? receiver, IReadOnlyList<CelValue> arguments) =>
        operationId switch
        {
            CelFunctionOperationId.StartsWith or CelFunctionOperationId.EndsWith =>
                FixedCost + arguments[0].AsString().Length,
            CelFunctionOperationId.Contains =>
                FixedCost + Receiver(receiver).Length + arguments[0].AsString().Length,
            CelFunctionOperationId.SizeString => FixedCost + Receiver(receiver).Length,
            CelFunctionOperationId.SizeList => FixedCost,
            CelFunctionOperationId.SizeMap => FixedCost,
            CelFunctionOperationId.ContainsKey => FixedCost,
            _ => throw new InvalidOperationException($"Unhandled built-in function operation '{operationId}'."),
        };

    private const long FixedCost = 1;

    private static string Receiver(CelValue? receiver) => NonNull(receiver).AsString();

    private static CelValue NonNull(CelValue? receiver) =>
        receiver ?? throw new InvalidOperationException(
            "Profile v1 declares no free-function overloads — every built-in call has a receiver.");
}
