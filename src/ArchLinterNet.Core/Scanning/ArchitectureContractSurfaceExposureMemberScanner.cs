using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ArchLinterNet.Core.Scanning;

// Owns visible constructors, methods, properties, fields, events, accessor metadata, parameters,
// returns, and method generic parameters. It delegates type-shaped evidence to the traversal and
// metadata evidence to the attribute scanner while preserving the original member paths.
internal sealed class ArchitectureContractSurfaceExposureMemberScanner
{
    internal const BindingFlags MemberFlags =
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static;

    private readonly ArchitectureContractSurfaceExposureScanState _state;
    private readonly ArchitectureContractSurfaceShape _surfaceShape;
    private readonly ArchitectureContractSurfaceExposureTraversal _traversal;
    private readonly ArchitectureContractSurfaceExposureAttributeScanner _attributes;

    internal ArchitectureContractSurfaceExposureMemberScanner(
        ArchitectureContractSurfaceExposureScanState state,
        ArchitectureContractSurfaceShape surfaceShape,
        ArchitectureContractSurfaceExposureTraversal traversal,
        ArchitectureContractSurfaceExposureAttributeScanner attributes)
    {
        _state = state;
        _surfaceShape = surfaceShape;
        _traversal = traversal;
        _attributes = attributes;
    }

    internal void Scan(Type type, ArchitectureContractExposurePath typePath)
    {
        ScanConstructors(type, typePath);
        ScanMethods(type, typePath);
        ScanProperties(type, typePath);
        ScanFields(type, typePath);
        ScanEvents(type, typePath);
    }

    internal void ScanParameters(MethodBase method, ArchitectureContractExposurePath memberPath)
    {
        ParameterInfo[] parameters = _state.TryReadArray(
            () => method.GetParameters(), memberPath.Append("parameter"), "parameters-unavailable");
        ScanParameters(parameters, memberPath);
    }

    internal void ScanParameters(
        IEnumerable<ParameterInfo> parameters,
        ArchitectureContractExposurePath memberPath)
    {
        int index = 0;
        foreach (ParameterInfo parameter in parameters)
        {
            ArchitectureContractExposurePath parameterPath = memberPath.Append(
                "parameter", index.ToString(CultureInfo.InvariantCulture));
            _attributes.Scan(parameter, parameterPath);
            Type? parameterType = _state.TryRead(
                () => parameter.ParameterType, parameterPath, "parameter-type-unavailable");
            if (parameterType != null)
            {
                _traversal.ScanShape(parameterType, parameterPath);
            }

            index++;
        }
    }

    internal void ScanReturn(MethodInfo method, ArchitectureContractExposurePath memberPath)
    {
        ArchitectureContractExposurePath returnPath = memberPath.Append("return");
        ParameterInfo? returnParameter = _state.TryRead(
            () => method.ReturnParameter, returnPath, "return-parameter-unavailable");
        if (returnParameter == null)
        {
            return;
        }

        _attributes.Scan(returnParameter, returnPath);
        Type? returnType = _state.TryRead(
            () => method.ReturnType, returnPath, "return-type-unavailable");
        if (returnType != null)
        {
            _traversal.ScanShape(returnType, returnPath);
        }
    }

    internal bool IsCompilerGenerated(MemberInfo member, ArchitectureContractExposurePath path)
    {
        try
        {
            return member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
        }
        catch (Exception exception) when (ArchitectureContractSurfaceExposureScanState.IsReflectionFailure(exception))
        {
            _state.AddIncomplete(path, "compiler-generated-metadata-unavailable");
            return false;
        }
    }

    private void ScanConstructors(Type type, ArchitectureContractExposurePath typePath)
    {
        ConstructorInfo[] constructors = _state.TryReadArray(
            () => type.GetConstructors(MemberFlags), typePath.Append("member", "constructors"),
            "constructors-unavailable");
        foreach (ConstructorInfo constructor in constructors.OrderBy(
            ArchitectureContractSurfaceExposureScanState.MemberSortKey, StringComparer.Ordinal))
        {
            if (!_surfaceShape.Includes(constructor) || IsCompilerGenerated(constructor, typePath))
            {
                continue;
            }

            ArchitectureContractExposurePath memberPath = typePath.Append(
                "member", ArchitectureContractSurfaceExposureScanState.MemberSortKey(constructor));
            _attributes.Scan(constructor, memberPath);
            ScanParameters(constructor, memberPath);
        }
    }

    private void ScanMethods(Type type, ArchitectureContractExposurePath typePath)
    {
        MethodInfo[] methods = _state.TryReadArray(
            () => type.GetMethods(MemberFlags), typePath.Append("member", "methods"),
            "methods-unavailable");
        foreach (MethodInfo method in methods.OrderBy(
            ArchitectureContractSurfaceExposureScanState.MemberSortKey, StringComparer.Ordinal))
        {
            if (!_surfaceShape.Includes(method) || IsCompilerGenerated(method, typePath) ||
                (method.IsSpecialName && IsAccessor(method.Name)))
            {
                continue;
            }

            ArchitectureContractExposurePath memberPath = typePath.Append(
                "member", ArchitectureContractSurfaceExposureScanState.MemberSortKey(method));
            _attributes.Scan(method, memberPath);
            ScanParameters(method, memberPath);
            ScanReturn(method, memberPath);
            ScanMethodGenericParameters(method, memberPath);
        }
    }

    private void ScanMethodGenericParameters(MethodInfo method, ArchitectureContractExposurePath memberPath)
    {
        Type[] parameters = _state.TryReadArray(
            () => method.GetGenericArguments(), memberPath.Append("generic_parameter"),
            "method-generic-parameters-unavailable");
        for (int index = 0; index < parameters.Length; index++)
        {
            ArchitectureContractExposurePath parameterPath = memberPath.Append(
                "generic_parameter", index.ToString(CultureInfo.InvariantCulture));
            _attributes.Scan(parameters[index], parameterPath);
            _traversal.ScanConstraints(parameters[index], parameterPath);
        }
    }

    private void ScanProperties(Type type, ArchitectureContractExposurePath typePath)
    {
        PropertyInfo[] properties = _state.TryReadArray(
            () => type.GetProperties(MemberFlags), typePath.Append("member", "properties"),
            "properties-unavailable");
        foreach (PropertyInfo property in properties.OrderBy(
            ArchitectureContractSurfaceExposureScanState.MemberSortKey, StringComparer.Ordinal))
        {
            MethodInfo? getter = _state.TryRead(
                () => property.GetGetMethod(nonPublic: true), typePath, "property-getter-unavailable");
            MethodInfo? setter = _state.TryRead(
                () => property.GetSetMethod(nonPublic: true), typePath, "property-setter-unavailable");
            if ((!_surfaceShape.Includes(getter) && !_surfaceShape.Includes(setter)) ||
                IsCompilerGenerated(property, typePath))
            {
                continue;
            }

            ArchitectureContractExposurePath memberPath = typePath.Append(
                "member", ArchitectureContractSurfaceExposureScanState.MemberSortKey(property));
            _attributes.Scan(property, memberPath);
            Type? propertyType = _state.TryRead(
                () => property.PropertyType, memberPath.Append("return"), "property-type-unavailable");
            if (propertyType != null)
            {
                _traversal.ScanShape(propertyType, memberPath.Append("return"));
            }

            ParameterInfo[] parameters = _state.TryReadArray(
                () => property.GetIndexParameters(), memberPath.Append("parameter"),
                "property-parameters-unavailable");
            ScanParameters(parameters, memberPath);
            if (getter != null && _surfaceShape.Includes(getter))
            {
                ScanAccessorMetadata(getter, memberPath, "get");
            }

            if (setter != null && _surfaceShape.Includes(setter))
            {
                ScanAccessorMetadata(setter, memberPath, "set");
            }
        }
    }

    private void ScanFields(Type type, ArchitectureContractExposurePath typePath)
    {
        FieldInfo[] fields = _state.TryReadArray(
            () => type.GetFields(MemberFlags), typePath.Append("member", "fields"),
            "fields-unavailable");
        foreach (FieldInfo field in fields.OrderBy(
            ArchitectureContractSurfaceExposureScanState.MemberSortKey, StringComparer.Ordinal))
        {
            if (!_surfaceShape.Includes(field) || IsCompilerGenerated(field, typePath) || field.IsSpecialName)
            {
                continue;
            }

            ArchitectureContractExposurePath memberPath = typePath.Append(
                "member", ArchitectureContractSurfaceExposureScanState.MemberSortKey(field));
            _attributes.Scan(field, memberPath);
            Type? fieldType = _state.TryRead(
                () => field.FieldType, memberPath.Append("field_type"), "field-type-unavailable");
            if (fieldType != null)
            {
                _traversal.ScanShape(fieldType, memberPath.Append("field_type"));
            }
        }
    }

    private void ScanEvents(Type type, ArchitectureContractExposurePath typePath)
    {
        EventInfo[] events = _state.TryReadArray(
            () => type.GetEvents(MemberFlags), typePath.Append("member", "events"),
            "events-unavailable");
        foreach (EventInfo @event in events.OrderBy(
            ArchitectureContractSurfaceExposureScanState.MemberSortKey, StringComparer.Ordinal))
        {
            MethodInfo? add = _state.TryRead(
                () => @event.AddMethod, typePath, "event-accessor-unavailable");
            MethodInfo? remove = _state.TryRead(
                () => @event.RemoveMethod, typePath, "event-accessor-unavailable");
            if (!_surfaceShape.Includes(add) || IsCompilerGenerated(@event, typePath))
            {
                continue;
            }

            ArchitectureContractExposurePath memberPath = typePath.Append(
                "member", ArchitectureContractSurfaceExposureScanState.MemberSortKey(@event));
            _attributes.Scan(@event, memberPath);
            if (add != null)
            {
                ScanAccessorMetadata(add, memberPath, "add");
            }

            if (remove != null && _surfaceShape.Includes(remove))
            {
                ScanAccessorMetadata(remove, memberPath, "remove");
            }

            Type? eventType = _state.TryRead(
                () => @event.EventHandlerType, memberPath.Append("event_type"),
                "event-type-unavailable");
            if (eventType != null)
            {
                _traversal.ScanShape(eventType, memberPath.Append("event_type"));
            }
        }
    }

    private void ScanAccessorMetadata(
        MethodInfo accessor,
        ArchitectureContractExposurePath memberPath,
        string accessorKind)
    {
        ArchitectureContractExposurePath accessorPath = memberPath.Append("accessor", accessorKind);
        _attributes.Scan(accessor, accessorPath);
        ScanParameters(accessor, accessorPath);
        ScanReturn(accessor, accessorPath);
    }

    private static bool IsAccessor(string name) => name.StartsWith("get_", StringComparison.Ordinal)
        || name.StartsWith("set_", StringComparison.Ordinal)
        || name.StartsWith("add_", StringComparison.Ordinal)
        || name.StartsWith("remove_", StringComparison.Ordinal);
}
