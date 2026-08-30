using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ArchLinterNet.Core.Scanning;

// Walks only the visible, reflected contract surface of a caller-selected root. This is an
// evidence scanner, not a policy scanner: every path is retained, including paths that reach the
// same target through different members or shape transitions.
internal static partial class ArchitectureContractSurfaceExposureScanner
{
    private const BindingFlags MemberFlags =
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static;

    internal static ArchitectureContractSurfaceExposureResult Scan(Type root)
    {
        return Scan(root, ArchitectureContractSurfaceShape.Exported);
    }

    internal static ArchitectureContractSurfaceExposureResult Scan(
        Type root,
        ArchitectureContractSurfaceShape surfaceShape)
    {
        ArgumentNullException.ThrowIfNull(root);
        surfaceShape.EnsureValid();
        return new Walker(root, surfaceShape).Run();
    }

    private sealed partial class Walker
    {
        private readonly Type _root;
        private readonly ArchitectureContractSurfaceShape _surfaceShape;
        private readonly List<ArchitectureContractExposure> _exposures = new();
        private readonly List<ArchitectureContractExposureIncompleteEvidence> _incomplete = new();
        private readonly HashSet<ArchitectureContractExposure> _exposureSet = new();
        private readonly HashSet<ArchitectureContractExposureIncompleteEvidence> _incompleteSet = new();
        // Reflection can recreate Type instances while resolving a recursive generic constraint,
        // so branch protection uses a canonical type identity rather than object reference.
        private readonly HashSet<string> _activeTypes = new(StringComparer.Ordinal);
        private readonly ArchitectureContractExposureTarget _rootTarget;
        private readonly ArchitectureContractExposurePath _rootPath;

        internal Walker(Type root, ArchitectureContractSurfaceShape surfaceShape)
        {
            _root = root;
            _surfaceShape = surfaceShape;
            _rootTarget = TypeIdentity(root, out bool complete);
            _rootPath = ArchitectureContractExposurePath.Empty.Append("type", _rootTarget.FullTypeName);
            if (!complete)
            {
                AddIncomplete(_rootPath, "root-type-identity-unavailable");
            }
        }

        internal ArchitectureContractSurfaceExposureResult Run()
        {
            ScanDeclaredType(_root, _rootPath);

            IReadOnlyList<ArchitectureContractExposure> exposures = _exposures
                .OrderBy(item => item.DeclaringType.AssemblyName, StringComparer.Ordinal)
                .ThenBy(item => item.DeclaringType.FullTypeName, StringComparer.Ordinal)
                .ThenBy(item => item.Path.CanonicalKey, StringComparer.Ordinal)
                .ThenBy(item => item.ReferencedType.AssemblyName, StringComparer.Ordinal)
                .ThenBy(item => item.ReferencedType.FullTypeName, StringComparer.Ordinal)
                .ToArray();
            IReadOnlyList<ArchitectureContractExposureIncompleteEvidence> incomplete = _incomplete
                .OrderBy(item => item.DeclaringType.AssemblyName, StringComparer.Ordinal)
                .ThenBy(item => item.DeclaringType.FullTypeName, StringComparer.Ordinal)
                .ThenBy(item => item.Path.CanonicalKey, StringComparer.Ordinal)
                .ThenBy(item => item.Reason, StringComparer.Ordinal)
                .ToArray();
            return new ArchitectureContractSurfaceExposureResult(
                Array.AsReadOnly(exposures.ToArray()), Array.AsReadOnly(incomplete.ToArray()));
        }

        private void ScanDeclaredType(Type type, ArchitectureContractExposurePath path)
        {
            if (!_activeTypes.Add(TraversalKey(type)))
            {
                return;
            }

            try
            {
                ScanTypeRelationships(type, path);
                ScanAttributes(type, path);
                ScanGenericParameters(type, path);
                ScanMembers(type, path);
                ScanNestedTypes(type, path);
            }
            catch (Exception exception) when (IsReflectionFailure(exception))
            {
                AddIncomplete(path, "type-scan-failed");
            }
            finally
            {
                _activeTypes.Remove(TraversalKey(type));
            }
        }

        private void ScanTypeRelationships(Type type, ArchitectureContractExposurePath path)
        {
            Type? baseType = TryRead(() => type.BaseType, path.Append("base_type"), "base-type-unavailable");
            if (baseType != null)
            {
                ScanShape(baseType, path.Append("base_type"));
            }

            Type[] interfaces = TryReadArray(
                () => type.GetInterfaces(), path.Append("interface"), "interfaces-unavailable");
            foreach (Type implemented in interfaces.OrderBy(TypeSortKey, StringComparer.Ordinal))
            {
                ScanShape(implemented, path.Append("interface", TypeSortKey(implemented)));
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                Type[] arguments = TryReadArray(
                    () => type.GetGenericArguments(), path.Append("generic_argument"), "generic-arguments-unavailable");
                for (int index = 0; index < arguments.Length; index++)
                {
                    ScanShape(arguments[index], path.Append("generic_argument", index.ToString(CultureInfo.InvariantCulture)));
                }
            }
        }

        private void ScanGenericParameters(Type type, ArchitectureContractExposurePath path)
        {
            Type[] parameters = TryReadArray(
                () => type.GetGenericArguments(), path.Append("generic_parameter"), "generic-parameters-unavailable");
            for (int index = 0; index < parameters.Length; index++)
            {
                Type parameter = parameters[index];
                ArchitectureContractExposurePath parameterPath = path.Append(
                    "generic_parameter", index.ToString(CultureInfo.InvariantCulture));
                ScanAttributes(parameter, parameterPath);
                ScanConstraints(parameter, parameterPath);
            }
        }

        private void ScanConstraints(Type parameter, ArchitectureContractExposurePath path)
        {
            Type[] constraints = TryReadArray(
                () => parameter.GetGenericParameterConstraints(), path.Append("constraint"),
                "generic-constraints-unavailable");
            foreach (Type constraint in constraints.OrderBy(TypeSortKey, StringComparer.Ordinal))
            {
                ScanShape(constraint, path.Append("constraint", TypeSortKey(constraint)));
            }
        }

        private void ScanMembers(Type type, ArchitectureContractExposurePath typePath)
        {
            ScanConstructors(type, typePath);
            ScanMethods(type, typePath);
            ScanProperties(type, typePath);
            ScanFields(type, typePath);
            ScanEvents(type, typePath);
        }

        private void ScanConstructors(Type type, ArchitectureContractExposurePath typePath)
        {
            ConstructorInfo[] constructors = TryReadArray(
                () => type.GetConstructors(MemberFlags), typePath.Append("member", "constructors"),
                "constructors-unavailable");
            foreach (ConstructorInfo constructor in constructors.OrderBy(MemberSortKey, StringComparer.Ordinal))
            {
                if (!_surfaceShape.Includes(constructor) || IsCompilerGenerated(constructor, typePath))
                {
                    continue;
                }

                ArchitectureContractExposurePath memberPath = typePath.Append("member", MemberSortKey(constructor));
                ScanAttributes(constructor, memberPath);
                ScanParameters(constructor, memberPath);
            }
        }

        private void ScanMethods(Type type, ArchitectureContractExposurePath typePath)
        {
            MethodInfo[] methods = TryReadArray(
                () => type.GetMethods(MemberFlags), typePath.Append("member", "methods"),
                "methods-unavailable");
            foreach (MethodInfo method in methods.OrderBy(MemberSortKey, StringComparer.Ordinal))
            {
                if (!_surfaceShape.Includes(method) || IsCompilerGenerated(method, typePath) ||
                    (method.IsSpecialName && IsAccessor(method.Name)))
                {
                    continue;
                }

                ArchitectureContractExposurePath memberPath = typePath.Append("member", MemberSortKey(method));
                ScanAttributes(method, memberPath);
                ScanParameters(method, memberPath);
                ScanReturn(method, memberPath);
                ScanMethodGenericParameters(method, memberPath);
            }
        }

        private void ScanMethodGenericParameters(MethodInfo method, ArchitectureContractExposurePath memberPath)
        {
            Type[] parameters = TryReadArray(
                () => method.GetGenericArguments(), memberPath.Append("generic_parameter"),
                "method-generic-parameters-unavailable");
            for (int index = 0; index < parameters.Length; index++)
            {
                ArchitectureContractExposurePath parameterPath = memberPath.Append(
                    "generic_parameter", index.ToString(CultureInfo.InvariantCulture));
                ScanAttributes(parameters[index], parameterPath);
                ScanConstraints(parameters[index], parameterPath);
            }
        }

        private void ScanProperties(Type type, ArchitectureContractExposurePath typePath)
        {
            PropertyInfo[] properties = TryReadArray(
                () => type.GetProperties(MemberFlags), typePath.Append("member", "properties"),
                "properties-unavailable");
            foreach (PropertyInfo property in properties.OrderBy(MemberSortKey, StringComparer.Ordinal))
            {
                MethodInfo? getter = TryRead(() => property.GetGetMethod(nonPublic: true), typePath, "property-getter-unavailable");
                MethodInfo? setter = TryRead(() => property.GetSetMethod(nonPublic: true), typePath, "property-setter-unavailable");
                if ((!_surfaceShape.Includes(getter) && !_surfaceShape.Includes(setter)) || IsCompilerGenerated(property, typePath))
                {
                    continue;
                }

                ArchitectureContractExposurePath memberPath = typePath.Append("member", MemberSortKey(property));
                ScanAttributes(property, memberPath);
                Type? propertyType = TryRead(() => property.PropertyType, memberPath.Append("return"), "property-type-unavailable");
                if (propertyType != null)
                {
                    ScanShape(propertyType, memberPath.Append("return"));
                }

                ParameterInfo[] parameters = TryReadArray(
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
            FieldInfo[] fields = TryReadArray(
                () => type.GetFields(MemberFlags), typePath.Append("member", "fields"),
                "fields-unavailable");
            foreach (FieldInfo field in fields.OrderBy(MemberSortKey, StringComparer.Ordinal))
            {
                if (!_surfaceShape.Includes(field) || IsCompilerGenerated(field, typePath) || field.IsSpecialName)
                {
                    continue;
                }

                ArchitectureContractExposurePath memberPath = typePath.Append("member", MemberSortKey(field));
                ScanAttributes(field, memberPath);
                Type? fieldType = TryRead(() => field.FieldType, memberPath.Append("field_type"), "field-type-unavailable");
                if (fieldType != null)
                {
                    ScanShape(fieldType, memberPath.Append("field_type"));
                }
            }
        }

        private void ScanEvents(Type type, ArchitectureContractExposurePath typePath)
        {
            EventInfo[] events = TryReadArray(
                () => type.GetEvents(MemberFlags), typePath.Append("member", "events"),
                "events-unavailable");
            foreach (EventInfo @event in events.OrderBy(MemberSortKey, StringComparer.Ordinal))
            {
                MethodInfo? add = TryRead(() => @event.AddMethod, typePath, "event-accessor-unavailable");
                MethodInfo? remove = TryRead(() => @event.RemoveMethod, typePath, "event-accessor-unavailable");
                if (!_surfaceShape.Includes(add) || IsCompilerGenerated(@event, typePath))
                {
                    continue;
                }

                ArchitectureContractExposurePath memberPath = typePath.Append("member", MemberSortKey(@event));
                ScanAttributes(@event, memberPath);
                if (add != null)
                {
                    ScanAccessorMetadata(add, memberPath, "add");
                }

                if (remove != null && _surfaceShape.Includes(remove))
                {
                    ScanAccessorMetadata(remove, memberPath, "remove");
                }

                Type? eventType = TryRead(() => @event.EventHandlerType, memberPath.Append("event_type"), "event-type-unavailable");
                if (eventType != null)
                {
                    ScanShape(eventType, memberPath.Append("event_type"));
                }
            }
        }

        private void ScanNestedTypes(Type type, ArchitectureContractExposurePath typePath)
        {
            Type[] nestedTypes = TryReadArray(
                () => type.GetNestedTypes(MemberFlags), typePath.Append("nested_type"),
                "nested-types-unavailable");
            foreach (Type nested in nestedTypes.OrderBy(TypeSortKey, StringComparer.Ordinal))
            {
                if (!IsNestedTypeVisible(nested, typePath) || IsCompilerGenerated(nested, typePath))
                {
                    continue;
                }

                ArchitectureContractExposurePath nestedPath = typePath.Append("nested_type", TypeSortKey(nested));
                AddExposure(nestedPath, nested);
            }
        }

        private void ScanShape(Type type, ArchitectureContractExposurePath path)
        {
            if (type.IsGenericParameter)
            {
                ScanAttributes(type, path);
                ScanConstraints(type, path);
                return;
            }

            Type targetType = type;
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                targetType = TryRead(() => type.GetGenericTypeDefinition(), path,
                    "generic-definition-unavailable") ?? type;
            }

            AddExposure(path, targetType);
            if (!_activeTypes.Add(TraversalKey(type)))
            {
                return;
            }

            try
            {
                if (type.IsByRef || type.IsPointer || type.IsArray)
                {
                    Type? element = TryRead(() => type.GetElementType(), path, "element-type-unavailable");
                    if (element != null)
                    {
                        string kind = type.IsArray ? "array_element" : type.IsPointer ? "pointer_element" : "byref_element";
                        ScanShape(element, path.Append(kind));
                    }
                }

                if (type.IsGenericType)
                {
                    Type[] arguments = TryReadArray(
                        () => type.GetGenericArguments(), path.Append("generic_argument"),
                        "generic-arguments-unavailable");
                    bool nullable = IsNullable(type);
                    bool tuple = IsTuple(type);
                    for (int index = 0; index < arguments.Length; index++)
                    {
                        string kind = nullable ? "nullable_underlying" : tuple ? "tuple_element" : "generic_argument";
                        ArchitectureContractExposurePath childPath = path.Append(
                            kind, index.ToString(CultureInfo.InvariantCulture));
                        ScanShape(arguments[index], childPath);
                    }
                }

                if (typeof(Delegate).IsAssignableFrom(type))
                {
                    ScanDelegateInvoke(type, path);
                }
            }
            catch (Exception exception) when (IsReflectionFailure(exception))
            {
                AddIncomplete(path, "shape-scan-failed");
            }
            finally
            {
                _activeTypes.Remove(TraversalKey(type));
            }
        }

        private void ScanDelegateInvoke(Type delegateType, ArchitectureContractExposurePath delegatePath)
        {
            MethodInfo? invoke = TryRead(
                () => delegateType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance),
                delegatePath.Append("delegate_invoke"), "delegate-invoke-unavailable");
            if (invoke == null)
            {
                return;
            }

            ArchitectureContractExposurePath invokePath = delegatePath.Append("delegate_invoke", "Invoke");
            ScanAttributes(invoke, invokePath);
            ScanParameters(invoke, invokePath);
            ScanReturn(invoke, invokePath);
        }

        private void ScanAttributes(MemberInfo member, ArchitectureContractExposurePath sitePath)
        {
            IReadOnlyList<CustomAttributeData> attributes = TryReadAttributes(
                () => member.GetCustomAttributesData(), sitePath, "attributes-unavailable");
            ScanAttributeData(attributes, sitePath);
        }

        private void ScanAttributes(ParameterInfo parameter, ArchitectureContractExposurePath sitePath)
        {
            IReadOnlyList<CustomAttributeData> attributes = TryReadAttributes(
                () => parameter.GetCustomAttributesData(), sitePath, "attributes-unavailable");
            ScanAttributeData(attributes, sitePath);
        }

        private void ScanAttributeData(IReadOnlyList<CustomAttributeData> attributes, ArchitectureContractExposurePath sitePath)
        {
            Dictionary<string, int> attributeOccurrences = new(StringComparer.Ordinal);
            foreach (CustomAttributeData attribute in attributes.OrderBy(AttributeSortKey, StringComparer.Ordinal))
            {
                Type? attributeType = TryRead(() => attribute.AttributeType, sitePath, "attribute-type-unavailable");
                if (attributeType == null)
                {
                    continue;
                }

                // AttributeSortKey includes the normalized metadata arguments, so the occurrence
                // ordinal below stays stable even if reflection enumerates AllowMultiple
                // attributes of the same type in a different order.
                string attributeTypeSortKey = TypeSortKey(attributeType);
                int occurrence = attributeOccurrences.TryGetValue(attributeTypeSortKey, out int previous)
                    ? previous
                    : 0;
                attributeOccurrences[attributeTypeSortKey] = occurrence + 1;
                ArchitectureContractExposurePath attributePath = sitePath.Append(
                    "attribute", $"{attributeTypeSortKey}:{occurrence}");
                AddExposure(attributePath, attributeType);
                IList<CustomAttributeTypedArgument> constructorArguments = TryRead(
                    () => attribute.ConstructorArguments, attributePath, "attribute-arguments-unavailable")
                    ?? Array.Empty<CustomAttributeTypedArgument>();
                for (int index = 0; index < constructorArguments.Count; index++)
                {
                    ScanAttributeArgument(constructorArguments[index], attributePath.Append(
                        "attribute_argument", $"constructor:{index}"));
                }

                IList<CustomAttributeNamedArgument> namedArguments = TryRead(
                    () => attribute.NamedArguments, attributePath, "attribute-named-arguments-unavailable")
                    ?? Array.Empty<CustomAttributeNamedArgument>();
                foreach (CustomAttributeNamedArgument named in namedArguments.OrderBy(argument => argument.MemberName, StringComparer.Ordinal))
                {
                    ScanAttributeArgument(named.TypedValue, attributePath.Append("attribute_argument", $"named:{named.MemberName}"));
                }
            }
        }

        private void ScanAttributeArgument(CustomAttributeTypedArgument argument, ArchitectureContractExposurePath path)
        {
            Type? argumentType = TryRead(() => argument.ArgumentType, path, "attribute-argument-type-unavailable");
            if (argumentType == null)
            {
                return;
            }

            if (argumentType.IsArray)
            {
                Type? elementType = TryRead(
                    () => argumentType.GetElementType(), path, "attribute-array-element-type-unavailable");
                if (elementType != null && TryRead(
                        () => elementType.IsEnum, path, "attribute-array-element-type-unavailable"))
                {
                    // The declared element type is semantic evidence even when the metadata array
                    // has no values to scan.
                    AddExposure(path, elementType);
                }

                object? value = TryRead(() => argument.Value, path, "attribute-array-value-unavailable");
                if (value is IEnumerable values)
                {
                    int index = 0;
                    foreach (object? item in values)
                    {
                        if (item is CustomAttributeTypedArgument typed)
                        {
                            ScanAttributeArgument(typed, path.Append("array_element", index.ToString(CultureInfo.InvariantCulture)));
                        }

                        index++;
                    }
                }

                return;
            }

            if (argumentType.IsEnum)
            {
                AddExposure(path, argumentType);
                return;
            }

            if (argumentType == typeof(Type))
            {
                object? value = TryRead(() => argument.Value, path, "attribute-type-value-unavailable");
                if (value is Type referenced)
                {
                    ScanShape(referenced, path);
                }
            }
            // Primitive, string, and null values deliberately do not become type targets.
        }

        private void AddExposure(ArchitectureContractExposurePath path, Type referencedType)
        {
            ArchitectureContractExposureTarget target = TypeIdentity(referencedType, out bool complete);
            if (!complete)
            {
                AddIncomplete(path, "type-identity-unavailable");
            }

            ArchitectureContractExposure exposure = new(_rootTarget, path, target);
            if (_exposureSet.Add(exposure))
            {
                _exposures.Add(exposure);
            }
        }

        private void AddIncomplete(ArchitectureContractExposurePath path, string reason)
        {
            ArchitectureContractExposureIncompleteEvidence evidence = new(_rootTarget, path, reason);
            if (_incompleteSet.Add(evidence))
            {
                _incomplete.Add(evidence);
            }
        }

        private T? TryRead<T>(Func<T> read, ArchitectureContractExposurePath path, string reason)
        {
            try
            {
                return read();
            }
            catch (Exception exception) when (IsReflectionFailure(exception))
            {
                AddIncomplete(path, reason);
                return default;
            }
        }

        private T[] TryReadArray<T>(Func<T[]> read, ArchitectureContractExposurePath path, string reason)
        {
            return TryRead(read, path, reason) ?? Array.Empty<T>();
        }

        private IReadOnlyList<CustomAttributeData> TryReadAttributes(
            Func<IList<CustomAttributeData>> read, ArchitectureContractExposurePath path, string reason)
        {
            return TryRead(read, path, reason)?.ToArray() ?? Array.Empty<CustomAttributeData>();
        }

        private static bool IsReflectionFailure(Exception exception) => exception is TypeLoadException
            or FileNotFoundException
            or FileLoadException
            or ReflectionTypeLoadException
            or CustomAttributeFormatException
            or MissingMemberException
            or MemberAccessException
            or NotSupportedException
            or InvalidOperationException
            or ArgumentException
            or InvalidCastException
            or TypeInitializationException
            or TargetInvocationException
            or System.Security.SecurityException;

        private static ArchitectureContractExposureTarget TypeIdentity(Type type, out bool complete)
        {
            try
            {
                string fullName = type.FullName ?? type.Name;
                string assemblyName = type.Assembly.FullName ?? string.Empty;
                complete = fullName.Length != 0 && assemblyName.Length != 0;
                return new ArchitectureContractExposureTarget(assemblyName, fullName);
            }
            catch (Exception exception) when (IsReflectionFailure(exception))
            {
                complete = false;
                return new ArchitectureContractExposureTarget(string.Empty, string.Empty);
            }
        }

        private static string TypeSortKey(Type type)
        {
            ArchitectureContractExposureTarget target = TypeIdentity(type, out _);
            return $"{target.AssemblyName}\u001f{target.FullTypeName}";
        }

        private static string TraversalKey(Type type)
        {
            try
            {
                if (!type.IsGenericParameter)
                {
                    return TypeSortKey(type);
                }

                string owner = type.DeclaringMethod != null
                    ? MemberSortKey(type.DeclaringMethod)
                    : TypeSortKey(type.DeclaringType!);
                return $"generic_parameter:{owner}:{type.GenericParameterPosition}";
            }
            catch (Exception exception) when (IsReflectionFailure(exception))
            {
                return "generic_parameter:<unavailable>";
            }
        }

        private static string AttributeSortKey(CustomAttributeData attribute)
        {
            try
            {
                IList<CustomAttributeTypedArgument> constructorArguments = attribute.ConstructorArguments;
                IList<CustomAttributeNamedArgument> namedArguments = attribute.NamedArguments;
                return JoinSortKeyParts(
                    TypeSortKey(attribute.AttributeType),
                    JoinSortKeyParts(constructorArguments.Select(AttributeArgumentSortKey)),
                    JoinSortKeyParts(namedArguments
                        .Select(argument => new
                        {
                            Name = argument.MemberName,
                            Value = AttributeArgumentSortKey(argument.TypedValue)
                        })
                        .OrderBy(argument => argument.Name, StringComparer.Ordinal)
                        .ThenBy(argument => argument.Value, StringComparer.Ordinal)
                        .Select(argument => JoinSortKeyParts(argument.Name, argument.Value))));
            }
            catch (Exception exception) when (IsReflectionFailure(exception))
            {
                return string.Empty;
            }
        }

        private static string AttributeArgumentSortKey(CustomAttributeTypedArgument argument)
        {
            Type argumentType = argument.ArgumentType;
            object? value = argument.Value;
            string valueKey = value switch
            {
                null => "null",
                Type referencedType => JoinSortKeyParts("type", TypeSortKey(referencedType)),
                IList<CustomAttributeTypedArgument> elements => JoinSortKeyParts(
                    "array", JoinSortKeyParts(elements.Select(AttributeArgumentSortKey))),
                IFormattable formattable => JoinSortKeyParts(
                    "value", formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty),
                _ => JoinSortKeyParts("value", value.ToString() ?? string.Empty)
            };
            return JoinSortKeyParts(TypeSortKey(argumentType), valueKey);
        }

        private static string JoinSortKeyParts(params string[] parts) => JoinSortKeyParts((IEnumerable<string>)parts);

        private static string JoinSortKeyParts(IEnumerable<string> parts) => string.Concat(parts.Select(
            part => $"{part.Length.ToString(CultureInfo.InvariantCulture)}:{part}"));

        private static string MemberSortKey(MemberInfo member)
        {
            try
            {
                string parameters = member switch
                {
                    MethodBase methodBase => string.Join(",", methodBase.GetParameters().Select(parameter => TypeSortKey(parameter.ParameterType))),
                    PropertyInfo property => string.Join(",", property.GetIndexParameters().Select(parameter => TypeSortKey(parameter.ParameterType))),
                    _ => string.Empty
                };
                string genericArity = member is MethodInfo { IsGenericMethodDefinition: true } method
                    ? $"`{method.GetGenericArguments().Length}"
                    : string.Empty;
                return $"{member.MemberType}:{member.Name}{genericArity}:{parameters}";
            }
            catch (Exception exception) when (IsReflectionFailure(exception))
            {
                return $"{member.MemberType}:{member.Name}";
            }
        }

        private static bool IsAccessor(string name) => name.StartsWith("get_", StringComparison.Ordinal)
            || name.StartsWith("set_", StringComparison.Ordinal)
            || name.StartsWith("add_", StringComparison.Ordinal)
            || name.StartsWith("remove_", StringComparison.Ordinal);

        private bool IsCompilerGenerated(MemberInfo member, ArchitectureContractExposurePath path)
        {
            try
            {
                return member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
            }
            catch (Exception exception) when (IsReflectionFailure(exception))
            {
                AddIncomplete(path, "compiler-generated-metadata-unavailable");
                return false;
            }
        }

        private bool IsNestedTypeVisible(Type type, ArchitectureContractExposurePath path)
        {
            try
            {
                Type? current = type;
                while (current != null && !ReferenceEquals(current, _root))
                {
                    if (!_surfaceShape.Includes(current))
                    {
                        return false;
                    }

                    current = current.DeclaringType;
                }

                return current != null;
            }
            catch (Exception exception) when (IsReflectionFailure(exception))
            {
                AddIncomplete(path, "nested-type-visibility-unavailable");
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
}
