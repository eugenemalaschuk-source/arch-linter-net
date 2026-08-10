namespace Synthetic.Shared.Abstractions;

/// <summary>One synthetic module contract shared by every generated module assembly.</summary>
public interface IModule
{
    string Name { get; }
}
