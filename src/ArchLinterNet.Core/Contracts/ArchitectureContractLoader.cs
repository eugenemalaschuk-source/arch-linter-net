using ArchLinterNet.Core.Resolution;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchLinterNet.Core.Contracts;

public static class ArchitectureContractLoader
{
    private static readonly Lazy<ArchitectureContractDocument> _document = new(LoadInternal);

    public static ArchitectureContractDocument Load()
    {
        return _document.Value;
    }

    private static ArchitectureContractDocument LoadInternal()
    {
        string repositoryRoot = ArchitectureRepositoryRootLocator.Resolve();
        return LoadFromRepositoryRoot(repositoryRoot);
    }

    public static ArchitectureContractDocument LoadFromRepositoryRoot(string repositoryRoot)
    {
        string contractPath = Path.Combine(repositoryRoot, "architecture", "dependencies.arch.yml");
        return LoadFromPath(contractPath);
    }

    public static ArchitectureContractDocument LoadFromPath(string contractPath)
    {
        if (!File.Exists(contractPath))
        {
            throw new FileNotFoundException($"Architecture contract file not found: {contractPath}");
        }

        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        string yaml = File.ReadAllText(contractPath);
        ArchitectureContractDocument? document = deserializer.Deserialize<ArchitectureContractDocument>(yaml);

        if (document == null)
        {
            throw new InvalidOperationException("Failed to deserialize architecture contract YAML.");
        }

        return document;
    }
}
