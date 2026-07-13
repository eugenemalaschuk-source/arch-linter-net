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
        ArchitecturePolicyRootPath root = _pathResolver.ResolveRoot(rootPath);
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
            string importPath = source.Imports[importIndex];
            ArchitecturePolicySourceLocation importLocation =
                ArchitecturePolicyDiagnosticFactory.ImportLocation(source, importIndex);
            _parser.ValidatePortableImport(importPath, source, importIndex);
            if (depth == MaximumDepth)
            {
                string[] chain = state.Stack.Append(importPath).ToArray();
                throw Limit(
                    $"Import depth exceeds {MaximumDepth}: {FormatChain(state.Stack, importPath)}",
                    importLocation,
                    chain);
            }

            if (state.Sources.Count == MaximumFileCount)
            {
                string[] chain = state.Stack.Append(importPath).ToArray();
                throw Limit(
                    $"Policy import graph exceeds {MaximumFileCount} files: {FormatChain(state.Stack, importPath)}",
                    importLocation,
                    chain);
            }

            ArchitecturePolicyResolvedPath resolved;
            try
            {
                resolved = _pathResolver.ResolveImport(state.Root, source.FullPath, importPath);
            }
            catch (ArchitecturePolicyImportException exception)
            {
                throw ArchitecturePolicyDiagnosticFactory.Enrich(
                    exception,
                    importLocation,
                    state.Stack.Append(importPath));
            }

            if (state.Active.Contains(resolved.FileIdentity))
            {
                ArchitecturePolicySource? activeSource = state.Sources
                    .FirstOrDefault(candidate => candidate.FileIdentity == resolved.FileIdentity);
                ArchitecturePolicySourceLocation[] related = activeSource is null
                    ? Array.Empty<ArchitecturePolicySourceLocation>()
                    : new[] { ArchitecturePolicyDiagnosticFactory.Location(activeSource, "$") };
                string[] chain = state.Stack.Append(resolved.PortableIdentity).ToArray();
                throw ArchitecturePolicyDiagnosticFactory.Exception(
                    ArchitecturePolicyImportErrorCategory.Cycle,
                    $"Policy import cycle detected: {FormatChain(state.Stack, resolved.PortableIdentity)}",
                    importLocation,
                    related,
                    chain);
            }

            if (state.Completed.Contains(resolved.FileIdentity)
                || state.PortableIdentities.Contains(resolved.PortableIdentity))
            {
                string first = state.FirstImports.GetValueOrDefault(resolved.FileIdentity, resolved.PortableIdentity);
                ArchitecturePolicySource? firstSource = state.Sources.FirstOrDefault(candidate =>
                    candidate.FileIdentity == resolved.FileIdentity
                    || string.Equals(candidate.PortableIdentity, resolved.PortableIdentity,
                        StringComparison.OrdinalIgnoreCase));
                ArchitecturePolicySourceLocation[] related = firstSource is null
                    ? Array.Empty<ArchitecturePolicySourceLocation>()
                    : new[] { ArchitecturePolicyDiagnosticFactory.Location(firstSource, "$") };
                throw ArchitecturePolicyDiagnosticFactory.Exception(
                    ArchitecturePolicyImportErrorCategory.DuplicateImport,
                    $"Duplicate policy import '{importPath}' resolves to '{resolved.PortableIdentity}'; first reached as '{first}'.",
                    importLocation,
                    related,
                    state.Stack.Append(resolved.PortableIdentity));
            }

            string yaml = _fileSystem.ReadAllText(resolved.FullPath);
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
                yaml);
            state.PortableIdentities.Add(resolved.PortableIdentity);
            state.FirstImports[resolved.FileIdentity] = importPath;
            Visit(child, depth + 1, state);
        }

        state.Stack.RemoveAt(state.Stack.Count - 1);
        state.Active.Remove(source.FileIdentity);
        state.Completed.Add(source.FileIdentity);
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
