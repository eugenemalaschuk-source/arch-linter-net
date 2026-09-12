using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal static class BadgeSetupOutputWriter
{
    internal const string ReadmeStartMarker = "<!-- arch-linter-net:managed-badge-setup/v1:start -->";
    internal const string ReadmeEndMarker = "<!-- arch-linter-net:managed-badge-setup/v1:end -->";

    internal static void Write(string outputDirectory, BadgeSetupConfiguration configuration, BadgeSetupPlan plan)
    {
        if (!plan.IsValid)
        {
            throw new InvalidOperationException("Only a valid setup plan can write managed output.");
        }

        if (!string.Equals(plan.Mode, configuration.Mode, StringComparison.Ordinal)
            || !string.Equals(plan.DisclosureProfile, configuration.DisclosureProfile, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The setup plan does not match the requested configuration.");
        }

        if (configuration.Mode == BadgeSetupMode.Relay.ToWireValue() && !configuration.DisclosureApproved)
        {
            throw new InvalidOperationException("Relay output requires explicit disclosure approval.");
        }

        string root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);
        BadgeSetupPins initialPins = ResolvePins(configuration.Pins, []);
        BadgeSetupConfiguration pinnedConfiguration = configuration with { Pins = initialPins };
        IReadOnlyList<GeneratedFile> relayFiles = pinnedConfiguration.Mode == BadgeSetupMode.Relay.ToWireValue()
            ? ReadRelayBundle(pinnedConfiguration)
            : [];
        BadgeSetupProducer producer = ResolveProducer(pinnedConfiguration);
        string producerWorkflow = RenderProducerWorkflow(pinnedConfiguration, producer);
        string producerSha = ComputeGitBlobSha(producerWorkflow);
        if (configuration.Producer is not null && configuration.Producer.WorkflowSha != new string('0', 40)
            && configuration.Producer.WorkflowSha != producerSha)
        {
            throw new InvalidOperationException("Configured producer.workflow_sha does not match the generated producer workflow blob.");
        }

        producer = producer with { WorkflowSha = producerSha };
        BadgeSetupPins pins = ResolvePins(initialPins, relayFiles);
        BadgeSetupConfiguration effective = pinnedConfiguration with
        {
            Producer = producer,
            Pins = pins,
            Project = pinnedConfiguration.Project ?? new("architecture/dependencies.arch.yml", "ArchLinterNet.slnx"),
        };
        List<string> managedPaths = BuildManagedPaths(effective, relayFiles);
        if (configuration.ManagedFiles is not null && !configuration.ManagedFiles.SequenceEqual(managedPaths, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Configured managed_files does not match the files produced by this setup.");
        }

        effective = effective with { ManagedFiles = managedPaths };
        BadgeSetupPlanResult effectivePlan = BadgeSetupEngine.BuildPlan(
            effective,
            new(
                effective.Repository.Owner,
                effective.Repository.Name,
                effective.Repository.Visibility,
                new(
                    HasRequiredCheck: true,
                    HasRulesApi: true,
                    CanUseGithubRaw: effective.Mode == BadgeSetupMode.GithubRaw.ToWireValue(),
                    CanUseOidc: true,
                    CanUseRelay: effective.Mode == BadgeSetupMode.Relay.ToWireValue(),
                    ProviderPlan: effective.ProviderPlan,
                    RepositoryId: effective.Repository.RepositoryId,
                    RepositoryOwnerId: effective.Repository.RepositoryOwnerId,
                    ProviderQuotaAvailable: true)));
        if (!effectivePlan.IsValid)
        {
            throw new InvalidOperationException(
                "Generated setup configuration does not satisfy the setup contract: "
                + string.Join(", ", effectivePlan.Diagnostics.Select(static diagnostic => diagnostic.Code)));
        }

        List<GeneratedFile> files =
        [
            new("badge-relay-config.json", Serialize(effective)),
            new(producer.WorkflowPath, producerWorkflow),
        ];
        if (effective.Mode != BadgeSetupMode.None.ToWireValue())
        {
            files.Add(new(".github/workflows/architecture-health-badge-publisher.yml", RenderPublisherWorkflow(effective)));
        }

        files.AddRange([
            new(".github/badge-promotion/registry.json", RenderRegistry(effective)),
            new("README.md", BuildReadme(root, effective)),
            new("schema/0.8.0/badge-relay-config.schema.json", ReadAsset("schema/0.8.0/badge-relay-config.schema.json")),
        ]);
        if (effective.Mode == BadgeSetupMode.Relay.ToWireValue())
        {
            files.Add(new(".github/workflows/architecture-health-badge-renewal.yml", RenderRenewalWorkflow(effective)));
            files.AddRange(relayFiles);
        }

        // Validate the exact bytes that are about to be written. This catches drift between
        // the in-memory model and the shipped closed parser before any consumer file exists.
        BadgeSetupConfigurationParseResult parsed = BadgeSetupConfigurationParser.Parse(files[0].Contents);
        if (!parsed.IsValid || parsed.Configuration is null)
        {
            throw new InvalidOperationException(
                "Generated setup configuration does not satisfy the shipped schema contract: "
                + string.Join(", ", parsed.Diagnostics.Select(static diagnostic => diagnostic.Code)));
        }

        string manifest = Serialize(new
        {
            schema_id = "badge-relay-manifest/v1",
            bundle = effective.Bundle,
            compatibility_plan = effective.CompatibilityPlan,
            mode = plan.Mode,
            files = files.Select(static file => FileEntry(file.Path, file.Contents)).ToArray(),
        });
        List<GeneratedFile> allFiles = [.. files, new("badge-relay-manifest.json", manifest)];
        foreach (GeneratedFile file in allFiles)
        {
            EnsureNoConflict(root, file.Path, file.Contents);
        }

        OriginalFile[] originals = allFiles.Select(file => CaptureOriginal(root, file.Path)).ToArray();
        try
        {
            foreach (GeneratedFile file in allFiles)
            {
                WriteManaged(root, file.Path, file.Contents);
            }
        }
        catch
        {
            RestoreOriginals(root, originals);
            throw;
        }
    }

    internal static string ComputeGitBlobSha(string contents)
    {
        byte[] payload = Encoding.UTF8.GetBytes(contents);
        byte[] header = Encoding.ASCII.GetBytes($"blob {payload.Length}\0");
        byte[] blob = new byte[header.Length + payload.Length];
        Buffer.BlockCopy(header, 0, blob, 0, header.Length);
        Buffer.BlockCopy(payload, 0, blob, header.Length, payload.Length);
        return Convert.ToHexString(SHA1.HashData(blob)).ToLowerInvariant();
    }

    private static BadgeSetupProducer ResolveProducer(BadgeSetupConfiguration configuration) =>
        configuration.Producer ?? new(
            BadgeSetupContract.DefaultProducerWorkflowPath,
            new string('0', 40),
            BadgeSetupContract.DefaultCheckName,
            BadgeSetupContract.DefaultCheckName,
            BadgeSetupContract.DefaultCheckApp,
            "pull_request",
            BadgeSetupContract.DefaultArtifactName,
            BadgeSetupContract.DefaultEvidenceArtifactName,
            BadgeSetupContract.DefaultPayloadPath);

    private static BadgeSetupPins ResolvePins(BadgeSetupPins? configured, IReadOnlyList<GeneratedFile> relayFiles)
    {
        BadgeSetupPins pins = configured ?? new(
            BadgeSetupContract.DefaultPublisherWorkflowRef,
            BadgeSetupContract.DefaultPublisherWorkflowSha,
            BadgeSetupContract.DefaultActionRef);
        string bundleDigest = relayFiles.Count == 0 ? pins.BundleDigest ?? string.Empty : ComputeBundleDigest(relayFiles);
        if (pins.BundleDigest is not null && relayFiles.Count != 0 && pins.BundleDigest != bundleDigest)
        {
            throw new InvalidOperationException("Configured bundle_digest does not match the shipped Relay bundle.");
        }

        return pins with
        {
            WorkflowRef = pins.WorkflowRef ?? BadgeSetupContract.DefaultPublisherWorkflowRef,
            WorkflowSha = pins.WorkflowSha ?? BadgeSetupContract.DefaultPublisherWorkflowSha,
            ActionRef = pins.ActionRef ?? BadgeSetupContract.DefaultActionRef,
            BundleDigest = relayFiles.Count == 0 ? pins.BundleDigest : bundleDigest,
        };
    }

    private static List<string> BuildManagedPaths(BadgeSetupConfiguration configuration, IReadOnlyList<GeneratedFile> relayFiles)
    {
        List<string> paths =
        [
            "badge-relay-config.json",
            configuration.Producer!.WorkflowPath,
            ".github/badge-promotion/registry.json",
            "README.md",
            "schema/0.8.0/badge-relay-config.schema.json",
        ];
        if (configuration.Mode != BadgeSetupMode.None.ToWireValue())
        {
            paths.Insert(2, ".github/workflows/architecture-health-badge-publisher.yml");
        }
        if (configuration.Mode == BadgeSetupMode.Relay.ToWireValue())
        {
            paths.Add(".github/workflows/architecture-health-badge-renewal.yml");
            paths.AddRange(relayFiles.Select(static file => file.Path));
        }

        return paths;
    }

    private static string RenderProducerWorkflow(BadgeSetupConfiguration configuration, BadgeSetupProducer producer) =>
        RenderProducerWorkflow(configuration, producer, configuration.Project ?? new("architecture/dependencies.arch.yml", "ArchLinterNet.slnx"));

    private static string RenderProducerWorkflow(
        BadgeSetupConfiguration configuration,
        BadgeSetupProducer producer,
        BadgeSetupProject project)
    {
        string baseRef = configuration.BaseRef;
        string policyPath = project.PolicyPath;
        string solutionPath = project.SolutionPath;
        const string Template = """
name: __JOB_NAME__

on:
  pull_request:
    branches:
      - __BASE_REF__

permissions:
  contents: read
  pull-requests: read

jobs:
  architecture_coverage:
    name: __CHECK_NAME__
    runs-on: ubuntu-latest
    timeout-minutes: 20
    steps:
      - name: Checkout
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1
        with:
          ref: ${{ github.event.pull_request.head.sha }}
          fetch-depth: 0
          persist-credentials: false
      - name: Setup .NET
        uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68
        with:
          dotnet-version: 10.0.x
      - name: Install ArchLinterNet
        run: dotnet tool install --tool-path "$RUNNER_TEMP/arch-linter-net-tool" ArchLinterNet.Cli --version 0.8.0
      - name: Generate canonical Architecture Health artifacts
        id: produce
        shell: bash
        env:
          POLICY_PATH: __POLICY_PATH__
          SOLUTION_PATH: __SOLUTION_PATH__
          ARTIFACT_DIRECTORY: ${{ runner.temp }}/architecture-health-badge-artifact
          HEALTH_ARTIFACT_DIRECTORY: ${{ runner.temp }}/architecture-health-artifact
          EXECUTION_CONTEXT: pr-${{ github.event.pull_request.number }}-${{ github.event.pull_request.head.sha }}
          PR_HEAD_SHA: ${{ github.event.pull_request.head.sha }}
        run: |
          set -u
          mkdir -p "$ARTIFACT_DIRECTORY" "$HEALTH_ARTIFACT_DIRECTORY"
          test -f "$POLICY_PATH"
          test -f "$SOLUTION_PATH"
          baseline_args=()
          if [[ -f architecture/baseline.arch.yml ]]; then
            baseline_args+=(--baseline architecture/baseline.arch.yml)
          else
            empty_baseline="$RUNNER_TEMP/arch-linter-net-empty-baseline.arch.yml"
            printf '%s\n' 'version: 3' 'baseline: {}' 'metric_baselines: []' > "$empty_baseline"
            baseline_args+=(--baseline "$empty_baseline")
          fi
          tool="$RUNNER_TEMP/arch-linter-net-tool/arch-linter-net"
          set +e
          "$tool" health --policy "$POLICY_PATH" --mode strict --ensure-built "${baseline_args[@]}" --format json --execution-context "$EXECUTION_CONTEXT" > "$HEALTH_ARTIFACT_DIRECTORY/architecture-health.json"
          health_status=$?
          "$tool" badge architecture-health --input "$HEALTH_ARTIFACT_DIRECTORY/architecture-health.json" --output "$ARTIFACT_DIRECTORY/architecture-health-badge.json"
          badge_status=$?
          set -e
          test -s "$HEALTH_ARTIFACT_DIRECTORY/architecture-health.json"
          test -s "$ARTIFACT_DIRECTORY/architecture-health-badge.json"
          cp "$HEALTH_ARTIFACT_DIRECTORY/architecture-health.json" "$RUNNER_TEMP/architecture-health.json"
          export PAYLOAD_PATH="$ARTIFACT_DIRECTORY/architecture-health-badge.json"
          export MANIFEST_PATH="$ARTIFACT_DIRECTORY/architecture-health-badge.manifest.json"
          export PR_HEAD_TREE_SHA="$(git rev-parse "$PR_HEAD_SHA^{tree}")"
          python3 - <<'PY'
          import hashlib
          import json
          import os
          from pathlib import Path

          payload_path = Path(os.environ["PAYLOAD_PATH"])
          payload = payload_path.read_bytes()
          manifest = {
              "schema": "architecture-health-badge-promotion/v1",
              "kind": "architecture-health-badge",
              "context": {
                  "repository": os.environ["GITHUB_REPOSITORY"],
                  "pr_number": os.environ["GITHUB_EVENT_NUMBER"],
                  "base_ref": os.environ["GITHUB_BASE_REF"],
                  "base_sha": os.environ["GITHUB_BASE_SHA"],
                  "head_sha": os.environ["PR_HEAD_SHA"],
                  "head_tree_sha": os.environ["PR_HEAD_TREE_SHA"],
                  "run_id": os.environ["GITHUB_RUN_ID"],
                  "run_attempt": os.environ["GITHUB_RUN_ATTEMPT"],
              },
              "payload": {
                  "path": "architecture-health-badge.json",
                  "bytes": len(payload),
                  "sha256": hashlib.sha256(payload).hexdigest(),
              },
          }
          Path(os.environ["MANIFEST_PATH"]).write_text(json.dumps(manifest, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8")
          PY
          if [[ "$health_status" -ne 0 || "$badge_status" -ne 0 ]]; then
            exit 1
          fi
      - name: Upload bound Architecture Health badge
        if: always() && hashFiles(format('${{ runner.temp }}/architecture-health-badge-artifact/architecture-health-badge.json')) != '' && hashFiles(format('${{ runner.temp }}/architecture-health-badge-artifact/architecture-health-badge.manifest.json')) != ''
        uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a
        with:
          name: __ARTIFACT_NAME__
          path: ${{ runner.temp }}/architecture-health-badge-artifact/*
          if-no-files-found: error
      - name: Upload semantic Architecture Health evidence
        if: always() && hashFiles(format('${{ runner.temp }}/architecture-health-artifact/architecture-health.json')) != ''
        uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a
        with:
          name: __EVIDENCE_ARTIFACT_NAME__
          path: ${{ runner.temp }}/architecture-health-artifact/architecture-health.json
          if-no-files-found: error
""";
        return Template
            .Replace("__JOB_NAME__", producer.JobName, StringComparison.Ordinal)
            .Replace("__CHECK_NAME__", producer.CheckName, StringComparison.Ordinal)
            .Replace("__BASE_REF__", baseRef, StringComparison.Ordinal)
            .Replace("__POLICY_PATH__", policyPath, StringComparison.Ordinal)
            .Replace("__SOLUTION_PATH__", solutionPath, StringComparison.Ordinal)
            .Replace("__ARTIFACT_NAME__", producer.ArtifactName, StringComparison.Ordinal)
            .Replace("__EVIDENCE_ARTIFACT_NAME__", producer.EvidenceArtifactName, StringComparison.Ordinal);
    }

    private static string RenderPublisherWorkflow(BadgeSetupConfiguration configuration)
    {
        string workflowRef = configuration.Pins?.WorkflowRef ?? BadgeSetupContract.DefaultPublisherWorkflowRef;
        string workflowSha = configuration.Pins?.WorkflowSha ?? BadgeSetupContract.DefaultPublisherWorkflowSha;
        const string Template = """
name: Publish Architecture Health badge

on:
  push:
    branches:
      - __BASE_REF__

permissions:
  contents: read

jobs:
  publish:
    permissions:
      actions: read
      checks: read
      contents: write
      id-token: write
      pull-requests: read
    uses: __WORKFLOW_REF__@__WORKFLOW_SHA__
    with:
      configuration-id: setup_generated
      adapter: __ADAPTER__
      operation: publish
""";
        return Template
            .Replace("__BASE_REF__", configuration.BaseRef, StringComparison.Ordinal)
            .Replace("__WORKFLOW_REF__", workflowRef, StringComparison.Ordinal)
            .Replace("__WORKFLOW_SHA__", workflowSha, StringComparison.Ordinal)
            .Replace("__ADAPTER__", configuration.Mode, StringComparison.Ordinal);
    }

    private static string RenderRenewalWorkflow(BadgeSetupConfiguration configuration)
    {
        string workflowRef = configuration.Pins?.WorkflowRef ?? BadgeSetupContract.DefaultPublisherWorkflowRef;
        string workflowSha = configuration.Pins?.WorkflowSha ?? BadgeSetupContract.DefaultPublisherWorkflowSha;
        const string Template = """
name: Renew Architecture Health badge

on:
  schedule:
    - cron: "__CRON__"

permissions:
  contents: read

jobs:
  renew:
    permissions:
      actions: read
      checks: read
      contents: write
      id-token: write
      pull-requests: read
    uses: __WORKFLOW_REF__@__WORKFLOW_SHA__
    with:
      configuration-id: setup_generated
      adapter: relay
      operation: renew
""";
        return Template
            .Replace("__CRON__", CronFor(configuration.Renewal.CadenceMinutes), StringComparison.Ordinal)
            .Replace("__WORKFLOW_REF__", workflowRef, StringComparison.Ordinal)
            .Replace("__WORKFLOW_SHA__", workflowSha, StringComparison.Ordinal);
    }

    private static string CronFor(int cadenceMinutes) => cadenceMinutes switch
    {
        <= 60 => $"*/{cadenceMinutes} * * * *",
        _ when cadenceMinutes % 60 == 0 => $"0 */{cadenceMinutes / 60} * * *",
        _ => "0 * * * *",
    };

    private static string BuildReadme(string root, BadgeSetupConfiguration configuration)
    {
        string existing = File.Exists(Path.Combine(root, "README.md"))
            ? File.ReadAllText(Path.Combine(root, "README.md"), new UTF8Encoding(false))
            : string.Empty;
        int startCount = Count(existing, ReadmeStartMarker);
        int endCount = Count(existing, ReadmeEndMarker);
        if (startCount != endCount || startCount > 1)
        {
            throw new IOException("README.md contains an ambiguous managed badge region.");
        }

        string block = ReadmeStartMarker + "\n" + RenderReadmeBlock(configuration) + "\n" + ReadmeEndMarker;
        if (startCount == 0)
        {
            return existing.Length == 0
                ? block + "\n"
                : existing + (existing.EndsWith("\n", StringComparison.Ordinal) ? "\n" : "\n\n") + block + "\n";
        }

        int start = existing.IndexOf(ReadmeStartMarker, StringComparison.Ordinal);
        int end = existing.IndexOf(ReadmeEndMarker, start + ReadmeStartMarker.Length, StringComparison.Ordinal);
        int afterEnd = end + ReadmeEndMarker.Length;
        return existing[..start] + block + existing[afterEnd..];
    }

    private static string RenderReadmeBlock(BadgeSetupConfiguration configuration)
    {
        if (configuration.Mode == BadgeSetupMode.None.ToWireValue())
        {
            return "Architecture Health badge publication is disabled (`none`). Private reports and checks remain available.";
        }

        string origin = configuration.Mode == BadgeSetupMode.GithubRaw.ToWireValue()
            ? $"https://raw.githubusercontent.com/{configuration.Repository.Owner}/{configuration.Repository.Name}/architecture-health-badge/architecture-health.json"
            : $"{configuration.Destination.Endpoint!.TrimEnd('/')}/badge-relay/v1/{configuration.Destination.Alias}";
        if (configuration.Mode == BadgeSetupMode.Relay.ToWireValue()
            && configuration.DisclosureProfile == BadgeSetupContract.HeadlinePlusFreshnessProfile)
        {
            return $"![Architecture Health]({origin}.svg)";
        }

        string shields = "https://img.shields.io/endpoint?url=" + Uri.EscapeDataString(
            configuration.Mode == BadgeSetupMode.GithubRaw.ToWireValue() ? origin : origin + ".json");
        return $"![Architecture Health]({shields})";
    }

    private static string RenderRegistry(BadgeSetupConfiguration configuration)
    {
        string adapter = configuration.Mode;
        object destination = adapter switch
        {
            "github-raw" => new { adapter, branch = "architecture-health-badge", endpoint_path = "architecture-health.json" },
            "relay" => new
            {
                adapter,
                alias = configuration.Destination.Alias,
                endpoint = configuration.Destination.Endpoint,
                audience = configuration.Destination.Audience,
            },
            _ => new { adapter },
        };
        BadgeSetupProducer producer = configuration.Producer!;
        return Serialize(new
        {
            schema = "architecture-health-badge-promotion/registry/v1",
            configurations = new
            {
                setup_generated = new
                {
                    schema_id = "architecture-health-badge-promotion/v1",
                    repository = $"{configuration.Repository.Owner}/{configuration.Repository.Name}",
                    repository_visibility = configuration.Repository.Visibility,
                    base_ref = configuration.BaseRef,
                    producer,
                    destination,
                    disclosure_profile = configuration.DisclosureProfile,
                    validity = new { max_lease_seconds = configuration.Renewal.MaxLeaseMinutes * 60 },
                    limits = new { max_archive_bytes = 65_536, max_member_bytes = 16_384, max_payload_bytes = 16_384, max_members = 2 },
                },
            },
        });
    }

    private static IReadOnlyList<GeneratedFile> ReadRelayBundle(BadgeSetupConfiguration configuration)
    {
        string? root = FindRepositoryPath("relay", "architecture-health-badge-setup") ?? FindRepositoryPath("relay", string.Empty);
        if (root is null)
        {
            throw new IOException("The shipped badge Relay bundle is missing.");
        }

        List<GeneratedFile> files = [];
        foreach (string source in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.ts", SearchOption.TopDirectoryOnly).OrderBy(static path => path, StringComparer.Ordinal))
        {
            files.Add(new("relay/src/" + Path.GetFileName(source), ReadText(source)));
        }

        foreach (string fileName in new[] { "package.json", "package-lock.json", "tsconfig.json" })
        {
            files.Add(new("relay/" + fileName, ReadText(Path.Combine(root, fileName))));
        }

        files.Add(new("relay/wrangler.jsonc", RenderWrangler(configuration, ReadText(Path.Combine(root, "wrangler.jsonc")))));
        return files;
    }

    private static string RenderWrangler(BadgeSetupConfiguration configuration, string template)
    {
        if (template.Contains("synthetic-", StringComparison.Ordinal) || template.Contains("RELAY_REGISTRY", StringComparison.Ordinal))
        {
            throw new IOException("The shipped Relay template contains fixture identity and cannot be used for an adopter bundle.");
        }

        string alias = configuration.Destination.Alias!;
        string registryEntry = Serialize(new
        {
            repository_id = configuration.Repository.RepositoryId!.Value,
            repository_owner_id = configuration.Repository.RepositoryOwnerId!.Value,
            owner = configuration.Repository.Owner,
            repository = configuration.Repository.Name,
            destination_alias = alias,
            permitted_event = "push",
            permitted_events = configuration.Renewal.Enabled ? new[] { "push", "schedule" } : new[] { "push" },
            permitted_ref = $"refs/heads/{configuration.BaseRef}",
            job_workflow_ref = $"{configuration.Pins!.WorkflowRef}@{configuration.Pins.WorkflowSha}",
            job_workflow_sha = configuration.Pins.WorkflowSha,
            disclosure_profile = configuration.DisclosureProfile,
            audience = configuration.Destination.Audience,
            consent = configuration.DisclosureApproved,
        });
        string config = Serialize(new
        {
            schema_id = "architecture-health-badge-relay-config/v1",
            mode = "relay",
            bundle = configuration.Bundle,
            oidc_trust = new
            {
                issuer = "https://token.actions.githubusercontent.com",
                jwks_uri = "https://token.actions.githubusercontent.com/.well-known/jwks",
                audience = configuration.Destination.Audience,
                allowed_algorithms = new[] { "RS256" },
            },
            registry_entry = JsonSerializer.Deserialize<JsonElement>(registryEntry),
        });
        string value = JsonSerializer.Serialize(config);
        string result = template.Replace("\"name\": \"badge-relay-v1\"", $"\"name\": \"badge-relay-{alias}\"", StringComparison.Ordinal).TrimEnd();
        int finalBrace = result.LastIndexOf('}');
        if (finalBrace < 0)
        {
            throw new IOException("The shipped Relay Wrangler template is malformed.");
        }

        string body = result[..finalBrace].TrimEnd();
        if (!body.EndsWith(",", StringComparison.Ordinal))
        {
            body += ",";
        }

        return body + "\n  \"vars\": {\n    \"RELAY_REGISTRY\": " + value + "\n  }\n}\n";
    }

    private static string ComputeBundleDigest(IReadOnlyList<GeneratedFile> files)
    {
        using MemoryStream stream = new();
        foreach (GeneratedFile file in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
        {
            byte[] path = Encoding.UTF8.GetBytes(file.Path);
            byte[] contents = Encoding.UTF8.GetBytes(file.Contents);
            stream.Write(path);
            stream.WriteByte(0);
            stream.Write(contents);
            stream.WriteByte(0);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static string ReadAsset(string relativePath)
    {
        string? path = FindRepositoryPath(relativePath, "architecture-health-badge-setup") ?? FindRepositoryPath(relativePath, string.Empty);
        return path is null ? throw new IOException($"Shipped setup asset '{relativePath}' is missing.") : ReadText(path);
    }

    private static string? FindRepositoryPath(string relativePath, string packagedRoot)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = string.IsNullOrEmpty(packagedRoot)
                ? Path.Combine(directory.FullName, relativePath)
                : Path.Combine(directory.FullName, packagedRoot, relativePath);
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string ReadText(string path) => File.Exists(path)
        ? File.ReadAllText(path, new UTF8Encoding(false))
        : throw new IOException($"The shipped setup asset '{Path.GetFileName(path)}' is missing.");

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;

    private static object FileEntry(string path, string contents) => new { path, sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contents))).ToLowerInvariant() };

    private static void EnsureNoConflict(string root, string relativePath, string contents)
    {
        string path = SafePath(root, relativePath);
        if (relativePath == "README.md" || !File.Exists(path))
        {
            return;
        }

        if (File.ReadAllText(path, new UTF8Encoding(false)) != contents)
        {
            throw new IOException($"Managed output conflict at '{relativePath}'. Review or remove the existing managed file before retrying.");
        }
    }

    private static void WriteManaged(string root, string relativePath, string contents)
    {
        string path = SafePath(root, relativePath);
        string? directory = Path.GetDirectoryName(path);
        if (directory is not null) Directory.CreateDirectory(directory);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, contents, new UTF8Encoding(false));
        File.Move(temporary, path, overwrite: true);
    }

    private static string SafePath(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath) || relativePath.Contains("..", StringComparison.Ordinal))
        {
            throw new IOException("Managed output path escapes the setup directory.");
        }

        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? path
            : throw new IOException("Managed output path escapes the setup directory.");
    }

    private static OriginalFile CaptureOriginal(string root, string relativePath)
    {
        string path = SafePath(root, relativePath);
        return new(relativePath, File.Exists(path), File.Exists(path) ? File.ReadAllBytes(path) : null);
    }

    private static void RestoreOriginals(string root, IReadOnlyList<OriginalFile> originals)
    {
        foreach (OriginalFile original in originals.Reverse())
        {
            string path = SafePath(root, original.Path);
            if (original.Exists)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, original.Contents!);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static int Count(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private sealed record GeneratedFile(string Path, string Contents);

    private sealed record OriginalFile(string Path, bool Exists, byte[]? Contents);
}
