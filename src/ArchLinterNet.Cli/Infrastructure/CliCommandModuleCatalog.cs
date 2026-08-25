using System.Reflection;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Cli.Infrastructure;

internal static class CliCommandModuleCatalog
{
    private const string CommandContainer = "ArchLinterNet.Cli.Commands";

    private static readonly HashSet<string> _genericModuleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Common", "Shared", "Utils",
    };

    public static IRootCliCommandModule CreateRootModule()
    {
        Type[] candidates = GetCandidateModuleTypes<IRootCliCommandModule>(Assembly.GetExecutingAssembly());
        Type[] governedCandidates = candidates.Where(IsGovernedModuleCandidate).ToArray();
        if (candidates.Length != governedCandidates.Length)
        {
            throw new InvalidOperationException(
                $"Root CLI module candidates must be declared below '{CommandContainer}.<Command>.EntryPoint'. " +
                $"Candidates: {DescribeCandidates(candidates)}.");
        }

        if (governedCandidates.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one governed root CLI module below '{CommandContainer}.<Command>.EntryPoint'; " +
                $"found {governedCandidates.Length}. Candidates: {DescribeCandidates(candidates)}.");
        }

        Type moduleType = governedCandidates[0];
        return CreateModule<IRootCliCommandModule>(moduleType);
    }

    public static IReadOnlyList<ITopLevelCliSubcommandModule> CreateSubcommandModules()
        => CreateSubcommandModules(Assembly.GetExecutingAssembly());

    // Kept internal for composition tests: production always discovers modules from the CLI
    // assembly, while the overload lets a generated module set prove it needs no central registry.
    internal static IReadOnlyList<ITopLevelCliSubcommandModule> CreateSubcommandModules(Assembly assembly)
    {
        Type[] candidates = GetCandidateModuleTypes<ITopLevelCliSubcommandModule>(assembly);
        Type[] governedCandidates = candidates.Where(IsGovernedModuleCandidate).ToArray();
        if (candidates.Length != governedCandidates.Length)
        {
            throw new InvalidOperationException(
                $"Top-level CLI module candidates must be declared below '{CommandContainer}.<Command>.EntryPoint'. " +
                $"Candidates: {DescribeCandidates(candidates)}.");
        }

        return governedCandidates
            .Select(CreateModule<ITopLevelCliSubcommandModule>)
            .ToArray();
    }

    internal static bool IsGovernedModuleCandidate(Type type)
    {
        string typeNamespace = type.Namespace ?? string.Empty;
        return ArchitectureModuleNamespaceMembershipResolver.TryResolve(
                   CommandContainer, typeNamespace, out ArchitectureModuleNamespaceMembership? membership)
            && membership is { IsContainerRoot: false, ModuleName: not null, Segment: "EntryPoint" }
            && !_genericModuleNames.Contains(membership.ModuleName);
    }

    private static Type[] GetCandidateModuleTypes<TModule>(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(type => typeof(TModule).IsAssignableFrom(type) && type is { IsAbstract: false, IsClass: true })
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    private static string DescribeCandidates(IEnumerable<Type> candidates)
    {
        string[] names = candidates.Select(static type => type.FullName ?? type.Name).ToArray();
        return names.Length == 0 ? "<none>" : string.Join(", ", names);
    }

    private static TModule CreateModule<TModule>(Type type)
    {
        object? instance = Activator.CreateInstance(type);
        if (instance is TModule module)
        {
            return module;
        }

        throw new InvalidOperationException($"Failed to create CLI module '{type.FullName}'.");
    }
}
