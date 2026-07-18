using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Expressions;

// Populates the closed ArchitectureExpressionSubjectFacts catalog from real reflection/classification
// data. Metadata values are already canonicalized into the string/bool/decimal domains by semantic
// classification (openspec/specs/semantic-classification-model/spec.md); decimal-domain entries are
// omitted here rather than converted, per cel-policy-model's "numeric metadata SHALL NOT be exposed"
// rule — they remain matchable only through literal `metadata` selectors.
internal static class ArchitectureExpressionSubjectFactBuilder
{
    public static ArchitectureExpressionSubjectFacts Build(
        Type type,
        ArchitectureRoleIndex roleIndex,
        ArchitectureSourceFileFactIndex sourceFileFactIndex,
        ProjectDiscoveryResult? projectDiscovery)
    {
        string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
        roleIndex.TryGetRole(type, out ArchitectureTypeClassificationResult descriptor);

        (Dictionary<string, string> metadataText, Dictionary<string, bool> metadataBool) =
            SplitMetadata(descriptor?.Metadata);
        IReadOnlyList<string> sourcePaths = ResolveSourcePaths(type, assemblyName, sourceFileFactIndex);

        return new ArchitectureExpressionSubjectFacts(
            FullName: ArchitectureTypeNames.SafeFullName(type),
            SimpleName: GetSimpleName(type),
            Namespace: ArchitectureTypeNames.SafeNamespace(type),
            AssemblyName: assemblyName,
            ProjectName: ResolveProjectName(assemblyName, projectDiscovery),
            Role: descriptor?.Role ?? string.Empty,
            MetadataText: metadataText,
            MetadataBool: metadataBool,
            Kind: ResolveKind(type),
            IsAbstract: SafeIsAbstract(type),
            IsSealed: SafeIsSealed(type),
            BaseTypeNames: BuildBaseTypeNames(type),
            InterfaceTypeNames: BuildInterfaceTypeNames(type),
            AttributeTypeNames: BuildAttributeTypeNames(type),
            SourcePaths: sourcePaths,
            SourceDirectoryPrefixes: BuildSourceDirectoryPrefixes(sourcePaths));
    }

    private static (Dictionary<string, string> Text, Dictionary<string, bool> Bool) SplitMetadata(
        IReadOnlyDictionary<string, object>? metadata)
    {
        Dictionary<string, string> text = new(StringComparer.Ordinal);
        Dictionary<string, bool> boolValues = new(StringComparer.Ordinal);
        if (metadata == null)
        {
            return (text, boolValues);
        }

        foreach ((string key, object value) in metadata)
        {
            switch (value)
            {
                case bool b:
                    boolValues[key] = b;
                    break;
                case string s:
                    text[key] = s;
                    break;
                    // Decimal-domain metadata (any numeric primitive, canonicalized) is intentionally
                    // omitted from the CEL-facing subject shape — see the type-level doc comment.
            }
        }

        return (text, boolValues);
    }

    private static string GetSimpleName(Type type)
    {
        string name = type.Name;
        int backtick = name.IndexOf('`');
        return backtick >= 0 ? name[..backtick] : name;
    }

    private static string ResolveProjectName(string assemblyName, ProjectDiscoveryResult? projectDiscovery)
    {
        ArchitectureDiscoveredProject? project = projectDiscovery?.DiscoveredProjects
            .FirstOrDefault(candidate => string.Equals(candidate.AssemblyName, assemblyName, StringComparison.Ordinal));
        if (project == null)
        {
            return assemblyName;
        }

        string normalizedPath = project.Path.Replace('\\', '/');
        int lastSlash = normalizedPath.LastIndexOf('/');
        string fileName = lastSlash >= 0 ? normalizedPath[(lastSlash + 1)..] : normalizedPath;
        int dot = fileName.LastIndexOf('.');
        return dot > 0 ? fileName[..dot] : fileName;
    }

    private static string ResolveKind(Type type)
    {
        if (type.IsEnum) return "enum";
        if (type.IsInterface) return "interface";
        if (type.IsValueType) return "struct";
        if (typeof(Delegate).IsAssignableFrom(type) && type != typeof(Delegate) && type != typeof(MulticastDelegate))
        {
            return "delegate";
        }

        return "class";
    }

    private static bool SafeIsAbstract(Type type)
    {
        try { return type.IsAbstract; }
        catch (FileNotFoundException) { return false; }
        catch (TypeLoadException) { return false; }
    }

    private static bool SafeIsSealed(Type type)
    {
        try { return type.IsSealed; }
        catch (FileNotFoundException) { return false; }
        catch (TypeLoadException) { return false; }
    }

    private static IReadOnlyList<string> BuildBaseTypeNames(Type type)
    {
        List<string> names = new();
        Type? current = SafeBaseType(type);
        while (current != null)
        {
            names.Add(ArchitectureTypeNames.SafeFullName(current));
            current = SafeBaseType(current);
        }

        return names;
    }

    private static Type? SafeBaseType(Type type)
    {
        try { return type.BaseType; }
        catch (FileNotFoundException) { return null; }
        catch (TypeLoadException) { return null; }
    }

    private static IReadOnlyList<string> BuildInterfaceTypeNames(Type type)
    {
        return ArchitectureReferenceScanner.SafeGetInterfaces(type)
            .Select(ArchitectureTypeNames.SafeFullName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string> BuildAttributeTypeNames(Type type)
    {
        try
        {
            return type.GetCustomAttributesData()
                .Select(data => data.AttributeType.FullName ?? data.AttributeType.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<string>();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<string>();
        }
    }

    // A single source declaration yields one path; a partial class declared across multiple files
    // (recorded by the source index as an ambiguity, with a null SourceFilePath on the fact itself)
    // yields every known declaration path. A type with no known source (reflection-only run, or no
    // source roots configured) yields an empty list.
    private static IReadOnlyList<string> ResolveSourcePaths(
        Type type,
        string assemblyName,
        ArchitectureSourceFileFactIndex sourceFileFactIndex)
    {
        string fullTypeName = ArchitectureTypeNames.SafeFullName(type);
        if (sourceFileFactIndex.TryGetFact(assemblyName, fullTypeName, out ArchitectureDeclaredTypeFact fact)
            && fact.SourceFilePath != null)
        {
            return new[] { fact.SourceFilePath };
        }

        ArchitectureDeclaredTypeSourceAmbiguity? ambiguity = sourceFileFactIndex.Ambiguities.FirstOrDefault(
            candidate => string.Equals(candidate.AssemblyName, assemblyName, StringComparison.Ordinal)
                && string.Equals(candidate.FullTypeName, fullTypeName, StringComparison.Ordinal));
        return ambiguity?.SourceFilePaths ?? Array.Empty<string>();
    }

    private static IReadOnlyList<string> BuildSourceDirectoryPrefixes(IReadOnlyList<string> sourcePaths)
    {
        HashSet<string> prefixes = new(StringComparer.Ordinal);
        foreach (string path in sourcePaths)
        {
            string normalized = path.Replace('\\', '/');
            int lastSlash = normalized.LastIndexOf('/');
            while (lastSlash > 0)
            {
                prefixes.Add(normalized[..lastSlash]);
                lastSlash = normalized.LastIndexOf('/', lastSlash - 1);
            }
        }

        return prefixes.OrderBy(prefix => prefix, StringComparer.Ordinal).ToList();
    }
}
