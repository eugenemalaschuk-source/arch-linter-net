using System.Reflection;

namespace ArchLinterNet.Core.Scanning;

// Owns recursive type relationships, generic parameters and constraints, type shapes, delegates,
// and visible nested types. Member and attribute facts are delegated to their focused scanners,
// while all recursive branches use the same state-owned active-type set.
internal sealed class ArchitectureContractSurfaceExposureTraversal
{
    private readonly ArchitectureContractSurfaceExposureScanState _state;
    private readonly ArchitectureContractSurfaceShape _surfaceShape;
    private readonly ArchitectureContractSurfaceExposureAttributeScanner _attributeScanner;
    private readonly ArchitectureContractSurfaceExposureMemberScanner _memberScanner;

    internal ArchitectureContractSurfaceExposureTraversal(
        ArchitectureContractSurfaceExposureScanState state,
        ArchitectureContractSurfaceShape surfaceShape)
    {
        _state = state;
        _surfaceShape = surfaceShape;
        _attributeScanner = new(state, this);
        _memberScanner = new(state, surfaceShape, this, _attributeScanner);
    }

    internal void ScanDeclaredType(Type type, ArchitectureContractExposurePath path)
    {
        if (!_state.EnterType(type))
        {
            return;
        }

        try
        {
            ScanTypeRelationships(type, path);
            _attributeScanner.Scan(type, path);
            ScanGenericParameters(type, path);
            _memberScanner.Scan(type, path);
            ScanNestedTypes(type, path);
        }
        catch (Exception exception) when (ArchitectureContractSurfaceExposureScanState.IsReflectionFailure(exception))
        {
            _state.AddIncomplete(path, "type-scan-failed");
        }
        finally
        {
            _state.ExitType(type);
        }
    }

    internal void ScanShape(Type type, ArchitectureContractExposurePath path)
    {
        if (type.IsGenericParameter)
        {
            _attributeScanner.Scan(type, path);
            ScanConstraints(type, path);
            return;
        }

        Type targetType = type;
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            targetType = _state.TryRead(() => type.GetGenericTypeDefinition(), path,
                "generic-definition-unavailable") ?? type;
        }

        _state.AddExposure(path, targetType);
        if (!_state.EnterType(type))
        {
            return;
        }

        try
        {
            if (type.IsByRef || type.IsPointer || type.IsArray)
            {
                Type? element = _state.TryRead(() => type.GetElementType(), path, "element-type-unavailable");
                if (element != null)
                {
                    string kind = type.IsArray ? "array_element" : type.IsPointer ? "pointer_element" : "byref_element";
                    ScanShape(element, path.Append(kind));
                }
            }

            if (type.IsGenericType)
            {
                Type[] arguments = _state.TryReadArray(
                    () => type.GetGenericArguments(), path.Append("generic_argument"),
                    "generic-arguments-unavailable");
                bool nullable = IsNullable(type);
                bool tuple = IsTuple(type);
                for (int index = 0; index < arguments.Length; index++)
                {
                    string kind = nullable ? "nullable_underlying" : tuple ? "tuple_element" : "generic_argument";
                    ArchitectureContractExposurePath childPath = path.Append(
                        kind, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    ScanShape(arguments[index], childPath);
                }
            }

            if (typeof(Delegate).IsAssignableFrom(type))
            {
                ScanDelegateInvoke(type, path);
            }
        }
        catch (Exception exception) when (ArchitectureContractSurfaceExposureScanState.IsReflectionFailure(exception))
        {
            _state.AddIncomplete(path, "shape-scan-failed");
        }
        finally
        {
            _state.ExitType(type);
        }
    }

    internal void ScanConstraints(Type parameter, ArchitectureContractExposurePath path)
    {
        Type[] constraints = _state.TryReadArray(
            () => parameter.GetGenericParameterConstraints(), path.Append("constraint"),
            "generic-constraints-unavailable");
        foreach (Type constraint in constraints.OrderBy(ArchitectureContractSurfaceExposureScanState.TypeSortKey, StringComparer.Ordinal))
        {
            ScanShape(constraint, path.Append(
                "constraint", ArchitectureContractSurfaceExposureScanState.TypeSortKey(constraint)));
        }
    }

    private void ScanTypeRelationships(Type type, ArchitectureContractExposurePath path)
    {
        Type? baseType = _state.TryRead(() => type.BaseType, path.Append("base_type"), "base-type-unavailable");
        if (baseType != null)
        {
            ScanShape(baseType, path.Append("base_type"));
        }

        Type[] interfaces = _state.TryReadArray(
            () => type.GetInterfaces(), path.Append("interface"), "interfaces-unavailable");
        foreach (Type implemented in interfaces.OrderBy(ArchitectureContractSurfaceExposureScanState.TypeSortKey, StringComparer.Ordinal))
        {
            ScanShape(implemented, path.Append(
                "interface", ArchitectureContractSurfaceExposureScanState.TypeSortKey(implemented)));
        }

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            Type[] arguments = _state.TryReadArray(
                () => type.GetGenericArguments(), path.Append("generic_argument"),
                "generic-arguments-unavailable");
            for (int index = 0; index < arguments.Length; index++)
            {
                ScanShape(arguments[index], path.Append(
                    "generic_argument", index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
        }
    }

    private void ScanGenericParameters(Type type, ArchitectureContractExposurePath path)
    {
        Type[] parameters = _state.TryReadArray(
            () => type.GetGenericArguments(), path.Append("generic_parameter"),
            "generic-parameters-unavailable");
        for (int index = 0; index < parameters.Length; index++)
        {
            Type parameter = parameters[index];
            ArchitectureContractExposurePath parameterPath = path.Append(
                "generic_parameter", index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _attributeScanner.Scan(parameter, parameterPath);
            ScanConstraints(parameter, parameterPath);
        }
    }

    private void ScanDelegateInvoke(Type delegateType, ArchitectureContractExposurePath delegatePath)
    {
        MethodInfo? invoke = _state.TryRead(
            () => delegateType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance),
            delegatePath.Append("delegate_invoke"), "delegate-invoke-unavailable");
        if (invoke == null)
        {
            return;
        }

        ArchitectureContractExposurePath invokePath = delegatePath.Append("delegate_invoke", "Invoke");
        _attributeScanner.Scan(invoke, invokePath);
        _memberScanner.ScanParameters(invoke, invokePath);
        _memberScanner.ScanReturn(invoke, invokePath);
    }

    private void ScanNestedTypes(Type type, ArchitectureContractExposurePath typePath)
    {
        Type[] nestedTypes = _state.TryReadArray(
            () => type.GetNestedTypes(ArchitectureContractSurfaceExposureMemberScanner.MemberFlags),
            typePath.Append("nested_type"), "nested-types-unavailable");
        foreach (Type nested in nestedTypes.OrderBy(ArchitectureContractSurfaceExposureScanState.TypeSortKey, StringComparer.Ordinal))
        {
            if (!IsNestedTypeVisible(nested, typePath) || _memberScanner.IsCompilerGenerated(nested, typePath))
            {
                continue;
            }

            ArchitectureContractExposurePath nestedPath = typePath.Append(
                "nested_type", ArchitectureContractSurfaceExposureScanState.TypeSortKey(nested));
            _state.AddExposure(nestedPath, nested);
        }
    }

    private bool IsNestedTypeVisible(Type type, ArchitectureContractExposurePath path)
    {
        try
        {
            Type? current = type;
            while (current != null && !ReferenceEquals(current, _state.RootType))
            {
                if (!_surfaceShape.Includes(current))
                {
                    return false;
                }

                current = current.DeclaringType;
            }

            return current != null;
        }
        catch (Exception exception) when (ArchitectureContractSurfaceExposureScanState.IsReflectionFailure(exception))
        {
            _state.AddIncomplete(path, "nested-type-visibility-unavailable");
            return false;
        }
    }

    private static bool IsNullable(Type type) => type.IsGenericType &&
        type.GetGenericTypeDefinition() == typeof(Nullable<>);

    private static bool IsTuple(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        string name = type.GetGenericTypeDefinition().FullName ?? string.Empty;
        return name is "System.ValueTuple`1" or "System.ValueTuple`2" or "System.ValueTuple`3"
            or "System.ValueTuple`4" or "System.ValueTuple`5" or "System.ValueTuple`6"
            or "System.ValueTuple`7" or "System.ValueTuple`8";
    }
}
