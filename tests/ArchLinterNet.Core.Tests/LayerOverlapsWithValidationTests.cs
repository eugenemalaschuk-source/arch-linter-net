using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Validators;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class LayerOverlapsWithValidationTests
{
    private static readonly string[] _value = { "audit_aspect" };
    [Test]
    public void LayerValidation_OverlapsWithUndeclaredLayer_IsRejected()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => new LayerNamespacesValidator().Validate(
            new ArchitectureContractDocument
            {
                Layers = new Dictionary<string, ArchitectureLayer>
                {
                    ["domain"] = new()
                    {
                        Namespace = "MyApp.Domain",
                        OverlapsWith = new List<string> { "nonexistent" }
                    }
                }
            }))!;

        Assert.That(ex.Message, Does.Contain("domain"));
        Assert.That(ex.Message, Does.Contain("nonexistent"));
    }

    [Test]
    public void LayerValidation_OverlapsWithEmptyEntry_IsRejected()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => new LayerNamespacesValidator().Validate(
            new ArchitectureContractDocument
            {
                Layers = new Dictionary<string, ArchitectureLayer>
                {
                    ["domain"] = new()
                    {
                        Namespace = "MyApp.Domain",
                        OverlapsWith = new List<string> { "  " }
                    }
                }
            }))!;

        Assert.That(ex.Message, Does.Contain("domain"));
        Assert.That(ex.Message, Does.Contain("non-empty"));
    }

    [Test]
    public void LayerValidation_OverlapsWithSelf_IsRejected()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => new LayerNamespacesValidator().Validate(
            new ArchitectureContractDocument
            {
                Layers = new Dictionary<string, ArchitectureLayer>
                {
                    ["domain"] = new()
                    {
                        Namespace = "MyApp.Domain",
                        OverlapsWith = new List<string> { "domain" }
                    }
                }
            }))!;

        Assert.That(ex.Message, Does.Contain("domain"));
        Assert.That(ex.Message, Does.Contain("not reference itself"));
    }

    [Test]
    public void LayerValidation_ValidOverlapsWith_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => new LayerNamespacesValidator().Validate(
            new ArchitectureContractDocument
            {
                Layers = new Dictionary<string, ArchitectureLayer>
                {
                    ["domain"] = new()
                    {
                        Namespace = "MyApp.Domain",
                        OverlapsWith = new List<string> { "cross_cutting" }
                    },
                    ["cross_cutting"] = new()
                    {
                        Namespace = "MyApp.CrossCutting"
                    }
                }
            }));
    }

    [Test]
    public void LayerValidation_OverlapsWith_LoadsFromRealYaml()
    {
        // Regression: the raw pre-deserialization YAML pass (ValidateLayerNodeKeys) has its own
        // hand-maintained key whitelist independent of the C# model, so a field added only to
        // ArchitectureLayer without also updating that whitelist would make every real policy
        // authoring overlaps_with fail to load with "contains unknown property 'overlaps_with'"
        // even though the model and LayerNamespacesValidator both support it.
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-overlaps-with-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string policyPath = Path.Combine(tempDir, "dependencies.arch.yml");
            File.WriteAllText(policyPath, """
                version: 1
                name: Overlaps With E2E
                layers:
                  sales_domain:
                    namespace: Test.Domain
                    overlaps_with: [audit_aspect]
                  audit_aspect:
                    namespace: Test.Domain
                analysis:
                  target_assemblies: []
                contracts:
                  strict: []
                """);

            ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);

            Assert.That(document.Layers["sales_domain"].OverlapsWith, Is.EquivalentTo(_value));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [TestCase("overlaps_with: audit_aspect", "must be a list of layer names")]
    [TestCase("overlaps_with:\n      role: DomainLayer", "must be a list of layer names")]
    [TestCase("overlaps_with:\n      - role: DomainLayer", "must be non-empty layer name strings")]
    [TestCase("overlaps_with:\n      - \"\"", "must be non-empty layer name strings")]
    public void LayerValidation_OverlapsWithMalformedRawShape_IsRejected(string overlapsWithYaml, string expectedMessageFragment)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-overlaps-with-shape-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string policyPath = Path.Combine(tempDir, "dependencies.arch.yml");
            File.WriteAllText(policyPath, $"""
                version: 1
                name: Overlaps With Shape
                layers:
                  sales_domain:
                    namespace: Test.Domain
                    {overlapsWithYaml}
                  audit_aspect:
                    namespace: Test.Domain
                analysis:
                  target_assemblies: []
                contracts:
                  strict: []
                """);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

            Assert.That(ex.Message, Does.Contain(expectedMessageFragment));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
