using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureExternalDependencyIlScanner
{
    private static readonly Dictionary<ushort, OpCode> _opCodes = BuildOpCodeMap();

    public static IEnumerable<ArchitectureViolation> FindMethodBodyViolations(
        string contractName,
        string? contractId,
        Type[] sourceTypes,
        string externalGroupName,
        ArchitectureExternalDependencyGroup externalGroup,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations,
        ArchitectureIgnoreUsageTracker? usageTracker = null,
        string? contractGroup = null,
        List<ArchitectureBaselineCandidate>? baselineCandidates = null)
    {
        foreach (Type sourceType in sourceTypes)
        {
            string sourceTypeName = ArchitectureTypeNames.SafeFullName(sourceType);
            string[] forbiddenReferences = FindTypeMatches(sourceType, externalGroup)
                .Where(match =>
                {
                    bool ignored = ArchitectureIgnoreMatcher.IsIgnored(sourceTypeName, match, ignoredViolations, usageTracker);
                    if (!ignored && contractGroup != null && baselineCandidates != null)
                    {
                        baselineCandidates.Add(new ArchitectureBaselineCandidate(contractGroup, contractId, sourceTypeName, match));
                    }
                    return !ignored;
                })
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            if (forbiddenReferences.Length == 0)
            {
                continue;
            }

            yield return new ArchitectureViolation(
                contractName,
                contractId,
                sourceTypeName,
                $"external dependency group '{externalGroupName}'",
                forbiddenReferences)
            {
                ForbiddenExternalGroup = externalGroupName
            };
        }
    }

    private static IEnumerable<string> FindTypeMatches(
        Type sourceType,
        ArchitectureExternalDependencyGroup externalGroup)
    {
        foreach (MethodBase method in EnumerateMethods(sourceType))
        {
            foreach (string match in FindMethodMatches(method, externalGroup))
            {
                yield return match;
            }
        }
    }

    private static IEnumerable<MethodBase> EnumerateMethods(Type sourceType)
    {
        const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        foreach (MethodInfo method in sourceType.GetMethods(Flags))
        {
            yield return method;
        }

        foreach (ConstructorInfo constructor in sourceType.GetConstructors(Flags))
        {
            yield return constructor;
        }
    }

    private static IEnumerable<string> FindMethodMatches(
        MethodBase method,
        ArchitectureExternalDependencyGroup externalGroup)
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

        int position = 0;
        while (position < il.Length)
        {
            if (!TryReadOpCode(il, ref position, out OpCode opCode))
            {
                yield break;
            }

            if (!TryReadMetadataTokenIfPresent(opCode, il, ref position, out int token))
            {
                yield break;
            }

            if (token == 0)
            {
                continue;
            }

            MemberInfo? referencedMember = ResolveReferencedMember(method, token);
            if (referencedMember == null)
            {
                continue;
            }

            string? matchedType = MatchReferencedTypes(referencedMember, externalGroup);
            if (matchedType == null)
            {
                continue;
            }

            yield return $"{methodName}: {matchedType}";
        }
    }

    private static string? MatchReferencedTypes(
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

        if (MatchesGroupOrGenericArgs(primaryType, externalGroup))
        {
            return FormatMemberName(member);
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
                if (MatchesGroupOrGenericArgs(arg, externalGroup))
                {
                    return FormatMemberName(member);
                }
            }
        }

        return null;
    }

    private static string FormatMemberName(MemberInfo member)
    {
        Type? declaringType = member switch
        {
            Type t => t,
            _ => member.DeclaringType
        };

        string fullName = declaringType != null
            ? ArchitectureTypeNames.SafeFullName(declaringType)
            : string.Empty;

        return member switch
        {
            MethodInfo m => $"{fullName}.{m.Name}",
            ConstructorInfo c => $"{fullName}..ctor",
            PropertyInfo p => $"{fullName}.{p.Name}",
            FieldInfo f => $"{fullName}.{f.Name}",
            EventInfo e => $"{fullName}.{e.Name}",
            Type t => ArchitectureTypeNames.SafeFullName(t),
            _ => fullName
        };
    }

    private static bool MatchesGroupOrGenericArgs(
        Type type,
        ArchitectureExternalDependencyGroup externalGroup)
    {
        string fullName = ArchitectureTypeNames.SafeFullName(type);
        string ns = ArchitectureTypeNames.SafeNamespace(type);

        if (ArchitectureExternalDependencyResolver.MatchesGroup(externalGroup, fullName, ns))
        {
            return true;
        }

        foreach (Type arg in SafeGetGenericArguments(type))
        {
            if (MatchesGroupOrGenericArgs(arg, externalGroup))
            {
                return true;
            }
        }

        return false;
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

    private static MemberInfo? ResolveReferencedMember(MethodBase method, int token)
    {
        try
        {
            Type[] typeArgs = method.DeclaringType?.IsGenericType == true
                ? method.DeclaringType.GetGenericArguments()
                : Type.EmptyTypes;

            Type[] methodArgs = method.IsGenericMethod
                ? method.GetGenericArguments()
                : Type.EmptyTypes;

            return method.Module.ResolveMember(token, typeArgs, methodArgs);
        }
        catch
        {
            return null;
        }
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

    private static bool TryReadMetadataTokenIfPresent(OpCode opCode, byte[] il, ref int position, out int token)
    {
        token = 0;

        switch (opCode.OperandType)
        {
            case OperandType.InlineMethod:
            case OperandType.InlineField:
            case OperandType.InlineType:
            case OperandType.InlineTok:
                if (!CanRead(il, position, 4))
                {
                    return false;
                }

                token = BitConverter.ToInt32(il, position);
                position += 4;
                return true;

            case OperandType.InlineSwitch:
                if (!CanRead(il, position, 4))
                {
                    return false;
                }

                int caseCount = BitConverter.ToInt32(il, position);
                int size = 4 + caseCount * 4;
                if (!CanRead(il, position, size))
                {
                    return false;
                }

                position += size;
                return true;

            case OperandType.ShortInlineBrTarget:
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
                if (!CanRead(il, position, 1))
                {
                    return false;
                }

                position += 1;
                return true;

            case OperandType.ShortInlineR:
                if (!CanRead(il, position, 4))
                {
                    return false;
                }

                position += 4;
                return true;

            case OperandType.InlineVar:
                if (!CanRead(il, position, 2))
                {
                    return false;
                }

                position += 2;
                return true;

            case OperandType.InlineI:
            case OperandType.InlineBrTarget:
            case OperandType.InlineSig:
            case OperandType.InlineString:
                if (!CanRead(il, position, 4))
                {
                    return false;
                }

                position += 4;
                return true;

            case OperandType.InlineR:
            case OperandType.InlineI8:
                if (!CanRead(il, position, 8))
                {
                    return false;
                }

                position += 8;
                return true;

            case OperandType.InlineNone:
                return true;

            default:
                return true;
        }
    }

    private static bool CanRead(byte[] il, int position, int size)
    {
        return size >= 0 && position >= 0 && position <= il.Length - size;
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
