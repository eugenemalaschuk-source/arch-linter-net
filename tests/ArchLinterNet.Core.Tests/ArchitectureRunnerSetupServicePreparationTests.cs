using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Discovery.Abstractions;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Resolution.Abstractions;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Covers the metadata-only preparation/materialization pair added for lazy cache preparation:
// ArchitectureRunnerSetupService.PrepareRunner (target selection + the real PE/PDB reference
// closure walk in BuildMetadataReferenceClosure + digest capture in CaptureArtifactDigests) and
// MaterializePreparedRunner (VerifyPreparedArtifacts's "did the bytes change since prepare" guard,
// then real assembly resolution). These are exercised against real files on disk — including real
// compiled assemblies via Roslyn — rather than mocked, because the whole point of this feature is
// that it reads real PE/PDB metadata and hashes real bytes.
[TestFixture]
public sealed class ArchitectureRunnerSetupServicePreparationTests
{
    private static readonly string[] _value = { "Missing" };
    private sealed class FixedDiscoveryService : IArchitectureProjectDiscoveryService
    {
        public ProjectDiscoveryResult Result { get; set; } = ProjectDiscoveryResult.Empty;

        public ProjectDiscoveryResult ResolveAndApply(
            ArchitectureContractDocument document, string repositoryRoot, bool resolveAssemblyOutputs,
            CancellationToken cancellationToken = default) => Result;
    }

    private string _repoRoot = null!;
    private string _policyPath = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-preparation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_repoRoot);
        _policyPath = Path.Combine(_repoRoot, "policy.arch.yml");
        File.WriteAllText(_policyPath, "version: 1\nname: test\n");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repoRoot))
        {
            Directory.Delete(_repoRoot, recursive: true);
        }
    }

    private static ArchitectureRunnerSetupService CreateService(IArchitectureProjectDiscoveryService discovery) =>
        new(
            new ArchitecturePolicyDocumentLoader(),
            new ArchitectureBaselineLoadingService(),
            new ArchitectureRepositoryRootResolver(),
            new ConditionSetResolutionService(),
            discovery,
            new ArchitectureAssemblyResolutionService());

    // Compiles a tiny, genuinely valid assembly (with a real PDB) to disk via Roslyn so
    // PrepareRunner's PEReader-based reference walk has real metadata to read, not a hand-rolled
    // fake PE. `extraReferences` lets a test control exactly which other assembly names show up in
    // this assembly's AssemblyRef table. Roslyn omits a reference from the emitted AssemblyRef
    // table entirely when nothing in the compiled source actually uses it, so `source` must
    // reference a real member of each extra reference for that reference to survive into metadata
    // (see the default, which merely declares an unused empty marker type).
    private static void CompileRealAssembly(
        string outputDllPath, string assemblyName,
        string? source = null,
        params Microsoft.CodeAnalysis.MetadataReference[] extraReferences)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source ?? $$"""public class {{assemblyName}}Marker { }""");
        var references = new List<Microsoft.CodeAnalysis.MetadataReference>
        {
            Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        };
        references.AddRange(extraReferences);

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));

        using (FileStream dllStream = File.Create(outputDllPath))
        using (FileStream pdbStream = File.Create(Path.ChangeExtension(outputDllPath, ".pdb")))
        {
            Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(dllStream, pdbStream);
            if (!result.Success)
            {
                throw new InvalidOperationException(
                    "Fixture assembly failed to compile: " +
                    string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString())));
            }
        }
    }

    [Test]
    public void PrepareRunner_TargetAssemblyNotDiscovered_RecordsMissingAndVacuouslyIncompleteClosure()
    {
        // No PE reading happens at all here — the target name never resolves to a path, so the
        // closure walk starts from zero roots. Per BuildMetadataReferenceClosure's own contract,
        // an empty root set is never "complete" merely because its (nonexistent) walk found no
        // problems.
        var discovery = new FixedDiscoveryService();
        var service = CreateService(discovery);
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "DoesNotExist" },
            },
        };

        ArchitectureRunnerPreparation preparation = service.PrepareRunner(document, _policyPath);

        Assert.Multiple(() =>
        {
            Assert.That(preparation.SelectedAssemblyArtifactPaths, Is.Empty);
            Assert.That(preparation.MissingAssemblyNames, Does.Contain("DoesNotExist"));
            Assert.That(preparation.HasCompleteRootSelection, Is.False);
            Assert.That(preparation.IsMetadataReferenceClosureComplete, Is.False);
            Assert.That(preparation.HasCompleteArtifactSelection, Is.False);
        });
    }

    [Test]
    public void PrepareRunner_CorruptPeFile_CatchesBadImageFormatAndMarksClosureIncomplete()
    {
        // The target itself resolves to a real path (so root selection is complete) but the bytes
        // at that path are not a PE file at all. BuildMetadataReferenceClosure's PEReader.GetMetadataReader
        // call must fail with BadImageFormatException, be caught, and mark the closure incomplete
        // rather than letting the exception escape PrepareRunner.
        string corruptPath = Path.Combine(_repoRoot, "Corrupt.dll");
        File.WriteAllBytes(corruptPath, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        var discovery = new FixedDiscoveryService
        {
            Result = ProjectDiscoveryResult.Empty with
            {
                ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Corrupt"] = corruptPath,
                },
            },
        };
        var service = CreateService(discovery);
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "Corrupt" },
            },
        };

        ArchitectureRunnerPreparation preparation = service.PrepareRunner(document, _policyPath);

        Assert.Multiple(() =>
        {
            Assert.That(preparation.HasCompleteRootSelection, Is.True, "the file exists, so it is a resolved root");
            Assert.That(preparation.IsMetadataReferenceClosureComplete, Is.False, "corrupt PE bytes must not be read as complete");
            Assert.That(preparation.HasCompleteArtifactSelection, Is.False);
            Assert.That(preparation.SelectedAssemblyArtifactPaths, Is.EqualTo(new[] { corruptPath }));
        });
    }

    [Test]
    public void PrepareRunner_RealReferenceClosureFullyResolved_IsCompleteAndCapturesDigestsIncludingMissingReceipts()
    {
        // A real compiled root assembly references a real compiled "Leaf" assembly and (implicitly,
        // via `object`) the BCL. Registering the BCL's own on-disk assembly as a
        // TRUSTED_PLATFORM_ASSEMBLIES candidate exercises that branch for real, exactly as the
        // .NET host normally populates it; Leaf is supplied via ordinary discovered-assembly
        // candidates. With every reference resolvable, the closure must be reported complete.
        string leafPath = Path.Combine(_repoRoot, "Leaf.dll");
        string rootPath = Path.Combine(_repoRoot, "Root.dll");
        CompileRealAssembly(leafPath, "Leaf", source: "public class Leaf { }");
        CompileRealAssembly(
            rootPath, "Root",
            source: "public class RootMarker : Leaf { }",
            Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(leafPath));

        object? originalTrustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        try
        {
            AppContext.SetData("TRUSTED_PLATFORM_ASSEMBLIES", typeof(object).Assembly.Location);

            var discovery = new FixedDiscoveryService
            {
                Result = ProjectDiscoveryResult.Empty with
                {
                    ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["Root"] = rootPath,
                        ["Leaf"] = leafPath,
                    },
                },
            };
            var service = CreateService(discovery);
            var document = new ArchitectureContractDocument
            {
                Version = 1,
                Name = "Test",
                Analysis = new ArchitectureAnalysisConfiguration
                {
                    TargetAssemblies = new List<string> { "Root" },
                },
            };

            ArchitectureRunnerPreparation preparation = service.PrepareRunner(document, _policyPath);

            Assert.Multiple(() =>
            {
                Assert.That(preparation.MissingAssemblyNames, Is.Empty);
                Assert.That(preparation.IsMetadataReferenceClosureComplete, Is.True);
                Assert.That(preparation.HasCompleteArtifactSelection, Is.True);

                // CaptureArtifactDigests hashes each closure member's dll, pdb, and build receipt.
                // The pdb exists (real digest); the build receipt never does in this fixture, so
                // its sentinel value proves the "missing" branch runs for real.
                string rootReceiptPath = BuildReceiptStore.ReceiptPathFor(rootPath);
                Assert.That(preparation.CapturedArtifactContentDigests[rootPath],
                    Is.EqualTo(BuildStateCanonicalHasher.ComputeContentDigest(rootPath)));
                Assert.That(preparation.CapturedArtifactContentDigests[Path.ChangeExtension(rootPath, ".pdb")],
                    Is.EqualTo(BuildStateCanonicalHasher.ComputeContentDigest(Path.ChangeExtension(rootPath, ".pdb"))));
                Assert.That(preparation.CapturedArtifactContentDigests[rootReceiptPath], Is.EqualTo("missing"));
                Assert.That(preparation.CapturedArtifactContentDigests, Contains.Key(leafPath));
            });
        }
        finally
        {
            AppContext.SetData("TRUSTED_PLATFORM_ASSEMBLIES", originalTrustedPlatformAssemblies);
        }
    }

    [Test]
    public void PrepareRunner_ReferenceNameAbsentFromCandidates_IsIncompleteClosure()
    {
        // Root references the BCL, but neither the discovery candidates nor
        // TRUSTED_PLATFORM_ASSEMBLIES supply that name here, so the walk must record the
        // reference as unresolved and mark the closure incomplete — the "else" of the completeness
        // check the previous test's TRUSTED_PLATFORM_ASSEMBLIES branch satisfies.
        string rootPath = Path.Combine(_repoRoot, "Root.dll");
        CompileRealAssembly(rootPath, "Root");

        object? originalTrustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        try
        {
            AppContext.SetData("TRUSTED_PLATFORM_ASSEMBLIES", null);

            var discovery = new FixedDiscoveryService
            {
                Result = ProjectDiscoveryResult.Empty with
                {
                    ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["Root"] = rootPath,
                    },
                },
            };
            var service = CreateService(discovery);
            var document = new ArchitectureContractDocument
            {
                Version = 1,
                Name = "Test",
                Analysis = new ArchitectureAnalysisConfiguration
                {
                    TargetAssemblies = new List<string> { "Root" },
                },
            };

            ArchitectureRunnerPreparation preparation = service.PrepareRunner(document, _policyPath);

            Assert.Multiple(() =>
            {
                Assert.That(preparation.HasCompleteRootSelection, Is.True);
                Assert.That(preparation.IsMetadataReferenceClosureComplete, Is.False);
                Assert.That(preparation.HasCompleteArtifactSelection, Is.False);
            });
        }
        finally
        {
            AppContext.SetData("TRUSTED_PLATFORM_ASSEMBLIES", originalTrustedPlatformAssemblies);
        }
    }

    [Test]
    public void MaterializePreparedRunner_IncompleteRootSelection_ThrowsInvalidOperationException()
    {
        var service = CreateService(new FixedDiscoveryService());
        var document = new ArchitectureContractDocument { Version = 1, Name = "Test" };
        var preparation = new ArchitectureRunnerPreparation(
            _repoRoot, null, ProjectDiscoveryResult.Empty, ResolveAssemblyOutputs: false,
            Array.Empty<string>(), new Dictionary<string, string>(), _value,
            IsMetadataReferenceClosureComplete: true);

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(
            () => service.MaterializePreparedRunner(document, preparation));

        Assert.That(exception!.Message, Does.Contain("incomplete"));
    }

    [Test]
    public void MaterializePreparedRunner_ArtifactChangedSincePrepare_ThrowsAndRefusesToLoad()
    {
        // This is the safety property CaptureArtifactDigests/VerifyPreparedArtifacts exist for:
        // once PrepareRunner has captured a digest, materialization must refuse to proceed if the
        // bytes on disk no longer match, rather than silently loading a different assembly than the
        // one cache authorization was computed against.
        string rootPath = Path.Combine(_repoRoot, "Root.dll");
        CompileRealAssembly(rootPath, "Root");

        var discovery = new FixedDiscoveryService
        {
            Result = ProjectDiscoveryResult.Empty with
            {
                ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Root"] = rootPath,
                },
            },
        };
        var service = CreateService(discovery);
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "Root" },
            },
        };

        ArchitectureRunnerPreparation preparation = service.PrepareRunner(document, _policyPath);

        // Mutate the artifact after preparation captured its digest.
        using (FileStream stream = new(rootPath, FileMode.Append, FileAccess.Write))
        {
            stream.WriteByte(0);
        }

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(
            () => service.MaterializePreparedRunner(document, preparation));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain(rootPath));
            Assert.That(exception.Message, Does.Contain("changed after cache authorization"));
        });
    }

    [Test]
    public void MaterializePreparedRunner_UnchangedCompletePreparation_MaterializesRealRunner()
    {
        string rootPath = Path.Combine(_repoRoot, "Root.dll");
        CompileRealAssembly(rootPath, "Root");

        var discovery = new FixedDiscoveryService
        {
            Result = ProjectDiscoveryResult.Empty with
            {
                ResolvedAssemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Root"] = rootPath,
                },
            },
        };
        var service = CreateService(discovery);
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "Root" },
            },
        };

        ArchitectureRunnerPreparation preparation = service.PrepareRunner(document, _policyPath);

        ArchitectureRunnerSetup setup = service.MaterializePreparedRunner(document, preparation);

        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(setup.RepositoryRoot, Is.EqualTo(preparation.RepositoryRoot));
                Assert.That(setup.AssemblyLoads, Is.EqualTo(1));
                Assert.That(
                    setup.Runner.Session.Context.TargetAssemblies.Select(a => a.GetName().Name),
                    Has.Member("Root"));
            });
        }
        finally
        {
            setup.Runner.Session.Context.Dispose();
        }
    }
}
