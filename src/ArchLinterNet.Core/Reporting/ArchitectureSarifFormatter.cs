using System.Text.Json;
using System.Text.RegularExpressions;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting.Abstractions;

namespace ArchLinterNet.Core.Reporting;

public sealed partial class ArchitectureSarifFormatter : IArchitectureSarifFormatter
{
    private const string SchemaUri =
        "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json";

    private const string ToolName = "arch-linter-net";
    private const string SarifVersion = "2.1.0";
    private const string VersionPropertyName = "version";
    private const string MessagePropertyName = "message";
    private const string PropertiesKey = "properties";
    private const string MethodBodyCategory = "method-body";
    private const string MethodBodyIlCategory = "method-body-il";
    private const string CycleRuleFallback = "dependency-cycle";
    private const string PhysicalLocationKey = "physicalLocation";
    private const string ArtifactLocationKey = "artifactLocation";

    [GeneratedRegex(@"^line (?<line>\d+):", RegexOptions.CultureInvariant)]
    private static partial Regex MethodBodyLinePattern();
    [GeneratedRegex(@"^\[(?<id>[^\]]+)\] ", RegexOptions.CultureInvariant)]
    private static partial Regex CycleIdPrefixPattern();

    public string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string> cycles,
        string toolVersion)
    {
        return FormatResultAsSarifCore(
            mode,
            violations,
            cycles.Select(cycle => (Func<string, ResultEntry>)(level => BuildCycleEntry(cycle, level))),
            toolVersion,
            Array.Empty<BuildStatePreflightDiagnostic>());
    }

    public static string FormatResultAsSarif(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<ArchitectureCycleFinding> cycles,
        string toolVersion)
    {
        return FormatResultAsSarifCore(
            mode,
            violations,
            cycles.Select(cycle => (Func<string, ResultEntry>)(level =>
                BuildCycleEntry(ArchitectureDiagnosticMapper.FromCycle(cycle), level))),
            toolVersion,
            Array.Empty<BuildStatePreflightDiagnostic>());
    }

    private static string FormatResultAsSarifCore( // NOSONAR: each parameter represents a semantically distinct section of the SARIF payload; grouping would obscure the data contract
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IEnumerable<Func<string, ResultEntry>> cycleEntryFactories,
        string toolVersion,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> preflightDiagnostics,
        IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
        ArchitectureSourceExpansionInventory? sourceExpansion = null,
        IReadOnlyCollection<ArchitectureSubtractiveMatcherParticipation>? subtractiveMatcherParticipation = null,
        CancellationToken cancellationToken = default)
    {
        string level = mode == "strict" ? "error" : "warning";

        // Violations are the dominant contributor to a large report's size, so this is checked
        // per finding — not just before/after the whole SARIF document is built. The final
        // OrderBy over every ResultEntry is interruptible too: a single comparer that replicates
        // the RuleId/SourceIdentifier/Category tiebreakers observes the token on every
        // comparison, so cancellation mid-sort of a large report stops at the next comparison
        // instead of after the whole sort has finished (LINQ's stable OrderBy keeps ties in
        // source order, so non-cancelled output is byte-for-byte unchanged). LINQ's sort
        // machinery wraps comparer exceptions in InvalidOperationException, so the comparer's
        // OperationCanceledException is unwrapped and rethrown as-is below to preserve the
        // cancellation completion semantics the CLI and Testing API depend on.
        List<ResultEntry> entries;
        try
        {
            entries = BuildViolationEntriesCancellationAware(
                    ArchitectureFindingMapper.FromViolations(violations, mode, cancellationToken), level, cancellationToken)
                .Concat(cycleEntryFactories.Select(factory => factory(level)))
                .Concat(preflightDiagnostics.Where(d => d.IsBlocking).Select(diagnostic => BuildPreflightEntry(diagnostic, mode)))
                .OrderBy(e => e, new ResultEntryOrderComparer(cancellationToken))
                .ToList();
        }
        catch (InvalidOperationException ex) when (ex.InnerException is OperationCanceledException)
        {
            throw ex.InnerException;
        }

        object[] rules = entries
            .GroupBy(e => e.RuleId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (object)new Dictionary<string, object?>
            {
                ["id"] = g.Key,
                ["shortDescription"] = new Dictionary<string, object?> { ["text"] = g.First().ContractName },
            })
            .ToArray();

        object[] results = entries.Select(e => (object)e.Json).ToArray();

        var payload = new Dictionary<string, object?>
        {
            ["$schema"] = SchemaUri,
            [VersionPropertyName] = SarifVersion,
            ["runs"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["tool"] = new Dictionary<string, object?>
                    {
                        ["driver"] = new Dictionary<string, object?>
                        {
                            ["name"] = ToolName,
                            ["version"] = toolVersion,
                            ["rules"] = rules,
                        },
                    },
                    ["results"] = results,
                    [PropertiesKey] = new Dictionary<string, object?>
                    {
                        ["coverage_summary"] = FormatCoverageSummaries(coverageSummaries ?? Array.Empty<ArchitectureCoverageSummary>()),
                        ["source_set_expansion"] = Reporting.ArchitectureSarifFormatter.FormatSourceExpansion(
                            sourceExpansion ?? ArchitectureSourceExpansionInventory.Empty),
                        ["subtractive_matcher_participation"] = Reporting.ArchitectureSarifFormatter.FormatSubtractiveMatcherParticipation(
                            subtractiveMatcherParticipation ?? Array.Empty<ArchitectureSubtractiveMatcherParticipation>())
                    },
                },
            },
        };

        return JsonSerializer.Serialize(payload);
    }

    private static List<ResultEntry> BuildViolationEntriesCancellationAware(
        IEnumerable<ArchitectureFinding> findings, string level, CancellationToken cancellationToken)
    {
        List<ResultEntry> entries = new();
        foreach (ArchitectureFinding finding in findings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(BuildViolationEntry(finding, level));
        }

        return entries;
    }

    private static object[] FormatCoverageSummaries(IReadOnlyCollection<ArchitectureCoverageSummary> summaries)
    {
        return summaries.OrderBy(summary => summary.ContractId ?? summary.ContractName, StringComparer.Ordinal)
            .Select(summary => (object)new Dictionary<string, object?>
            {
                ["contract"] = summary.ContractName,
                ["contract_id"] = summary.ContractId,
                ["scope"] = summary.Scope,
                ["optional_empty_items"] = summary.OptionalEmptyItems
                    .OrderBy(item => item.Item, StringComparer.Ordinal)
                    .Select(item => (object)new Dictionary<string, object?>
                    {
                        ["item"] = item.Item,
                        ["contract_id"] = item.ContractId,
                        ["input"] = item.Input,
                        ["layer"] = item.Layer,
                        ["reason"] = item.Reason,
                        ["evidence"] = item.Evidence,
                        ["policy_location"] = item.PolicyLocation is null ? null : new Dictionary<string, object?>
                        {
                            ["source_path"] = item.PolicyLocation.SourcePath,
                            ["yaml_path"] = item.PolicyLocation.YamlPath,
                            ["line"] = item.PolicyLocation.Line,
                            ["column"] = item.PolicyLocation.Column
                        }
                    }).ToArray()
            }).ToArray();
    }

    private static ResultEntry BuildViolationEntry(ArchitectureFinding finding, string level)
    {
        ArchitectureDiagnostic diagnostic = finding.Details;
        (string sourceType, string forbiddenNamespace, IReadOnlyCollection<string> references) = ExtractFields(diagnostic);
        string ruleId = diagnostic.ContractId ?? ArchitecturePolicyDocumentLoader.NormalizeToContractId(diagnostic.ContractName);

        var json = new Dictionary<string, object?>
        {
            ["ruleId"] = ruleId,
            ["level"] = level,
            [MessagePropertyName] = new Dictionary<string, object?>
            {
                ["text"] = $"[{diagnostic.ContractName}] {sourceType} -> {forbiddenNamespace}: {string.Join(", ", references)}",
            },
        };

        if (forbiddenNamespace == MethodBodyCategory)
        {
            json["locations"] = BuildPhysicalLocations(sourceType, references);
        }
        else if (diagnostic is LayoutConventionDiagnostic { MatchedFilePath: { } matchedFilePath })
        {
            // Unlike every other family's SourceType (a fully-qualified type name with no direct
            // filesystem mapping), a layout convention diagnostic's MatchedFilePath is already a
            // real repository-relative .cs path - using it as a physical location lets GitHub Code
            // Scanning anchor the finding to that file/line instead of falling back to a generic
            // logical (type-name) location it cannot resolve on disk.
            json["locations"] = BuildPhysicalLocations(matchedFilePath, Array.Empty<string>());
        }
        else if (FirstFrameworkReferenceSourcePath(diagnostic) is { } frameworkSourcePath)
        {
            // Every matched FrameworkReference was evaluated from the same source project's .csproj -
            // use that real, on-disk project-file location as a physical location (in addition to the
            // structured evidence in `properties`) rather than only a generic logical (assembly-name)
            // location.
            json["locations"] = BuildPhysicalLocations(frameworkSourcePath, Array.Empty<string>());
        }
        else
        {
            json["logicalLocations"] = BuildLogicalLocations(sourceType, LogicalLocationKindFor(diagnostic, forbiddenNamespace));
        }

        object[] relatedPolicyLocations = FormatPolicyLocationsForSarif(
            diagnostic.PolicyLocation,
            diagnostic.RelatedPolicyLocations);
        object[] relatedLocations = AppendWhenExpressionRelatedLocations(relatedPolicyLocations, GetWhenExpressions(diagnostic));
        if (relatedLocations.Length > 0)
        {
            json["relatedLocations"] = relatedLocations;
        }

        Dictionary<string, object?> properties = BuildProperties(diagnostic) ?? new Dictionary<string, object?>();
        // SARIF's standard fields remain the interoperable summary. The exact same
        // versioned JSON finding used by the CI formatter is retained under a
        // namespaced property so no evidence has to be reconstructed from prose.
        properties["arch_linter_net"] = ArchitectureDiagnosticFormatter.FormatNormalizedFindingForSarif(finding);
        json[PropertiesKey] = properties;

        return new ResultEntry(ruleId, diagnostic.ContractName, sourceType, forbiddenNamespace, json);
    }

    private static Dictionary<string, object?>? BuildProperties(ArchitectureDiagnostic diagnostic)
    {
        if (diagnostic is CompositionDiagnostic composition)
        {
            return BuildCompositionProperties(composition);
        }

        if (diagnostic is PublicApiSurfaceDiagnostic publicApiSurface)
        {
            return BuildPublicApiSurfaceProperties(publicApiSurface);
        }

        if (diagnostic is LayoutConventionDiagnostic layoutConvention)
        {
            return BuildLayoutConventionProperties(layoutConvention);
        }

        IReadOnlyCollection<FrameworkReferenceEvidence>? evidence = diagnostic switch
        {
            FrameworkReferenceDiagnostic d => d.Evidence,
            FrameworkReferenceAllowOnlyDiagnostic d => d.Evidence,
            _ => null,
        };

        if (evidence == null || evidence.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, object?>
        {
            ["evidence"] = evidence.Select(e => (object)new Dictionary<string, object?>
            {
                ["framework_name"] = e.FrameworkName,
                ["target_framework"] = e.TargetFramework,
                ["explicit"] = e.Explicit,
                ["source_path"] = e.SourcePath,
                ["configuration"] = e.Configuration,
            }).ToArray(),
        };
    }

    // Composition is the one non-FrameworkReference family whose per-violation identity carries
    // structured evidence (source assembly/member, matched API) beyond the generic
    // sourceType/forbiddenNamespace/references triple every family already gets via ExtractFields —
    // exposed here so same-named types in different assemblies are distinguishable in SARIF, not
    // just in human/--json/--explain output (issue #360).
    // API delta records must read the same in SARIF as in human and --json output: a reviewer
    // triaging a SARIF result needs to see whether a member was added, removed, or re-signed —
    // and, for a re-signed member, what the reviewed snapshot previously recorded.
    private static Dictionary<string, object?>? BuildPublicApiSurfaceProperties(PublicApiSurfaceDiagnostic publicApiSurface)
    {
        if (publicApiSurface.ApiDeltaKind == null && publicApiSurface.PreviousApiSignature == null
            && publicApiSurface.ApiAssemblyName == null && publicApiSurface.ApiVisibility == null
            && publicApiSurface.ForbiddenPublicConstant == null
            && publicApiSurface.UnselectedFirstPartyDependency == null)
        {
            return null;
        }

        var properties = new Dictionary<string, object?>();
        if (publicApiSurface.ApiDeltaKind != null)
            properties["api_delta_kind"] = publicApiSurface.ApiDeltaKind;

        if (publicApiSurface.PreviousApiSignature != null)
            properties["previous_api_signature"] = publicApiSurface.PreviousApiSignature;

        if (publicApiSurface.UnselectedFirstPartyDependency != null)
            properties["unselected_first_party_dependency"] = publicApiSurface.UnselectedFirstPartyDependency;

        if (publicApiSurface.UndeclaredApiSignature != null)
            properties["api_signature"] = publicApiSurface.UndeclaredApiSignature;

        if (publicApiSurface.ApiAssemblyName != null)
            properties["assembly"] = publicApiSurface.ApiAssemblyName;

        if (publicApiSurface.ApiVisibility != null)
            properties["visibility"] = publicApiSurface.ApiVisibility;

        if (publicApiSurface.ForbiddenPublicConstant != null)
            properties["forbidden_public_constant"] = publicApiSurface.ForbiddenPublicConstant;

        return properties;
    }

    private static Dictionary<string, object?>? BuildLayoutConventionProperties(LayoutConventionDiagnostic layoutConvention)
    {
        if (layoutConvention.ExpectedDeclarationCount == null
            && layoutConvention.ActualDeclarationCount == null
            && layoutConvention.DeclarationPaths == null
            && layoutConvention.ExpectedRoles == null
            && layoutConvention.ExpectedAbstractClass == null)
        {
            return null;
        }

        var properties = new Dictionary<string, object?>();
        if (layoutConvention.ExpectedDeclarationCount != null)
            properties["expected_declaration_count"] = layoutConvention.ExpectedDeclarationCount;

        if (layoutConvention.ActualDeclarationCount != null)
            properties["actual_declaration_count"] = layoutConvention.ActualDeclarationCount;

        if (layoutConvention.DeclarationPaths != null)
            properties["declaration_paths"] = layoutConvention.DeclarationPaths.ToArray();

        if (layoutConvention.ExpectedRoles != null)
            properties["expected_roles"] = layoutConvention.ExpectedRoles.ToArray();

        if (layoutConvention.ActualRole != null)
            properties["actual_role"] = layoutConvention.ActualRole;

        if (layoutConvention.ExpectedAbstractClass != null)
            properties["expected_abstract_class"] = layoutConvention.ExpectedAbstractClass;

        if (layoutConvention.ActualIsAbstract != null)
            properties["actual_abstract"] = layoutConvention.ActualIsAbstract;

        return properties;
    }

    private static Dictionary<string, object?>? BuildCompositionProperties(CompositionDiagnostic composition)
    {
        if (composition.SourceAssembly == null && composition.SourceMember == null
            && composition.MatchedForbiddenApi == null && composition.ExpectedCompositionBoundary == null)
        {
            return null;
        }

        var properties = new Dictionary<string, object?>();
        if (composition.SourceAssembly != null)
            properties["source_assembly"] = composition.SourceAssembly;

        if (composition.SourceMember != null)
            properties["source_member"] = composition.SourceMember;

        if (composition.MatchedForbiddenApi != null)
            properties["matched_forbidden_api"] = composition.MatchedForbiddenApi;

        if (composition.ExpectedCompositionBoundary != null)
            properties["expected_composition_boundary"] = composition.ExpectedCompositionBoundary;

        return properties;
    }

    private static string? FirstFrameworkReferenceSourcePath(ArchitectureDiagnostic diagnostic)
    {
        IReadOnlyCollection<FrameworkReferenceEvidence>? evidence = diagnostic switch
        {
            FrameworkReferenceDiagnostic d => d.Evidence,
            FrameworkReferenceAllowOnlyDiagnostic d => d.Evidence,
            _ => null,
        };

        return evidence?.FirstOrDefault()?.SourcePath;
    }

    // CEL expression participation (violation-reporting/sarif-diagnostics-output capability): added
    // alongside, never replacing, existing policy-origin related locations - a diagnostic can carry
    // both at once. A single violation can have multiple participating expressions (e.g. source.when
    // and forbidden[*].when), each appended as its own related location.
    private static IReadOnlyList<ExpressionParticipation>? GetWhenExpressions(ArchitectureDiagnostic diagnostic) => diagnostic switch
    {
        ContextDependencyDiagnostic d => d.WhenExpressions,
        ContextAllowOnlyDiagnostic d => d.WhenExpressions,
        LayoutConventionDiagnostic d => d.WhenExpressions,
        _ => null,
    };

    private static object[] AppendWhenExpressionRelatedLocations(
        object[] relatedPolicyLocations, IReadOnlyList<ExpressionParticipation>? whenExpressions)
    {
        if (whenExpressions == null || whenExpressions.Count == 0)
        {
            return relatedPolicyLocations;
        }

        object[] additional = whenExpressions.Select((whenExpression, index) =>
        {
            string result = whenExpression.Result switch
            {
                ExpressionParticipationResult.Matched => "matched",
                ExpressionParticipationResult.NotMatched => "did not match",
                _ => "failed to evaluate",
            };

            var entry = new Dictionary<string, object?>
            {
                ["id"] = relatedPolicyLocations.Length + index + 1,
                [MessagePropertyName] = new Dictionary<string, object?>
                {
                    ["text"] = $"CEL expression '{whenExpression.Source}' ({whenExpression.Location}) {result}" +
                        (whenExpression.YamlPath != null ? $" (at {whenExpression.YamlPath})" : string.Empty),
                },
            };
            if (whenExpression.PolicySourcePath != null)
            {
                entry[PhysicalLocationKey] = new Dictionary<string, object?>
                {
                    [ArtifactLocationKey] = new Dictionary<string, object?> { ["uri"] = whenExpression.PolicySourcePath },
                    ["region"] = new Dictionary<string, object?>
                    {
                        ["startLine"] = whenExpression.PolicySourceLine,
                        ["startColumn"] = whenExpression.PolicySourceColumn,
                    },
                };
            }
            return (object)entry;
        }).ToArray();

        return relatedPolicyLocations.Concat(additional).ToArray();
    }

    public static object[] FormatPolicyLocationsForSarif(
        ArchitecturePolicySourceLocation? primaryLocation,
        IEnumerable<ArchitecturePolicySourceLocation> relatedLocations)
    {
        IEnumerable<ArchitecturePolicySourceLocation> locations =
            primaryLocation is null
                ? relatedLocations
                : new[] { primaryLocation }.Concat(relatedLocations);

        return locations
            .Distinct()
            .OrderBy(location => location.SourceOrdinal)
            .ThenBy(location => location.EncounterOrdinal)
            .Select((location, index) => (object)new Dictionary<string, object?>
            {
                ["id"] = index + 1,
                [MessagePropertyName] = new Dictionary<string, object?>
                {
                    ["text"] = $"Policy {location.Role.ToString().ToLowerInvariant()} definition at {location.YamlPath}"
                },
                [PhysicalLocationKey] = new Dictionary<string, object?>
                {
                    [ArtifactLocationKey] = new Dictionary<string, object?> { ["uri"] = location.SourcePath },
                    ["region"] = new Dictionary<string, object?>
                    {
                        ["startLine"] = location.Line,
                        ["startColumn"] = location.Column
                    }
                }
            })
            .ToArray();
    }

    private static ResultEntry BuildCycleEntry(string cycle, string level)
    {
        Match match = CycleIdPrefixPattern().Match(cycle);
        string ruleId = match.Success ? match.Groups["id"].Value : CycleRuleFallback;
        string path = match.Success ? cycle[match.Length..] : cycle;
        ArchitectureFinding finding = ArchitectureFindingMapper.FromDiagnostic(
            new CycleDiagnostic(ruleId, match.Success ? ruleId : null, path),
            level == "error" ? "strict" : "audit");

        var json = new Dictionary<string, object?>
        {
            ["ruleId"] = ruleId,
            ["level"] = level,
            [MessagePropertyName] = new Dictionary<string, object?> { ["text"] = $"Dependency cycle detected: {path}" },
            ["logicalLocations"] = BuildLogicalLocations(path, "namespace"),
            [PropertiesKey] = new Dictionary<string, object?>
            {
                ["arch_linter_net"] = ArchitectureDiagnosticFormatter.FormatNormalizedFindingForSarif(finding),
            },
        };

        return new ResultEntry(ruleId, ruleId, path, "cycle", json);
    }

    private static ResultEntry BuildCycleEntry(CycleDiagnostic diagnostic, string level)
    {
        string ruleId = diagnostic.ContractId ?? CycleRuleFallback;
        ArchitectureFinding finding = ArchitectureFindingMapper.FromDiagnostic(
            diagnostic,
            level == "error" ? "strict" : "audit");

        var json = new Dictionary<string, object?>
        {
            ["ruleId"] = ruleId,
            ["level"] = level,
            [MessagePropertyName] = new Dictionary<string, object?> { ["text"] = $"Dependency cycle detected: {diagnostic.Path}" },
            ["logicalLocations"] = BuildLogicalLocations(diagnostic.Path, "namespace"),
            [PropertiesKey] = new Dictionary<string, object?>
            {
                ["arch_linter_net"] = ArchitectureDiagnosticFormatter.FormatNormalizedFindingForSarif(finding),
            },
        };

        object[] relatedPolicyLocations = FormatPolicyLocationsForSarif(
            diagnostic.PolicyLocation,
            diagnostic.RelatedPolicyLocations);
        if (relatedPolicyLocations.Length > 0)
        {
            json["relatedLocations"] = relatedPolicyLocations;
        }

        return new ResultEntry(ruleId, diagnostic.ContractName, diagnostic.Path, "cycle", json);
    }

    private static object[] BuildPhysicalLocations(string filePath, IReadOnlyCollection<string> references)
    {
        if (references.Count == 0)
        {
            return new object[]
            {
                new Dictionary<string, object?>
                {
                    [PhysicalLocationKey] = new Dictionary<string, object?>
                    {
                        [ArtifactLocationKey] = new Dictionary<string, object?> { ["uri"] = filePath },
                    },
                },
            };
        }

        return references.Select(reference =>
        {
            var physicalLocation = new Dictionary<string, object?>
            {
                [ArtifactLocationKey] = new Dictionary<string, object?> { ["uri"] = filePath },
            };

            Match match = MethodBodyLinePattern().Match(reference);
            if (match.Success && int.TryParse(match.Groups["line"].Value, out int line))
            {
                physicalLocation["region"] = new Dictionary<string, object?> { ["startLine"] = line };
            }

            return (object)new Dictionary<string, object?> { [PhysicalLocationKey] = physicalLocation };
        }).ToArray();
    }

    private static object[] BuildLogicalLocations(string fullyQualifiedName, string kind)
    {
        return new object[]
        {
            new Dictionary<string, object?>
            {
                ["fullyQualifiedName"] = fullyQualifiedName,
                ["kind"] = kind,
            },
        };
    }

    // Best-effort hint: no diagnostic kind carries an explicit "this identifier is a
    // namespace/type/package" flag, so the kind is inferred from the diagnostic's concrete subtype.
    // IL-scanned method-body violations are a special case: they map to the generic
    // DependencyDiagnostic subtype like namespace/layer violations do, but SourceType is a
    // type's fully-qualified name (see ArchitectureIlMethodBodyScanner), not a namespace.
    private static string LogicalLocationKindFor(ArchitectureDiagnostic diagnostic, string forbiddenNamespace)
    {
        if (forbiddenNamespace == MethodBodyIlCategory)
        {
            return "type";
        }

        return diagnostic switch
        {
            DependencyDiagnostic or ConfigurationDiagnostic => "namespace",
            PackageDependencyDiagnostic or PackageAllowOnlyDiagnostic => "package",
            FrameworkReferenceDiagnostic or FrameworkReferenceAllowOnlyDiagnostic => "framework-reference",
            _ => "type",
        };
    }

    private static (string SourceType, string ForbiddenNamespace, IReadOnlyCollection<string> References) ExtractFields(
        ArchitectureDiagnostic diagnostic) => diagnostic switch
        {
            DependencyDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            ConfigurationDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            ExternalDependencyDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            PackageDependencyDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            PackageAllowOnlyDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            FrameworkReferenceDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            FrameworkReferenceAllowOnlyDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            TypePlacementDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            LayoutConventionDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            PublicApiSurfaceDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            AttributeUsageDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            InheritanceDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            InterfaceImplementationDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            CompositionDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            ProjectMetadataDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            ContextDependencyDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            ContextAllowOnlyDiagnostic d => (d.SourceType, d.ForbiddenNamespace, d.ForbiddenReferences),
            _ => (string.Empty, string.Empty, Array.Empty<string>()),
        };

    private sealed record ResultEntry(
        string RuleId,
        string ContractName,
        string SourceIdentifier,
        string Category,
        Dictionary<string, object?> Json);

    // A single OrderBy(keySelector: identity, comparer) call with one comparer replicating the
    // former OrderBy(RuleId).ThenBy(SourceIdentifier).ThenBy(Category) chain keeps LINQ's
    // stable-sort guarantee (ties preserve source order, so sequential/non-cancelled output is
    // byte-for-byte unchanged) while making the whole sort interruptible: the token is observed
    // on every comparison, not just before/after the call.
    private sealed class ResultEntryOrderComparer : IComparer<ResultEntry>
    {
        private readonly CancellationToken _cancellationToken;

        internal ResultEntryOrderComparer(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public int Compare(ResultEntry? x, ResultEntry? y)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            int result = StringComparer.Ordinal.Compare(x!.RuleId, y!.RuleId);
            if (result != 0)
            {
                return result;
            }

            result = StringComparer.Ordinal.Compare(x.SourceIdentifier, y.SourceIdentifier);
            if (result != 0)
            {
                return result;
            }

            return StringComparer.Ordinal.Compare(x.Category, y.Category);
        }
    }
}
