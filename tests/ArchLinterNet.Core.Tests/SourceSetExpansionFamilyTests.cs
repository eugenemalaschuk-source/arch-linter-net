using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SourceSetExpansionFamilyTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-source-set-families-{Guid.NewGuid():N}");
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
    public void PackageAllowOnly_ExpandsPerResolvedSource()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Modules.Billing, Acme.Modules.Orders]
            source_sets:
              modules:
                globs: ["Acme.Modules.*"]
            packages:
              approved:
                package_ids: [Acme.Approved]
            contracts:
              strict_package_allow_only:
                - name: modules use approved packages only
                  id: modules-approved-packages
                  source_sets: [modules]
                  allowed: [approved]
            """);

        List<ArchitecturePackageAllowOnlyContract> contracts = document.Contracts.StrictPackageAllowOnly;

        Assert.Multiple(() =>
        {
            Assert.That(contracts.Select(c => c.Source),
                Is.EqualTo(new[] { "Acme.Modules.Billing", "Acme.Modules.Orders" }));
            Assert.That(contracts[0].Allowed, Is.EqualTo(new[] { "approved" }));
            Assert.That(contracts[0].Id, Is.EqualTo("modules-approved-packages/acme-modules-billing"));
        });
    }

    [Test]
    public void FrameworkDependencyAndAllowOnly_ExpandPerResolvedSource()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Modules.Billing, Acme.Modules.Orders]
            source_sets:
              modules:
                globs: ["Acme.Modules.*"]
            framework_references:
              web:
                framework_names: [Microsoft.AspNetCore.App]
              approved:
                framework_names: [Microsoft.NETCore.App]
            contracts:
              strict_framework_dependency:
                - name: modules avoid web framework
                  id: modules-no-web
                  source_sets: [modules]
                  forbidden: [web]
              audit_framework_allow_only:
                - name: modules use approved frameworks only
                  id: modules-approved-frameworks
                  source_sets: [modules]
                  allowed: [approved]
            """);

        Assert.Multiple(() =>
        {
            Assert.That(document.Contracts.StrictFrameworkDependency.Select(c => c.Source),
                Is.EqualTo(new[] { "Acme.Modules.Billing", "Acme.Modules.Orders" }));
            Assert.That(document.Contracts.StrictFrameworkDependency[0].Forbidden, Is.EqualTo(new[] { "web" }));
            Assert.That(document.Contracts.AuditFrameworkAllowOnly.Select(c => c.Id), Is.EqualTo(new[]
            {
                "modules-approved-frameworks/acme-modules-billing",
                "modules-approved-frameworks/acme-modules-orders"
            }));
            Assert.That(document.Contracts.AuditFrameworkAllowOnly[0].Allowed, Is.EqualTo(new[] { "approved" }));
        });
    }

    [Test]
    public void ExternalAllowOnly_ExpandsPerResolvedLayerAndCopiesAllowedTypes()
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
              approved:
                namespace_prefixes: [Approved]
            contracts:
              strict_external_allow_only:
                - name: inner layers use approved vendors only
                  id: inner-approved-vendors
                  source_sets: [inner_layers]
                  allowed: [approved]
                  allowed_types: [Approved.Marker]
            """);

        List<ArchitectureExternalAllowOnlyContract> contracts = document.Contracts.StrictExternalAllowOnly;

        Assert.Multiple(() =>
        {
            Assert.That(contracts.Select(c => c.Source), Is.EqualTo(new[] { "application", "domain" }));
            Assert.That(contracts[0].AllowedTypes, Is.EqualTo(new[] { "Approved.Marker" }));
            Assert.That(contracts[0].Id, Is.EqualTo("inner-approved-vendors/application"));
        });
    }

    [Test]
    public void ContractWithoutDeclaredId_DerivesInstanceIdsFromItsName()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Modules.Orders]
            source_sets:
              modules:
                members: [Acme.Modules.Orders]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  source_sets: [modules]
                  forbidden: [infrastructure]
            """);

        Assert.That(document.Contracts.StrictPackageDependency.Single().Id,
            Is.EqualTo("modules-avoid-infrastructure/acme-modules-orders"));
    }

    [Test]
    public void ExplicitSourceOutsideDeclaredTargets_IsRejected()
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
                  sources: [Acme.Modules.Orders]
                  forbidden: [infrastructure]
            """))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("lists source 'Acme.Modules.Orders' in 'sources'"));
            Assert.That(exception.Message, Does.Contain("'analysis.target_assemblies'"));
        });
    }

    [Test]
    public void SetWithNeitherMembersNorGlobs_IsRejected()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Host]
            source_sets:
              modules:
                reason: nothing declared
            contracts:
              strict: []
            """))!;

        Assert.That(exception.Message, Does.Contain("declares neither 'members' nor 'globs'"));
    }

    [Test]
    public void OptionalSetResolvingToNothing_LeavesReferencingListFieldsUnchanged()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Host]
            source_sets:
              future_hosts:
                globs: ["Acme.Future.*"]
                optional: true
                reason: The additional hosts are planned.
            contracts:
              strict_composition:
                - name: composition roots only
                  id: composition-roots-only
                  forbidden_apis: [AddSingleton]
                  allowed_only_in_assemblies: [Acme.Host]
                  allowed_only_in_assembly_sets: [future_hosts]
            """);

        Assert.That(document.Contracts.StrictComposition.Single().AllowedOnlyInAssemblies,
            Is.EqualTo(new[] { "Acme.Host" }));
    }

    [Test]
    public void UnknownSetReferencedByListShapedField_IsRejected()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Host]
            contracts:
              strict_composition:
                - name: composition roots only
                  forbidden_apis: [AddSingleton]
                  allowed_only_in_assembly_sets: [missing_set]
            """))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("unknown source set 'missing_set'"));
            Assert.That(exception.Message, Does.Contain("allowed_only_in_assembly_sets"));
        });
    }

    [Test]
    public void DuplicateAuthoredContractIds_AreStillRejectedAfterExpansion()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Modules.Billing, Acme.Modules.Orders]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: billing avoids infrastructure
                  id: modules-no-infrastructure
                  sources: [Acme.Modules.Billing]
                  forbidden: [infrastructure]
                - name: orders avoids infrastructure
                  id: modules-no-infrastructure
                  sources: [Acme.Modules.Orders]
                  forbidden: [infrastructure]
            """))!;

        Assert.That(exception.Message,
            Does.Contain("Duplicate contract IDs found: modules-no-infrastructure"));
    }

    [Test]
    public void DuplicateAuthoredIdAcrossExpandedAndExactContracts_IsRejected()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Modules.Billing, Acme.Modules.Orders]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: billing avoids infrastructure
                  id: modules-no-infrastructure
                  source: Acme.Modules.Billing
                  forbidden: [infrastructure]
                - name: orders avoids infrastructure
                  id: modules-no-infrastructure
                  sources: [Acme.Modules.Orders]
                  forbidden: [infrastructure]
            """))!;

        Assert.That(exception.Message,
            Does.Contain("Duplicate contract IDs found: modules-no-infrastructure"));
    }

    [Test]
    public void DistinctAuthoredIds_ExpandWithoutDuplicateDiagnostics()
    {
        ArchitectureContractDocument document = Load($"""
            version: 1
            name: Test
            analysis:
              target_assemblies: [Acme.Modules.Billing, Acme.Modules.Orders]
            source_sets:
              modules:
                globs: ["Acme.Modules.*"]
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  id: modules-no-infrastructure
                  source_sets: [modules]
                  forbidden: [infrastructure]
                - name: modules avoid legacy infrastructure
                  id: modules-no-legacy-infrastructure
                  source_sets: [modules]
                  forbidden: [infrastructure]
            """);

        Assert.That(document.Contracts.StrictPackageDependency.Select(c => c.Id), Is.EqualTo(new[]
        {
            "modules-no-infrastructure/acme-modules-billing",
            "modules-no-infrastructure/acme-modules-orders",
            "modules-no-legacy-infrastructure/acme-modules-billing",
            "modules-no-legacy-infrastructure/acme-modules-orders"
        }));
    }

    [Test]
    public void ProjectMetadataContractWithOnlyProjectSets_PassesComposedPolicySchemaValidation()
    {
        Write("architecture/parts/projects.yml", """
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

        string root = Write("architecture/root.yml", """
            version: 1
            name: Test
            imports:
              - parts/projects.yml
            layers:
              domain:
                namespace: Acme.Domain
            analysis:
              target_assemblies: [Acme.Host]
              projects:
                - src/Acme.Modules.Orders/Acme.Modules.Orders.csproj
            """);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(root);

        Assert.That(document.Contracts.StrictProjectMetadata.Single().Projects,
            Is.EqualTo(new[] { "src/Acme.Modules.Orders/Acme.Modules.Orders.csproj" }));
    }

    [Test]
    public void ProjectMetadataContractWithNeitherProjectsNorSets_IsRejectedBySchema()
    {
        Write("architecture/parts/projects.yml", """
            contracts:
              strict_project_metadata:
                - name: modules are packable
                  id: modules-packable
                  required_properties:
                    IsPackable: "true"
            """);

        string root = Write("architecture/root.yml", """
            version: 1
            name: Test
            imports:
              - parts/projects.yml
            layers:
              domain:
                namespace: Acme.Domain
            analysis:
              target_assemblies: [Acme.Host]
            """);

        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.That(exception.Message, Does.Contain("does not satisfy the effective policy schema"));
    }

    [Test]
    public void ImportedZeroMatchSet_ReportsAuthoredFragmentLocation()
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
              target_assemblies: [Acme.Host]
            """);

        ArchitecturePolicyValidationException exception = Assert.Throws<ArchitecturePolicyValidationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("matches nothing"));
            Assert.That(exception.Message, Does.Contain("architecture/parts/modules.yml"));
            Assert.That(exception.Diagnostic.Location!.SourcePath, Is.EqualTo("architecture/parts/modules.yml"));
            Assert.That(exception.Diagnostic.Location.YamlPath, Does.Contain("source_sets"));
        });
    }

    [Test]
    public void ImportedUnknownSetReference_ReportsAuthoredContractLocation()
    {
        Write("architecture/parts/modules.yml", """
            packages:
              infrastructure:
                package_ids: [Acme.Infrastructure]
            contracts:
              strict_package_dependency:
                - name: modules avoid infrastructure
                  id: modules-no-infrastructure
                  source_sets: [missing_set]
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
              target_assemblies: [Acme.Host]
            """);

        ArchitecturePolicyValidationException exception = Assert.Throws<ArchitecturePolicyValidationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("unknown source set 'missing_set'"));
            Assert.That(exception.Diagnostic.Location!.SourcePath, Is.EqualTo("architecture/parts/modules.yml"));
            Assert.That(exception.Diagnostic.Location.YamlPath,
                Does.Contain("contracts.strict_package_dependency[0]"));
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
