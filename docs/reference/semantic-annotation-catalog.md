# Semantic annotation catalog

This page is the public reference for the source only `ArchLinterNet.Annotations`
catalog. The [machine readable manifest in the repository](https://github.com/eugenemalaschuk-source/arch-linter-net/blob/main/architecture/semantic-annotations.v1.json)
is authoritative for identities, targets, metadata, and compatibility. This
page describes the exact projection of that manifest; it does not add
annotation names or infer framework behavior.

Canonical role annotations provide one primary semantic role. Their optional
properties are the six context keys listed below. Metadata only annotations
provide one context key and do not create a primary role. A role selector can
therefore use `role: <CanonicalRole>` and, when needed, metadata alongside it.

The metadata key `feature` is supplied by `FeatureAttribute`. It is distinct
from the catalog role `Feature`, which is custom mapping only and has no
canonical role attribute FQN.

## Package and compatibility

These values are read from the manifest and apply to the generation 1 catalog.

| Manifest field | Value |
| --- | --- |
| `manifestFormatVersion` | `1` |
| `catalogGeneration` | `1` |
| `productRelease` | `0.10.0` |
| `package.id` | `ArchLinterNet.Annotations` |
| `package.version` | `0.10.0` |
| `package.distribution` | source-only NuGet content files |
| `package.prohibitedAssets` | `lib`, `ref`, `runtime`, `analyzer`, `build`, `buildTransitive` |
| `package.minimumCSharpLanguageVersion` | `9.0` |
| `package.contentFiles.pattern` | `cs/any/**/*.cs` |
| `package.contentFiles.buildAction` | `Compile` |
| `package.contentFiles.copyToOutput` | `false` |
| `package.contentFiles.directReferenceRequiredPerAuthoringProject` | `true` |
| `package.contentFiles.transitiveSourceInjection` | `false` |
| `package.minimumAnnotationAwareToolVersion` | `0.10.0` |
| `package.compatiblePackageRangeForGeneration1` | `[0.10.0,0.11.0)` |

Within the compatible package range, generation 1 preserves role FQNs,
meanings, scopes, constructors, and metadata property names and types. Catalog
generation and package SemVer are independent compatibility axes. Tools older
than `0.10.0` cannot diagnose source only annotation metadata retroactively.

## Role attribute contract

Every row in this table is a first class role from `roles[]`. Role attributes
are `internal sealed` types deriving from `System.Attribute`, have a public
parameterless constructor, can be used once, and are not inherited. The
supported role properties are the six optional string properties shown in the
table. Each value must be an exact, non empty compile time string.

| Canonical role | Exact FQN | Manifest family | Valid `attributeTargets` | Supported role properties | Assembly use supported | Example selector or policy use |
| --- | --- | --- | --- | --- | --- | --- |
| `DomainLayer` | `ArchLinterNet.Annotations.DomainLayerAttribute` | layered-clean | `assembly` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | yes | `selector: { role: DomainLayer }` |
| `ApplicationLayer` | `ArchLinterNet.Annotations.ApplicationLayerAttribute` | layered-clean | `assembly` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | yes | `selector: { role: ApplicationLayer }` |
| `InfrastructureLayer` | `ArchLinterNet.Annotations.InfrastructureLayerAttribute` | layered-clean | `assembly` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | yes | `selector: { role: InfrastructureLayer }` |
| `PresentationLayer` | `ArchLinterNet.Annotations.PresentationLayerAttribute` | layered-clean | `assembly` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | yes | `selector: { role: PresentationLayer }` |
| `ApiLayer` | `ArchLinterNet.Annotations.ApiLayerAttribute` | layered-clean | `assembly` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | yes | `selector: { role: ApiLayer }` |
| `PersistenceLayer` | `ArchLinterNet.Annotations.PersistenceLayerAttribute` | layered-clean | `assembly` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | yes | `selector: { role: PersistenceLayer }` |
| `IntegrationLayer` | `ArchLinterNet.Annotations.IntegrationLayerAttribute` | layered-clean | `assembly` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | yes | `selector: { role: IntegrationLayer }` |
| `SharedKernel` | `ArchLinterNet.Annotations.SharedKernelAttribute` | layered-clean | `assembly` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | yes | `selector: { role: SharedKernel }` |
| `CompositionRoot` | `ArchLinterNet.Annotations.CompositionRootAttribute` | layered-clean | `class`, `struct`, `interface`, `assembly` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | yes | `selector: { role: CompositionRoot }` |
| `Entity` | `ArchLinterNet.Annotations.EntityAttribute` | ddd | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: Entity }` |
| `AggregateRoot` | `ArchLinterNet.Annotations.AggregateRootAttribute` | ddd | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: AggregateRoot }` |
| `ValueObject` | `ArchLinterNet.Annotations.ValueObjectAttribute` | ddd | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: ValueObject }` |
| `DomainService` | `ArchLinterNet.Annotations.DomainServiceAttribute` | ddd | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: DomainService }` |
| `DomainEvent` | `ArchLinterNet.Annotations.DomainEventAttribute` | ddd | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: DomainEvent }` |
| `Repository` | `ArchLinterNet.Annotations.RepositoryAttribute` | ddd | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: Repository }` |
| `Specification` | `ArchLinterNet.Annotations.SpecificationAttribute` | ddd | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: Specification }` |
| `Factory` | `ArchLinterNet.Annotations.FactoryAttribute` | ddd | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: Factory }` |
| `Command` | `ArchLinterNet.Annotations.CommandAttribute` | cqrs-messages | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: Command }` |
| `CommandHandler` | `ArchLinterNet.Annotations.CommandHandlerAttribute` | cqrs-messages | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: CommandHandler }` |
| `Query` | `ArchLinterNet.Annotations.QueryAttribute` | cqrs-messages | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: Query }` |
| `QueryHandler` | `ArchLinterNet.Annotations.QueryHandlerAttribute` | cqrs-messages | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: QueryHandler }` |
| `Event` | `ArchLinterNet.Annotations.EventAttribute` | cqrs-messages | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: Event }` |
| `EventHandler` | `ArchLinterNet.Annotations.EventHandlerAttribute` | cqrs-messages | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: EventHandler }` |
| `IntegrationEvent` | `ArchLinterNet.Annotations.IntegrationEventAttribute` | cqrs-messages | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: IntegrationEvent }` |
| `ReadModel` | `ArchLinterNet.Annotations.ReadModelAttribute` | cqrs-messages | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: ReadModel }` |
| `Controller` | `ArchLinterNet.Annotations.ControllerAttribute` | web-api | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: Controller }` |
| `Endpoint` | `ArchLinterNet.Annotations.EndpointAttribute` | web-api | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: Endpoint }` |
| `RequestDto` | `ArchLinterNet.Annotations.RequestDtoAttribute` | web-api | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: RequestDto }` |
| `ResponseDto` | `ArchLinterNet.Annotations.ResponseDtoAttribute` | web-api | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: ResponseDto }` |
| `ApiContract` | `ArchLinterNet.Annotations.ApiContractAttribute` | web-api | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: ApiContract }` |
| `Validator` | `ArchLinterNet.Annotations.ValidatorAttribute` | web-api | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: Validator }` |
| `Port` | `ArchLinterNet.Annotations.PortAttribute` | ports-infrastructure | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: Port }` |
| `Adapter` | `ArchLinterNet.Annotations.AdapterAttribute` | ports-infrastructure | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: Adapter }` |
| `AntiCorruptionLayer` | `ArchLinterNet.Annotations.AntiCorruptionLayerAttribute` | ports-infrastructure | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: AntiCorruptionLayer }` |
| `DbContext` | `ArchLinterNet.Annotations.DbContextAttribute` | ports-infrastructure | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: DbContext }` |
| `RepositoryImplementation` | `ArchLinterNet.Annotations.RepositoryImplementationAttribute` | ports-infrastructure | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: RepositoryImplementation }` |
| `ExternalClient` | `ArchLinterNet.Annotations.ExternalClientAttribute` | ports-infrastructure | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: ExternalClient }` |
| `MessageBusAdapter` | `ArchLinterNet.Annotations.MessageBusAdapterAttribute` | ports-infrastructure | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: MessageBusAdapter }` |
| `FileSystemAdapter` | `ArchLinterNet.Annotations.FileSystemAdapterAttribute` | ports-infrastructure | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: FileSystemAdapter }` |
| `ClockAdapter` | `ArchLinterNet.Annotations.ClockAdapterAttribute` | ports-infrastructure | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: ClockAdapter }` |
| `View` | `ArchLinterNet.Annotations.ViewAttribute` | ui | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: View }` |
| `ViewModel` | `ArchLinterNet.Annotations.ViewModelAttribute` | ui | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: ViewModel }` |
| `Presenter` | `ArchLinterNet.Annotations.PresenterAttribute` | ui | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: Presenter }` |
| `UnityRuntime` | `ArchLinterNet.Annotations.UnityRuntimeAttribute` | unity-client | `assembly` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | yes | `selector: { role: UnityRuntime }` |
| `UnityEditor` | `ArchLinterNet.Annotations.UnityEditorAttribute` | unity-client | `assembly` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | yes | `selector: { role: UnityEditor }` |
| `MonoBehaviourAdapter` | `ArchLinterNet.Annotations.MonoBehaviourAdapterAttribute` | unity-client | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: MonoBehaviourAdapter }` |
| `ScriptableObjectAsset` | `ArchLinterNet.Annotations.ScriptableObjectAssetAttribute` | unity-client | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: ScriptableObjectAsset }` |
| `InputAdapter` | `ArchLinterNet.Annotations.InputAdapterAttribute` | unity-client | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: InputAdapter }` |
| `SceneAdapter` | `ArchLinterNet.Annotations.SceneAdapterAttribute` | unity-client | `class`, `struct`, `interface` | `Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, `Runtime` | no | `selector: { role: SceneAdapter }` |

The valid target list is closed. Assembly only roles cannot be placed on a
type; type only roles cannot be placed on an assembly; and no canonical role
targets a member or parameter.

## Context metadata attributes

The six metadata only attributes have the manifest's exact FQN and each takes
one required `string value` constructor argument. They are `internal sealed`,
allow one use, are not inherited, and target classes, structs, interfaces, and
assemblies. They add the named metadata key to a winning role; they do not
select a role by themselves.

| Metadata key | Exact FQN | Valid `attributeTargets` | Constructor | Accessibility | `allowMultiple` | `inherited` | Constructor accessibility | Metadata value rule | Assembly use supported | Example (metadata alongside a primary role selector) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `domain` | `ArchLinterNet.Annotations.DomainAttribute` | `class`, `struct`, `interface`, `assembly` | `value: string` (required) | `internal sealed` | false | false | public constructor on internal attribute type | exact non-empty compile-time string; no normalization or inference | yes | Metadata alongside a primary role selector: `selector: { role: DomainLayer, metadata: { domain: Sales } }` |
| `boundedContext` | `ArchLinterNet.Annotations.BoundedContextAttribute` | `class`, `struct`, `interface`, `assembly` | `value: string` (required) | `internal sealed` | false | false | public constructor on internal attribute type | exact non-empty compile-time string; no normalization or inference | yes | Metadata alongside a primary role selector: `selector: { role: DomainLayer, metadata: { boundedContext: Orders } }` |
| `module` | `ArchLinterNet.Annotations.ModuleAttribute` | `class`, `struct`, `interface`, `assembly` | `value: string` (required) | `internal sealed` | false | false | public constructor on internal attribute type | exact non-empty compile-time string; no normalization or inference | yes | Metadata alongside a primary role selector: `selector: { role: DomainLayer, metadata: { module: Billing } }` |
| `feature` | `ArchLinterNet.Annotations.FeatureAttribute` | `class`, `struct`, `interface`, `assembly` | `value: string` (required) | `internal sealed` | false | false | public constructor on internal attribute type | exact non-empty compile-time string; no normalization or inference | yes | Metadata alongside a primary role selector: `selector: { role: DomainLayer, metadata: { feature: Invoicing } }` |
| `platform` | `ArchLinterNet.Annotations.PlatformAttribute` | `class`, `struct`, `interface`, `assembly` | `value: string` (required) | `internal sealed` | false | false | public constructor on internal attribute type | exact non-empty compile-time string; no normalization or inference | yes | Metadata alongside a primary role selector: `selector: { role: DomainLayer, metadata: { platform: Web } }` |
| `runtime` | `ArchLinterNet.Annotations.RuntimeAttribute` | `class`, `struct`, `interface`, `assembly` | `value: string` (required) | `internal sealed` | false | false | public constructor on internal attribute type | exact non-empty compile-time string; no normalization or inference | yes | Metadata alongside a primary role selector: `selector: { role: UnityRuntime, metadata: { runtime: player } }` |

`FeatureAttribute` is therefore metadata for `feature`; it is not the
custom-mapping-only catalog role `Feature`.

## Assembly compatibility marker

The identity marker is separate from the role and context catalogs. It is
assembly-targeted compatibility metadata only. It is not selectable semantic
evidence and contributes neither a primary role nor a context key.

| Marker field | Manifest value |
| --- | --- |
| Exact FQN | `ArchLinterNet.Annotations.AnnotationCatalogIdentityAttribute` |
| Accessibility | `internal sealed` |
| Valid `attributeTargets` | `assembly` |
| Constructor | `catalogGeneration: int` (required), `packageVersion: string` (required) |
| Marker value | `catalogGeneration: 1`, `packageVersion: 0.10.0` |
| Value rule | catalogGeneration and exact package SemVer of the package source compiled into the assembly |
| `allowMultiple` | false |
| `inherited` | false |
| Constructor accessibility | public constructor on internal attribute type |
| Assembly use supported | yes, as compatibility metadata only |
| Selectable semantic evidence | no |

An annotation aware tool checks the marker's generation and package version
against its supported compatibility range. Missing, malformed, unsupported, or
out of range identity data fails closed and does not resolve canonical
annotation evidence.
