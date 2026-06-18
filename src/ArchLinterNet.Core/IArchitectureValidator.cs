namespace ArchLinterNet.Core;

public interface IArchitectureValidator
{
    bool Validate(string policyPath);
}
