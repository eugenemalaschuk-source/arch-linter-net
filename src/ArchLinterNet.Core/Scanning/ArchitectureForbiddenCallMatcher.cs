using System.Reflection;
using Microsoft.CodeAnalysis;

namespace ArchLinterNet.Core.Scanning;

internal sealed record ForbiddenCallPattern(string Raw, string Normalized, bool IsNamespacePrefix);

internal sealed record SymbolDescriptor(
    string Name,
    string ContainingTypeName,
    string ContainingNamespace,
    string FullyQualifiedMember)
{
    public string CacheKey => $"{ContainingNamespace}|{ContainingTypeName}|{Name}|{FullyQualifiedMember}";
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

            if (normalized.EndsWith("(", StringComparison.Ordinal))
            {
                normalized = normalized[..^1];
            }

            bool isNamespacePrefix = normalized.EndsWith(".", StringComparison.Ordinal);
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

        return new SymbolDescriptor(name, containingTypeName, containingNamespace, fullyQualifiedMember);
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

        return new SymbolDescriptor(name, containingTypeName, containingNamespace, fullyQualifiedMember);
    }

    private static bool Matches(SymbolDescriptor symbol, ForbiddenCallPattern pattern)
    {
        if (pattern.IsNamespacePrefix)
        {
            return !string.IsNullOrEmpty(symbol.ContainingNamespace)
                   && symbol.ContainingNamespace.StartsWith(pattern.Normalized, StringComparison.Ordinal);
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
}
