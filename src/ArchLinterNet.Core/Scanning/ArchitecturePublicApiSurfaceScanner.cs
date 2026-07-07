using System.Reflection;
using System.Runtime.CompilerServices;

namespace ArchLinterNet.Core.Scanning;

internal readonly record struct ArchitectureExportedApiEntry(
    string Signature,
    string DeclaringTypeName,
    bool IsConst,
    string? ConstQualifiedName);

// Reflection-based enumeration of a type's exported (public/protected/protected-internal) surface,
// normalized into deterministic signature strings. Mirrors the defensive-reflection posture used by
// ArchitectureTypeScanner/ArchitectureTypeRoleMatcher elsewhere in this codebase: individual members
// that fail to reflect are skipped rather than crashing the whole scan.
internal static class ArchitecturePublicApiSurfaceScanner
{
    private const BindingFlags MemberFlags =
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static IEnumerable<ArchitectureExportedApiEntry> GetExportedSurface(Assembly assembly)
    {
        foreach (Type type in ArchitectureTypeScanner.GetLoadableTypes(assembly))
        {
            if (!IsExportedType(type) || IsCompilerGenerated(type))
            {
                continue;
            }

            string typeName = ArchitectureTypeNames.SafeFullName(type);
            yield return new ArchitectureExportedApiEntry(NormalizeType(type), typeName, false, null);

            foreach (ArchitectureExportedApiEntry member in GetExportedMembers(type))
            {
                yield return member;
            }
        }
    }

    // A type is exported if it (and every enclosing type, for nested types) is itself public, or
    // protected/protected-internal nested inside an already-exported enclosing chain. A protected
    // nested type inside an internal outer type is unreachable from outside the assembly, so it is
    // not part of the exported surface even though the modifier itself says "protected".
    private static bool IsExportedType(Type type)
    {
        Type current = type;
        while (true)
        {
            try
            {
                if (!current.IsNested)
                {
                    return current.IsPublic;
                }

                if (!(current.IsNestedPublic || current.IsNestedFamily || current.IsNestedFamORAssem))
                {
                    return false;
                }

                Type? declaring = current.DeclaringType;
                if (declaring == null)
                {
                    return false;
                }

                current = declaring;
            }
            catch (TypeLoadException)
            {
                return false;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }
    }

    private static IEnumerable<ArchitectureExportedApiEntry> GetExportedMembers(Type type)
    {
        foreach (ConstructorInfo ctor in SafeGetMembers(type, t => t.GetConstructors(MemberFlags)))
        {
            if (!IsExportedVisibility(ctor) || IsCompilerGenerated(ctor))
            {
                continue;
            }

            string? signature = TryNormalizeMethodLike(type, ctor, "ctor", includeName: false);
            if (signature != null)
            {
                yield return new ArchitectureExportedApiEntry(signature, ArchitectureTypeNames.SafeFullName(type), false, null);
            }
        }

        foreach (MethodInfo method in SafeGetMembers(type, t => t.GetMethods(MemberFlags)))
        {
            if (!IsExportedVisibility(method) || IsCompilerGenerated(method))
            {
                continue;
            }

            if (method.IsSpecialName && IsAccessorMethodName(method.Name))
            {
                continue;
            }

            string? signature = TryNormalizeMethodLike(type, method, "method", includeName: true);
            if (signature != null)
            {
                yield return new ArchitectureExportedApiEntry(signature, ArchitectureTypeNames.SafeFullName(type), false, null);
            }
        }

        foreach (PropertyInfo property in SafeGetMembers(type, t => t.GetProperties(MemberFlags)))
        {
            if (!IsExportedAccessor(property.GetMethod) && !IsExportedAccessor(property.SetMethod))
            {
                continue;
            }

            if (IsCompilerGenerated(property))
            {
                continue;
            }

            string? signature = TryNormalizeProperty(type, property);
            if (signature != null)
            {
                yield return new ArchitectureExportedApiEntry(signature, ArchitectureTypeNames.SafeFullName(type), false, null);
            }
        }

        foreach (FieldInfo field in SafeGetMembers(type, t => t.GetFields(MemberFlags)))
        {
            if (!IsExportedVisibility(field) || IsCompilerGenerated(field))
            {
                continue;
            }

            string? typeName = TryRenderTypeName(field.FieldType);
            if (typeName == null)
            {
                continue;
            }

            string declaringTypeName = ArchitectureTypeNames.SafeFullName(type);
            bool isConst = field.IsLiteral;
            string kind = isConst ? "const" : "field";
            string signature = $"{kind} {declaringTypeName}.{field.Name}: {typeName}";
            string? constQualifiedName = isConst ? $"{declaringTypeName}.{field.Name}" : null;
            yield return new ArchitectureExportedApiEntry(signature, declaringTypeName, isConst, constQualifiedName);
        }

        foreach (EventInfo evt in SafeGetMembers(type, t => t.GetEvents(MemberFlags)))
        {
            if (!IsExportedAccessor(evt.AddMethod) || IsCompilerGenerated(evt))
            {
                continue;
            }

            Type? handlerType = evt.EventHandlerType;
            string? typeName = handlerType != null ? TryRenderTypeName(handlerType) : null;
            if (typeName == null)
            {
                continue;
            }

            string declaringTypeName = ArchitectureTypeNames.SafeFullName(type);
            yield return new ArchitectureExportedApiEntry($"event {declaringTypeName}.{evt.Name}: {typeName}", declaringTypeName, false, null);
        }
    }

    private static IEnumerable<TMember> SafeGetMembers<TMember>(Type type, Func<Type, TMember[]> selector)
    {
        try
        {
            return selector(type);
        }
        catch (TypeLoadException)
        {
            return Array.Empty<TMember>();
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<TMember>();
        }
    }

    private static bool IsAccessorMethodName(string name)
    {
        return name.StartsWith("get_", StringComparison.Ordinal)
            || name.StartsWith("set_", StringComparison.Ordinal)
            || name.StartsWith("add_", StringComparison.Ordinal)
            || name.StartsWith("remove_", StringComparison.Ordinal);
    }

    private static bool IsExportedVisibility(MethodBase method) =>
        method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

    private static bool IsExportedVisibility(FieldInfo field) =>
        field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;

    private static bool IsExportedAccessor(MethodInfo? accessor) =>
        accessor != null && IsExportedVisibility(accessor);

    private static bool IsCompilerGenerated(MemberInfo member)
    {
        try
        {
            return member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
        }
        catch (TypeLoadException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (CustomAttributeFormatException)
        {
            return false;
        }
    }

    private static string NormalizeType(Type type)
    {
        return $"{TypeKind(type)} {ArchitectureTypeNames.SafeFullName(type)}";
    }

    private static string TypeKind(Type type)
    {
        if (type.IsInterface)
        {
            return "interface";
        }

        if (type.IsEnum)
        {
            return "enum";
        }

        if (typeof(Delegate).IsAssignableFrom(type))
        {
            return "delegate";
        }

        return type.IsValueType ? "struct" : "class";
    }

    private static string? TryNormalizeMethodLike(Type declaringType, MethodBase method, string kind, bool includeName)
    {
        ParameterInfo[] parameters;
        try
        {
            parameters = method.GetParameters();
        }
        catch (TypeLoadException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        string[] parameterTypeNames = new string[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            string? renderedParameterType = TryRenderTypeName(parameters[i].ParameterType);
            if (renderedParameterType == null)
            {
                return null;
            }

            parameterTypeNames[i] = renderedParameterType;
        }

        string declaringTypeName = ArchitectureTypeNames.SafeFullName(declaringType);
        string name = declaringTypeName;
        if (includeName)
        {
            string genericArity = method is MethodInfo { IsGenericMethodDefinition: true } genericMethod
                ? $"`{genericMethod.GetGenericArguments().Length}"
                : string.Empty;
            name = $"{declaringTypeName}.{method.Name}{genericArity}";
        }

        string parameterList = string.Join(", ", parameterTypeNames);

        if (method is MethodInfo methodInfo)
        {
            string? returnTypeName = TryRenderTypeName(methodInfo.ReturnType);
            if (returnTypeName == null)
            {
                return null;
            }

            return $"{kind} {name}({parameterList}): {returnTypeName}";
        }

        return $"{kind} {name}({parameterList})";
    }

    private static string? TryNormalizeProperty(Type declaringType, PropertyInfo property)
    {
        string? propertyTypeName = TryRenderTypeName(property.PropertyType);
        if (propertyTypeName == null)
        {
            return null;
        }

        ParameterInfo[] indexParameters;
        try
        {
            indexParameters = property.GetIndexParameters();
        }
        catch (TypeLoadException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        string declaringTypeName = ArchitectureTypeNames.SafeFullName(declaringType);

        if (indexParameters.Length == 0)
        {
            return $"property {declaringTypeName}.{property.Name}: {propertyTypeName}";
        }

        string[] indexParameterTypeNames = new string[indexParameters.Length];
        for (int i = 0; i < indexParameters.Length; i++)
        {
            string? renderedIndexParameterType = TryRenderTypeName(indexParameters[i].ParameterType);
            if (renderedIndexParameterType == null)
            {
                return null;
            }

            indexParameterTypeNames[i] = renderedIndexParameterType;
        }

        return $"property {declaringTypeName}.{property.Name}({string.Join(", ", indexParameterTypeNames)}): {propertyTypeName}";
    }

    // Deterministic own grammar (not full C#-syntax pretty-printing): generic type/method parameters
    // are rendered positionally (!N for a declaring-type parameter, !!N for a declaring-method
    // parameter) so renaming a generic parameter alone never changes the normalized signature.
    // Everything else falls back to Type.FullName, which already carries the CLR arity marker
    // (Foo`1) for generic type definitions.
    private static string? TryRenderTypeName(Type type)
    {
        try
        {
            return RenderTypeName(type);
        }
        catch (TypeLoadException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static string RenderTypeName(Type type)
    {
        if (type.IsGenericParameter)
        {
            return type.DeclaringMethod != null
                ? $"!!{type.GenericParameterPosition}"
                : $"!{type.GenericParameterPosition}";
        }

        if (type.IsByRef)
        {
            return RenderTypeName(type.GetElementType()!) + "&";
        }

        if (type.IsPointer)
        {
            return RenderTypeName(type.GetElementType()!) + "*";
        }

        if (type.IsArray)
        {
            int rank = type.GetArrayRank();
            string commas = rank > 1 ? new string(',', rank - 1) : string.Empty;
            return RenderTypeName(type.GetElementType()!) + "[" + commas + "]";
        }

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            string genericDefinitionName = ArchitectureTypeNames.SafeFullName(type.GetGenericTypeDefinition());
            string args = string.Join(",", type.GetGenericArguments().Select(RenderTypeName));
            return $"{genericDefinitionName}[{args}]";
        }

        return ArchitectureTypeNames.SafeFullName(type);
    }
}
