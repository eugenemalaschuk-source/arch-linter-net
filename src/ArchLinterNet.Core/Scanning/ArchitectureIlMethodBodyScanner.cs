using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Scanning;

internal static class ArchitectureIlMethodBodyScanner
{
    private static readonly Dictionary<ushort, OpCode> _opCodes = BuildOpCodeMap();

    public static IEnumerable<ArchitectureViolation> FindMethodBodyViolations(
        string contractName,
        IReadOnlyCollection<Assembly> targetAssemblies,
        string sourceNamespacePrefix,
        IReadOnlyList<string> forbiddenCallPatterns,
        IReadOnlyList<ArchitectureIgnoredViolation> ignoredViolations)
    {
        Type[] sourceTypes = ArchitectureTypeScanner.FindTypesInNamespace(targetAssemblies, sourceNamespacePrefix);
        if (sourceTypes.Length == 0)
        {
            return Array.Empty<ArchitectureViolation>();
        }

        IReadOnlyList<ForbiddenCallPattern> patterns =
            ArchitectureForbiddenCallMatcher.NormalizePatterns(forbiddenCallPatterns);
        Dictionary<string, bool> matchCache = new(StringComparer.Ordinal);
        List<ArchitectureViolation> violations = new();

        foreach (Type sourceType in sourceTypes)
        {
            string sourceTypeName = ArchitectureTypeNames.SafeFullName(sourceType);
            var matches = FindTypeMatches(sourceType, patterns, matchCache)
                .Where(match => !ArchitectureIgnoreMatcher.IsIgnored(sourceTypeName, match, ignoredViolations))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(match => match, StringComparer.Ordinal)
                .ToList();

            if (matches.Count == 0)
            {
                continue;
            }

            violations.Add(new ArchitectureViolation(
                contractName,
                sourceTypeName,
                "method-body-il",
                matches));
        }

        return violations;
    }

    private static IEnumerable<string> FindTypeMatches(
        Type sourceType,
        IReadOnlyList<ForbiddenCallPattern> patterns,
        Dictionary<string, bool> matchCache)
    {
        foreach (MethodBase method in EnumerateMethods(sourceType))
        {
            foreach (string match in FindMethodMatches(method, patterns, matchCache))
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
        IReadOnlyList<ForbiddenCallPattern> patterns,
        Dictionary<string, bool> matchCache)
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

        int position = 0;
        while (position < il.Length)
        {
            int instructionOffset = position;
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

            SymbolDescriptor descriptor = ArchitectureForbiddenCallMatcher.FromMemberInfo(referencedMember);
            if (!ArchitectureForbiddenCallMatcher.TryMatch(descriptor, patterns, matchCache, out string matchedPattern))
            {
                continue;
            }

            string methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            yield return
                $"il {instructionOffset:X4} ({methodName}): {matchedPattern} -> {descriptor.FullyQualifiedMember}";
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
