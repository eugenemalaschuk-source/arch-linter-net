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
