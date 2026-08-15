using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Contracts.PolicyImports;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Execution.Configuration;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

// Owns the ordered configuration-validation phase. Run-scoped facts and caches remain in the
// session, which is passed only to registered contributors and the small fact-access boundary.
internal sealed class ArchitectureConfigurationValidationService
{
    private const string ConfigurationSource = "<configuration>";

    private readonly ArchitectureAnalysisSession _session;

    public ArchitectureConfigurationValidationService(ArchitectureAnalysisSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    private ArchitectureAnalysisContext Context => _session.Context;

    private ArchitectureContractDocument Document => _session.Document;

    public List<ArchitectureViolation> Check(bool strict)
    {
        List<ArchitectureViolation> violations = new();

        AddMissingAssemblyViolations(violations);
        AddDiscoveryDiagnosticViolations(violations);

        ArchitectureConfigurationReferenceCollector collector = BuildConfigurationReferenceCollector(strict);
        HashSet<string> ruleInputCoveredContractIds = CollectRuleInputCoveredContractIds(strict);

        AddLayerReferenceViolations(violations, collector, ruleInputCoveredContractIds);
        AddExternalDependencyGroupViolations(violations, collector);
        AddPackageGroupViolations(violations, collector);
        AddPackageMetadataViolations(violations, collector);
        AddFrameworkGroupViolations(violations, collector);
        AddFrameworkMetadataViolations(violations, collector);
        _session.AddFrameworkEvaluationFailureViolations(violations, collector);
        AddProjectMetadataViolations(violations, collector);

        return violations;
    }

    private static string[] ContractIdAliases(IArchitectureContract contract)
    {
        return new[]
            {
                contract.Id,
                (contract as IArchitectureSourceExpandableContract)?.ExpansionOrigin?.AuthoredContractId
            }
            .OfType<string>()
            .ToArray();
    }

    private void AddMissingAssemblyViolations(List<ArchitectureViolation> violations)
    {
        foreach (string missingAssembly in Context.MissingAssemblyNames)
        {
            string probeInfo = Context.AssemblyProbingPaths.Count > 0
                ? $" Probing paths: {string.Join("; ", Context.AssemblyProbingPaths)}"
                : string.Empty;

            var violation = new ArchitectureViolation(
                ConfigurationSource,
                null,
                missingAssembly,
                "missing target assembly",
                new[] { $"Assembly '{missingAssembly}' is declared in analysis.target_assemblies but could not be resolved.{probeInfo}" });
            int index = Document.Analysis.TargetAssemblies.IndexOf(missingAssembly);
            violations.Add(index < 0
                ? violation
                : Document.Provenance.EnrichAtPath(
                    violation,
                    ArchitecturePolicyProvenancePath.AppendIndex(
                        ArchitecturePolicyProvenancePath.AppendProperty(
                            ArchitecturePolicyProvenancePath.Property("analysis"), "target_assemblies"),
                        index)));
        }
    }

    private void AddDiscoveryDiagnosticViolations(List<ArchitectureViolation> violations)
    {
        foreach (ArchitectureProjectDiscoveryDiagnostic discoveryDiagnostic in Context.DiscoveryDiagnostics)
        {
            violations.Add(new ArchitectureViolation(
                ConfigurationSource,
                null,
                discoveryDiagnostic.Subject,
                discoveryDiagnostic.Kind,
                new[] { discoveryDiagnostic.Message }));
        }
    }

    private ArchitectureConfigurationReferenceCollector BuildConfigurationReferenceCollector(bool strict)
    {
        ArchitectureConfigurationReferenceCollector collector = new();

        foreach (ArchitectureContractFamilyDescriptor descriptor in ArchitectureContractFamilyRegistry.All)
        {
            if (descriptor.ConfigurationContributor is null)
            {
                continue;
            }

            IEnumerable<IArchitectureContract> contracts = strict
                ? descriptor.StrictContracts(Document.Contracts)
                : descriptor.AuditContracts(Document.Contracts);

            foreach (IArchitectureContract contract in contracts)
            {
                descriptor.ConfigurationContributor(_session, collector, contract);
            }
        }

        return collector;
    }

    private void AddLayerReferenceViolations(
        List<ArchitectureViolation> violations,
        ArchitectureConfigurationReferenceCollector collector,
        HashSet<string> ruleInputCoveredContractIds)
    {
        foreach ((string layerName, List<IArchitectureContract> referencingContracts) in
                 collector.LayerReferencingContracts)
        {
            List<string[]> referencingContractIdAliases = referencingContracts
                .Select(ContractIdAliases)
                .Where(aliases => aliases.Length > 0)
                .ToList();
            bool isFullyOwnedByRuleInputCoverage = referencingContractIdAliases.Count > 0
                && referencingContractIdAliases.All(aliases => aliases.Any(ruleInputCoveredContractIds.Contains));

            // A dangling layer name referenced exclusively by contracts a rule_input coverage
            // contract tracks defers to that coverage contract's own "unresolved" finding
            // instead of throwing here — otherwise scope: rule_input's unresolved diagnostic
            // would be unreachable through the real validation pipeline, since this resolution
            // happens before any contract or coverage check runs.
            if (!Document.Layers.ContainsKey(layerName) && isFullyOwnedByRuleInputCoverage)
            {
                continue;
            }

            ArchitectureLayer layer;
            try
            {
                layer = ArchitectureLayerResolver.ResolveLayer(Document, ConfigurationSource, layerName);
            }
            catch (InvalidOperationException exception)
            {
                Exception enriched = Document.Provenance.EnrichValidationException(
                    exception,
                    referencingContracts.Cast<object>());
                if (ReferenceEquals(enriched, exception))
                {
                    throw;
                }

                throw enriched;
            }

            if (layer.External)
            {
                continue;
            }

            Type[] types = _session.Facts.FindTypesInLayer(layer);

            if (types.Length == 0)
            {
                // A layer referenced exclusively by contracts that a rule_input coverage contract
                // explicitly tracks (via contract_ids) defers to that coverage contract's own
                // empty-input classification and severity instead of also failing here as a hard,
                // unconditional configuration error — otherwise analysis.coverage and exclude
                // entries could never actually govern the outcome for these contracts.
                if (isFullyOwnedByRuleInputCoverage)
                {
                    continue;
                }

                string matchDescription = layer.Selector == null
                    ? $"namespace '{layer.Namespace}'"
                    : $"semantic selector '{ArchitectureLayerResolver.DescribeLayer(layer)}'";

                var violation = new ArchitectureViolation(
                    ConfigurationSource,
                    null,
                    ArchitectureLayerResolver.DescribeLayer(layer),
                    layer.Selector == null ? "empty layer namespace" : "empty layer selector",
                    new[] { $"Layer '{layerName}' {matchDescription} contains no matching types in loaded assemblies." });
                violations.Add(Document.Provenance.EnrichAtPath(
                    violation,
                    ArchitecturePolicyProvenancePath.AppendProperty(
                        ArchitecturePolicyProvenancePath.Property("layers"), layerName)));
            }
        }
    }

    private void AddExternalDependencyGroupViolations(
        List<ArchitectureViolation> violations, ArchitectureConfigurationReferenceCollector collector)
    {
        foreach ((string groupName, List<IArchitectureContract> referencingContracts) in
                 collector.ReferencedExternalGroups)
        {
            if (!Document.ExternalDependencies.TryGetValue(groupName, out ArchitectureExternalDependencyGroup? group))
            {
                var violation = new ArchitectureViolation(
                    ConfigurationSource,
                    null,
                    groupName,
                    "unknown external dependency group",
                    new[]
                    {
                        $"External dependency group '{groupName}' is referenced by a contract but is not declared in external_dependencies."
                    })
                {
                    Payload = new ExternalDependencyPayload(groupName)
                };
                violations.Add(Document.Provenance.Enrich(
                    violation,
                    referencingContracts.FirstOrDefault(),
                    referencingContracts.Skip(1).Cast<object>()));

                continue;
            }

            if (ArchitectureExternalDependencyResolver.HasUsableMatchers(group))
            {
                continue;
            }

            var invalidGroup = new ArchitectureViolation(
                ConfigurationSource,
                null,
                groupName,
                "invalid external dependency group",
                new[]
                {
                    $"External dependency group '{groupName}' must declare at least one non-empty namespace_prefixes or type_prefixes matcher."
                })
            {
                Payload = new ExternalDependencyPayload(groupName)
            };
            violations.Add(Document.Provenance.EnrichAtPath(
                invalidGroup,
                ArchitecturePolicyProvenancePath.AppendProperty(
                    ArchitecturePolicyProvenancePath.Property("external_dependencies"), groupName)));
        }
    }

    private void AddPackageGroupViolations(
        List<ArchitectureViolation> violations, ArchitectureConfigurationReferenceCollector collector)
    {
        foreach ((string groupName, List<IArchitectureContract> referencingContracts) in
                 collector.ReferencedPackageGroups)
        {
            if (!Document.Packages.TryGetValue(groupName, out ArchitecturePackageGroup? group))
            {
                var violation = new ArchitectureViolation(
                    ConfigurationSource,
                    null,
                    groupName,
                    "unknown package group",
                    new[]
                    {
                        $"Package group '{groupName}' is referenced by a contract but is not declared in packages."
                    })
                {
                    Payload = new PackageDependencyPayload(groupName)
                };
                violations.Add(Document.Provenance.Enrich(
                    violation,
                    referencingContracts.FirstOrDefault(),
                    referencingContracts.Skip(1).Cast<object>()));

                continue;
            }

            if (ArchitecturePackageDependencyResolver.HasUsableMatchers(group))
            {
                continue;
            }

            var invalidGroup = new ArchitectureViolation(
                ConfigurationSource,
                null,
                groupName,
                "invalid package group",
                new[]
                {
                    $"Package group '{groupName}' must declare at least one non-empty package_ids or package_prefixes matcher."
                })
            {
                Payload = new PackageDependencyPayload(groupName)
            };
            violations.Add(Document.Provenance.EnrichAtPath(
                invalidGroup,
                ArchitecturePolicyProvenancePath.AppendProperty(
                    ArchitecturePolicyProvenancePath.Property("packages"), groupName)));
        }
    }

    private void AddPackageMetadataViolations(
        List<ArchitectureViolation> violations, ArchitectureConfigurationReferenceCollector collector)
    {
        if (collector.PackageContractSources.Count == 0)
        {
            return;
        }

        HashSet<string> projectsWithPackageData = new(
            Context.ProjectDiscovery?.DiscoveredProjects.Select(project => project.AssemblyName) ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);

        foreach ((IArchitectureContract contract, string source) in collector.PackageContractSources
                     .DistinctBy(entry => (entry.Contract, entry.Source)))
        {
            if (projectsWithPackageData.Contains(source))
            {
                continue;
            }

            var violation = new ArchitectureViolation(
                contract.Name,
                contract.Id,
                source,
                "no package metadata discovered",
                new[]
                {
                    $"Contract '{contract.Name}' declares source '{source}', but no discovered project with that assembly name has package reference metadata available. " +
                    "Package dependency/allow-only contracts require analysis.solution or analysis.projects to be configured so project discovery can parse PackageReference items; " +
                    "without it, this contract will never report a violation."
                });
            violations.Add(Document.Provenance.Enrich(violation, contract));
        }
    }

    private void AddFrameworkGroupViolations(
        List<ArchitectureViolation> violations, ArchitectureConfigurationReferenceCollector collector)
    {
        foreach ((string groupName, List<IArchitectureContract> referencingContracts) in
                 collector.ReferencedFrameworkGroups)
        {
            if (!Document.FrameworkReferences.TryGetValue(groupName, out ArchitectureFrameworkReferenceGroup? group))
            {
                var violation = new ArchitectureViolation(
                    ConfigurationSource,
                    null,
                    groupName,
                    "unknown framework group",
                    new[]
                    {
                        $"Framework group '{groupName}' is referenced by a contract but is not declared in framework_references."
                    })
                {
                    Payload = new FrameworkReferencePayload(groupName)
                };
                violations.Add(Document.Provenance.Enrich(
                    violation,
                    referencingContracts.FirstOrDefault(),
                    referencingContracts.Skip(1).Cast<object>()));

                continue;
            }

            if (ArchitectureFrameworkReferenceResolver.HasUsableMatchers(group))
            {
                continue;
            }

            var invalidGroup = new ArchitectureViolation(
                ConfigurationSource,
                null,
                groupName,
                "invalid framework group",
                new[]
                {
                    $"Framework group '{groupName}' must declare at least one non-empty framework_names or framework_name_prefixes matcher."
                })
            {
                Payload = new FrameworkReferencePayload(groupName)
            };
            violations.Add(Document.Provenance.EnrichAtPath(
                invalidGroup,
                ArchitecturePolicyProvenancePath.AppendProperty(
                    ArchitecturePolicyProvenancePath.Property("framework_references"), groupName)));
        }
    }

    private void AddFrameworkMetadataViolations(
        List<ArchitectureViolation> violations, ArchitectureConfigurationReferenceCollector collector)
    {
        if (collector.FrameworkContractSources.Count == 0)
        {
            return;
        }

        HashSet<string> projectsWithFrameworkData = new(
            Context.ProjectDiscovery?.DiscoveredProjects.Select(project => project.AssemblyName) ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);

        foreach ((IArchitectureContract contract, string source) in collector.FrameworkContractSources
                     .DistinctBy(entry => (entry.Contract, entry.Source)))
        {
            if (projectsWithFrameworkData.Contains(source))
            {
                continue;
            }

            var violation = new ArchitectureViolation(
                contract.Name,
                contract.Id,
                source,
                "no project metadata discovered",
                new[]
                {
                    $"Contract '{contract.Name}' declares source '{source}', but no discovered project with that assembly name has project metadata available. " +
                    "Framework dependency/allow-only contracts require analysis.solution or analysis.projects to be configured so project discovery can parse FrameworkReference items; " +
                    "without it, this contract will never report a violation."
                });
            violations.Add(Document.Provenance.Enrich(violation, contract));
        }
    }

    private void AddProjectMetadataViolations(
        List<ArchitectureViolation> violations, ArchitectureConfigurationReferenceCollector collector)
    {
        if (collector.ProjectMetadataContractProjects.Count == 0)
        {
            return;
        }

        HashSet<string> discoveredProjectPaths = new(
            Context.ProjectDiscovery?.DiscoveredProjects.Select(project => ProjectPathNormalizer.Normalize(project.Path))
            ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach ((IArchitectureContract contract, string projectPath) in collector.ProjectMetadataContractProjects
                     .DistinctBy(entry => (entry.Contract, entry.ProjectPath)))
        {
            if (discoveredProjectPaths.Contains(projectPath))
            {
                continue;
            }

            var violation = new ArchitectureViolation(
                contract.Name,
                contract.Id,
                projectPath,
                "no project metadata discovered",
                new[]
                {
                    $"Contract '{contract.Name}' targets project '{projectPath}', but project discovery did not expose metadata for that path. " +
                    "Project metadata contracts require analysis.solution or analysis.projects to discover and parse the matching .csproj file."
                })
            {
                Payload = new ProjectMetadataPayload(ProjectMetadataKind: "missing_project")
            };
            violations.Add(Document.Provenance.Enrich(violation, contract));
        }
    }

    // Only contracts that ArchitectureContractExecutor will actually run for this request can
    // defer CheckConfiguration's hard failure: ContractsFor(mode, "coverage") only executes the
    // group matching the current mode (strict_coverage for strict, audit_coverage for audit), and
    // CheckCoverageContract itself no-ops when the coverage contract isn't selected. Deferring
    // for a coverage contract that won't run this request would silently drop the finding
    // entirely instead of handing it off — the same false-green risk this deferral exists to
    // avoid in the first place.
    internal HashSet<string> CollectRuleInputCoveredContractIds(bool strict)
    {
        IEnumerable<ArchitectureCoverageContract> coverageContractsForMode = strict
            ? Document.Contracts.StrictCoverage
            : Document.Contracts.AuditCoverage;

        return new HashSet<string>(
            coverageContractsForMode
                .Where(c => string.Equals(c.Scope, "rule_input", StringComparison.Ordinal))
                .Where(c => _session.IsContractSelected(c.Id))
                .SelectMany(c => c.ContractIds),
            StringComparer.OrdinalIgnoreCase);
    }
}
