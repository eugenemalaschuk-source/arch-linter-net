using ArchLinterNet.CEL.Schema;

namespace ArchLinterNet.CEL.Binding;

/// <summary>
/// Renders a <see cref="CelType"/> as a stable, PascalCase display string for use in diagnostic
/// <c>expectedType</c>/<c>actualType</c> parameters — distinct from <see cref="CelType.ToString"/>,
/// which is a lowercase display form intended for human-readable messages, not machine parameters.
/// </summary>
internal static class CelTypeDisplay
{
    public static string Describe(CelType type) => type.Kind switch
    {
        CelTypeKind.List => $"List<{Describe(type.ElementType!)}>",
        CelTypeKind.Map => $"Map<String, {Describe(type.ValueType!)}>",
        CelTypeKind.Object => $"Object({type.SchemaId})",
        _ => type.Kind.ToString(),
    };
}
