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
        ArchitectureContractExecutionContext executionContext);
}

internal sealed class ArchitectureExternalDependencyIlScanner : IArchitectureExternalDependencyIlScanner
{
    private static readonly Dictionary<ushort, OpCode> _opCodes = BuildOpCodeMap();

    public IEnumerable<ArchitectureViolation> FindMethodBodyViolations(
        Type[] sourceTypes,
        string externalGroupName,
        ArchitectureExternalDependencyGroup externalGroup,
        ArchitectureContractExecutionContext executionContext)
    {
        foreach (Type sourceType in sourceTypes)
        {
            string sourceTypeName = ArchitectureTypeNames.SafeFullName(sourceType);
            string[] forbiddenReferences = FindTypeMatches(sourceType, externalGroup)
                .Where(match => !executionContext.IsIgnored(sourceTypeName, match))
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

            string? matchedType = FindMatchedExternalType(referencedMember, externalGroup);
            if (matchedType == null)
            {
                continue;
            }

            yield return $"{methodName}: {matchedType}";
        }
    }

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
