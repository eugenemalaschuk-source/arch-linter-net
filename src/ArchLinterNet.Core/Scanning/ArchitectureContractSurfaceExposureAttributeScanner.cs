using System.Collections;
using System.Globalization;
using System.Reflection;

namespace ArchLinterNet.Core.Scanning;

// Owns compiled custom-attribute metadata, typed arguments, and stable occurrence ordering.
// Type-valued arguments recurse through the shared traversal rather than maintaining another
// result or recursion set.
internal sealed class ArchitectureContractSurfaceExposureAttributeScanner
{
    private readonly ArchitectureContractSurfaceExposureScanState _state;
    private readonly ArchitectureContractSurfaceExposureTraversal _traversal;

    internal ArchitectureContractSurfaceExposureAttributeScanner(
        ArchitectureContractSurfaceExposureScanState state,
        ArchitectureContractSurfaceExposureTraversal traversal)
    {
        _state = state;
        _traversal = traversal;
    }

    internal void Scan(MemberInfo member, ArchitectureContractExposurePath sitePath)
    {
        IReadOnlyList<CustomAttributeData> attributes = _state.TryReadAttributes(
            () => member.GetCustomAttributesData(), sitePath, "attributes-unavailable");
        ScanData(attributes, sitePath);
    }

    internal void Scan(ParameterInfo parameter, ArchitectureContractExposurePath sitePath)
    {
        IReadOnlyList<CustomAttributeData> attributes = _state.TryReadAttributes(
            () => parameter.GetCustomAttributesData(), sitePath, "attributes-unavailable");
        ScanData(attributes, sitePath);
    }

    private void ScanData(
        IReadOnlyList<CustomAttributeData> attributes,
        ArchitectureContractExposurePath sitePath)
    {
        Dictionary<string, int> attributeOccurrences = new(StringComparer.Ordinal);
        foreach (CustomAttributeData attribute in attributes.OrderBy(
            ArchitectureContractSurfaceExposureScanState.AttributeSortKey, StringComparer.Ordinal))
        {
            Type? attributeType = _state.TryRead(
                () => attribute.AttributeType, sitePath, "attribute-type-unavailable");
            if (attributeType == null)
            {
                continue;
            }

            // AttributeSortKey includes normalized metadata arguments, so the occurrence ordinal
            // stays stable even if reflection enumerates AllowMultiple attributes differently.
            string attributeTypeSortKey = ArchitectureContractSurfaceExposureScanState.TypeSortKey(attributeType);
            int occurrence = attributeOccurrences.TryGetValue(attributeTypeSortKey, out int previous)
                ? previous
                : 0;
            attributeOccurrences[attributeTypeSortKey] = occurrence + 1;
            ArchitectureContractExposurePath attributePath = sitePath.Append(
                "attribute", $"{attributeTypeSortKey}:{occurrence}");
            _state.AddExposure(attributePath, attributeType);
            IList<CustomAttributeTypedArgument> constructorArguments = _state.TryRead(
                () => attribute.ConstructorArguments, attributePath, "attribute-arguments-unavailable")
                ?? Array.Empty<CustomAttributeTypedArgument>();
            for (int index = 0; index < constructorArguments.Count; index++)
            {
                ScanArgument(constructorArguments[index], attributePath.Append(
                    "attribute_argument", $"constructor:{index}"));
            }

            IList<CustomAttributeNamedArgument> namedArguments = _state.TryRead(
                () => attribute.NamedArguments, attributePath, "attribute-named-arguments-unavailable")
                ?? Array.Empty<CustomAttributeNamedArgument>();
            foreach (CustomAttributeNamedArgument named in namedArguments.OrderBy(
                argument => argument.MemberName, StringComparer.Ordinal))
            {
                ScanArgument(named.TypedValue, attributePath.Append(
                    "attribute_argument", $"named:{named.MemberName}"));
            }
        }
    }

    private void ScanArgument(
        CustomAttributeTypedArgument argument,
        ArchitectureContractExposurePath path)
    {
        Type? argumentType = _state.TryRead(
            () => argument.ArgumentType, path, "attribute-argument-type-unavailable");
        if (argumentType == null)
        {
            return;
        }

        if (argumentType.IsArray)
        {
            Type? elementType = _state.TryRead(
                () => argumentType.GetElementType(), path, "attribute-array-element-type-unavailable");
            if (elementType != null && _state.TryRead(
                    () => elementType.IsEnum, path, "attribute-array-element-type-unavailable"))
            {
                // The declared element type is semantic evidence even when the metadata array
                // has no values to scan.
                _state.AddExposure(path, elementType);
            }

            object? value = _state.TryRead(
                () => argument.Value, path, "attribute-array-value-unavailable");
            if (value is IEnumerable values)
            {
                int index = 0;
                foreach (object? item in values)
                {
                    if (item is CustomAttributeTypedArgument typed)
                    {
                        ScanArgument(typed, path.Append(
                            "array_element", index.ToString(CultureInfo.InvariantCulture)));
                    }

                    index++;
                }
            }

            return;
        }

        if (argumentType.IsEnum)
        {
            _state.AddExposure(path, argumentType);
            return;
        }

        if (argumentType == typeof(Type))
        {
            object? value = _state.TryRead(
                () => argument.Value, path, "attribute-type-value-unavailable");
            if (value is Type referenced)
            {
                _traversal.ScanShape(referenced, path);
            }
        }
        // Primitive, string, and null values deliberately do not become type targets.
    }
}
