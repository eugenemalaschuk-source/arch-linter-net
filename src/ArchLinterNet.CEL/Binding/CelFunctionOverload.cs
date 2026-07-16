using ArchLinterNet.CEL.Schema;

namespace ArchLinterNet.CEL.Binding;

/// <summary>
/// A single resolved overload from the closed Profile v1 built-in function catalog.
/// </summary>
internal sealed class CelFunctionOverload
{
    /// <summary>Gets the function name as it appears in a call expression.</summary>
    public string FunctionName { get; }

    /// <summary>
    /// Gets the required receiver type kind for this overload. Profile v1 declares no
    /// free-function overloads, so this is never <c>null</c> for a catalog entry, but the binder
    /// treats <c>null</c> generically as "no receiver required" should a future profile version
    /// add one.
    /// </summary>
    public CelTypeKind? ReceiverKind { get; }

    /// <summary>Gets the required argument type kinds, in order.</summary>
    public IReadOnlyList<CelTypeKind> ArgumentKinds { get; }

    /// <summary>Gets the static result type produced by this overload.</summary>
    public CelType ResultType { get; }

    public CelFunctionOverload(
        string functionName,
        CelTypeKind? receiverKind,
        IReadOnlyList<CelTypeKind> argumentKinds,
        CelType resultType)
    {
        FunctionName = functionName;
        ReceiverKind = receiverKind;
        ArgumentKinds = argumentKinds;
        ResultType = resultType;
    }
}
