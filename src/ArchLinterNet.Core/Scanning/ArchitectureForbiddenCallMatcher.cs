using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace ArchLinterNet.Core.Scanning;

internal sealed record ForbiddenCallPattern(string Raw, string Normalized, bool IsNamespacePrefix);

internal sealed record SymbolDescriptor(
    string Name,
    string ContainingTypeName,
    string ContainingNamespace,
    string FullyQualifiedMember,
    string? ExtensionReceiverType = null)
{
    public string CacheKey => $"{ContainingNamespace}|{ContainingTypeName}|{Name}|{FullyQualifiedMember}|{ExtensionReceiverType}";
}

internal static class ArchitectureForbiddenCallMatcher
{
    public static IReadOnlyList<ForbiddenCallPattern> NormalizePatterns(IReadOnlyList<string> forbiddenPatterns)
    {
        List<ForbiddenCallPattern> result = new(forbiddenPatterns.Count);

        foreach (string pattern in forbiddenPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            string normalized = pattern.Trim();

            if (normalized.EndsWith('('))
            {
                normalized = normalized[..^1];
            }

            bool isNamespacePrefix = normalized.EndsWith('.');
            if (isNamespacePrefix)
            {
                normalized = normalized.TrimEnd('.');
            }

            result.Add(new ForbiddenCallPattern(pattern, normalized, isNamespacePrefix));
        }

        return result;
    }

    public static bool TryMatch(
        SymbolDescriptor symbol,
        IReadOnlyList<ForbiddenCallPattern> patterns,
        Dictionary<string, bool> cache,
        out string matchedRawPattern)
    {
        foreach (ForbiddenCallPattern pattern in patterns)
        {
            string cacheKey = $"{symbol.CacheKey}||{pattern.Normalized}||{pattern.IsNamespacePrefix}";
            if (!cache.TryGetValue(cacheKey, out bool isMatch))
            {
                isMatch = Matches(symbol, pattern);
                cache[cacheKey] = isMatch;
            }

            if (!isMatch)
            {
                continue;
            }

            matchedRawPattern = pattern.Raw;
            return true;
        }

        matchedRawPattern = string.Empty;
        return false;
    }

    public static SymbolDescriptor FromRoslynSymbol(ISymbol symbol)
    {
        string name = symbol.Name;
        string containingTypeName = symbol.ContainingType?.Name ?? string.Empty;
        string containingNamespace = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        string fullyQualifiedMember = string.IsNullOrWhiteSpace(symbol.ContainingType?.ToDisplayString())
            ? name
            : $"{symbol.ContainingType!.ToDisplayString()}.{name}";
        string? extensionReceiverType = symbol is IMethodSymbol methodSymbol
            ? GetRoslynExtensionReceiverType(methodSymbol)
            : null;

        return new SymbolDescriptor(name, containingTypeName, containingNamespace, fullyQualifiedMember, extensionReceiverType);
    }

    public static SymbolDescriptor FromMemberInfo(MemberInfo memberInfo)
    {
        Type? containingType = memberInfo.DeclaringType;
        string containingTypeName = containingType?.Name ?? string.Empty;
        string containingNamespace = containingType?.Namespace ?? string.Empty;
        string name = memberInfo.Name;
        string fullyQualifiedMember = string.IsNullOrWhiteSpace(containingType?.FullName)
            ? name
            : $"{containingType!.FullName}.{name}";
        string? extensionReceiverType = memberInfo is MethodInfo methodInfo
            ? GetReflectionExtensionReceiverType(methodInfo)
            : null;

        return new SymbolDescriptor(name, containingTypeName, containingNamespace, fullyQualifiedMember, extensionReceiverType);
    }

    private static bool Matches(SymbolDescriptor symbol, ForbiddenCallPattern pattern)
    {
        if (pattern.IsNamespacePrefix)
        {
            return PrefixMatches(symbol.ContainingNamespace, pattern.Normalized)
                   || PrefixMatches(symbol.FullyQualifiedMember, pattern.Normalized)
                   || PrefixMatches(symbol.ExtensionReceiverType, pattern.Normalized);
        }

        if (symbol.Name.Equals(pattern.Normalized, StringComparison.Ordinal))
        {
            return true;
        }

        string qualifiedTypeMember = string.IsNullOrWhiteSpace(symbol.ContainingTypeName)
            ? symbol.Name
            : $"{symbol.ContainingTypeName}.{symbol.Name}";

        if (qualifiedTypeMember.Equals(pattern.Normalized, StringComparison.Ordinal))
        {
            return true;
        }

        return symbol.FullyQualifiedMember.Equals(pattern.Normalized, StringComparison.Ordinal);
    }

    private static bool PrefixMatches(string? value, string normalizedPrefix)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return value.Equals(normalizedPrefix, StringComparison.Ordinal)
               || value.StartsWith(normalizedPrefix + ".", StringComparison.Ordinal);
    }

    private static string? GetRoslynExtensionReceiverType(IMethodSymbol methodSymbol)
    {
        IMethodSymbol sourceMethod = methodSymbol.ReducedFrom ?? methodSymbol;
        if (!sourceMethod.IsExtensionMethod || sourceMethod.Parameters.Length == 0)
        {
            return null;
        }

        return sourceMethod.Parameters[0].Type.ToDisplayString();
    }

    private static string? GetReflectionExtensionReceiverType(MethodInfo methodInfo)
    {
        if (!methodInfo.IsDefined(typeof(ExtensionAttribute), inherit: false))
        {
            return null;
        }

        ParameterInfo[] parameters = methodInfo.GetParameters();
        return parameters.Length == 0 ? null : parameters[0].ParameterType.FullName;
    }
}
