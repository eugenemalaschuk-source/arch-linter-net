using System.Text.Json;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.BuildState;

// Orchestrates all three preparation modes (Ordinary, NoRestore, EnsureBuilt) through one entry
// point so CLI and Testing API share identical state-machine behavior. Runtime build preparation
// is kept in BuildStateRuntimeBuildPreparation so this entrypoint owns only orchestration,
// prerequisite checking, and cache-eligibility projection.
public sealed class BuildStatePreparationService : IBuildStatePreparationService
{
    private const string ContractName = "build-state-preflight";

    public BuildStatePreflightResult Prepare(BuildStatePreflightRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.NoRestore)
        {
            BuildStatePreflightResult restoreCheck = CheckRestorePrerequisites(request);
            if (restoreCheck.Blocked)
            {
                return WithCacheEligibility(restoreCheck);
            }
        }

        BuildStatePreflightResult result = request.PreparationMode == BuildPreparationMode.EnsureBuilt
            ? BuildStateRuntimeBuildPreparation.EnsureBuilt(request)
            : BuildStatePreflightEvaluator.Evaluate(request);
        return WithCacheEligibility(result);
    }

    private static BuildStatePreflightResult WithCacheEligibility(BuildStatePreflightResult result) =>
        new(result.Diagnostics.Select(BuildStatePreflightEvaluator.EnsureCacheEligibility).ToArray());

    // dotnet restore/build resolves prerequisites from the local NuGet cache without network
    // access whenever a project has already been restored once; the presence of
    // obj/project.assets.json is the same signal `dotnet build --no-restore` itself relies on.
    private static BuildStatePreflightResult CheckRestorePrerequisites(BuildStatePreflightRequest request)
    {
        // Must match the same project set `--ensure-built` would actually build (relevant
        // projects plus their transitive ProjectReference closure, not just the relevant seeds) —
        // otherwise a missing restore on a transitively-referenced library surfaces late as a
        // generic build-failed instead of this typed restore-required diagnostic.
        List<BuildStatePreflightDiagnostic> blocking = new();
        foreach (ArchitectureDiscoveredProject project in
            BuildStateRuntimeBuildPreparation.SelectRelevantProjectsWithTransitiveReferences(request))
        {
            string? projectDirectory = Path.GetDirectoryName(
                BuildStatePathResolution.ResolveAbsoluteProjectPath(request.RepositoryRoot, project.Path));
            string assetsPath = projectDirectory == null
                ? string.Empty
                : Path.Combine(projectDirectory, "obj", "project.assets.json");

            if (projectDirectory != null && HasRestoredTargets(assetsPath))
            {
                continue;
            }

            blocking.Add(new BuildStatePreflightDiagnostic(
                ContractName,
                project.Path,
                BuildStatePreflightState.RestoreRequired,
                new BuildStatePreflightEvidence(
                    project.Path,
                    project.AssemblyName,
                    BuildCommand: $"dotnet restore \"{project.Path}\"",
                    Detail: "No prior restore output was found and --no-restore prevents restoring now. " +
                        "Run `dotnet restore` (or `--ensure-built` without --no-restore) first.")));
        }

        return new BuildStatePreflightResult(blocking);
    }

    // A weaker signal than a real restore verification (it doesn't cross-check the assets file's
    // recorded package identities/versions against the project's current PackageReferences, so a
    // stale assets.json can still pass this and fail later inside `dotnet build --no-restore` as a
    // generic build failure instead of this typed restore-required diagnostic — a known,
    // documented limitation). It is a meaningful improvement over bare File.Exists, though: a
    // trivially empty or corrupt assets file (e.g. `{}`) — which a real `dotnet restore` never
    // produces — no longer passes as "restored".
    private static bool HasRestoredTargets(string assetsPath)
    {
        if (!File.Exists(assetsPath))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(assetsPath));
            return document.RootElement.TryGetProperty("targets", out JsonElement targets)
                && targets.ValueKind == JsonValueKind.Object
                && targets.EnumerateObject().Any();
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
