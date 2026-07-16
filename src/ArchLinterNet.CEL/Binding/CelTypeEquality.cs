using ArchLinterNet.CEL.Schema;

namespace ArchLinterNet.CEL.Binding;

/// <summary>
/// Full structural equality for <see cref="CelType"/> — used where a binder rule requires an exact
/// type match (e.g. the <c>in</c> operator's <c>T in List&lt;T&gt;</c> element-type check), as
/// opposed to a same-<see cref="CelTypeKind"/>-only check (e.g. <c>==</c>/<c>!=</c>).
/// </summary>
internal static class CelTypeEquality
{
    public static bool AreEqual(CelType a, CelType b)
    {
        if (a.Kind != b.Kind)
            return false;
        return a.Kind switch
        {
            CelTypeKind.List => AreEqual(a.ElementType!, b.ElementType!),
            CelTypeKind.Map => AreEqual(a.ValueType!, b.ValueType!),
            CelTypeKind.Object => string.Equals(a.SchemaId, b.SchemaId, StringComparison.Ordinal),
            _ => true,
        };
    }
}
