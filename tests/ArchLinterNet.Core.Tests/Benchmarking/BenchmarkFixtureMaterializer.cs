using System.Diagnostics;
using System.Text;
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
            WriteProjects(root, definition);
            string solutionPath = WriteSolution(root, definition);
            string policyPath = WritePolicy(root, definition);
            string manifestPath = WriteBenchmarkMetadata(root, definition);
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
                for (int typeIndex = firstType; typeIndex <= lastType; typeIndex++)
                {
                    content.AppendLine($"namespace Synthetic.{project.AssemblyName};")
                        .AppendLine()
                        .AppendLine($"public sealed class SourceRoot{sourceRoot:00}Type{typeIndex:000}")
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
            .AppendLine("layers:");
        for (int layer = 1; layer <= definition.Dimensions.LayerCount; layer++)
        {
            content.AppendLine($"  synthetic_layer_{layer:00}:")
                .AppendLine("    namespace: Synthetic");
            if (layer > 1)
            {
                string overlapsWith = string.Join(
                    ", ",
                    Enumerable.Range(1, layer - 1).Select(previous => $"synthetic_layer_{previous:00}"));
                content.AppendLine($"    overlaps_with: [{overlapsWith}]");
            }

            content.AppendLine();
        }

        content.AppendLine("analysis:")
            .AppendLine("  projects:");
        foreach (BenchmarkProjectNode project in definition.Projects)
        {
            content.AppendLine($"    - src/{project.AssemblyName}/{project.AssemblyName}.csproj");
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
        File.WriteAllText(path, content.ToString());
        return path;
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
                    assemblies = definition.Projects.Select(project => project.AssemblyName).ToList(),
                    source = "synthetic-external-build",
                }));
        }

        return manifestPath;
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

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(SolutionPath);
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--verbosity");
        startInfo.ArgumentList.Add("quiet");
        startInfo.ArgumentList.Add("--maxcpucount:1");

        using Process process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Synthetic benchmark fixture failed to build.{Environment.NewLine}{output}{Environment.NewLine}{error}");
        }
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
