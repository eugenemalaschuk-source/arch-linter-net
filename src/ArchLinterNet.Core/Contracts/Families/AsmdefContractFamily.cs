using YamlDotNet.Serialization;

namespace ArchLinterNet.Core.Contracts.Families;

public sealed partial class ArchitectureContractGroups
{
    [YamlMember(Alias = "strict_asmdef")] public List<ArchitectureAsmdefContract> StrictAsmdef { get; set; } = new();

    [YamlMember(Alias = "audit_asmdef")] public List<ArchitectureAsmdefContract> AuditAsmdef { get; set; } = new();
}

public sealed class ArchitectureAsmdefContract : IArchitectureContract
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "id")] public string? Id { get; set; }

    [YamlMember(Alias = "source_assemblies")]
    public List<string> SourceAssemblies { get; set; } = new();

    [YamlMember(Alias = "forbidden_editor_refs")]
    public bool ForbiddenEditorRefs { get; set; }

    [YamlMember(Alias = "forbidden_asmdef_prefixes")]
    public List<string> ForbiddenAsmdefPrefixes { get; set; } = new();

    [YamlMember(Alias = "reason")] public string Reason { get; set; } = string.Empty;
}
