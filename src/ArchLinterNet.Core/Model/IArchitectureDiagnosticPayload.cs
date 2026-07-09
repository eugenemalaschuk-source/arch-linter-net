namespace ArchLinterNet.Core.Model;

public interface IArchitectureDiagnosticPayload
{
    ArchitectureDiagnostic ToDiagnostic(ArchitectureViolation violation);
}
