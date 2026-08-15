namespace ArchLinterNet.Core.Contracts;

public interface IArchitectureContract
{
    string Name { get; }

    string? Id { get; set; }
}
