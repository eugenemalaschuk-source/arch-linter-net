namespace ArchLinterNet.Core.Model;

// Stream-loaded assemblies do not retain a usable Assembly.Location. Keep the physical path and
// raw byte identities captured from the very streams passed to AssemblyLoadContext.LoadFromStream.
// The cache can therefore prove the outcome corresponds to those in-memory PE/PDB bytes rather
// than a later replacement at the same path.
internal sealed record ArchitectureLoadedAssemblyArtifact(
    string AssemblyPath,
    string AssemblyContentDigest,
    string PdbContentDigest,
    long BytesLoaded);
