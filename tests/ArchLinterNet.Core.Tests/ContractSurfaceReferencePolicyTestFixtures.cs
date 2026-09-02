// These are deliberately user-owned attributes. ArchLinterNet does not provide any of them;
// the policy below maps only the role attributes and treats PublicApiContract as membership.
namespace ArchLinterNet.Core.Tests.ReferencePolicyFixtures
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PublicApiContractAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ValueObjectAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class EntityAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PersistenceModelAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class TransportImplementationAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class TransportMarkerAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SerializationMarkerAttribute : Attribute;

    public interface IRuntimeContract;

    public abstract class RuntimeContractBase;

    public static class ContractSurfaceReferencePolicyTestFixtures
    {
        public static string AssemblyName => typeof(ContractSurfaceReferencePolicyTestFixtures).Assembly.GetName().Name!;

        public static string PublicApiMarkerName => typeof(PublicApiContractAttribute).FullName!;

        public static string ValueObjectAttributeName => typeof(ValueObjectAttribute).FullName!;

        public static string EntityAttributeName => typeof(EntityAttribute).FullName!;

        public static string PersistenceModelAttributeName => typeof(PersistenceModelAttribute).FullName!;

        public static string TransportImplementationAttributeName => typeof(TransportImplementationAttribute).FullName!;

        public static string TransportMarkerName => typeof(TransportMarkerAttribute).FullName!;

        public static string SerializationMarkerName => typeof(SerializationMarkerAttribute).FullName!;

        public static string PolicyYaml => $"""
            version: 1
            name: Contract surface reference policy

            analysis:
              target_assemblies: [{AssemblyName}]

            classification:
              attributes:
                - attribute: {ValueObjectAttributeName}
                  role: ValueObject
                - attribute: {EntityAttributeName}
                  role: Entity
                - attribute: {PersistenceModelAttributeName}
                  role: PersistenceModel
                - attribute: {TransportImplementationAttributeName}
                  role: TransportImplementation

            contracts:
              strict_public_api_surface:
                - id: server-v1-reviewed-surface
                  name: Server V1 reviewed contract surface
                  assemblies: [{AssemblyName}]
                  surface_selector:
                    namespace: ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Contracts.V1
                    has_attribute: {PublicApiMarkerName}
                  reason: User-owned membership marker selects the intentionally reviewed V1 DTO.

              audit_public_api_surface:
                - id: runtime-reviewed-surface
                  name: Runtime reviewed contract surface
                  assemblies: [{AssemblyName}]
                  surface_selector:
                    implements_interface: ArchLinterNet.Core.Tests.ReferencePolicyFixtures.IRuntimeContract
                  reason: An existing interface boundary selects runtime API membership without an API role.

              strict_attribute_usage:
                - id: transport-marker-placement
                  name: Transport markers stay on server contracts
                  attributes: [{TransportMarkerName}]
                  allowed_only_in_namespaces:
                    - ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Contracts
                  reason: Transport markers belong to contract DTOs, not persistence records.

              audit_attribute_usage:
                - id: serialization-marker-placement
                  name: Serialization markers stay on transport types
                  attributes: [{SerializationMarkerName}]
                  allowed_only_in_namespaces:
                    - ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Transport
                  reason: Serialization markers are audited before transport placement is enforced.

              strict_contract_surface_exposure:
                - id: server-v1-no-internal-types
                  name: Server V1 contracts expose no internals
                  source:
                    public_api_surface: server-v1-reviewed-surface
                  forbidden:
                    - role: Entity
                    - role: PersistenceModel
                    - role: TransportImplementation
                  reason: Published DTO signatures must not expose domain, persistence, or transport implementation types.

              audit_contract_surface_exposure:
                - id: runtime-no-editor-types
                  name: Runtime contracts expose no editor types
                  source:
                    public_api_surface: runtime-reviewed-surface
                  forbidden:
                    - namespace: ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Library.Editor
                  reason: Runtime API signatures must not disclose editor-only implementation types.

              strict_versioned_contract_surface_isolation:
                - id: server-v1-isolation
                  name: Server V1 contracts stay isolated
                  surfaces:
                    - id: v1-contracts
                      types_matching:
                        namespace: ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Contracts.V1
                    - id: v2-contracts
                      types_matching:
                        namespace: ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Contracts.V2
                    - id: transport-implementation
                      types_matching:
                        role: TransportImplementation
                  source_surface: v1-contracts
                  forbidden_surfaces: [v2-contracts, transport-implementation]
                  reason: V1 cannot expose newer DTOs or transport implementation details.

              audit_versioned_contract_surface_isolation:
                - id: server-v1-isolation-audit
                  name: Audit V1 isolation migration findings
                  surfaces:
                    - id: v1-contracts
                      types_matching:
                        namespace: ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Contracts.V1
                    - id: v2-contracts
                      types_matching:
                        namespace: ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Contracts.V2
                    - id: transport-implementation
                      types_matching:
                        role: TransportImplementation
                  source_surface: v1-contracts
                  forbidden_surfaces: [v2-contracts, transport-implementation]
                  reason: Audit mode reports the same V1 isolation facts during migration.
            """;
    }
}

namespace ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Contracts.V1
{
    using ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Domain;
    using ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Persistence;
    using ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Transport;
    using V2 = ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Contracts.V2;

    // PublicApiContract is API membership only. ValueObject remains the selected type's one
    // semantic role, and TransportMarker is an orthogonal user-owned placement marker.
    [PublicApiContract]
    [ValueObject]
    [TransportMarker]
    public sealed class OrderContractV1
    {
        // The domain target is deliberately not selected by the reviewed API selector. The
        // nested ContractEnvelope<List<T>> shape proves recursive path evidence, not dependency
        // direction, and the public-api selector consequently fails closed for this escape too.
        public static ContractEnvelope<List<OrderEntity>> Orders => new();

        public static ContractEnvelope<OrderRecord> Stored => new();

        public static ContractEnvelope<V2.OrderContractV2> NextVersion => new();

        public static TransportEnvelope<OrderContractV1> Transport => new();
    }

    public sealed class ContractEnvelope<T>
    {
        public T Value => default!;
    }
}

namespace ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Contracts.V2
{
    [TransportMarker]
    [SerializationMarker]
    public sealed class OrderContractV2
    {
        public string Id => "v2";
    }
}

namespace ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Domain
{
    // A domain entity carrying a transport marker is a strict attribute_usage violation. It is
    // intentionally separate from the recursive signature leak that makes this type observable
    // from OrderContractV1.
    [Entity]
    [TransportMarker]
    public sealed class OrderEntity
    {
        public Guid Id { get; init; }
    }
}

namespace ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Persistence
{
    // This misplaced marker is the strict attribute_usage finding. The same type is exposed by
    // OrderContractV1.Stored so the persistence leak is independently visible in the exposure
    // payload rather than being inferred from marker placement.
    [PersistenceModel]
    [TransportMarker]
    public sealed class OrderRecord
    {
        public Guid Id { get; init; }
    }
}

namespace ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Transport
{
    [TransportImplementation]
    [SerializationMarker]
    public sealed class TransportEnvelope<T>
    {
        public T Payload => default!;
    }
}

namespace ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Library.Runtime
{
    using ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Library.Editor;

    // This type participates in the non-annotation interface-selected surface. Its ValueObject
    // role remains independently classified, while the editor return type is an audit leak.
    [ValueObject]
    public sealed class RuntimeEditorBridge : RuntimeContractBase, IRuntimeContract
    {
        public static EditorSettings EditorSettings => new();
    }
}

namespace ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Library.Editor
{
    // Editor settings are useful in an editor assembly but must not cross the runtime contract
    // boundary. The marker also provides a deliberately misplaced audit attribute_usage finding.
    [SerializationMarker]
    public sealed class EditorSettings
    {
        public string Theme => "editor";
    }
}
