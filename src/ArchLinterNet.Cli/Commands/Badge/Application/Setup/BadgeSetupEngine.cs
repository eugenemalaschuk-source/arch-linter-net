using static ArchLinterNet.Cli.Commands.Badge.Application.Setup.BadgeSetupValidationHelpers;

namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal static class BadgeSetupEngine
{
    internal static BadgeSetupPlanResult BuildPlan(BadgeSetupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<BadgeSetupDiagnostic> diagnostics = [];
        string modeValue = SelectMode(request);
        string profileValue = request.DisclosureProfile
            ?? BadgeSetupContract.HeadlineOnlyProfile;

        BadgeSetupConfiguration configuration = new(
            request.SchemaId,
            request.ContractVersion,
            modeValue,
            profileValue,
            request.Bundle,
            request.CompatibilityPlan,
            new(
                request.Repository.Owner,
                request.Repository.Name,
                request.Repository.Visibility,
                request.Repository.Capabilities.RepositoryId,
                request.Repository.Capabilities.RepositoryOwnerId),
            new(request.DestinationAlias, request.DestinationAccount),
            new(
                request.RenewalEnabled,
                request.RenewalCadenceMinutes ?? BadgeSetupContract.DefaultRenewalCadenceMinutes,
                request.MaxLeaseMinutes ?? BadgeSetupContract.DefaultLeaseMinutes),
            ProviderPlan: request.ProviderPlan);

        return BuildPlan(configuration, request.Repository, request.Existing, diagnostics);
    }

    internal static BadgeSetupPlanResult BuildPlan(
        BadgeSetupConfiguration configuration,
        BadgeSetupRepositoryContext repository,
        BadgeSetupExistingState? existing = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(repository);
        return BuildPlan(configuration, repository, existing, []);
    }

    internal static BadgeDoctorReport RunDoctor(
        BadgeSetupPlanResult planResult,
        BadgeDoctorObservations? observations = null)
    {
        ArgumentNullException.ThrowIfNull(planResult);
        observations ??= new();
        List<BadgeSetupDiagnostic> diagnostics = [.. planResult.Diagnostics];

        if (!observations.DestinationReachable)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.DestinationUnavailable,
                privateDetail: observations.PrivateContext));
        }

        if (!observations.FirstEvidenceAvailable)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.FirstEvidenceMissing,
                privateDetail: observations.PrivateContext));
        }

        if (!observations.ArtifactValid)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.ArtifactInvalid,
                privateDetail: observations.RawProviderResponse));
        }

        if (!observations.ValidityCurrent)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.ValidityExpired,
                privateDetail: observations.PrivateContext));
        }

        if (observations.DestinationRevoked)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.DestinationRevoked,
                privateDetail: observations.PrivateContext));
        }

        if (!observations.ProviderQuotaAvailable)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.ProviderQuotaFailure,
                privateDetail: observations.PrivateContext));
        }

        if (!observations.IdentityValid)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.MalformedIdentity,
                privateDetail: observations.PrivateContext));
        }

        if (!observations.PinsValid)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.InvalidPin,
                privateDetail: observations.PrivateContext));
        }

        if (!observations.OidcValid || !observations.RequiredCheckAvailable || !observations.RulesApiAvailable)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.MissingCapability,
                privateDetail: observations.PrivateContext));
        }

        if (!observations.CacheFresh)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.CacheStale,
                privateDetail: observations.PrivateContext));
        }

        return new(
            diagnostics.All(static diagnostic => diagnostic.Severity != BadgeSetupDiagnosticSeverity.Error),
            diagnostics);
    }

    private static BadgeSetupPlanResult BuildPlan(
        BadgeSetupConfiguration configuration,
        BadgeSetupRepositoryContext repository,
        BadgeSetupExistingState? existing,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        ValidateContractVersion(configuration, diagnostics);
        ValidateRepository(repository, diagnostics);
        ValidateConfigurationVersions(configuration, diagnostics);
        ValidateProjectAndPins(configuration, diagnostics);

        BadgeSetupMode? mode = ParseMode(configuration.Mode, diagnostics);
        BadgeDisclosureProfile? profile = ParseProfile(configuration.DisclosureProfile, diagnostics);
        BadgeRepositoryVisibility? visibility = ParseVisibility(repository.Visibility, diagnostics);

        BadgeSetupCostEstimate cost = BadgeSetupCostEstimate.None;
        List<BadgeSetupPrerequisite> prerequisites = [];
        List<string> plannedChanges = [];
        if (mode is not null && profile is not null && visibility is not null)
        {
            ValidateIdentity(configuration, repository, mode.Value, diagnostics);
            ValidateExistingState(configuration, existing, diagnostics);
            cost = BuildCost(configuration.Renewal, visibility.Value, mode.Value, diagnostics);
            BuildPrerequisites(configuration, repository, mode.Value, visibility.Value, prerequisites, diagnostics);
            BuildPlannedChanges(mode.Value, prerequisites, plannedChanges);
        }

        bool valid = diagnostics.Count == 0;
        BadgeSetupPlan plan = new(
            valid,
            mode?.ToWireValue() ?? configuration.Mode,
            profile?.ToWireValue() ?? configuration.DisclosureProfile,
            mode is BadgeSetupMode.GithubRaw or BadgeSetupMode.Relay && valid,
            mode is BadgeSetupMode.GithubRaw or BadgeSetupMode.Relay && valid,
            prerequisites,
            valid ? cost : BadgeSetupCostEstimate.None,
            plannedChanges,
            diagnostics);
        return new(plan, diagnostics);
    }

    private static string SelectMode(BadgeSetupRequest request)
    {
        if (request.Mode is { } mode && !string.IsNullOrWhiteSpace(mode))
        {
            return mode;
        }

        if (string.Equals(request.Repository.Visibility, "private", StringComparison.Ordinal))
        {
            return "none";
        }

        return "github-raw";
    }

    private static void ValidateContractVersion(
        BadgeSetupConfiguration configuration,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (!string.Equals(configuration.ContractVersion, BadgeSetupContract.ContractVersion, StringComparison.Ordinal))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.UnsupportedContractVersion,
                privateDetail: "The supplied contract version is not the current v1 contract."));
        }
    }

    private static void ValidateConfigurationVersions(
        BadgeSetupConfiguration configuration,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        AddIfUnexpected(configuration.SchemaId, BadgeSetupContract.SchemaId, BadgeSetupDiagnosticCodes.UnsupportedSchema, diagnostics);
        AddIfUnexpected(configuration.Bundle, BadgeSetupContract.Bundle, BadgeSetupDiagnosticCodes.UnsupportedBundle, diagnostics);
        AddIfUnexpected(
            configuration.CompatibilityPlan,
            BadgeSetupContract.CompatibilityPlan,
            BadgeSetupDiagnosticCodes.UnsupportedCompatibilityPlan,
            diagnostics);
    }

    private static void ValidateRepository(
        BadgeSetupRepositoryContext repository,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (!IsIdentity(repository.Owner) || !IsIdentity(repository.Name))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.MalformedIdentity,
                privateDetail: "Repository owner or name is not a valid approved identity."));
        }
    }

    private static void ValidateIdentity(
        BadgeSetupConfiguration configuration,
        BadgeSetupRepositoryContext repository,
        BadgeSetupMode mode,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (!IsIdentity(configuration.Repository.Owner)
            || !IsIdentity(configuration.Repository.Name)
            || !string.Equals(configuration.Repository.Owner, repository.Owner, StringComparison.Ordinal)
            || !string.Equals(configuration.Repository.Name, repository.Name, StringComparison.Ordinal))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.MalformedIdentity,
                privateDetail: "Configuration repository identity does not match the inspected repository."));
        }

        if (mode == BadgeSetupMode.Relay)
        {
            ValidateRelayIdentity(configuration, repository, diagnostics);
        }
    }

    private static void ValidateRelayIdentity(
        BadgeSetupConfiguration configuration,
        BadgeSetupRepositoryContext repository,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (configuration.Destination.Account is null || configuration.Destination.Alias is null)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.MalformedIdentity,
                privateDetail: "Relay account or alias is missing or malformed."));
        }

        if (!IsPositive(configuration.Repository.RepositoryId) || !IsPositive(configuration.Repository.RepositoryOwnerId))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.MalformedIdentity,
                privateDetail: "Relay setup requires immutable repository and owner IDs."));
        }

        if (RepositoryIdsContradict(configuration, repository))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.MalformedIdentity,
                privateDetail: "Configured immutable repository IDs do not match the inspected repository."));
        }

        if (configuration.Pins is null
            || configuration.Pins.WorkflowRef is null
            || configuration.Pins.WorkflowSha is null
            || configuration.Pins.ActionRef is null)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.InvalidPin,
                privateDetail: "Relay setup requires complete reusable-workflow and action pins."));
        }

        if (configuration.ProviderPlan is null)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.UnsupportedPlan,
                privateDetail: "Relay setup requires an explicit supported provider plan."));
        }

        if (configuration.Destination.Endpoint is null)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.InvalidEndpoint,
                privateDetail: "Relay endpoint must be an HTTPS origin without query or fragment."));
        }

        if (configuration.Destination.Audience is null)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.InvalidEndpoint,
                privateDetail: "Relay audience is missing or contains unsafe characters."));
        }
    }

    private static bool RepositoryIdsContradict(
        BadgeSetupConfiguration configuration,
        BadgeSetupRepositoryContext repository) =>
        repository.Capabilities.RepositoryId is long repositoryId
            && configuration.Repository.RepositoryId != repositoryId
        || repository.Capabilities.RepositoryOwnerId is long repositoryOwnerId
            && configuration.Repository.RepositoryOwnerId != repositoryOwnerId;

    private static void ValidateProjectAndPins(
        BadgeSetupConfiguration configuration,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        ValidateRepositoryIds(configuration, diagnostics);
        ValidateProviderPlan(configuration, diagnostics);
        ValidateDestination(configuration, diagnostics);
        ValidateProject(configuration, diagnostics);
        ValidatePins(configuration, diagnostics);
        ValidateManagedFiles(configuration, diagnostics);
        ValidateProducer(configuration, diagnostics);
    }

    private static void ValidateRepositoryIds(
        BadgeSetupConfiguration configuration,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (configuration.Repository.RepositoryId is not null && !IsSafePositiveId(configuration.Repository.RepositoryId)
            || configuration.Repository.RepositoryOwnerId is not null && !IsSafePositiveId(configuration.Repository.RepositoryOwnerId))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.MalformedIdentity,
                privateDetail: "Immutable repository IDs must be positive safe integers."));
        }
    }

    private static void ValidateProviderPlan(
        BadgeSetupConfiguration configuration,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (configuration.ProviderPlan is not null && !IsSupportedPlan(configuration.ProviderPlan))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.UnsupportedPlan,
                privateDetail: "The provider plan is outside the shipped closed set."));
        }
    }

    private static void ValidateDestination(
        BadgeSetupConfiguration configuration,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (configuration.Destination.Alias is not null && !IsOpaqueAlias(configuration.Destination.Alias))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.MalformedIdentity,
                privateDetail: "The destination alias is not an approved opaque alias."));
        }

        if (configuration.Destination.Account is not null && !IsCloudflareAccountId(configuration.Destination.Account))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.MalformedIdentity,
                privateDetail: "The destination account is not a valid provider account ID."));
        }

        if (configuration.Destination.Endpoint is not null && !IsHttpsOrigin(configuration.Destination.Endpoint))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.InvalidEndpoint,
                privateDetail: "The destination endpoint is not an approved HTTPS origin."));
        }

        if (configuration.Destination.Audience is not null && !IsSafeAudience(configuration.Destination.Audience))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.InvalidEndpoint,
                privateDetail: "The destination audience contains unsupported characters."));
        }
    }

    private static void ValidateProject(
        BadgeSetupConfiguration configuration,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (!IsSafeRef(configuration.BaseRef))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.InvalidConfiguration,
                privateDetail: "Base ref is not a safe repository ref."));
        }

        if (configuration.Project is not null
            && (!IsSafeRepositoryPath(configuration.Project.PolicyPath) || !IsSafeRepositoryPath(configuration.Project.SolutionPath)))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.InvalidProject,
                privateDetail: "Policy or solution path is not repository-relative and safe."));
        }
    }

    private static void ValidatePins(
        BadgeSetupConfiguration configuration,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (configuration.Pins is null)
        {
            return;
        }

        if (configuration.Pins.WorkflowRef is not null && !IsReusableWorkflowReference(configuration.Pins.WorkflowRef))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(BadgeSetupDiagnosticCodes.InvalidPin, privateDetail: "Reusable workflow reference is malformed."));
        }

        if (configuration.Pins.WorkflowSha is not null && !IsSha(configuration.Pins.WorkflowSha, 40))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(BadgeSetupDiagnosticCodes.InvalidPin, privateDetail: "Reusable workflow pin is not a lowercase 40-character SHA."));
        }

        if (configuration.Pins.ActionRef is not null && !IsPinnedActionReference(configuration.Pins.ActionRef))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(BadgeSetupDiagnosticCodes.InvalidPin, privateDetail: "Action reference is malformed."));
        }

        if (configuration.Pins.BundleDigest is not null && !IsSha(configuration.Pins.BundleDigest, 64))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(BadgeSetupDiagnosticCodes.InvalidPin, privateDetail: "Bundle digest is not a lowercase 64-character SHA."));
        }
    }

    private static void ValidateManagedFiles(
        BadgeSetupConfiguration configuration,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (configuration.ManagedFiles is not null
            && (configuration.ManagedFiles.Count > 32
                || configuration.ManagedFiles.Count != configuration.ManagedFiles.Distinct(StringComparer.Ordinal).Count()
                || configuration.ManagedFiles.Any(static path => !IsSafeRepositoryPath(path))))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.InvalidManagedPath,
                privateDetail: "Managed file paths must be unique, relative, and traversal-free."));
        }
    }

    private static void ValidateProducer(
        BadgeSetupConfiguration configuration,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (configuration.Producer is not null
            && (!IsWorkflowPath(configuration.Producer.WorkflowPath)
                || !IsSha(configuration.Producer.WorkflowSha, 40)
                || configuration.Producer.Event != "pull_request"
                || configuration.Producer.PayloadPath != BadgeSetupContract.DefaultPayloadPath
                || configuration.Producer.JobName != configuration.Producer.CheckName
                || !IsSafeName(configuration.Producer.JobName)
                || !IsSafeName(configuration.Producer.CheckName)
                || !IsSafeCheckApp(configuration.Producer.CheckApp)
                || !IsSafeName(configuration.Producer.ArtifactName)
                || !IsSafeName(configuration.Producer.EvidenceArtifactName)))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.InvalidPin,
                privateDetail: "Producer metadata is not an approved immutable binding."));
        }
    }

    private static void ValidateExistingState(
        BadgeSetupConfiguration configuration,
        BadgeSetupExistingState? existing,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (existing is not { HasDeployment: true })
        {
            return;
        }

        if (existing.Mode is not null && !string.Equals(existing.Mode, configuration.Mode, StringComparison.Ordinal)
            || existing.DisclosureProfile is not null
            && !string.Equals(existing.DisclosureProfile, configuration.DisclosureProfile, StringComparison.Ordinal))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.ConfigurationConflict,
                privateDetail: "The requested mode or disclosure profile differs from the approved deployment."));
        }

        if (existing.Alias is not null && !string.Equals(existing.Alias, configuration.Destination.Alias, StringComparison.Ordinal))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.ConfigurationConflict,
                privateDetail: "The requested alias differs from the existing deployment identity."));
        }
    }

    private static BadgeSetupCostEstimate BuildCost(
        BadgeSetupConfigurationRenewal renewal,
        BadgeRepositoryVisibility visibility,
        BadgeSetupMode mode,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (renewal.CadenceMinutes < BadgeSetupContract.MinimumRenewalCadenceMinutes
            || renewal.CadenceMinutes > BadgeSetupContract.MaximumRenewalCadenceMinutes)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(BadgeSetupDiagnosticCodes.InvalidRenewal));
        }

        if (mode != BadgeSetupMode.Relay && renewal.Enabled)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                BadgeSetupDiagnosticCodes.ConfigurationConflict,
                privateDetail: "Renewal is only supported for adopter-owned Relay."));
        }

        if (!renewal.Enabled)
        {
            if (renewal.MaxLeaseMinutes < 1 || renewal.MaxLeaseMinutes > BadgeSetupContract.MaximumLeaseMinutes)
            {
                diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(BadgeSetupDiagnosticCodes.LeaseExceeded));
            }

            return BadgeSetupCostEstimate.None;
        }

        if (renewal.MaxLeaseMinutes < 1 || renewal.MaxLeaseMinutes > BadgeSetupContract.MaximumLeaseMinutes)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(BadgeSetupDiagnosticCodes.LeaseExceeded));
        }

        if (renewal.CadenceMinutes <= 0)
        {
            return BadgeSetupCostEstimate.None;
        }

        int jobsPerDay = (int)Math.Min(
            int.MaxValue,
            (1440L + renewal.CadenceMinutes - 1) / renewal.CadenceMinutes);
        if (jobsPerDay > BadgeSetupContract.MaximumRenewalJobsPerDay)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(BadgeSetupDiagnosticCodes.RenewalCostExceeded));
        }

        int privateMinutesPerDay = visibility == BadgeRepositoryVisibility.Private
            ? jobsPerDay * BadgeSetupContract.PrivateMinutesPerRenewalJob
            : 0;
        int jobsPerThirtyDayMonth = SaturatingProduct(jobsPerDay, 30);
        int privateMinutesPerThirtyDayMonth = SaturatingProduct(privateMinutesPerDay, 30);
        return new(
            true,
            renewal.CadenceMinutes,
            renewal.MaxLeaseMinutes,
            jobsPerDay,
            jobsPerThirtyDayMonth,
            privateMinutesPerDay,
            privateMinutesPerThirtyDayMonth,
            "Assumes one metadata-only provider request and one billed private GitHub minute per renewal job; verify adopter plan quota before registration.");
    }

    private static int SaturatingProduct(int left, int right) =>
        (int)Math.Min(int.MaxValue, (long)left * right);

    private static void BuildPrerequisites(
        BadgeSetupConfiguration configuration,
        BadgeSetupRepositoryContext repository,
        BadgeSetupMode mode,
        BadgeRepositoryVisibility visibility,
        List<BadgeSetupPrerequisite> prerequisites,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        switch (mode)
        {
            case BadgeSetupMode.None:
                prerequisites.Add(new("local-evidence", "Local evidence and private report output remain available.", true, true));
                return;
            case BadgeSetupMode.GithubRaw:
                if (visibility == BadgeRepositoryVisibility.Private)
                {
                    diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                        BadgeSetupDiagnosticCodes.VisibilityConflict,
                        privateDetail: "github-raw is not permitted for a private repository."));
                }

                AddCapability(
                    prerequisites,
                    diagnostics,
                    "public-visibility",
                    "The repository must be public for github-raw publication.",
                    visibility == BadgeRepositoryVisibility.Public,
                    required: true);
                AddCapability(
                    prerequisites,
                    diagnostics,
                    "github-raw-publication",
                    "The existing public github-raw publication capability must be available.",
                    repository.Capabilities.CanUseGithubRaw,
                    required: true);
                return;
            case BadgeSetupMode.Relay:
                AddCapability(
                    prerequisites,
                    diagnostics,
                    "relay-account",
                    "An adopter-owned Relay account and opaque alias are required.",
                    IsCloudflareAccountId(configuration.Destination.Account) && IsOpaqueAlias(configuration.Destination.Alias),
                    required: true);
                AddCapability(
                    prerequisites,
                    diagnostics,
                    "relay-plan",
                    "A supported provider plan with sufficient quota is required.",
                    repository.Capabilities.CanUseRelay
                        && IsSupportedPlan(configuration.ProviderPlan)
                        && string.Equals(configuration.ProviderPlan, repository.Capabilities.ProviderPlan, StringComparison.Ordinal),
                    required: true);
                AddCapability(
                    prerequisites,
                    diagnostics,
                    "provider-quota",
                    "The provider account must expose the Worker and SQLite Durable Object quota needed by the bundle.",
                    repository.Capabilities.ProviderQuotaAvailable,
                    required: true);
                AddCapability(
                    prerequisites,
                    diagnostics,
                    "oidc",
                    "The pinned workflow must be allowed to issue the approved OIDC claims.",
                    repository.Capabilities.CanUseOidc,
                    required: true);
                AddCapability(
                    prerequisites,
                    diagnostics,
                    "required-check",
                    "The approved required check must be available.",
                    repository.Capabilities.HasRequiredCheck,
                    required: true);
                AddCapability(
                    prerequisites,
                    diagnostics,
                    "rules-api",
                    "The repository rules API capability must be available.",
                    repository.Capabilities.HasRulesApi,
                    required: true);
                return;
        }
    }

    private static void AddCapability(
        List<BadgeSetupPrerequisite> prerequisites,
        List<BadgeSetupDiagnostic> diagnostics,
        string code,
        string description,
        bool satisfied,
        bool required)
    {
        prerequisites.Add(new(code, description, required, satisfied));
        if (required && !satisfied)
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
                code == "relay-plan" ? BadgeSetupDiagnosticCodes.UnsupportedPlan : BadgeSetupDiagnosticCodes.MissingCapability,
                privateDetail: $"Prerequisite '{code}' was not proven."));
        }
    }

    private static void BuildPlannedChanges(
        BadgeSetupMode mode,
        List<BadgeSetupPrerequisite> prerequisites,
        List<string> plannedChanges)
    {
        if (mode == BadgeSetupMode.None)
        {
            return;
        }

        if (prerequisites.All(static prerequisite => prerequisite.Satisfied))
        {
            plannedChanges.Add("versioned badge setup configuration");
            plannedChanges.Add(mode == BadgeSetupMode.Relay
                ? "adopter-owned Relay destination registration"
                : "public github-raw publication wiring");
        }
    }

    private static BadgeSetupMode? ParseMode(string value, List<BadgeSetupDiagnostic> diagnostics)
    {
        if (BadgeSetupContract.TryParseMode(value, out BadgeSetupMode mode))
        {
            return mode;
        }

        diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
            BadgeSetupDiagnosticCodes.UnsupportedMode,
            privateDetail: "The mode value is not in the closed supported set."));
        return null;
    }

    private static BadgeDisclosureProfile? ParseProfile(string value, List<BadgeSetupDiagnostic> diagnostics)
    {
        if (BadgeSetupContract.TryParseProfile(value, out BadgeDisclosureProfile profile))
        {
            return profile;
        }

        diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
            BadgeSetupDiagnosticCodes.UnsupportedDisclosureProfile,
            privateDetail: "The disclosure profile value is not in the closed supported set."));
        return null;
    }

    private static BadgeRepositoryVisibility? ParseVisibility(
        string value,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (value == "public")
        {
            return BadgeRepositoryVisibility.Public;
        }

        if (value == "private")
        {
            return BadgeRepositoryVisibility.Private;
        }

        diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(
            BadgeSetupDiagnosticCodes.UnsupportedVisibility,
            privateDetail: "The visibility value is not public or private."));
        return null;
    }

    private static void AddIfUnexpected(
        string actual,
        string expected,
        string code,
        List<BadgeSetupDiagnostic> diagnostics)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            diagnostics.Add(BadgeSetupDiagnosticCatalog.Create(code, privateDetail: "A non-shipped version identifier was supplied."));
        }
    }

}
