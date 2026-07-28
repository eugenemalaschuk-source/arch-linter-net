namespace ArchLinterNet.Core.Schema;

/// <summary>Describes one immutable, release-matched public document schema.</summary>
public sealed record PackagedSchemaDescriptor(
    string LogicalId,
    string DocumentVersion,
    string ResourcePath,
    string SchemaId,
    string Sha256,
    bool SupportsRead,
    bool SupportsWrite,
    string MigrationNote,
    string OwningCapability);
