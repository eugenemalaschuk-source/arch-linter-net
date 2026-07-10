using System.Reflection;
using System.Reflection.Emit;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Scanning;

internal interface IArchitectureIlMethodBodyScanner
{
    IEnumerable<ArchitectureViolation> FindMethodBodyViolations(
        IReadOnlyCollection<Assembly> targetAssemblies,
        string sourceNamespacePrefix,
        IReadOnlyList<string> forbiddenCallPatterns,
        ArchitectureContractExecutionContext executionContext,
        ArchitectureLayer? sourceLayer = null);
}

internal readonly record struct ArchitectureIlForbiddenCallMatch(
    string SourceMember,
    string MatchedPattern,
    string MatchedMember);

internal sealed class ArchitectureIlMethodBodyScanner : IArchitectureIlMethodBodyScanner
{
    private static readonly Dictionary<ushort, OpCode> _opCodes = BuildOpCodeMap();

    public IEnumerable<ArchitectureViolation> FindMethodBodyViolations(
        IReadOnlyCollection<Assembly> targetAssemblies,
        string sourceNamespacePrefix,
        IReadOnlyList<string> forbiddenCallPatterns,
        ArchitectureContractExecutionContext executionContext,
        ArchitectureLayer? sourceLayer = null)
    {
        Type[] sourceTypes = sourceLayer != null
            ? ArchitectureTypeScanner.FindTypesInLayer(targetAssemblies, sourceLayer)
            : ArchitectureTypeScanner.FindTypesInNamespace(targetAssemblies, sourceNamespacePrefix);
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
                .Where(match => !executionContext.IsIgnored(sourceTypeName, match))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(match => match, StringComparer.Ordinal)
                .ToList();

            if (matches.Count == 0)
            {
                continue;
            }

            violations.Add(new ArchitectureViolation(
                executionContext.ContractName,
                executionContext.ContractId,
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

    // Shared per-type IL scan loop, extracted so callers outside the namespace/layer-scoped public
    // entry point (e.g. composition contracts, which scan an already-filtered arbitrary type set
    // rather than a single named source layer) can reuse the same IL matching logic without
    // duplicating it. Returns the matched forbidden API's fully-qualified member name per match
    // (not the "il <offset> (...)" diagnostic-formatted strings FindMethodMatches yields for
    // method-body violations), since composition diagnostics report the matched API itself.
    internal static IEnumerable<string> FindMatchesForType(
        Type type,
        IReadOnlyList<ForbiddenCallPattern> patterns,
        Dictionary<string, bool> matchCache)
    {
        return FindMatchDetailsForType(type, patterns, matchCache)
            .Select(match => match.MatchedMember);
    }

    internal static IEnumerable<ArchitectureIlForbiddenCallMatch> FindMatchDetailsForType(
        Type type,
        IReadOnlyList<ForbiddenCallPattern> patterns,
        Dictionary<string, bool> matchCache)
    {
        foreach (MethodBase method in EnumerateMethods(type))
        {
            foreach (IlForbiddenCallMatch match in FindMethodMatchDetails(method, patterns, matchCache))
            {
                yield return new ArchitectureIlForbiddenCallMatch(
                    match.MethodName,
                    match.MatchedPattern,
                    match.MatchedMember);
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

    private static IEnumerable<string> FindMethodMatches(
        MethodBase method,
        IReadOnlyList<ForbiddenCallPattern> patterns,
        Dictionary<string, bool> matchCache)
    {
        foreach (IlForbiddenCallMatch match in FindMethodMatchDetails(method, patterns, matchCache))
        {
            yield return
                $"il {match.InstructionOffset:X4} ({match.MethodName}): {match.MatchedPattern} -> {match.MatchedMember}";
        }
    }

    private readonly record struct IlForbiddenCallMatch(
        int InstructionOffset,
        string MethodName,
        string MatchedPattern,
        string MatchedMember);

    private static IEnumerable<IlForbiddenCallMatch> FindMethodMatchDetails(
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

            if (!ArchitectureIlOperandSkipper.TryReadMetadataTokenIfPresent(opCode, il, ref position, out int token))
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
            yield return new IlForbiddenCallMatch(instructionOffset, methodName, matchedPattern, descriptor.FullyQualifiedMember);
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
