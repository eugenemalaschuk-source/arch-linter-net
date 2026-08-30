using System.Reflection;

namespace ArchLinterNet.Core.Scanning;

internal static partial class ArchitecturePublicApiSurfaceScanner
{
    private static IEnumerable<ArchitectureExportedApiEntry> GetExportedMembers(
        Type type,
        string assemblyName,
        SurfaceScanCompleteness completeness)
    {
        string declaringTypeName = ArchitectureTypeNames.SafeFullName(type);

        foreach (ArchitectureExportedApiEntry entry in GetExportedConstructors(type, declaringTypeName, assemblyName, completeness))
        {
            yield return entry;
        }

        foreach (ArchitectureExportedApiEntry entry in GetExportedMethods(type, declaringTypeName, assemblyName, completeness))
        {
            yield return entry;
        }

        foreach (ArchitectureExportedApiEntry entry in GetExportedProperties(type, declaringTypeName, assemblyName, completeness))
        {
            yield return entry;
        }

        foreach (ArchitectureExportedApiEntry entry in GetExportedFields(type, declaringTypeName, assemblyName, completeness))
        {
            yield return entry;
        }

        foreach (ArchitectureExportedApiEntry entry in GetExportedEvents(type, declaringTypeName, assemblyName, completeness))
        {
            yield return entry;
        }
    }

    private static IEnumerable<ArchitectureExportedApiEntry> GetExportedConstructors(
        Type type,
        string declaringTypeName,
        string assemblyName,
        SurfaceScanCompleteness completeness)
    {
        foreach (ConstructorInfo ctor in SafeGetMembers(type, t => t.GetConstructors(MemberFlags), completeness))
        {
            if (!IsExportedVisibility(ctor) || IsCompilerGenerated(ctor, completeness))
            {
                continue;
            }

            string? signature = TryNormalizeMethodLike(type, ctor, "ctor", includeName: false, completeness);
            if (signature != null)
            {
                string visibility = MemberVisibility(ctor);
                var referenced = ReferencedTypes(SafeGetParameters(ctor).Select(p => p.ParameterType));
                yield return new ArchitectureExportedApiEntry(
                    signature,
                    ArchitecturePublicApiSignatureDetails.Compose(
                        signature, ArchitecturePublicApiSignatureDetails.ForMethod(
                            ctor, visibility, completeness.MarkIncomplete)),
                    declaringTypeName, assemblyName, visibility, false, null, referenced);
            }
        }
    }

    private static IEnumerable<ArchitectureExportedApiEntry> GetExportedMethods(
        Type type,
        string declaringTypeName,
        string assemblyName,
        SurfaceScanCompleteness completeness)
    {
        foreach (MethodInfo method in SafeGetMembers(type, t => t.GetMethods(MemberFlags), completeness))
        {
            if (!IsExportedVisibility(method) || IsCompilerGenerated(method, completeness))
            {
                continue;
            }

            if (method.IsSpecialName && IsAccessorMethodName(method.Name))
            {
                continue;
            }

            string? signature = TryNormalizeMethodLike(type, method, "method", includeName: true, completeness);
            if (signature != null)
            {
                string visibility = MemberVisibility(method);
                Type[]? genericParameters = method.IsGenericMethodDefinition ? method.GetGenericArguments() : null;
                var referenced = ReferencedTypes(
                    SafeGetParameters(method).Select(p => p.ParameterType).Append(method.ReturnType), genericParameters);
                yield return new ArchitectureExportedApiEntry(
                    signature,
                    ArchitecturePublicApiSignatureDetails.Compose(
                        signature, ArchitecturePublicApiSignatureDetails.ForMethod(
                            method, visibility, completeness.MarkIncomplete)),
                    declaringTypeName, assemblyName, visibility, false, null, referenced);
            }
        }
    }

    private static IEnumerable<ArchitectureExportedApiEntry> GetExportedProperties(
        Type type,
        string declaringTypeName,
        string assemblyName,
        SurfaceScanCompleteness completeness)
    {
        foreach (PropertyInfo property in SafeGetMembers(type, t => t.GetProperties(MemberFlags), completeness))
        {
            if (!IsExportedAccessor(property.GetMethod) && !IsExportedAccessor(property.SetMethod))
            {
                continue;
            }

            if (IsCompilerGenerated(property, completeness))
            {
                continue;
            }

            string? signature = TryNormalizeProperty(type, property, completeness);
            if (signature != null)
            {
                var referenced = ReferencedTypes(
                    new[] { property.PropertyType }.Concat(SafeGetIndexParameters(property).Select(p => p.ParameterType)));
                yield return new ArchitectureExportedApiEntry(
                    signature,
                    ArchitecturePublicApiSignatureDetails.Compose(
                        signature, ArchitecturePublicApiSignatureDetails.ForProperty(property, completeness.MarkIncomplete)),
                    declaringTypeName, assemblyName, AccessorVisibility(property.GetMethod, property.SetMethod), false, null,
                    referenced);
            }
        }
    }

    private static IEnumerable<ArchitectureExportedApiEntry> GetExportedFields(
        Type type,
        string declaringTypeName,
        string assemblyName,
        SurfaceScanCompleteness completeness)
    {
        foreach (FieldInfo field in SafeGetMembers(type, t => t.GetFields(MemberFlags), completeness))
        {
            // Skip compiler/runtime-synthesized special-name fields, most notably an enum's
            // `value__` backing field, which reflection reports as an ordinary public instance
            // field alongside the enum's real literal members and is not part of any type's
            // intentional exported surface.
            if (!IsExportedVisibility(field) || IsCompilerGenerated(field, completeness) || field.IsSpecialName)
            {
                continue;
            }

            string? fieldTypeName = TryRenderTypeName(field.FieldType, completeness);
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
                        signature, ArchitecturePublicApiSignatureDetails.ForField(
                            field, fieldVisibility, completeness.MarkIncomplete)),
                declaringTypeName, assemblyName, fieldVisibility, isConst, constQualifiedName,
                ReferencedTypes(new[] { field.FieldType }));
        }
    }

    private static IEnumerable<ArchitectureExportedApiEntry> GetExportedEvents(
        Type type,
        string declaringTypeName,
        string assemblyName,
        SurfaceScanCompleteness completeness)
    {
        foreach (EventInfo evt in SafeGetMembers(type, t => t.GetEvents(MemberFlags), completeness))
        {
            if (!IsExportedAccessor(evt.AddMethod) || IsCompilerGenerated(evt, completeness))
            {
                continue;
            }

            Type? handlerType = evt.EventHandlerType;
            string? eventTypeName = handlerType != null ? TryRenderTypeName(handlerType, completeness) : null;
            if (eventTypeName == null)
            {
                completeness.MarkIncomplete();
                continue;
            }

            string eventSignature = $"event {declaringTypeName}.{evt.Name}: {eventTypeName}";
            string eventVisibility = MemberVisibility(evt.AddMethod!);
            yield return new ArchitectureExportedApiEntry(
                    eventSignature,
                    ArchitecturePublicApiSignatureDetails.Compose(
                        eventSignature, ArchitecturePublicApiSignatureDetails.ForEvent(
                            evt, eventVisibility, completeness.MarkIncomplete)),
                declaringTypeName, assemblyName, eventVisibility, false, null,
                ReferencedTypes(new[] { handlerType! }));
        }
    }

    private static string? TryNormalizeMethodLike(
        Type declaringType,
        MethodBase method,
        string kind,
        bool includeName,
        SurfaceScanCompleteness completeness)
    {
        ParameterInfo[] parameters;
        try
        {
            parameters = method.GetParameters();
        }
        catch (TypeLoadException)
        {
            completeness.MarkIncomplete();
            return null;
        }
        catch (FileNotFoundException)
        {
            completeness.MarkIncomplete();
            return null;
        }

        string[] parameterTypeNames = new string[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            string? renderedParameterType = TryRenderTypeName(parameters[i].ParameterType, completeness);
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
            string? returnTypeName = TryRenderTypeName(methodInfo.ReturnType, completeness);
            if (returnTypeName == null)
            {
                return null;
            }

            return $"{kind} {name}({parameterList}): {returnTypeName}";
        }

        return $"{kind} {name}({parameterList})";
    }

    private static string? TryNormalizeProperty(
        Type declaringType,
        PropertyInfo property,
        SurfaceScanCompleteness completeness)
    {
        string? propertyTypeName = TryRenderTypeName(property.PropertyType, completeness);
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
            completeness.MarkIncomplete();
            return null;
        }
        catch (FileNotFoundException)
        {
            completeness.MarkIncomplete();
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
            string? renderedIndexParameterType = TryRenderTypeName(indexParameters[i].ParameterType, completeness);
            if (renderedIndexParameterType == null)
            {
                return null;
            }

            indexParameterTypeNames[i] = renderedIndexParameterType;
        }

        return $"property {declaringTypeName}.{property.Name}({string.Join(", ", indexParameterTypeNames)}): {propertyTypeName}";
    }

    private static string? TryRenderTypeName(Type type, SurfaceScanCompleteness? completeness = null)
    {
        try
        {
            return RenderTypeName(type);
        }
        catch (TypeLoadException)
        {
            completeness?.MarkIncomplete();
            return null;
        }
        catch (FileNotFoundException)
        {
            completeness?.MarkIncomplete();
            return null;
        }
    }

    // Deterministic own grammar (not full C#-syntax pretty-printing): generic type/method parameters
    // are rendered positionally (!N for a declaring-type parameter, !!N for a declaring-method
    // parameter) so renaming a generic parameter alone never changes the normalized signature.
    // Everything else falls back to Type.FullName, which already carries the CLR arity marker
    // (Foo`1) for generic type definitions.
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

    private static TMember[] SafeGetMembers<TMember>(
        Type type,
        Func<Type, TMember[]> selector,
        SurfaceScanCompleteness? completeness = null)
    {
        try
        {
            return selector(type);
        }
        catch (TypeLoadException)
        {
            completeness?.MarkIncomplete();
            return Array.Empty<TMember>();
        }
        catch (FileNotFoundException)
        {
            completeness?.MarkIncomplete();
            return Array.Empty<TMember>();
        }
    }

    private sealed class SurfaceScanCompleteness
    {
        public SurfaceScanCompleteness(bool isComplete) => IsComplete = isComplete;

        public bool IsComplete { get; private set; }

        public void MarkIncomplete() => IsComplete = false;
    }
}
