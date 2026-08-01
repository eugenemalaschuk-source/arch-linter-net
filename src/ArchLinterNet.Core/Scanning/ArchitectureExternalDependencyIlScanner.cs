using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Scanning;

internal interface IArchitectureExternalDependencyIlScanner
{
    IEnumerable<ArchitectureViolation> FindMethodBodyViolations(
        Type[] sourceTypes,
        string externalGroupName,
        ArchitectureExternalDependencyGroup externalGroup,
        ArchitectureContractExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}

internal sealed class ArchitectureExternalDependencyIlScanner : IArchitectureExternalDependencyIlScanner
{
    private static readonly Dictionary<ushort, OpCode> _opCodes = BuildOpCodeMap();

    // Module.ResolveMember is one of the most expensive reflection APIs, and a whole-assembly IL
    // walk hits the same metadata token over and over (every call site of the same method, every
    // access to the same field). Resolution depends only on the module, the token and the generic
    // context the token is resolved in, so the result is cached across every method and every
    // external group this scanner instance scans. The cache is bounded by the number of distinct
    // tokens actually walked and is never shared between scanner instances, keeping it
    // deterministic and per-run.
    private readonly Dictionary<IlTokenKey, MemberInfo?> _resolvedMembers = new();

    // Seam over Module.ResolveMember. The cache is invisible in the scanner's results — an uncached
    // implementation reports exactly the same findings — so a test can only prove the cache exists
    // by observing how often resolution is actually requested. This is that observation point;
    // production always goes through the parameterless constructor below.
    private readonly Func<Module, int, Type[], Type[], MemberInfo?> _resolveMember;

    public ArchitectureExternalDependencyIlScanner()
        : this(static (module, token, typeArguments, methodArguments) =>
            module.ResolveMember(token, typeArguments, methodArguments))
    {
    }

    internal ArchitectureExternalDependencyIlScanner(
        Func<Module, int, Type[], Type[], MemberInfo?> resolveMember)
    {
        _resolveMember = resolveMember;
    }

    public IEnumerable<ArchitectureViolation> FindMethodBodyViolations(
        Type[] sourceTypes,
        string externalGroupName,
        ArchitectureExternalDependencyGroup externalGroup,
        ArchitectureContractExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        // Group matching is per external group, so its cache lives for one call while the token
        // resolution cache above spans the whole scanner instance.
        Dictionary<MemberInfo, ExternalMemberMatch?> matchedTypes = new();

        // Checked per type — the same IL-scanning-per-type boundary
        // ArchitectureIlMethodBodyScanner/ArchitectureTypeIndex already use.
        foreach (Type sourceType in sourceTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string sourceTypeName = ArchitectureTypeNames.SafeFullName(sourceType);
            string sourceAssembly = sourceType.Assembly.GetName().Name ?? string.Empty;
            string[] forbiddenReferences = FindTypeMatches(sourceType, externalGroup, matchedTypes)
                .Where(match => !executionContext.IsIgnored(
                    sourceTypeName,
                    match.Display,
                    sourceAssembly: sourceAssembly,
                    targetAssembly: match.TargetAssembly,
                    targetType: match.TargetType,
                    sourceMember: match.SourceMember,
                    targetMember: match.TargetType))
                .Select(match => match.Display)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            if (forbiddenReferences.Length == 0)
            {
                continue;
            }

            yield return new ArchitectureViolation(
                executionContext.ContractName,
                executionContext.ContractId,
                sourceTypeName,
                $"external dependency group '{externalGroupName}'",
                forbiddenReferences)
            {
                Payload = new ExternalDependencyPayload(externalGroupName)
            };
        }
    }

    private IEnumerable<ExternalIlMatch> FindTypeMatches(
        Type sourceType,
        ArchitectureExternalDependencyGroup externalGroup,
        Dictionary<MemberInfo, ExternalMemberMatch?> matchedTypes)
    {
        foreach (MethodBase method in EnumerateMethods(sourceType))
        {
            foreach (ExternalIlMatch match in FindMethodMatches(method, externalGroup, matchedTypes))
            {
                yield return match;
            }
        }
    }

    private static IEnumerable<MethodBase> EnumerateMethods(Type sourceType)
    {
        const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly; // NOSONAR: intentional — IL scanning needs reflection access to all members

        foreach (MethodInfo method in sourceType.GetMethods(Flags))
        {
            yield return method;
        }

        foreach (ConstructorInfo constructor in sourceType.GetConstructors(Flags))
        {
            yield return constructor;
        }
    }

    private IEnumerable<ExternalIlMatch> FindMethodMatches(
        MethodBase method,
        ArchitectureExternalDependencyGroup externalGroup,
        Dictionary<MemberInfo, ExternalMemberMatch?> matchedTypes)
    {
        MethodBody? body;
        try
        {
            body = method.GetMethodBody();
        }
        catch (FileNotFoundException)
        {
            yield break;
        }

        if (body == null)
        {
            yield break;
        }

        byte[]? il = body.GetILAsByteArray();
        if (il == null || il.Length == 0)
        {
            yield break;
        }

        string methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        if (!IlGenericContext.TryCreate(method, out IlGenericContext genericContext))
        {
            // Reading the generic context used to happen inside the per-token resolve, where a
            // failure made every token of this method unresolvable. Hoisting it out of the loop
            // keeps that outcome: no token of this method can produce a match.
            yield break;
        }

        int position = 0;
        while (position < il.Length)
        {
            if (!TryReadOpCode(il, ref position, out OpCode opCode))
            {
                yield break;
            }

            if (!ArchitectureIlOperandSkipper.TryReadMetadataTokenIfPresent(opCode, il, ref position, out int token))
            {
                yield break;
            }

            if (token == 0)
            {
                continue;
            }

            MemberInfo? referencedMember = ResolveReferencedMember(method.Module, token, genericContext);
            if (referencedMember == null)
            {
                continue;
            }

            if (!matchedTypes.TryGetValue(referencedMember, out ExternalMemberMatch? memberMatch))
            {
                string? matched = FindMatchedExternalType(referencedMember, externalGroup);
                memberMatch = matched == null
                    ? null
                    : new ExternalMemberMatch(matched, referencedMember.DeclaringType?.Assembly.GetName().Name);
                matchedTypes[referencedMember] = memberMatch;
            }

            if (memberMatch == null)
            {
                continue;
            }

            yield return new ExternalIlMatch(
                $"{methodName}: {memberMatch.MatchedType}",
                methodName,
                memberMatch.MatchedType,
                memberMatch.TargetAssembly);
        }
    }

    private sealed record ExternalIlMatch(
        string Display,
        string SourceMember,
        string TargetType,
        string? TargetAssembly);

    // Per-member outcome of matching a resolved IL reference against one external group: the
    // matched type name plus the assembly name reported alongside it.
    private sealed record ExternalMemberMatch(string MatchedType, string? TargetAssembly);

    // The generic arguments Module.ResolveMember needs to resolve a token that appears inside a
    // generic type or generic method. Two methods on the same declaring type share the declaring
    // type's arguments, so caching keyed on this collapses the whole type's tokens into one set.
    private readonly record struct IlGenericContext(Type[] TypeArguments, Type[] MethodArguments)
    {
        public static bool TryCreate(MethodBase method, out IlGenericContext context)
        {
            try
            {
                Type[] typeArgs = method.DeclaringType?.IsGenericType == true
                    ? method.DeclaringType.GetGenericArguments()
                    : Type.EmptyTypes;

                Type[] methodArgs = method.IsGenericMethod
                    ? method.GetGenericArguments()
                    : Type.EmptyTypes;

                context = new IlGenericContext(typeArgs, methodArgs);
                return true;
            }
            catch
            {
                context = new IlGenericContext(Type.EmptyTypes, Type.EmptyTypes);
                return false;
            }
        }

        public bool Equals(IlGenericContext other)
        {
            return SameArguments(TypeArguments, other.TypeArguments)
                && SameArguments(MethodArguments, other.MethodArguments);
        }

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(TypeArguments.Length);
            foreach (Type argument in TypeArguments)
            {
                hash.Add(argument);
            }

            hash.Add(MethodArguments.Length);
            foreach (Type argument in MethodArguments)
            {
                hash.Add(argument);
            }

            return hash.ToHashCode();
        }

        private static bool SameArguments(Type[] left, Type[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    private readonly record struct IlTokenKey(Module Module, int Token, IlGenericContext GenericContext);

    private static string? FindMatchedExternalType(
        MemberInfo member,
        ArchitectureExternalDependencyGroup externalGroup)
    {
        Type? primaryType = member switch
        {
            Type t => t,
            _ => member.DeclaringType
        };

        if (primaryType == null)
        {
            return null;
        }

        string? result = FindMatchedTypeInHierarchy(primaryType, externalGroup);
        if (result != null)
        {
            return result;
        }

        if (member is MethodInfo mi && mi.IsGenericMethod)
        {
            Type[] methodArgs;
            try
            {
                methodArgs = mi.GetGenericArguments();
            }
            catch
            {
                methodArgs = Type.EmptyTypes;
            }

            foreach (Type arg in methodArgs)
            {
                result = FindMatchedTypeInHierarchy(arg, externalGroup);
                if (result != null)
                {
                    return result;
                }
            }
        }

        return null;
    }

    private static string? FindMatchedTypeInHierarchy(
        Type type,
        ArchitectureExternalDependencyGroup externalGroup)
    {
        string fullName = ArchitectureTypeNames.SafeFullName(type);
        string ns = ArchitectureTypeNames.SafeNamespace(type);

        if (ArchitectureExternalDependencyResolver.MatchesGroup(externalGroup, fullName, ns))
        {
            return fullName;
        }

        foreach (Type arg in SafeGetGenericArguments(type))
        {
            string? result = FindMatchedTypeInHierarchy(arg, externalGroup);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static Type[] SafeGetGenericArguments(Type type)
    {
        try
        {
            return type.GetGenericArguments();
        }
        catch
        {
            return Type.EmptyTypes;
        }
    }

    private MemberInfo? ResolveReferencedMember(Module module, int token, IlGenericContext genericContext)
    {
        IlTokenKey key = new(module, token, genericContext);
        if (_resolvedMembers.TryGetValue(key, out MemberInfo? cached))
        {
            return cached;
        }

        MemberInfo? resolved;
        try
        {
            resolved = _resolveMember(module, token, genericContext.TypeArguments, genericContext.MethodArguments);
        }
        catch
        {
            // Unresolvable tokens are cached too: a token that cannot be resolved once cannot be
            // resolved later either, and re-throwing per call site is exactly the cost being removed.
            resolved = null;
        }

        _resolvedMembers[key] = resolved;
        return resolved;
    }

    private static bool TryReadOpCode(byte[] il, ref int position, out OpCode opCode)
    {
        opCode = default;

        if (position >= il.Length)
        {
            return false;
        }

        byte first = il[position++];
        if (first != 0xFE)
        {
            return _opCodes.TryGetValue(first, out opCode);
        }

        if (position >= il.Length)
        {
            return false;
        }

        byte second = il[position++];
        ushort key = (ushort)((first << 8) | second);
        return _opCodes.TryGetValue(key, out opCode);
    }

    private static Dictionary<ushort, OpCode> BuildOpCodeMap()
    {
        Dictionary<ushort, OpCode> result = new();
        IEnumerable<OpCode> opCodes = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!);

        foreach (OpCode opCode in opCodes)
        {
            ushort key = unchecked((ushort)opCode.Value);
            result[key] = opCode;
        }

        return result;
    }
}
