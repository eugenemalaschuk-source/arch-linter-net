// Compile-only fixture demonstrating the recommended user-owned attribute
// pattern documented in docs/policy-format/semantic-classification.md
// ("Annotation strategy"). No extraction engine reads these types yet
// (#108-#114); this file only proves the documented shape compiles as a
// realistic, internal, user-defined attribute — see issue #108.

namespace SemanticClassificationAnnotationTestFixtures
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
    internal sealed class DomainLayerAttribute(string domain) : Attribute
    {
        public string Domain { get; } = domain;
    }

    [DomainLayer("Sales")]
    internal sealed class SalesOrder;
}
