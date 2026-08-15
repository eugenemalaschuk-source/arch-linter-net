namespace ArchLinterNet.Core.Contracts.RawValidators;

// Pre-deserialization counterpart to Validators/IArchitecturePolicyDocumentValidator: a raw validator
// inspects the policy's YAML node tree, which is the only place a key that
// IgnoreUnmatchedProperties() would silently discard is still visible. Implementations throw
// InvalidOperationException; the loader enriches it with the offending policy location.
internal interface IArchitecturePolicyRawDocumentValidator
{
    void Validate(ArchitecturePolicyRawDocument document);
}
