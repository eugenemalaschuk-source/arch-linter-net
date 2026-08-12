using System.Reflection;
using System.Runtime.CompilerServices;

namespace ArchLinterNet.Core.Scanning;
// Note: relies on System.Linq extension methods (Select/Where/Concat/Distinct/ToArray/ToHashSet)
// available via ArchLinterNet.Core's global usings.

// Signature is the legacy `declared_api` identity (kind + name + parameter/member types).
// ExactSignature adds the deterministic detail suffix that identity deliberately drops — constant
// values, accessor shape, static/ref/out/in, sealed/abstract, enum underlying type, generic
// constraints — and is what a reviewed snapshot records. Keeping both is what lets an existing
// inline allowlist and an exact snapshot coexist on the same contract. ReferencedTypes is
// assembly-qualified, not just full name: two distinct assemblies can legitimately export a type
// under the identical full name, so a first-party-escape check keyed on name alone could let a
// selected assembly's type mask an unselected same-named type from a different assembly.
internal readonly record struct ArchitectureExportedApiEntry(
    string Signature,
    string ExactSignature,
    string DeclaringTypeName,
    string AssemblyName,
    string Visibility,
    bool IsConst,
    string? ConstQualifiedName,
    IReadOnlyList<(string AssemblyName, string TypeFullName)> ReferencedTypes);

// Reflection-based enumeration of a type's exported (public/protected/protected-internal) surface,
// normalized into deterministic signature strings. Mirrors the defensive-reflection posture used by
// ArchitectureTypeScanner/ArchitectureTypeRoleMatcher elsewhere in this codebase: individual members
// that fail to reflect are skipped rather than crashing the whole scan.
internal static class ArchitecturePublicApiSurfaceScanner
{
    private const string PublicVisibility = "public";
    private const string ProtectedInternalVisibility = "protected internal";
    private const string ProtectedVisibility = "protected";

    private const BindingFlags MemberFlags =
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static; // NOSONAR: intentional — IL scanning needs reflection access to all members

    public static IEnumerable<ArchitectureExportedApiEntry> GetExportedSurface(Assembly assembly)
    {
        string assemblyName = assembly.GetName().Name ?? string.Empty;

        foreach (Type type in GetExportedTypes(assembly))
        {
            string typeName = ArchitectureTypeNames.SafeFullName(type);
            string typeSignature = NormalizeType(type);
            string typeVisibility = TypeVisibility(type);
            // A generic type's own declaration can reference a first-party type purely through a
            // constraint (`class Foo<T> where T : HiddenExported`), with no member involved at all.
            (string, string)[] typeReferenced = type.IsGenericTypeDefinition
                ? ReferencedTypes(Array.Empty<Type>(), type.GetGenericArguments())
                : Array.Empty<(string, string)>();
            yield return new ArchitectureExportedApiEntry(
                typeSignature,
                ArchitecturePublicApiSignatureDetails.Compose(
                    typeSignature, ArchitecturePublicApiSignatureDetails.ForType(type, typeVisibility)),
                typeName, assemblyName, typeVisibility, false, null, typeReferenced);

            foreach (ArchitectureExportedApiEntry member in GetExportedMembers(type, assemblyName))
            {
                yield return member;
            }
        }
    }

    // The exported type universe GetExportedSurface enumerates, factored out so a surface_selector
    // predicate (issue #525) can be evaluated against exactly the same candidate types without
    // duplicating the visibility-chain filter.
    internal static IEnumerable<Type> GetExportedTypes(Assembly assembly)
    {
        foreach (Type type in ArchitectureTypeScanner.GetLoadableTypes(assembly))
        {
            if (!IsExportedType(type) || IsCompilerGenerated(type))
            {
                continue;
            }

            yield return type;
        }
    }

    // Full names of exported types selected by predicate, for the checker/session to determine
    // which of an assembly's exported types a surface_selector matched, without exposing Type
    // objects (or the selector matching engine) outside this scanning layer.
    public static HashSet<string> SelectedTypeFullNames(Assembly assembly, Func<Type, bool> predicate)
    {
        return GetExportedTypes(assembly)
            .Where(predicate)
            .Select(ArchitectureTypeNames.SafeFullName)
            .ToHashSet(StringComparer.Ordinal);
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

    private static IEnumerable<ArchitectureExportedApiEntry> GetExportedMembers(Type type, string assemblyName)
    {
        string declaringTypeName = ArchitectureTypeNames.SafeFullName(type);

        foreach (ArchitectureExportedApiEntry entry in GetExportedConstructors(type, declaringTypeName, assemblyName))
        {
            yield return entry;
        }

        foreach (ArchitectureExportedApiEntry entry in GetExportedMethods(type, declaringTypeName, assemblyName))
        {
            yield return entry;
        }

        foreach (ArchitectureExportedApiEntry entry in GetExportedProperties(type, declaringTypeName, assemblyName))
        {
            yield return entry;
        }

        foreach (ArchitectureExportedApiEntry entry in GetExportedFields(type, declaringTypeName, assemblyName))
        {
            yield return entry;
        }

        foreach (ArchitectureExportedApiEntry entry in GetExportedEvents(type, declaringTypeName, assemblyName))
        {
            yield return entry;
        }
    }

    private static IEnumerable<ArchitectureExportedApiEntry> GetExportedConstructors(
        Type type, string declaringTypeName, string assemblyName)
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
                string visibility = MemberVisibility(ctor);
                var referenced = ReferencedTypes(SafeGetParameters(ctor).Select(p => p.ParameterType));
                yield return new ArchitectureExportedApiEntry(
                    signature,
                    ArchitecturePublicApiSignatureDetails.Compose(
                        signature, ArchitecturePublicApiSignatureDetails.ForMethod(ctor, visibility)),
                    declaringTypeName, assemblyName, visibility, false, null, referenced);
            }
        }
    }

    private static IEnumerable<ArchitectureExportedApiEntry> GetExportedMethods(
        Type type, string declaringTypeName, string assemblyName)
    {
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
                string visibility = MemberVisibility(method);
                Type[]? genericParameters = method.IsGenericMethodDefinition ? method.GetGenericArguments() : null;
                var referenced = ReferencedTypes(
                    SafeGetParameters(method).Select(p => p.ParameterType).Append(method.ReturnType), genericParameters);
                yield return new ArchitectureExportedApiEntry(
                    signature,
                    ArchitecturePublicApiSignatureDetails.Compose(
                        signature, ArchitecturePublicApiSignatureDetails.ForMethod(method, visibility)),
                    declaringTypeName, assemblyName, visibility, false, null, referenced);
            }
        }
    }

    private static IEnumerable<ArchitectureExportedApiEntry> GetExportedProperties(
        Type type, string declaringTypeName, string assemblyName)
    {
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
                var referenced = ReferencedTypes(
                    new[] { property.PropertyType }.Concat(SafeGetIndexParameters(property).Select(p => p.ParameterType)));
                yield return new ArchitectureExportedApiEntry(
                    signature,
                    ArchitecturePublicApiSignatureDetails.Compose(
                        signature, ArchitecturePublicApiSignatureDetails.ForProperty(property)),
                    declaringTypeName, assemblyName, AccessorVisibility(property.GetMethod, property.SetMethod), false, null,
                    referenced);
            }
        }
    }

    private static IEnumerable<ArchitectureExportedApiEntry> GetExportedFields(
        Type type, string declaringTypeName, string assemblyName)
    {
        foreach (FieldInfo field in SafeGetMembers(type, t => t.GetFields(MemberFlags)))
        {
            // Skip compiler/runtime-synthesized special-name fields, most notably an enum's
            // `value__` backing field, which reflection reports as an ordinary public instance
            // field alongside the enum's real literal members and is not part of any type's
            // intentional exported surface.
            if (!IsExportedVisibility(field) || IsCompilerGenerated(field) || field.IsSpecialName)
            {
                continue;
            }

            string? fieldTypeName = TryRenderTypeName(field.FieldType);
            if (fieldTypeName == null)
            {
                continue;
            }

            bool isConst = field.IsLiteral;
            string kind = isConst ? "const" : "field";
            string signature = $"{kind} {declaringTypeName}.{field.Name}: {fieldTypeName}";
            string? constQualifiedName = isConst ? $"{declaringTypeName}.{field.Name}" : null;
            string fieldVisibility = MemberVisibility(field);
            yield return new ArchitectureExportedApiEntry(
                signature,
                ArchitecturePublicApiSignatureDetails.Compose(
                    signature, ArchitecturePublicApiSignatureDetails.ForField(field, fieldVisibility)),
                declaringTypeName, assemblyName, fieldVisibility, isConst, constQualifiedName,
                ReferencedTypes(new[] { field.FieldType }));
        }
    }

    private static IEnumerable<ArchitectureExportedApiEntry> GetExportedEvents(
        Type type, string declaringTypeName, string assemblyName)
    {
        foreach (EventInfo evt in SafeGetMembers(type, t => t.GetEvents(MemberFlags)))
        {
            if (!IsExportedAccessor(evt.AddMethod) || IsCompilerGenerated(evt))
            {
                continue;
            }

            Type? handlerType = evt.EventHandlerType;
            string? eventTypeName = handlerType != null ? TryRenderTypeName(handlerType) : null;
            if (eventTypeName == null)
            {
                continue;
            }

            string eventSignature = $"event {declaringTypeName}.{evt.Name}: {eventTypeName}";
            string eventVisibility = MemberVisibility(evt.AddMethod!);
            yield return new ArchitectureExportedApiEntry(
                eventSignature,
                ArchitecturePublicApiSignatureDetails.Compose(
                    eventSignature, ArchitecturePublicApiSignatureDetails.ForEvent(evt, eventVisibility)),
                declaringTypeName, assemblyName, eventVisibility, false, null,
                ReferencedTypes(new[] { handlerType! }));
        }
    }

    // Distinct, assembly-qualified identity of every type a member's signature references
    // (parameter/return/field/property/event-handler types, plus its own generic parameters'
    // constraints when genericParameters is supplied), walking through array/pointer/byref wrappers
    // and generic instantiations. Used to fail closed when a selected member depends on an
    // unselected first-party exported type (issue #525) — not full C#-syntax rendering, just type
    // identity. Assembly-qualified because two distinct assemblies can legitimately export a type
    // under the identical full name.
    private static (string AssemblyName, string TypeFullName)[] ReferencedTypes(
        IEnumerable<Type> types, IEnumerable<Type>? genericParameters = null)
    {
        var collected = new HashSet<Type>();
        foreach (Type type in types)
        {
            CollectReferencedTypes(type, collected);
        }

        if (genericParameters != null)
        {
            foreach (Type parameter in genericParameters)
            {
                foreach (Type constraint in SafeGetGenericParameterConstraints(parameter))
                {
                    CollectReferencedTypes(constraint, collected);
                }
            }
        }

        return collected
            .Select(type => (SafeAssemblyName(type), ArchitectureTypeNames.SafeFullName(type)))
            .Distinct()
            .ToArray();
    }

    private static void CollectReferencedTypes(Type type, HashSet<Type> collected)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            Type? element = SafeGetElementType(type);
            if (element != null)
            {
                CollectReferencedTypes(element, collected);
            }

            return;
        }

        if (type.IsGenericParameter)
        {
            return;
        }

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            collected.Add(type.GetGenericTypeDefinition());
            foreach (Type argument in type.GetGenericArguments())
            {
                CollectReferencedTypes(argument, collected);
            }

            return;
        }

        collected.Add(type);
    }

    private static string SafeAssemblyName(Type type)
    {
        try
        {
            return type.Assembly.GetName().Name ?? string.Empty;
        }
        catch (TypeLoadException)
        {
            return string.Empty;
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
    }

    private static Type[] SafeGetGenericParameterConstraints(Type parameter)
    {
        try
        {
            return parameter.GetGenericParameterConstraints();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<Type>();
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<Type>();
        }
    }

    private static Type? SafeGetElementType(Type type)
    {
        try
        {
            return type.GetElementType();
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

    private static ParameterInfo[] SafeGetParameters(MethodBase method)
    {
        try
        {
            return method.GetParameters();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<ParameterInfo>();
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<ParameterInfo>();
        }
    }

    private static ParameterInfo[] SafeGetIndexParameters(PropertyInfo property)
    {
        try
        {
            return property.GetIndexParameters();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<ParameterInfo>();
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<ParameterInfo>();
        }
    }

    private static TMember[] SafeGetMembers<TMember>(Type type, Func<Type, TMember[]> selector)
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

    // Only "public", "protected", and "protected internal" are ever passed here — the caller has
    // already filtered to IsExportedVisibility, so these three checks are exhaustive.
    private static string MemberVisibility(MethodBase method)
    {
        if (method.IsPublic)
        {
            return PublicVisibility;
        }

        return method.IsFamilyOrAssembly ? ProtectedInternalVisibility : ProtectedVisibility;
    }

    private static string MemberVisibility(FieldInfo field)
    {
        if (field.IsPublic)
        {
            return PublicVisibility;
        }

        return field.IsFamilyOrAssembly ? ProtectedInternalVisibility : ProtectedVisibility;
    }

    private static string TypeVisibility(Type type)
    {
        if (!type.IsNested)
        {
            return PublicVisibility;
        }

        if (type.IsNestedPublic)
        {
            return PublicVisibility;
        }

        return type.IsNestedFamORAssem ? ProtectedInternalVisibility : ProtectedVisibility;
    }

    // A property/event is exported if at least one of its accessors is; when both accessors are
    // exported but at different visibilities (e.g. `public get; protected set;`), the more open
    // accessor's visibility describes what's actually reachable from outside the assembly.
    private static string AccessorVisibility(MethodInfo? getMethod, MethodInfo? setMethod)
    {
        string? getVisibility = getMethod != null && IsExportedVisibility(getMethod) ? MemberVisibility(getMethod) : null;
        string? setVisibility = setMethod != null && IsExportedVisibility(setMethod) ? MemberVisibility(setMethod) : null;
        return MostOpenVisibility(getVisibility, setVisibility);
    }

    private static string MostOpenVisibility(string? first, string? second)
    {
        static int Rank(string? visibility) => visibility switch
        {
            PublicVisibility => 0,
            ProtectedInternalVisibility => 1,
            ProtectedVisibility => 2,
            _ => 3
        };

        return Rank(first) <= Rank(second) ? first ?? PublicVisibility : second ?? PublicVisibility;
    }

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
