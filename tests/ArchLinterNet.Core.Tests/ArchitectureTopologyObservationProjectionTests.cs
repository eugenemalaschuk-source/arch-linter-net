using System.Reflection;
using System.Runtime.Loader;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureTopologyObservationProjectionTests
{
    [Test]
    public void Observers_PreserveSharedFactsWhileRetainingTheirDistinctIdentityContracts()
    {
        Assembly assembly = typeof(ArchitectureMetricMeasurement).Assembly;
        using var context = new ArchitectureAnalysisContext(
            Path.GetTempPath(),
            [assembly],
            Array.Empty<string>(),
            Array.Empty<string>());
        var session = new ArchitectureAnalysisSession(
            context,
            new ArchitectureContractDocument(),
            null,
            false,
            null);

        ArchitectureTopologyObservation validation = ArchitectureTopologyValidationObserver.Observe(session, "namespace");
        ArchitectureTopologyObservation metric = ArchitectureTopologyMetricObserver.Observe(session, "namespace");

        Assert.Multiple(() =>
        {
            Assert.That(
                validation.Subjects.Select(subject => $"{subject.Assembly}|{subject.Subject}"),
                Is.EqualTo(metric.Subjects.Select(subject => $"{subject.Assembly}|{subject.Subject}")));
            Assert.That(
                validation.Dependencies.Select(dependency => dependency.Witness),
                Is.EqualTo(metric.Dependencies.Select(dependency => dependency.Witness)));
            Assert.That(
                validation.Subjects.All(subject =>
                    !subject.Identity.Contains("canonical_assembly=", StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                metric.Subjects.All(subject =>
                    subject.Identity.Contains("canonical_assembly=", StringComparison.Ordinal)),
                Is.True);
        });
    }

    [Test]
    public void ValidationObserver_IncompleteTypeUniverse_FailsInsteadOfReturningPartialSubjects()
    {
        string root = Path.Combine(Path.GetTempPath(), $"arch-topology-incomplete-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string dependencyPath = Path.Combine(root, "Fixture.Dependency.dll");
        string targetPath = Path.Combine(root, "Fixture.Target.dll");
        CompileAssembly(
            dependencyPath,
            "Fixture.Dependency",
            "namespace Fixture.Dependency; public interface IExternalContract { }");
        CompileAssembly(
            targetPath,
            "Fixture.Target",
            "namespace Fixture.Target; " +
            "public sealed class LoadableType { } " +
            "public sealed class UnloadableType : Fixture.Dependency.IExternalContract { }",
            dependencyPath);

        var loadContext = new AssemblyLoadContext($"topology-incomplete-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            Assembly targetAssembly;
            using (FileStream stream = File.OpenRead(targetPath))
            {
                targetAssembly = loadContext.LoadFromStream(stream);
            }

            using var context = new ArchitectureAnalysisContext(
                root,
                [targetAssembly],
                Array.Empty<string>(),
                Array.Empty<string>());
            var session = new ArchitectureAnalysisSession(
                context,
                new ArchitectureContractDocument(),
                null,
                false,
                null);

            Assert.That(session.TypeIndex.HasCompleteTypeUniverse, Is.False);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ArchitectureTopologyValidationObserver.Observe(session, "type"))!;
            Assert.That(exception.Message, Does.Contain("complete type universe"));
        }
        finally
        {
            loadContext.Unload();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void CompileAssembly(
        string outputPath,
        string assemblyName,
        string source,
        params string[] references)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        List<MetadataReference> metadataReferences =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        ];
        metadataReferences.AddRange(references.Select(reference => MetadataReference.CreateFromFile(reference)));
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            metadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using FileStream stream = File.Create(outputPath);
        Microsoft.CodeAnalysis.Emit.EmitResult emitted = compilation.Emit(stream);
        if (!emitted.Success)
        {
            throw new InvalidOperationException(
                $"Could not compile topology fixture '{assemblyName}': {string.Join(Environment.NewLine, emitted.Diagnostics)}");
        }
    }
}
