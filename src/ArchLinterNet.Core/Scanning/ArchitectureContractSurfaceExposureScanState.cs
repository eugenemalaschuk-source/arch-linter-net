using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;

namespace ArchLinterNet.Core.Scanning;

// The single mutable state for one contract-surface scan. Collaborators only contribute evidence
// through this object, so recursion, deduplication, reflection-failure recording, and final
// ordering cannot drift between the traversal branches.
internal sealed class ArchitectureContractSurfaceExposureScanState
{
    private readonly List<ArchitectureContractExposure> _exposures = new();
    private readonly List<ArchitectureContractExposureIncompleteEvidence> _incomplete = new();
    private readonly HashSet<ArchitectureContractExposure> _exposureSet = new();
    private readonly HashSet<ArchitectureContractExposureIncompleteEvidence> _incompleteSet = new();
    private readonly Dictionary<ArchitectureContractExposureTarget, Type> _referencedTypes = new();
    // Reflection can recreate Type instances while resolving a recursive generic constraint,
    // so branch protection uses a canonical type identity rather than object reference.
    private readonly HashSet<string> _activeTypes = new(StringComparer.Ordinal);

    internal ArchitectureContractSurfaceExposureScanState(Type root)
    {
        RootType = root;
        RootTarget = TypeIdentity(root, out bool complete);
        RootPath = ArchitectureContractExposurePath.Empty.Append("type", RootTarget.FullTypeName);
        if (!complete)
        {
            AddIncomplete(RootPath, "root-type-identity-unavailable");
        }
    }

    internal Type RootType { get; }

    internal ArchitectureContractExposureTarget RootTarget { get; }

    internal ArchitectureContractExposurePath RootPath { get; }

    internal bool EnterType(Type type) => _activeTypes.Add(TraversalKey(type));

    internal void ExitType(Type type) => _activeTypes.Remove(TraversalKey(type));

    internal void AddExposure(ArchitectureContractExposurePath path, Type referencedType)
    {
        ArchitectureContractExposureTarget target = TypeIdentity(referencedType, out bool complete);
        if (!complete)
        {
            AddIncomplete(path, "type-identity-unavailable");
        }

        ArchitectureContractExposure exposure = new(RootTarget, path, target);
        if (_exposureSet.Add(exposure))
        {
            _exposures.Add(exposure);
        }

        if (complete)
        {
            _referencedTypes.TryAdd(target, referencedType);
        }
    }

    internal void AddIncomplete(ArchitectureContractExposurePath path, string reason)
    {
        ArchitectureContractExposureIncompleteEvidence evidence = new(RootTarget, path, reason);
        if (_incompleteSet.Add(evidence))
        {
            _incomplete.Add(evidence);
        }
    }

    internal T? TryRead<T>(Func<T> read, ArchitectureContractExposurePath path, string reason)
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

    internal T[] TryReadArray<T>(Func<T[]> read, ArchitectureContractExposurePath path, string reason)
    {
        return TryRead(read, path, reason) ?? Array.Empty<T>();
    }

    internal IReadOnlyList<CustomAttributeData> TryReadAttributes(
        Func<IList<CustomAttributeData>> read,
        ArchitectureContractExposurePath path,
        string reason)
    {
        return TryRead(read, path, reason)?.ToArray() ?? Array.Empty<CustomAttributeData>();
    }

    internal ArchitectureContractSurfaceExposureResult CreateResult()
    {
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
            Array.AsReadOnly(exposures.ToArray()), Array.AsReadOnly(incomplete.ToArray()))
        {
            ReferencedTypes = new ReadOnlyDictionary<ArchitectureContractExposureTarget, Type>(
                _referencedTypes
                    .OrderBy(item => item.Key.AssemblyName, StringComparer.Ordinal)
                    .ThenBy(item => item.Key.FullTypeName, StringComparer.Ordinal)
                    .ToDictionary(item => item.Key, item => item.Value)),
        };
    }

    internal static bool IsReflectionFailure(Exception exception) => exception is TypeLoadException
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

    internal static ArchitectureContractExposureTarget TypeIdentity(Type type, out bool complete)
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

    internal static string TypeSortKey(Type type)
    {
        ArchitectureContractExposureTarget target = TypeIdentity(type, out _);
        return $"{target.AssemblyName}\u001f{target.FullTypeName}";
    }

    internal static string TraversalKey(Type type)
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

    internal static string AttributeSortKey(CustomAttributeData attribute)
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

    internal static string AttributeArgumentSortKey(CustomAttributeTypedArgument argument)
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

    internal static string JoinSortKeyParts(params string[] parts) =>
        JoinSortKeyParts((IEnumerable<string>)parts);

    internal static string JoinSortKeyParts(IEnumerable<string> parts) => string.Concat(parts.Select(
        part => $"{part.Length.ToString(CultureInfo.InvariantCulture)}:{part}"));

    internal static string MemberSortKey(MemberInfo member)
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
}
