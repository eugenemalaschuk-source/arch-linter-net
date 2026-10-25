using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    internal sealed partial class CandidatePackageFeed
    {
        private static string[] WriteSemanticShapeProjects(string consumerDirectory)
        {
            (string Name, string Source)[] shapes =
            [
                ("PlainLibrary", "using ArchLinterNet.Annotations; namespace PlainLibrary; [Entity] public sealed class PlainRecord { }"),
                ("Modular.Domain", "using ArchLinterNet.Annotations; [assembly: DomainLayer] namespace Modular.Domain; public sealed class Order { }"),
                ("Modular.Application", "using ArchLinterNet.Annotations; [assembly: ApplicationLayer] namespace Modular.Application; public sealed class OrderService { public Modular.Domain.Order? Order { get; set; } }"),
                ("Ddd.Domain", "using ArchLinterNet.Annotations; namespace Ddd.Domain; [AggregateRoot(BoundedContext = \"Sales\")] public sealed class Order { }"),
                ("Cqrs.Api", "using ArchLinterNet.Annotations; namespace Cqrs.Api; [Command] public sealed class PlaceOrder { } [CommandHandler] public sealed class PlaceOrderHandler { } [Query] public sealed class FindOrder { } [Endpoint] public sealed class OrdersEndpoint { }"),
                ("Infrastructure.Adapters", "using ArchLinterNet.Annotations; namespace Infrastructure.Adapters; [Adapter] public sealed class InventoryAdapter { } [RepositoryImplementation] public sealed class OrderRepository { }"),
                ("Public.Library", "using ArchLinterNet.Annotations; namespace Public.Library; [ApiContract] public sealed class ApiContractRole { } [ReviewedApi] public interface IOrderClient { } [AttributeUsage(AttributeTargets.Interface)] public sealed class ReviewedApiAttribute : Attribute { }"),
                ("Unity.Client", "using ArchLinterNet.Annotations; [assembly: UnityRuntime] namespace Unity.Client; public sealed class Bootstrap { }"),
            ];
            List<string> projectPaths = new();
            foreach ((string name, string source) in shapes)
            {
                string directory = Path.Combine(consumerDirectory, name.Replace('.', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(directory);
                string projectPath = Path.Combine(directory, $"{name}.csproj");
                string projectReference = name == "Modular.Application"
                    ? "<ItemGroup><ProjectReference Include=\"../Domain/Modular.Domain.csproj\" /></ItemGroup>"
                    : string.Empty;
                File.WriteAllText(projectPath, $"""
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><AssemblyName>{name}</AssemblyName></PropertyGroup>
                      {projectReference}
                    </Project>
                    """);
                File.WriteAllText(Path.Combine(directory, "Shape.cs"), source);
                projectPaths.Add(Path.GetRelativePath(consumerDirectory, projectPath));
            }
            return projectPaths.ToArray();
        }

        private static JsonElement FindJsonProperty(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return property.Value;
                    }
                }

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    JsonElement nested = FindJsonProperty(property.Value, propertyName);
                    if (nested.ValueKind != JsonValueKind.Undefined)
                    {
                        return nested;
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    JsonElement nested = FindJsonProperty(item, propertyName);
                    if (nested.ValueKind != JsonValueKind.Undefined)
                    {
                        return nested;
                    }
                }
            }

            return default;
        }

        private static string[] CollectJsonStrings(JsonElement element, params string[] propertyNames)
        {
            var values = new List<string>();
            Collect(element, values);
            return values.ToArray();

            void Collect(JsonElement current, List<string> destination)
            {
                if (current.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty property in current.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.String
                            && propertyNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            destination.Add(property.Value.GetString()!);
                        }
                        else
                        {
                            Collect(property.Value, destination);
                        }
                    }
                }
                else if (current.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in current.EnumerateArray())
                    {
                        Collect(item, destination);
                    }
                }
            }
        }

        private static void AssertSemanticRoleFact(
            JsonElement summary,
            string itemName,
            string expectedRole,
            params string[] expectedMetadata)
        {
            JsonElement[] facts = new[] { "CoveredItems", "UncoveredItems" }
                .SelectMany(propertyName => summary.GetProperty(propertyName).EnumerateArray())
                .Where(item => item.GetProperty("Item").GetString() == itemName)
                .ToArray();
            Assert.That(facts, Has.Length.EqualTo(1), $"Expected exactly one semantic fact for {itemName}.");

            string evidence = facts[0].GetProperty("Evidence").GetString()!;
            string rolePrefix = $"role={expectedRole}";
            Assert.That(evidence, Does.StartWith(rolePrefix), $"Unexpected semantic role evidence for {itemName}: {evidence}");
            Assert.That(evidence.Length == rolePrefix.Length || char.IsWhiteSpace(evidence[rolePrefix.Length]), Is.True,
                $"Expected role {expectedRole} for {itemName}, got {evidence}.");
            foreach (string metadata in expectedMetadata)
            {
                Assert.That(evidence, Does.Contain(metadata), $"Expected metadata {metadata} for {itemName}, got {evidence}.");
            }
        }
    }
}
