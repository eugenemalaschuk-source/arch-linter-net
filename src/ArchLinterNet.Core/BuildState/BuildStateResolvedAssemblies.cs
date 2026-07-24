using System.Reflection;

namespace ArchLinterNet.Core.BuildState;

// A BuildState-local view of assembly resolution results, decoupled from
// ArchLinterNet.Core.Execution.ResolutionResult so this layer does not import Execution — see
// the self-architecture "execution" protected-layer contract, whose allowed importers do not
// include BuildState. Callers (ArchitectureValidationApplicationService, which is already an
// approved Execution importer) map from ResolutionResult to this type at the call boundary.
public sealed record BuildStateResolvedAssemblies(
    IReadOnlyCollection<Assembly> ResolvedAssemblies,
    IReadOnlyCollection<string> MissingAssemblyNames);
