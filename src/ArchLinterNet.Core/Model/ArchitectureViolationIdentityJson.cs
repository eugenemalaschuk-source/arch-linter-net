using System.Text.Json;

namespace ArchLinterNet.Core.Model;

/// <summary>Stable JSON wire projection of the authoritative baseline identity.</summary>
public static class ArchitectureViolationIdentityJson
{
    public static string Serialize(ArchitectureViolationIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return JsonSerializer.Serialize(new
        {
            identity_version = identity.IdentityVersion,
            contract_family = identity.ContractFamily,
            kind = identity.Kind,
            contract_id = identity.ContractId,
            source_assembly = identity.SourceAssembly,
            source_type = identity.SourceType,
            source_member = identity.SourceMember,
            target_assembly = identity.TargetAssembly,
            target_type = identity.TargetType,
            target_member = identity.TargetMember,
            occurrence = identity.Occurrence,
            configuration = identity.Configuration,
        });
    }
}
