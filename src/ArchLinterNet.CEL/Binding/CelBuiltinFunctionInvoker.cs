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
    /// <see cref="CelFunctionOperationId.SizeList"/> and <see cref="CelFunctionOperationId.SizeMap"/>
    /// are O(1) (backed by a count field) and cost only the fixed floor.
    /// <see cref="CelFunctionOperationId.ContainsKey"/> is NOT O(1) despite being a dictionary
    /// lookup: <c>Dictionary&lt;string, _&gt;.ContainsKey</c> first computes
    /// <c>string.GetHashCode()</c>, which hashes the key's entire content (.NET's string hash —
    /// Marvin32 — is a linear pass over the string, not a cached/O(1) value), and a hash collision
    /// can then compare the key against other entries in the same bucket via <c>string.Equals</c> —
    /// and each of those comparisons can itself scan up to the full key length (a near-match
    /// colliding key fails late, not on the first character). A cost of
    /// <c>keyLength + entryCount</c> (comparison *count* only, ignoring each comparison's own
    /// length) would still undercharge an adversarial map by orders of magnitude: entry count
    /// near-match comparisons at up to key length each is <c>keyLength * entryCount</c> work, not
    /// <c>keyLength + entryCount</c>. Because <c>CelEvaluationContextBuilder.Set()</c> bounds
    /// map/list *entry count* but not individual *string length*, this is a real, exploitable gap,
    /// not a theoretical one. `ComputeCost` for `containsKey` is therefore the fixed floor plus the
    /// key's length times <c>(entryCount + 1)</c> — the <c>+1</c> covers the initial hash-computation
    /// pass over the key itself, and the <c>* entryCount</c> term is the conservative worst-case
    /// bound on collision-chain comparisons, each assumed to cost a full key-length scan. The
    /// product cannot overflow <see cref="long"/> under Profile v1's existing collection-size bound
    /// (<c>MaxValidationCollectionSize</c> caps entry count at 1024): even an
    /// <see cref="int.MaxValue"/>-length key times 1025 is far below <see cref="long.MaxValue"/>.
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
            // keyLength * (entryCount + 1): each of up to entryCount collision-chain comparisons
            // can itself scan the full key length, not just one character — see the method's
            // <remarks> for why a keyLength + entryCount sum would still be unsafe here.
            CelFunctionOperationId.ContainsKey =>
                FixedCost + (long)arguments[0].AsString().Length * (NonNull(receiver).AsMap().Count + 1),
            _ => throw new InvalidOperationException($"Unhandled built-in function operation '{operationId}'."),
        };

    private const long FixedCost = 1;

    private static string Receiver(CelValue? receiver) => NonNull(receiver).AsString();

    private static CelValue NonNull(CelValue? receiver) =>
        receiver ?? throw new InvalidOperationException(
            "Profile v1 declares no free-function overloads — every built-in call has a receiver.");
}
