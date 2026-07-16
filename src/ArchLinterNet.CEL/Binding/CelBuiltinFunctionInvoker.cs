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
/// Stateless and holds no mutable data — a future evaluator (not yet implemented) calls
/// <see cref="Invoke"/> using the operation identifier carried on the bound call's resolved
/// overload, never by re-dispatching on the function name string.
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

    private static string Receiver(CelValue? receiver) => NonNull(receiver).AsString();

    private static CelValue NonNull(CelValue? receiver) =>
        receiver ?? throw new InvalidOperationException(
            "Profile v1 declares no free-function overloads — every built-in call has a receiver.");
}
