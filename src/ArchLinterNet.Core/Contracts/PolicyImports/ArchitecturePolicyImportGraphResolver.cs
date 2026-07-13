using ArchLinterNet.Core.IO.Abstractions;

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
        ArchitecturePolicyRootPath root = _pathResolver.ResolveRoot(rootPath);
        string rootIdentity = Path.GetRelativePath(root.BoundaryPath, root.FullPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        ArchitecturePolicySource rootSource = _parser.Parse(
            ArchitecturePolicySourceRole.Root,
            root.FullPath,
            root.PhysicalPath,
            rootIdentity,
            rootYaml);
        var state = new ResolutionState(root, rootSource);

        Visit(rootSource, depth: 0, state);
        return state.Sources;
    }

    private void Visit(ArchitecturePolicySource source, int depth, ResolutionState state)
    {
        state.Active.Add(source.PhysicalPath);
        state.Stack.Add(source.PortableIdentity);
        state.Sources.Add(source);

        foreach (string importPath in source.Imports)
        {
            _parser.ValidatePortableImport(importPath, source.PortableIdentity);
            if (depth == MaximumDepth)
            {
                throw Limit($"Import depth exceeds {MaximumDepth}: {FormatChain(state.Stack, importPath)}");
            }

            if (state.Sources.Count == MaximumFileCount)
            {
                throw Limit($"Policy import graph exceeds {MaximumFileCount} files: {FormatChain(state.Stack, importPath)}");
            }

            ArchitecturePolicyResolvedPath resolved = _pathResolver.ResolveImport(
                state.Root,
                source.FullPath,
                importPath);
            if (state.Active.Contains(resolved.PhysicalPath))
            {
                throw new ArchitecturePolicyImportException(
                    ArchitecturePolicyImportErrorCategory.Cycle,
                    $"Policy import cycle detected: {FormatChain(state.Stack, resolved.PortableIdentity)}");
            }

            if (state.Completed.Contains(resolved.PhysicalPath)
                || state.PortableIdentities.Contains(resolved.PortableIdentity))
            {
                string first = state.FirstImports.GetValueOrDefault(resolved.PhysicalPath, resolved.PortableIdentity);
                throw new ArchitecturePolicyImportException(
                    ArchitecturePolicyImportErrorCategory.DuplicateImport,
                    $"Duplicate policy import '{importPath}' resolves to '{resolved.PortableIdentity}'; first reached as '{first}'.");
            }

            string yaml = _fileSystem.ReadAllText(resolved.FullPath);
            ArchitecturePolicySource child = _parser.Parse(
                ArchitecturePolicySourceRole.Fragment,
                resolved.FullPath,
                resolved.PhysicalPath,
                resolved.PortableIdentity,
                yaml);
            state.PortableIdentities.Add(resolved.PortableIdentity);
            state.FirstImports[resolved.PhysicalPath] = importPath;
            Visit(child, depth + 1, state);
        }

        state.Stack.RemoveAt(state.Stack.Count - 1);
        state.Active.Remove(source.PhysicalPath);
        state.Completed.Add(source.PhysicalPath);
    }

    private static string FormatChain(IEnumerable<string> stack, string next)
    {
        return string.Join(" -> ", stack.Append(next));
    }

    private static ArchitecturePolicyImportException Limit(string message)
    {
        return new ArchitecturePolicyImportException(ArchitecturePolicyImportErrorCategory.GraphLimit, message);
    }

    private sealed class ResolutionState
    {
        public ResolutionState(ArchitecturePolicyRootPath root, ArchitecturePolicySource rootSource)
        {
            Root = root;
            PortableIdentities.Add(rootSource.PortableIdentity);
            FirstImports[rootSource.PhysicalPath] = rootSource.PortableIdentity;
        }

        public ArchitecturePolicyRootPath Root { get; }
        public List<ArchitecturePolicySource> Sources { get; } = new();
        public HashSet<string> Active { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Completed { get; } = new(StringComparer.Ordinal);
        public HashSet<string> PortableIdentities { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> FirstImports { get; } = new(StringComparer.Ordinal);
        public List<string> Stack { get; } = new();
    }
}
