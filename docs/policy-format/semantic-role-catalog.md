# Semantic role catalog

This is the reviewed first-wave vocabulary for semantic architecture facts. It
gives future extraction and selector work shared names without requiring every
.NET repository to use the same architecture. The catalog is YAML-first and
static-analysis-only: it does not inspect runtime DI graphs, registration,
execution behavior, or data flow.

## Support tiers

| Tier | Meaning |
| --- | --- |
| **Canonical vocabulary** | Stable, style-neutral role suitable for shared selectors and fixtures. |
| **Optional annotation candidate** | Stable enough for a future convenience annotation; YAML remains the baseline. |
| **Examples-only** | Useful in guidance but needs real fixtures before becoming a default. |
| **Custom-mapping expected** | Valid concept whose names/evidence vary; map project conventions explicitly. |
| **Deferred** | Too framework-specific, ambiguous, or runtime-dependent for the first wave. |

The existing classification order is the evidence guidance: explicit YAML
override, type attribute, assembly attribute, inheritance/interface fact,
namespace, then path. The table identifies useful evidence, not an automatic
inference promise.

## Single-role classification

Classification assigns a type exactly one role and the metadata from that
winning source. Catalog roles are alternative classifications, not accumulated
tags: a type cannot simultaneously retain `DomainLayer` and `AggregateRoot`,
`PresentationLayer` and `Controller`, or `UnityRuntime` and
`MonoBehaviourAdapter` through the current model. When a policy needs context
such as a bounded context, module, platform, or layer, use the winning role's
metadata or an existing namespace layer. Adding roles together, or merging
metadata from a lower-precedence source, needs a separately reviewed model
extension.

## Role catalog

Each row has: definition; intended static evidence; typical metadata; example;
and support tier. `Attr` means an explicit type or assembly attribute, `Base`
means a base type/interface fact, and `Ns/Path` means a namespace or path
convention.

### Layered/clean architecture

| Role | Definition; evidence; metadata; example | Tier |
| --- | --- | --- |
| `DomainLayer` | Business rules; Ns/Attr; `domain`, `boundedContext`; Sales order rules. | Canonical |
| `ApplicationLayer` | Use-case orchestration; Ns/Attr; `domain`, `feature`; Inventory service. | Canonical |
| `InfrastructureLayer` | Technical implementations; Ns/Attr; `subsystem`, `adapter`; persistence. | Canonical |
| `PresentationLayer` | User-facing boundary; Ns/Base/Attr; `platform`, `feature`; desktop UI. | Canonical |
| `ApiLayer` | HTTP/RPC/message boundary; Ns/Attr; `platform`, `module`; Orders API. | Optional annotation candidate |
| `PersistenceLayer` | Storage-facing code; Ns/Base/Attr; `subsystem`, `boundedContext`; SQL storage. | Optional annotation candidate |
| `IntegrationLayer` | External-system boundary; Ns/Base/Attr; `adapter`, `direction`; payment gateway. | Optional annotation candidate |
| `SharedKernel` | Deliberately shared domain concepts; Ns/assembly Attr; `owner`, `stability`; shared values. | Canonical |
| `Common` | Broad reusable code without a narrower role; Ns/Path; `module`, `stability`; primitives. | Custom-mapping expected |
| `CompositionRoot` | Startup/composition boundary; Ns/known static convention; `platform`, `runtime`; host startup. | Optional annotation candidate |

### DDD

| Role | Definition; evidence; metadata; example | Tier |
| --- | --- | --- |
| `Entity` | Stable identity; Base/Attr/Ns; `domain`, `boundedContext`; Customer. | Canonical |
| `AggregateRoot` | Aggregate consistency entry point; Base/Attr; `domain`, `feature`; Order. | Canonical |
| `ValueObject` | Identity-free value; Base/Attr; `domain`, `boundedContext`; Money. | Canonical |
| `DomainService` | Stateless domain operation; Base/Attr/Ns; `domain`, `feature`; pricing. | Canonical |
| `DomainEvent` | Business state-transition fact; Base/Attr/Ns; `domain`, `feature`; OrderSubmitted. | Canonical |
| `Repository` | Aggregate persistence abstraction; Base/Attr/Ns; `domain`, `adapter`; repository port. | Canonical |
| `Specification` | Composable domain predicate; Base/Attr/Ns; `domain`, `feature`; eligibility rule. | Optional annotation candidate |
| `Factory` | Named construction policy; Base/Attr/Ns; `domain`, `feature`; InvoiceFactory. | Optional annotation candidate |
| `Policy` | Named business decision/rule; Base/Attr/Ns; `domain`, `stability`; CreditPolicy. | Custom-mapping expected |
| `Saga` | Long-running business process; Base/Attr/Ns; `boundedContext`, `feature`; fulfillment. | Examples-only |
| `ProcessManager` | State-bearing process coordinator; Base/Attr/Ns; `boundedContext`, `feature`; shipment workflow. | Examples-only |

### CQRS and Event Sourcing

| Role | Definition; evidence; metadata; example | Tier |
| --- | --- | --- |
| `Command` | Request to change state; Base/Attr/Ns; `boundedContext`, `feature`; SubmitOrder. | Canonical |
| `CommandHandler` | Handles a command; Base/Attr/Ns; `boundedContext`, `direction`; handler. | Canonical |
| `Query` | Read request; Base/Attr/Ns; `boundedContext`, `feature`; FindInventory. | Canonical |
| `QueryHandler` | Handles a query; Base/Attr/Ns; `boundedContext`, `direction`; query handler. | Canonical |
| `Event` | Fact/notification contract; Base/Attr/Ns; `boundedContext`, `direction`; OrderAccepted. | Canonical |
| `EventHandler` | Reacts to an event; Base/Attr/Ns; `boundedContext`, `direction`; reserve-stock handler. | Canonical |
| `IntegrationEvent` | Cross-process event contract; Base/Attr/Ns; `platform`, `direction`; PaymentCaptured. | Optional annotation candidate |
| `Projection` | Event/state to read representation; Base/Attr/Ns; `feature`, `adapter`; sales projection. | Examples-only |
| `ReadModel` | Query-optimized representation; Ns/Attr; `boundedContext`, `feature`; inventory read model. | Optional annotation candidate |
| `EventStore` | Event-stream persistence boundary; Base/Attr/Ns; `adapter`, `subsystem`; event store. | Examples-only |
| `Snapshot` | Point-in-time event-sourced state; Ns/Attr/Path; `boundedContext`, `feature`; aggregate snapshot. | Examples-only |

### Web/API and desktop/mobile UI

| Role | Definition; evidence; metadata; example | Tier |
| --- | --- | --- |
| `Controller` | Request controller; Base/Ns/Attr; `platform`, `feature`; ASP.NET controller. | Optional annotation candidate |
| `Endpoint` | Individual request/message endpoint; Base/Ns/Attr; `platform`, `direction`; minimal API. | Optional annotation candidate |
| `RequestDto` | Boundary input shape; Ns/Attr/Path; `platform`, `feature`; CreateOrderRequest. | Canonical |
| `ResponseDto` | Boundary output shape; Ns/Attr/Path; `platform`, `feature`; OrderResponse. | Canonical |
| `ApiContract` | Explicit boundary contract; Base/Ns/Attr; `platform`, `stability`; public API. | Optional annotation candidate |
| `Middleware` | Ordered pipeline component; Base/Ns/Attr; `platform`, `module`; correlation middleware. | Examples-only |
| `Filter` | Boundary filter/enricher; Base/Ns/Attr; `platform`, `feature`; authorization filter. | Custom-mapping expected |
| `Validator` | Input/domain validation rules; Base/Ns/Attr; `feature`, `boundedContext`; order validator. | Canonical |
| `Mapper` | Representation conversion; Base/Ns/Attr; `adapter`, `direction`; DTO mapper. | Custom-mapping expected |
| `View` | Visual surface; Base/Ns/Attr; `platform`, `feature`; WPF/MAUI view. | Optional annotation candidate |
| `ViewModel` | Presentation state/commands; Base/Ns/Attr; `platform`, `feature`; inventory VM. | Optional annotation candidate |
| `Presenter` | Application-to-view translator; Base/Ns/Attr; `platform`, `feature`; MVP presenter. | Examples-only |
| `Model` | UI data representation; Ns/Attr; `platform`, `feature`; screen model. | Custom-mapping expected |
| `Page` | Navigation-addressable surface; Base/Ns/Attr; `platform`, `feature`; MAUI page. | Examples-only |
| `Component` | Reusable UI unit; Ns/Attr; `platform`, `module`; Avalonia component. | Custom-mapping expected |
| `NavigationService` | UI navigation boundary; Base/Ns/Attr; `platform`, `direction`; mobile navigation. | Examples-only |
| `UiService` | UI support service; Base/Ns/Attr; `platform`, `feature`; dialog service. | Custom-mapping expected |

`View`, `ViewModel`, `Presenter`, `Model`, `Page`, and `Component` span MVVM,
MVP, MVC, WPF, WinUI, Avalonia, and MAUI, but their exact meaning is project-
dependent. Prefer explicit mappings for them.

### Unity/client

| Role | Definition; evidence; metadata; example | Tier |
| --- | --- | --- |
| `UnityRuntime` | Player/runtime code; assembly Attr/Ns; `platform`, `runtime`; gameplay assembly. | Optional annotation candidate |
| `UnityEditor` | Editor-only tooling; assembly Attr/Ns; `platform`, `runtime`; importer. | Optional annotation candidate |
| `Feature` | Coherent client feature; Ns/Path/Attr; `feature`, `module`; gameplay feature. | Custom-mapping expected |
| `System` | Focused client responsibility; Base/Ns/Attr; `feature`, `runtime`; gameplay system. | Custom-mapping expected |
| `MonoBehaviourAdapter` | Unity component adapter; Base/Ns/Attr; `platform`, `adapter`; scene adapter. | Examples-only |
| `ScriptableObjectAsset` | Asset-backed configuration/data; Base/Ns/Attr; `platform`, `feature`; balance asset. | Examples-only |
| `Installer` | Static composition entry point; Ns/Attr/Path; `platform`, `runtime`; client installer. | Examples-only |
| `InputAdapter` | Input-to-intent adapter; Base/Ns/Attr; `platform`, `adapter`; controller input. | Optional annotation candidate |
| `SceneAdapter` | Scene-to-application adapter; Base/Ns/Attr; `platform`, `adapter`; scene boundary. | Examples-only |

### Infrastructure and cross-cutting

| Role | Definition; evidence; metadata; example | Tier |
| --- | --- | --- |
| `DbContext` | Database session boundary; Base/Ns/Attr; `subsystem`, `adapter`; EF context. | Optional annotation candidate |
| `RepositoryImplementation` | Repository port implementation; interface/Ns/Attr; `adapter`, `boundedContext`; SQL repo. | Canonical |
| `ExternalClient` | External service/SDK client; Base/Ns/Attr; `adapter`, `direction`; payment client. | Canonical |
| `MessageBusAdapter` | Message transport adapter; Base/Ns/Attr; `adapter`, `direction`; bus adapter. | Optional annotation candidate |
| `FileSystemAdapter` | File-system boundary; Base/Ns/Attr; `adapter`, `platform`; document adapter. | Optional annotation candidate |
| `ClockAdapter` | Time boundary; Base/Ns/Attr; `adapter`, `platform`; system clock. | Optional annotation candidate |
| `TelemetryAdapter` | Telemetry export boundary; Base/Ns/Attr; `adapter`, `subsystem`; metrics exporter. | Examples-only |
| `PersistenceModel` | Storage representation; Ns/Attr/Path; `subsystem`, `boundedContext`; row model. | Custom-mapping expected |
| `Migration` | Schema/data migration unit; Base/Ns/Path; `subsystem`, `boundedContext`; database migration. | Examples-only |
| `Logging` | Logging boundary/policy; Base/Ns/Attr; `subsystem`, `platform`; logging adapter. | Canonical |
| `Telemetry` | Metrics/tracing concern; Base/Ns/Attr; `subsystem`, `adapter`; tracing component. | Canonical |
| `Validation` | Cross-cutting validation concern; Base/Ns/Attr; `feature`, `boundedContext`; shared validation. | Canonical |
| `Mapping` | Cross-cutting conversion concern; Base/Ns/Attr; `direction`, `adapter`; mapping profile. | Custom-mapping expected |
| `Serialization` | Wire/storage conversion; Base/Ns/Attr; `platform`, `direction`; JSON adapter. | Optional annotation candidate |
| `Authorization` | Access-decision boundary; Base/Ns/Attr; `platform`, `feature`; API policy. | Examples-only |
| `Caching` | Cache boundary/policy; Base/Ns/Attr; `adapter`, `feature`; query cache. | Examples-only |
| `Configuration` | Configuration boundary; Base/Ns/Attr; `platform`, `runtime`; host configuration. | Optional annotation candidate |
| `Options` | Typed configuration values; Ns/Attr/Path; `feature`, `runtime`; payment options. | Custom-mapping expected |
| `ExceptionHandling` | Failure translation/recording; Base/Ns/Attr; `platform`, `feature`; API handler. | Examples-only |
| `BackgroundJob` | Scheduled/queued work; Base/Ns/Attr; `feature`, `direction`; reconciliation job. | Examples-only |

## Metadata vocabulary

| Key | Meaning and example | Guidance |
| --- | --- | --- |
| `domain` | Business domain, e.g. `Sales`. | Good contextual selector. |
| `boundedContext` | DDD boundary, e.g. `Orders`. | Selector; do not infer ownership from name alone. |
| `module` | Product/technical module, e.g. `Admin`. | Selector when reviewed. |
| `feature` | Capability, e.g. `Inventory`. | Prefer stable identifiers. |
| `layer` | Layer such as `Domain` or `Application`. | Useful for migration/selectors. |
| `subsystem` | Technical subsystem, e.g. `Persistence`. | Infrastructure context. |
| `platform` | Host such as `Web`, `Desktop`, `MAUI`, `Unity`. | Cross-platform selector. |
| `runtime` | Static target such as `player` or `editor`. | Useful for Unity; not runtime inspection. |
| `adapter` | Technical/external boundary identity. | Explicit boundary policies. |
| `direction` | `inbound`, `outbound`, `publishes`, or `consumes`. | Use only when static evidence establishes it. |
| `stability` | `stable`, `experimental`, or `legacy`. | Migration/docs; policy use needs ownership. |
| `owner` | Accountable team/group. | Documentation by default; controlled policy metadata only if maintained. |

Avoid vague keys such as `kind`, `type`, `category`, or `miscellaneous`. Values
are exact canonical values, not regexes or scripts.

## Optional annotations and YAML mappings

The first catalog wave approves no ArchLinterNet-provided annotation types or
annotation package. Annotation names in this document are candidates/examples,
not shipped product APIs or a binary dependency. Projects define their own
attributes and map them by full type name:

```yaml
classification:
  attributes:
    - attribute: MyCompany.Architecture.DomainLayerAttribute
      role: DomainLayer
      metadata:
        domain: constructor[0]
  assembly_attributes:
    - attribute: MyCompany.Architecture.SharedKernelAttribute
      role: SharedKernel
      metadata:
        boundedContext: constructor[0]
```

An illustrative user-owned attribute usage shape is:

```csharp
[DomainLayer("Sales")]
public sealed class Order { }

[assembly: SharedKernel("Billing")]
```

These names are vocabulary candidates/examples, not types supplied by the
current product. The defined extraction forms are `constructor[N]`,
`property:Name`, `const:Full.Type.NAME`, and literal scalar values.
[Issue #108](https://github.com/eugenemalaschuk-source/arch-linter-net/issues/108)
resolved the packaging decision: ArchLinterNet ships no binary and no
source-only annotation package in this wave — user-defined attributes mapped
by full type name in YAML remain the sole supported adoption path. See
[Annotation strategy](semantic-classification.md#annotation-strategy) for
the full decision and trade-offs. A future optional package remains possible
as a separate, separately-decided change if concrete adoption need emerges.

A role-bearing assembly mapping is valid only when the assembly-attribute
source wins for a type. The current model cannot use
`[assembly: BoundedContext("Billing")]` as metadata-only shared context for
types that already have a higher-precedence type role: every mapping requires a
role, and metadata from losing sources is not merged. Metadata-only assembly
context is deferred until a separate semantic-classification-model/schema change
defines both its shape and merge semantics.

## Worked examples

### Sales, Inventory, and SharedKernel modular monolith

```yaml
classification:
  namespace:
    - namespace: Acme.Sales.Domain
      role: DomainLayer
      metadata: { domain: Sales, boundedContext: Sales }
    - namespace: Acme.Inventory.Application
      role: ApplicationLayer
      metadata: { domain: Inventory, boundedContext: Inventory }
    - namespace: Acme.SharedKernel
      role: SharedKernel
      metadata: { stability: stable }

layers:
  sales-domain:
    namespace: Acme.Sales.Domain
    selector:
      role: DomainLayer
      metadata: { boundedContext: Sales }
  inventory-application:
    namespace: Acme.Inventory.Application
    selector:
      role: ApplicationLayer
      metadata: { boundedContext: Inventory }
  shared-kernel:
    namespace: Acme.SharedKernel
    selector: { role: SharedKernel }
```

### Unity/client namespace conventions

```yaml
classification:
  precedence: [namespace]
  namespace:
    - namespace: Game.Gameplay.Systems
      role: System
      metadata: { platform: Unity, runtime: player }
    - namespace_suffix: ViewModels
      role: ViewModel
      metadata: { platform: Unity }
    - namespace_suffix: Views
      role: View
      metadata: { platform: Unity }

layers:
  gameplay-systems:
    namespace: Game.Gameplay.Systems
    selector:
      role: System
      metadata: { platform: Unity }
```

`namespace` is optional when a layer declares `selector`; when both are
present, both constraints must match. Use namespace facts, not scene
inspection, for Unity boundaries.
Asmdef and package-reference facts are useful future discovery guidance, but
they are not among the current six classification sources and require a separate
semantic-classification-model change before automatic use. These shapes now
have an active selector consumer; classification sources remain limited to the
implemented extraction capabilities.

## Conflict and safe policy guidance

- Prefer explicit type/assembly attributes for exceptions to namespace conventions.
- Respect the model's precedence and first-declared same-source conflict rules; never rely on accidental order across sources.
- Treat `Common`, `Model`, `Component`, `System`, and `Policy` as contextual unless explicitly mapped.
- Use exact role/metadata criteria. Do not write always-true selectors, broad exclusions, or policy generation that weakens reviewed YAML.
- Keep `reason` on broad overrides and every exclusion; explain the architectural intent.
- A conflict or unresolved evidence is a reviewable fact, not permission to guess.

The catalog does not add binary runtime dependencies, execute annotations,
validate DI registration, validate framework behavior, run plugins, or replace
project-specific YAML. Future extraction/selector issues consume this
vocabulary while preserving static evidence and YAML-first customization.
