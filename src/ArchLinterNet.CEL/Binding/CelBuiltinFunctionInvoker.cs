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
    /// <para>
    /// Cost must never *underestimate* an operation's actual work — that is the one property that
    /// makes <see cref="ArchLinterNet.CEL.Evaluation.CelEvaluationLimits.MaxCostUnits"/> a real
    /// budget rather than a decoration. Most overloads have an execution mechanism with a real,
    /// provable linear bound, so a fixed floor of <c>1</c> plus the UTF-16 length of every string
    /// operand scanned is exact enough: <see cref="StringComparison.Ordinal"/>
    /// <c>StartsWith</c>/<c>EndsWith</c> compare exactly one aligned prefix/suffix window (a single
    /// memory comparison, never re-scanned from a different offset), and <c>size()</c> on a
    /// <see cref="CelValue"/> string is one linear <c>Rune</c> pass.
    /// </para>
    /// <para>
    /// <see cref="CelFunctionOperationId.Contains"/> is the one exception: .NET's
    /// <c>string.Contains(string, StringComparison.Ordinal)</c> is a candidate-position substring
    /// search, not a single aligned comparison — on an adversarial receiver built from a repeating
    /// near-match prefix (e.g. <c>"aaaa...aaab"</c> searched for <c>"aaa...ab"</c>) it re-compares a
    /// long overlapping run at many candidate offsets, approaching <c>O(receiverLength *
    /// argumentLength)</c>. A linear estimate here would let a crafted input's real cost exceed its
    /// charged cost by orders of magnitude, defeating the budget. Its cost is therefore the
    /// conservative worst-case product of both operand lengths, not their sum — this can
    /// overcharge realistic calls but never undercharges, which is the only direction that's safe
    /// for a budget.  The product cannot overflow <see cref="long"/>: <see cref="string.Length"/>
    /// is an <see cref="int"/>, and <c>int.MaxValue * (long)int.MaxValue</c> is well within
    /// <see cref="long.MaxValue"/>.
    /// </para>
    /// <para>
    /// <see cref="CelFunctionOperationId.SizeList"/>, <see cref="CelFunctionOperationId.SizeMap"/>,
    /// and <see cref="CelFunctionOperationId.ContainsKey"/> are O(1) (backed by a count field / hash
    /// lookup) and cost only the fixed floor.
    /// </para>
    /// </remarks>
    /// <param name="operationId">Which catalog overload's cost to compute.</param>
    /// <param name="receiver">The receiver value that would be passed to <see cref="Invoke"/>.</param>
    /// <param name="arguments">The argument values that would be passed to <see cref="Invoke"/>.</param>
    public static long ComputeCost(CelFunctionOperationId operationId, CelValue? receiver, IReadOnlyList<CelValue> arguments) =>
        operationId switch
        {
            CelFunctionOperationId.StartsWith or CelFunctionOperationId.EndsWith =>
                FixedCost + arguments[0].AsString().Length,
            // Worst-case product, not a sum: string.Contains is a candidate-position search whose
            // real cost on an adversarial input approaches receiverLength * argumentLength — see
            // the method's <remarks> for why a linear estimate would be unsafe here.
            CelFunctionOperationId.Contains =>
                FixedCost + (long)Receiver(receiver).Length * arguments[0].AsString().Length,
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
