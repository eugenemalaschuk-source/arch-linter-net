using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class SourceExpansionInclusionProvenanceTests
{
    [Test]
    public void SourceExpansion_InclusionsPreserveEveryAuthoredSetReferenceIncludingOptionalEmpty()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-inclusion-provenance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "dependencies.arch.yml");
            File.WriteAllText(path, """
                version: 1
                name: Test
                layers:
                  application:
                    namespace: Acme.Application
                source_sets:
                  first_application:
                    kind: layer
                    members: [application]
                  repeated_application:
                    kind: layer
                    members: [application]
                  future_layers:
                    kind: layer
                    globs: [future.*]
                    optional: true
                    reason: Reserved for a future module.
                external_dependencies:
                  vendor:
                    namespace_prefixes: [Vendor]
                contracts:
                  strict_external:
                    - name: application avoids vendor
                      id: application-no-vendor
                      source_sets: [first_application, repeated_application, future_layers]
                      forbidden: [vendor]
                """);

            ArchitectureContractExpansion expansion = new ArchitecturePolicyDocumentLoader().Load(path)
                .SourceExpansion.Contracts.Single();

            Assert.Multiple(() =>
            {
                Assert.That(expansion.Instances.Select(instance => instance.Source), Is.EqualTo(new[] { "application" }));
                Assert.That(expansion.Inclusions.Where(instance => instance.Source == "application").Select(instance => instance.SetName),
                    Is.EqualTo(new[] { "first_application", "repeated_application" }));
                Assert.That(expansion.Inclusions.Where(instance => instance.Source == "application")
                        .Select(instance => instance.SourceSetReferencePolicyLocation!.YamlPath),
                    Is.EqualTo(new[] { "contracts.strict_external[0]/source_sets/0", "contracts.strict_external[0]/source_sets/1" }));

                ArchitectureExpandedContractInstance optional = expansion.Inclusions.Single(instance => instance.OptionalEmpty);
                Assert.That(optional.SetName, Is.EqualTo("future_layers"));
                Assert.That(optional.Source, Is.Null);
                Assert.That(optional.OptionalReason, Does.Contain("future module"));
                Assert.That(optional.SourceSetReferencePolicyLocation!.YamlPath,
                    Is.EqualTo("contracts.strict_external[0]/source_sets/2"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
