using System.Diagnostics;
using System.Text;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Tests;

internal static class BenchmarkFixtureMaterializer
{
    public static BenchmarkMaterializedFixture Materialize(BenchmarkWorkloadDefinition definition)
    {
        string root = Path.Combine(Path.GetTempPath(), $"arch-linter-benchmark-{definition.WorkloadId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            if (definition.Topology.ContainsCycle)
            {
                throw new InvalidOperationException(
                    "CyclicScc is a structural-only workload in large-solution-benchmark/v1; project-reference compilation is not supported.");
            }

            WriteProjects(root, definition);
            string solutionPath = WriteSolution(root, definition);
            string policyPath = WritePolicy(root, definition);
            string manifestPath = WriteBenchmarkMetadata(root, definition);
            if (definition.CompilationMode == BenchmarkCompilationMode.StagedAssemblies)
            {
                StageAssemblies(root, definition, solutionPath);
                manifestPath = WriteBenchmarkMetadata(root, definition);
            }

            return new BenchmarkMaterializedFixture(definition, root, solutionPath, policyPath, manifestPath);
        }
        catch
        {
            DeleteDirectory(root);
            throw;
        }
    }

    private static void WriteProjects(string root, BenchmarkWorkloadDefinition definition)
    {
        Dictionary<string, string> projectPaths = definition.Projects.ToDictionary(
            project => project.Id,
            project => Path.Combine(root, "src", project.AssemblyName, $"{project.AssemblyName}.csproj"),
            StringComparer.Ordinal);
        foreach (BenchmarkProjectNode project in definition.Projects)
        {
            string projectDirectory = Path.GetDirectoryName(projectPaths[project.Id])!;
            Directory.CreateDirectory(projectDirectory);
            WriteProject(projectDirectory, project, definition, projectPaths);
            WriteSources(projectDirectory, project, definition);
        }
    }

    private static void WriteProject(
        string projectDirectory,
        BenchmarkProjectNode project,
        BenchmarkWorkloadDefinition definition,
        IReadOnlyDictionary<string, string> projectPaths)
    {
        var builder = new StringBuilder()
            .AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">")
            .AppendLine("  <PropertyGroup>")
            .AppendLine("    <TargetFramework>net10.0</TargetFramework>")
            .AppendLine("    <ImplicitUsings>enable</ImplicitUsings>")
            .AppendLine("    <Nullable>enable</Nullable>")
            .AppendLine($"    <AssemblyName>{project.AssemblyName}</AssemblyName>")
            .AppendLine("  </PropertyGroup>");

        IReadOnlyList<BenchmarkProjectEdge> references = definition.Edges
            .Where(edge => edge.FromProjectId == project.Id)
            .OrderBy(edge => edge.ToProjectId, StringComparer.Ordinal)
            .ToList();
        if (references.Count > 0)
        {
            builder.AppendLine("  <ItemGroup>");
            foreach (BenchmarkProjectEdge reference in references)
            {
                string relativePath = Path.GetRelativePath(projectDirectory, projectPaths[reference.ToProjectId]);
                builder.AppendLine($"    <ProjectReference Include=\"{relativePath.Replace('\\', '/')}\" />");
            }

            builder.AppendLine("  </ItemGroup>");
        }

        builder.AppendLine("</Project>");
        File.WriteAllText(Path.Combine(projectDirectory, $"{project.AssemblyName}.csproj"), builder.ToString());
    }

    private static void WriteSources(
        string projectDirectory,
        BenchmarkProjectNode project,
        BenchmarkWorkloadDefinition definition)
    {
        int filesPerProject = definition.Dimensions.SourceRootCount * definition.Dimensions.SourceFilesPerProject;
        for (int sourceRoot = 1; sourceRoot <= definition.Dimensions.SourceRootCount; sourceRoot++)
        {
            string sourceDirectory = Path.Combine(projectDirectory, $"SourceRoot{sourceRoot:00}");
            Directory.CreateDirectory(sourceDirectory);
            for (int fileIndex = 1; fileIndex <= definition.Dimensions.SourceFilesPerProject; fileIndex++)
            {
                int globalFileIndex = ((sourceRoot - 1) * definition.Dimensions.SourceFilesPerProject) + fileIndex;
                int firstType = ((globalFileIndex - 1) * definition.Dimensions.TypesPerProject / filesPerProject) + 1;
                int lastType = globalFileIndex * definition.Dimensions.TypesPerProject / filesPerProject;
                var content = new StringBuilder();
                if (firstType <= lastType)
                {
                    content.AppendLine($"namespace Synthetic.{project.AssemblyName};").AppendLine();
                }

                for (int typeIndex = firstType; typeIndex <= lastType; typeIndex++)
                {
                    content.AppendLine($"public sealed class SourceRoot{sourceRoot:00}Type{typeIndex:000}")
                        .AppendLine("{")
                        .AppendLine($"    public const string Identity = \"{project.Id}-type-{typeIndex:000}\";")
                        .AppendLine("}")
                        .AppendLine();
                }

                if (content.Length == 0)
                {
                    content.AppendLine("// Synthetic source-root marker.");
                }

                string sourcePath = Path.Combine(sourceDirectory, $"Type{fileIndex:000}.cs");
                File.WriteAllText(sourcePath, content.ToString());
            }
        }
    }

    private static string WriteSolution(string root, BenchmarkWorkloadDefinition definition)
    {
        string path = Path.Combine(root, $"{definition.WorkloadId}.slnx");
        var content = new StringBuilder().AppendLine("<Solution>");
        foreach (BenchmarkProjectNode project in definition.Projects)
        {
            content.AppendLine($"  <Project Path=\"src/{project.AssemblyName}/{project.AssemblyName}.csproj\" />");
        }

        content.AppendLine("</Solution>");
        File.WriteAllText(path, content.ToString());
        return path;
    }

    private static string WritePolicy(string root, BenchmarkWorkloadDefinition definition)
    {
        string path = Path.Combine(root, "dependencies.arch.yml");
        var content = new StringBuilder()
            .AppendLine("version: 1")
            .AppendLine($"name: {definition.WorkloadId}")
            .AppendLine("classification:")
            .AppendLine("  namespace:")
            .AppendLine("    - namespace: Synthetic")
            .AppendLine("      role: SyntheticBenchmarkType")
            .AppendLine("layers:");
        for (int layer = 1; layer <= definition.Dimensions.LayerCount; layer++)
        {
            content.AppendLine($"  synthetic_layer_{layer:00}:")
                .AppendLine("    namespace: Synthetic")
                .AppendLine("    selector:")
                .AppendLine("      role: SyntheticBenchmarkType")
                .AppendLine($"      when: {BuildSelectorPredicate(definition.Dimensions.SelectorMembershipsPerLayer)}");
            if (layer > 1)
            {
                string overlapsWith = string.Join(
                    ", ",
                    Enumerable.Range(1, layer - 1).Select(previous => $"synthetic_layer_{previous:00}"));
                content.AppendLine($"    overlaps_with: [{overlapsWith}]");
            }

            content.AppendLine();
        }

        content.AppendLine("analysis:");
        if (definition.CompilationMode == BenchmarkCompilationMode.StagedAssemblies)
        {
            content.AppendLine("  target_assemblies:");
            foreach (BenchmarkProjectNode project in definition.Projects)
            {
                content.AppendLine($"    - {project.AssemblyName}");
            }

            content.AppendLine("  assembly_search_paths:")
                .AppendLine("    - .benchmark/staged-assemblies");
        }
        else
        {
            content.AppendLine("  projects:");
            foreach (BenchmarkProjectNode project in definition.Projects)
            {
                content.AppendLine($"    - src/{project.AssemblyName}/{project.AssemblyName}.csproj");
            }
        }

        content.AppendLine("contracts:")
            .AppendLine("  strict:");
        for (int contract = 1; contract <= definition.Inventory.ContractCount; contract++)
        {
            content.AppendLine($"    - id: synthetic-contract-{contract:000}")
                .AppendLine($"      name: synthetic-contract-{contract:000}")
                .AppendLine("      source: synthetic_layer_01")
                .AppendLine("      forbidden: []")
                .AppendLine("      reason: Synthetic benchmark workload contract.");
        }

        content.AppendLine("  strict_layout_conventions:")
            .AppendLine("    - id: synthetic-source-fact-index")
            .AppendLine("      name: synthetic-source-fact-index")
            .AppendLine("      files_matching:")
            .AppendLine("        namespace_segment: Synthetic")
            .AppendLine("      forbidden_name_prefix: __never__")
            .AppendLine("      reason: Materialize the synthetic source fact index.");
        for (int candidate = 1; candidate <= definition.Dimensions.FindingCandidates; candidate++)
        {
            content.AppendLine($"    - id: synthetic-finding-candidate-{candidate:000}")
                .AppendLine($"      name: synthetic-finding-candidate-{candidate:000}")
                .AppendLine("      files_matching:")
                .AppendLine("        namespace_segment: Synthetic")
                .AppendLine("      forbidden_name_prefix: SourceRoot")
                .AppendLine("      reason: Synthetic benchmark finding candidate.");
        }
        File.WriteAllText(path, content.ToString());
        return path;
    }

    private static string BuildSelectorPredicate(int membershipCount)
    {
        const string Predicate = "subject.kind == 'class'";
        return $"\"{string.Join(" && ", Enumerable.Repeat(Predicate, membershipCount))}\"";
    }

    private static string WriteBenchmarkMetadata(string root, BenchmarkWorkloadDefinition definition)
    {
        string directory = Path.Combine(root, ".benchmark");
        Directory.CreateDirectory(directory);
        string manifestPath = Path.Combine(directory, "workload.json");
        File.WriteAllText(manifestPath, BenchmarkJson.Serialize(definition.ToManifest()));
        File.WriteAllText(Path.Combine(directory, "workflow.json"), BenchmarkJson.Serialize(definition.Workflow));
        File.WriteAllText(Path.Combine(directory, "pull-request-change.json"), BenchmarkJson.Serialize(definition.PullRequestChange));
        if (definition.CompilationMode == BenchmarkCompilationMode.StagedAssemblies)
        {
            string stagedDirectory = Path.Combine(directory, "staged-assemblies");
            Directory.CreateDirectory(stagedDirectory);
            File.WriteAllText(
                Path.Combine(stagedDirectory, "manifest.json"),
                BenchmarkJson.Serialize(new
                {
                    mode = "external-staged-assemblies",
                    assemblies = definition.Projects.Select(project => new
                    {
                        name = project.AssemblyName,
                        path = $"{project.AssemblyName}.dll",
                        receipt = $"{project.AssemblyName}.dll.archlinternet-receipt.json",
                    }).ToList(),
                    source = "synthetic-external-build",
                }));
        }

        return manifestPath;
    }

    private static void StageAssemblies(string root, BenchmarkWorkloadDefinition definition, string solutionPath)
    {
        RunDotnet(root, ["restore", solutionPath, "--nologo", "--disable-parallel"]);
        RunDotnet(root, ["build", solutionPath, "--nologo", "--no-restore", "--verbosity", "quiet", "--maxcpucount:1"]);

        string stagedDirectory = Path.Combine(root, ".benchmark", "staged-assemblies");
        foreach (BenchmarkProjectNode project in definition.Projects)
        {
            string projectDirectory = Path.Combine(root, "src", project.AssemblyName);
            string projectPath = Path.Combine(projectDirectory, $"{project.AssemblyName}.csproj");
            string assemblyPath = Path.Combine(projectDirectory, "bin", "Debug", "net10.0", $"{project.AssemblyName}.dll");
            string stagedAssemblyPath = Path.Combine(stagedDirectory, Path.GetFileName(assemblyPath));
            File.Copy(assemblyPath, stagedAssemblyPath, overwrite: true);
            BuildReceiptStore.Write(
                stagedAssemblyPath,
                new BuildReceiptV1(
                    Path.Combine("src", project.AssemblyName, $"{project.AssemblyName}.csproj"),
                    project.AssemblyName,
                    "Debug",
                    "net10.0",
                    BuildStateCanonicalHasher.ComputeBuildInputFingerprint(projectPath, root),
                    BuildStateCanonicalHasher.ComputeContentDigest(stagedAssemblyPath)));
        }
    }

    internal static void RunDotnet(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        string output = outputTask.GetAwaiter().GetResult();
        string error = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Synthetic benchmark dotnet command failed: dotnet {string.Join(' ', arguments)}{Environment.NewLine}{output}{Environment.NewLine}{error}");
        }
    }

    private static void DeleteDirectory(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

internal sealed class BenchmarkMaterializedFixture : IDisposable
{
    public BenchmarkMaterializedFixture(
        BenchmarkWorkloadDefinition definition,
        string root,
        string solutionPath,
        string policyPath,
        string manifestPath)
    {
        Definition = definition;
        Root = root;
        SolutionPath = solutionPath;
        PolicyPath = policyPath;
        ManifestPath = manifestPath;
    }

    public BenchmarkWorkloadDefinition Definition { get; }

    public string Root { get; }

    public string SolutionPath { get; }

    public string PolicyPath { get; }

    public string ManifestPath { get; }

    public IReadOnlyList<string> StagedAssemblyPaths =>
        Directory.Exists(Path.Combine(Root, ".benchmark", "staged-assemblies"))
            ? Directory.GetFiles(Path.Combine(Root, ".benchmark", "staged-assemblies"), "*.dll", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();

    public IReadOnlyList<string> ProjectPaths =>
        Directory.GetFiles(Path.Combine(Root, "src"), "*.csproj", SearchOption.AllDirectories);

    public IReadOnlyList<string> SourcePaths =>
        Directory.GetFiles(Path.Combine(Root, "src"), "*.cs", SearchOption.AllDirectories);

    public void Build()
    {
        if (Definition.CompilationMode != BenchmarkCompilationMode.RealMsBuild)
        {
            throw new InvalidOperationException("Only real-MSBuild benchmark fixtures can be built by this materializer.");
        }

        BenchmarkFixtureMaterializer.RunDotnet(Root, ["restore", SolutionPath, "--nologo", "--disable-parallel"]);
        BenchmarkFixtureMaterializer.RunDotnet(Root, ["build", SolutionPath, "--nologo", "--no-restore", "--verbosity", "quiet", "--maxcpucount:1"]);
    }

    public BenchmarkCliRun RunValidation(bool ensureBuilt = true)
    {
        string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        string cliPath = Path.Combine(repositoryRoot, "src", "ArchLinterNet.Cli", "bin", "Debug", "net10.0", "ArchLinterNet.Cli.dll");
        if (!File.Exists(cliPath))
        {
            throw new InvalidOperationException($"CLI not built at {cliPath}.");
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.ArgumentList.Add(cliPath);
        startInfo.ArgumentList.Add("--policy");
        startInfo.ArgumentList.Add(PolicyPath);
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("strict");
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add("json");
        if (ensureBuilt)
        {
            startInfo.ArgumentList.Add("--ensure-built");
        }

        using Process process = Process.Start(startInfo)!;
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new BenchmarkCliRun(
            process.ExitCode,
            outputTask.GetAwaiter().GetResult(),
            errorTask.GetAwaiter().GetResult());
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

internal sealed record BenchmarkCliRun(int ExitCode, string StandardOutput, string StandardError);
