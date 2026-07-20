using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Contracts.PolicyImports;

internal sealed class ArchitecturePolicyImportGraphResolver
{
    internal const int MaximumDepth = 16;
    internal const int MaximumFileCount = 256;

    private readonly IArchitectureFileSystem _fileSystem;
    private readonly IArchitecturePolicyPathResolver _pathResolver;
    private readonly ArchitecturePolicySourceParser _parser;

    public ArchitecturePolicyImportGraphResolver(
        IArchitectureFileSystem fileSystem,
        IArchitecturePolicyPathResolver pathResolver,
        ArchitecturePolicySourceParser parser)
    {
        _fileSystem = fileSystem;
        _pathResolver = pathResolver;
        _parser = parser;
    }

    public IReadOnlyList<ArchitecturePolicySource> Resolve(string rootPath, string rootYaml)
    {
        ArchitecturePolicyRootPath root;
        try
        {
            root = _pathResolver.ResolveRoot(rootPath);
        }
        catch (ArchitecturePolicyImportException exception)
        {
            throw ArchitecturePolicyDiagnosticFactory.EnrichRoot(
                exception,
                ArchitecturePolicyProvenanceFactory.CreateUnresolvedRootDescriptor(rootPath));
        }

        string rootIdentity = Path.GetRelativePath(root.BoundaryPath, root.FullPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        var rootDescriptor = new ArchitecturePolicySourceDescriptor(
            rootIdentity,
            rootIdentity,
            ArchitecturePolicyDocumentRole.Root,
            0,
            null,
            null,
            new[] { rootIdentity });
        ArchitecturePolicySource rootSource = _parser.Parse(
            rootDescriptor,
            root.FullPath,
            root.PhysicalPath,
            root.FileIdentity,
            rootYaml);
        var state = new ResolutionState(root, rootSource);

        Visit(rootSource, depth: 0, state);
        return state.Sources;
    }

    private void Visit(ArchitecturePolicySource source, int depth, ResolutionState state)
    {
        state.Active.Add(source.FileIdentity);
        state.Stack.Add(source.PortableIdentity);
        state.Sources.Add(source);

        for (int importIndex = 0; importIndex < source.Imports.Count; importIndex++)
        {
            VisitImport(source, importIndex, depth, state);
        }

        state.Stack.RemoveAt(state.Stack.Count - 1);
        state.Active.Remove(source.FileIdentity);
        state.Completed.Add(source.FileIdentity);
    }

    private void VisitImport(
        ArchitecturePolicySource source,
        int importIndex,
        int depth,
        ResolutionState state)
    {
        string importPath = source.Imports[importIndex];
        ArchitecturePolicySourceLocation importLocation =
            ArchitecturePolicyDiagnosticFactory.ImportLocation(source, importIndex);
        ArchitecturePolicySourceParser.ValidatePortableImport(importPath, source, importIndex);
        EnsureWithinLimits(depth, state, importPath, importLocation);

        ArchitecturePolicyResolvedPath resolved = ResolveImport(source, state, importPath, importLocation);
        EnsureNotActive(state, resolved, importLocation);
        EnsureNotDuplicate(state, resolved, importPath, importLocation);
        ParseAndVisitImport(source, depth, state, importPath, importLocation, resolved);
    }

    private static void EnsureWithinLimits(
        int depth,
        ResolutionState state,
        string importPath,
        ArchitecturePolicySourceLocation importLocation)
    {
        if (depth == MaximumDepth)
        {
            throw Limit(
                $"Import depth exceeds {MaximumDepth}: {FormatChain(state.Stack, importPath)}",
                importLocation,
                state.Stack.Append(importPath).ToArray());
        }

        if (state.Sources.Count == MaximumFileCount)
        {
            throw Limit(
                $"Policy import graph exceeds {MaximumFileCount} files: {FormatChain(state.Stack, importPath)}",
                importLocation,
                state.Stack.Append(importPath).ToArray());
        }
    }

    private ArchitecturePolicyResolvedPath ResolveImport(
        ArchitecturePolicySource source,
        ResolutionState state,
        string importPath,
        ArchitecturePolicySourceLocation importLocation)
    {
        try
        {
            return _pathResolver.ResolveImport(state.Root, source.FullPath, importPath);
        }
        catch (ArchitecturePolicyImportException exception)
        {
            throw ArchitecturePolicyDiagnosticFactory.Enrich(
                exception,
                importLocation,
                state.Stack.Append(importPath));
        }
    }

    private static void EnsureNotActive(
        ResolutionState state,
        ArchitecturePolicyResolvedPath resolved,
        ArchitecturePolicySourceLocation importLocation)
    {
        if (!state.Active.Contains(resolved.FileIdentity))
        {
            return;
        }

        throw ArchitecturePolicyDiagnosticFactory.Exception(
            ArchitecturePolicyImportErrorCategory.Cycle,
            $"Policy import cycle detected: {FormatChain(state.Stack, resolved.PortableIdentity)}",
            importLocation,
            RelatedLocationFor(state.Sources, source => source.FileIdentity == resolved.FileIdentity),
            state.Stack.Append(resolved.PortableIdentity).ToArray());
    }

    private static void EnsureNotDuplicate(
        ResolutionState state,
        ArchitecturePolicyResolvedPath resolved,
        string importPath,
        ArchitecturePolicySourceLocation importLocation)
    {
        if (!state.Completed.Contains(resolved.FileIdentity)
            && !state.PortableIdentities.Contains(resolved.PortableIdentity))
        {
            return;
        }

        string first = state.FirstImports.GetValueOrDefault(resolved.FileIdentity, resolved.PortableIdentity);
        throw ArchitecturePolicyDiagnosticFactory.Exception(
            ArchitecturePolicyImportErrorCategory.DuplicateImport,
            $"Duplicate policy import '{importPath}' resolves to '{resolved.PortableIdentity}'; first reached as '{first}'.",
            importLocation,
            RelatedLocationFor(
                state.Sources,
                source => source.FileIdentity == resolved.FileIdentity
                    || string.Equals(source.PortableIdentity, resolved.PortableIdentity, StringComparison.OrdinalIgnoreCase)),
            state.Stack.Append(resolved.PortableIdentity));
    }

    private void ParseAndVisitImport(
        ArchitecturePolicySource source,
        int depth,
        ResolutionState state,
        string importPath,
        ArchitecturePolicySourceLocation importLocation,
        ArchitecturePolicyResolvedPath resolved)
    {
        string[] importChain = state.Stack.Append(resolved.PortableIdentity).ToArray();
        var descriptor = new ArchitecturePolicySourceDescriptor(
            state.RootSource.Descriptor.RootPath,
            resolved.PortableIdentity,
            ArchitecturePolicyDocumentRole.Fragment,
            state.Sources.Count,
            source.PortableIdentity,
            importPath,
            importChain);
        ArchitecturePolicySource child = _parser.Parse(
            descriptor,
            resolved.FullPath,
            resolved.PhysicalPath,
            resolved.FileIdentity,
            ArchitecturePolicySourceReader.ReadAllText(
                _fileSystem,
                resolved.FullPath,
                resolved.PortableIdentity,
                importLocation,
                importChain));
        state.PortableIdentities.Add(resolved.PortableIdentity);
        state.FirstImports[resolved.FileIdentity] = importPath;
        Visit(child, depth + 1, state);
    }

    private static ArchitecturePolicySourceLocation[] RelatedLocationFor(
        IEnumerable<ArchitecturePolicySource> sources,
        Func<ArchitecturePolicySource, bool> predicate)
    {
        ArchitecturePolicySource? source = sources.FirstOrDefault(predicate);
        return source is null
            ? Array.Empty<ArchitecturePolicySourceLocation>()
            : new[] { ArchitecturePolicyDiagnosticFactory.Location(source, "$") };
    }

    private static string FormatChain(IEnumerable<string> stack, string next)
    {
        return string.Join(" -> ", stack.Append(next));
    }

    private static ArchitecturePolicyImportException Limit(
        string message,
        ArchitecturePolicySourceLocation location,
        IReadOnlyList<string> importChain)
    {
        return ArchitecturePolicyDiagnosticFactory.Exception(
            ArchitecturePolicyImportErrorCategory.GraphLimit,
            message,
            location,
            importChain: importChain);
    }

    private sealed class ResolutionState
    {
        public ResolutionState(ArchitecturePolicyRootPath root, ArchitecturePolicySource rootSource)
        {
            Root = root;
            RootSource = rootSource;
            PortableIdentities.Add(rootSource.PortableIdentity);
            FirstImports[rootSource.FileIdentity] = rootSource.PortableIdentity;
        }

        public ArchitecturePolicyRootPath Root { get; }
        public ArchitecturePolicySource RootSource { get; }
        public List<ArchitecturePolicySource> Sources { get; } = new();
        public HashSet<string> Active { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Completed { get; } = new(StringComparer.Ordinal);
        public HashSet<string> PortableIdentities { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> FirstImports { get; } = new(StringComparer.Ordinal);
        public List<string> Stack { get; } = new();
    }
}
