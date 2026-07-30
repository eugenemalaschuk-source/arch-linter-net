using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class LayoutConventionContractTests
{
    [Test]
    public void CheckLayoutConventionsContract_ExcludeWhen_SubtractsMatchedDeclaredTypes()
    {
        string assemblyName = typeof(LayoutConventionContractTests).Assembly.GetName().Name!;
        string policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, $"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{assemblyName}]
              source_roots: ["."]
            contracts:
              strict_layout_conventions:
                - name: exclusion-when
                  files_matching:
                    folder_segment: WhenRefinement
                  exclude_files_matching:
                    - folder_segment: WhenRefinement
                      when: subject.simpleName == "ExcludedByWhen"
                  required_name_suffix: DoesNotMatchAnything
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        var contract = document.Contracts.StrictLayoutConventions[0];
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v => v.SourceType.Contains("IncludedByWhen", StringComparison.Ordinal)), Is.True);
        Assert.That(violations.Any(v => v.SourceType.Contains("ExcludedByWhen", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void CheckLayoutConventionsContract_AmbiguousPartialType_ExcludeMatcherSuppressesViolation()
    {
        string assemblyName = typeof(LayoutConventionContractTests).Assembly.GetName().Name!;
        string policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, $"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{assemblyName}]
              source_roots: ["."]
            contracts:
              strict_layout_conventions:
                - name: services-must-not-contain-offenders
                  files_matching:
                    folder_segment: Services
                    when: subject.simpleName == "PartialOffender"
                  exclude_files_matching:
                    - folder_segment: Services
                      when: subject.simpleName == "PartialOffender"
                  forbid_type_kind: class
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        var contract = document.Contracts.StrictLayoutConventions[0];
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v => v.SourceType.Contains("PartialOffender", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void CheckLayoutConventionsContract_ExcludeWhenReferencesSourcePaths_PartialEnrichment_UnfiledCandidateIsViolation()
    {
        string assemblyName = typeof(LayoutConventionContractTests).Assembly.GetName().Name!;
        string policyPath = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, $"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{assemblyName}]
              source_roots: ["."]
            contracts:
              strict_layout_conventions:
                - name: path-based-exclusion
                  files_matching:
                    namespace_segment: UnfiledNamespace
                  exclude_files_matching:
                    - namespace_segment: UnfiledNamespace
                      when: subject.sourcePaths.size() > 0
                  forbid_type_kind: interface
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        var contract = document.Contracts.StrictLayoutConventions[0];
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckLayoutConventionsContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType.Contains("NoSourceFileType", StringComparison.Ordinal)
            && v.Payload is LayoutConventionPayload { DataUnavailable: true }), Is.True);
    }
}
