using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SourceSetExpansionTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-source-sets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void ExplicitSources_ExpandIntoOneInstancePerSource()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Modules.Billing, Acme.Modules.Orders]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  id: modules-no-infrastructure
                  sources: [Acme.Modules.Orders, Acme.Modules.Billing]
                  forbidden: [infrastructure]
            """);

        List<ArchitecturePackageDependencyContract> contracts = document.Contracts.StrictPackageDependency;

        Assert.Multiple(() =>
        {
            Assert.That(contracts.Select(c => c.Source),
                Is.EqualTo(new[] { "Acme.Modules.Billing", "Acme.Modules.Orders" }));
            Assert.That(contracts.Select(c => c.Id), Is.EqualTo(new[]
            {
                "modules-no-infrastructure/acme-modules-billing",
                "modules-no-infrastructure/acme-modules-orders"
            }));
            Assert.That(contracts.Select(c => c.ExpansionOrigin!.AuthoredContractId),
                Is.All.EqualTo("modules-no-infrastructure"));
            Assert.That(contracts[0].Forbidden, Is.EqualTo(new[] { "infrastructure" }));
        });
    }

    [Test]
    public void ExpandedInstances_DoNotShareMutableIgnoredViolations()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Modules.Billing, Acme.Modules.Orders]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  id: modules-no-infrastructure
                  sources: [Acme.Modules.Orders, Acme.Modules.Billing]
                  forbidden: [infrastructure]
            """);

        List<ArchitecturePackageDependencyContract> contracts = document.Contracts.StrictPackageDependency;
        contracts[0].IgnoredViolations.Add(new ArchitectureIgnoredViolation
        {
            SourceType = "Acme.Modules.Billing",
            ForbiddenReference = "Acme.Infrastructure",
            Reason = "baseline entry for one resolved source only"
        });

        Assert.Multiple(() =>
        {
            Assert.That(contracts[0].IgnoredViolations, Has.Count.EqualTo(1));
            Assert.That(contracts[1].IgnoredViolations, Is.Empty);
        });
    }

    [Test]
    public void NamedAssemblySet_ResolvesGlobAgainstDeclaredTargetAssemblies()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Host, Acme.Modules.Billing, Acme.Modules.Orders]
            source_sets:
              module_assemblies:
                kind: assembly
                globs: ["Acme.Modules.*"]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  id: modules-no-infrastructure
                  source_sets: [module_assemblies]
                  forbidden: [infrastructure]
            """);

        ArchitectureContractExpansion expansion = document.SourceExpansion.Contracts.Single();

        Assert.Multiple(() =>
        {
            Assert.That(document.SourceExpansion.Sets.Single().ResolvedSources,
                Is.EqualTo(new[] { "Acme.Modules.Billing", "Acme.Modules.Orders" }));
            Assert.That(expansion.Instances.Select(i => i.Source),
                Is.EqualTo(new[] { "Acme.Modules.Billing", "Acme.Modules.Orders" }));
            Assert.That(expansion.Instances.Select(i => i.Selector), Is.All.EqualTo("Acme.Modules.*"));
            Assert.That(expansion.Instances.Select(i => i.SetName), Is.All.EqualTo("module_assemblies"));
            Assert.That(document.Contracts.StrictPackageDependency, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void NamedAssemblySet_ExpandsLargeFixtureDeterministically()
    {
        string assemblies = string.Join(", ", Enumerable.Range(1, 24).Select(i => $"Acme.Modules.M{i:D2}"));

        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{assemblies}]
            source_sets:
              module_assemblies:
                globs: ["Acme.Modules.*"]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  id: modules-no-infrastructure
                  source_sets: [module_assemblies]
                  forbidden: [infrastructure]
            """);

        List<string> sources = document.Contracts.StrictPackageDependency.Select(c => c.Source).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(sources, Has.Count.EqualTo(24));
            Assert.That(sources, Is.EqualTo(sources.OrderBy(source => source, StringComparer.Ordinal).ToList()));
            Assert.That(sources, Is.Unique);
        });
    }

    [Test]
    public void OverlappingSets_ProduceOneInstancePerSource()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Modules.Billing, Acme.Modules.Orders]
            source_sets:
              all_modules:
                globs: ["Acme.Modules.*"]
              billing_only:
                members: [Acme.Modules.Billing]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  id: modules-no-infrastructure
                  source_sets: [all_modules, billing_only]
                  forbidden: [infrastructure]
            """);

        Assert.That(document.Contracts.StrictPackageDependency.Select(c => c.Source),
            Is.EqualTo(new[] { "Acme.Modules.Billing", "Acme.Modules.Orders" }));
    }

    [Test]
    public void ZeroMatchGlob_FailsClosedWithActionableDiagnostic()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Host]
            source_sets:
              module_assemblies:
                globs: ["Acme.Modules.*"]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  source_sets: [module_assemblies]
                  forbidden: [infrastructure]
            """))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("module_assemblies"));
            Assert.That(exception.Message, Does.Contain("Acme.Modules.*"));
            Assert.That(exception.Message, Does.Contain("matches nothing"));
        });
    }

    [Test]
    public void OptionalEmptySet_IsAcceptedAndReported()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Host]
            source_sets:
              future_modules:
                globs: ["Acme.Modules.*"]
                optional: true
                reason: The module slice is planned but not extracted yet.
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  id: modules-no-infrastructure
                  source_sets: [future_modules]
                  forbidden: [infrastructure]
            """);

        ArchitectureContractExpansion expansion = document.SourceExpansion.Contracts.Single();

        Assert.Multiple(() =>
        {
            Assert.That(expansion.OptionalEmpty, Is.True);
            Assert.That(expansion.Instances, Is.Empty);
            Assert.That(expansion.OptionalReason, Does.Contain("planned"));
            Assert.That(document.Contracts.StrictPackageDependency, Is.Empty);
        });
    }

    [Test]
    public void OptionalSetWithoutReason_IsRejected()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Host]
            source_sets:
              future_modules:
                globs: ["Acme.Modules.*"]
                optional: true
            contracts:
              strict: []
            """))!;

        Assert.That(exception.Message, Does.Contain("without a 'reason'"));
    }

    [Test]
    public void UnknownSetReference_IsRejected()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Host]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  source_sets: [missing_set]
                  forbidden: [infrastructure]
            """))!;

        Assert.That(exception.Message, Does.Contain("unknown source set 'missing_set'"));
    }

    [Test]
    public void SetKindMismatch_IsRejected()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load($"""
            version: 1
            name: Test
            layers:
              domain:
                namespace: Acme.Domain
            analysis:
              target_assemblies: [Acme.Host]
            source_sets:
              domain_layers:
                kind: layer
                members: [domain]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  source_sets: [domain_layers]
                  forbidden: [infrastructure]
            """))!;

        Assert.That(exception.Message, Does.Contain("of kind 'layer'"));
    }

    [Test]
    public void DeclaringBothSourceAndSources_IsRejected()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Host, Acme.Modules.Orders]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  source: Acme.Host
                  sources: [Acme.Modules.Orders]
                  forbidden: [infrastructure]
            """))!;

        Assert.That(exception.Message, Does.Contain("declares both 'source' and 'sources'/'source_sets'"));
    }

    [Test]
    public void SetMemberOutsideDeclaredTargets_IsRejected()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Host]
            source_sets:
              module_assemblies:
                members: [Acme.Modules.Orders]
            contracts:
              strict: []
            """))!;

        Assert.That(exception.Message, Does.Contain("'analysis.target_assemblies'"));
    }

    [Test]
    public void ProjectSetWithGlobs_IsRejected()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Host]
              projects: [src/Acme.Host/Acme.Host.csproj]
            source_sets:
              host_projects:
                kind: project
                globs: ["src.*"]
            contracts:
              strict: []
            """))!;

        Assert.That(exception.Message, Does.Contain("accept explicit 'members' only"));
    }

    [Test]
    public void LayerSet_ExpandsExternalDependencyContract()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            layers:
              application:
                namespace: Acme.Application
              domain:
                namespace: Acme.Domain
            analysis:
              target_assemblies: [Acme.Host]
            source_sets:
              inner_layers:
                kind: layer
                members: [domain, application]
            external_dependencies:
              vendor:
                namespace_prefixes: [Vendor]
            contracts:
              strict_external:
                - name: inner layers avoid vendor
                  id: inner-no-vendor
                  source_sets: [inner_layers]
                  forbidden: [vendor]
            """);

        Assert.That(document.Contracts.StrictExternal.Select(c => c.Source),
            Is.EqualTo(new[] { "application", "domain" }));
    }

    [Test]
    public void CompositionAssemblySets_AreUnionedIntoAllowedOnlyInAssemblies()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Host.Api, Acme.Host.Worker]
            source_sets:
              hosts:
                globs: ["Acme.Host.*"]
            contracts:
              strict_composition:
                - name: composition roots only
                  id: composition-roots-only
                  forbidden_apis: [AddSingleton]
                  allowed_only_in_assembly_sets: [hosts]
            """);

        Assert.Multiple(() =>
        {
            Assert.That(document.Contracts.StrictComposition.Single().AllowedOnlyInAssemblies,
                Is.EqualTo(new[] { "Acme.Host.Api", "Acme.Host.Worker" }));
            Assert.That(document.Contracts.StrictComposition, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ProjectSets_AreUnionedIntoProjects()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Host]
              projects:
                - src/Acme.Host/Acme.Host.csproj
                - src/Acme.Modules.Orders/Acme.Modules.Orders.csproj
            source_sets:
              module_projects:
                kind: project
                members: [src/Acme.Modules.Orders/Acme.Modules.Orders.csproj]
            contracts:
              strict_project_metadata:
                - name: modules are packable
                  id: modules-packable
                  project_sets: [module_projects]
                  required_properties:
                    IsPackable: "true"
            """);

        Assert.That(document.Contracts.StrictProjectMetadata.Single().Projects,
            Is.EqualTo(new[] { "src/Acme.Modules.Orders/Acme.Modules.Orders.csproj" }));
    }

    [Test]
    public void ExactSourcePolicy_IsUnchangedAndProducesNoExpansion()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Host]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: host avoids infrastructure
                  id: host-no-infrastructure
                  source: Acme.Host
                  forbidden: [infrastructure]
            """);

        ArchitecturePackageDependencyContract contract = document.Contracts.StrictPackageDependency.Single();

        Assert.Multiple(() =>
        {
            Assert.That(contract.Id, Is.EqualTo("host-no-infrastructure"));
            Assert.That(contract.Source, Is.EqualTo("Acme.Host"));
            Assert.That(contract.ExpansionOrigin, Is.Null);
            Assert.That(document.SourceExpansion.IsEmpty, Is.True);
        });
    }

    [Test]
    public void ExpansionBeyondSupportedLimit_IsRejected()
    {
        string assemblies = string.Join(
            ", ",
            Enumerable.Range(1, ArchitectureSourceSetExpander.MaxInstancesPerContract + 1)
                .Select(i => $"Acme.Modules.M{i:D4}"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [{assemblies}]
            source_sets:
              module_assemblies:
                globs: ["Acme.Modules.*"]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  source_sets: [module_assemblies]
                  forbidden: [infrastructure]
            """))!;

        Assert.That(exception.Message, Does.Contain("exceeds the supported limit"));
    }

    [Test]
    public void ImportedExpandedContract_KeepsAuthoredFragmentLocation()
    {
        Write("architecture/parts/modules.yml", """
            source_sets:
              module_assemblies:
                globs: ["Acme.Modules.*"]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  id: modules-no-infrastructure
                  source_sets: [module_assemblies]
                  forbidden: [infrastructure]
            """);

        string root = Write("architecture/root.yml", """
            version: 1
            name: Test
            imports:
              - parts/modules.yml
            layers:
              domain:
                namespace: Acme.Domain
            analysis:
              target_assemblies: [Acme.Modules.Billing, Acme.Modules.Orders]
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);
        ArchitectureContractExpansion expansion = document.SourceExpansion.Contracts.Single();

        Assert.Multiple(() =>
        {
            Assert.That(expansion.PolicyLocation!.SourcePath, Is.EqualTo("architecture/parts/modules.yml"));
            Assert.That(document.SourceExpansion.Sets.Single().PolicyLocation!.SourcePath,
                Is.EqualTo("architecture/parts/modules.yml"));
            Assert.That(document.Provenance.LocationFor(document.Contracts.StrictPackageDependency[0])!.SourcePath,
                Is.EqualTo("architecture/parts/modules.yml"));
        });
    }

    private ArchitectureContractDocument Load(string yaml)
    {
        string path = Write("dependencies.arch.yml", yaml);
        return new ArchitecturePolicyDocumentLoader().Load(path);
    }

    private string Write(string relativePath, string content)
    {
        string path = Path.Combine(_temporaryDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
