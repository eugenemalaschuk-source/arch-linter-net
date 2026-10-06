# Semantic role catalog

This is the reviewed vocabulary for semantic architecture facts. It gives
extraction and selector work shared names without requiring every .NET
repository to use the same architecture. The catalog is YAML-first and
static-analysis-only: it does not inspect runtime DI graphs, registration,
execution behavior, or data flow. The v0.10 annotation dispositions below are
approved design contract; the optional package and built-in recognition are not
available in released tools yet.

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

Classification assigns a type at most one effective primary role. Equivalent
canonical and custom-mapped evidence at the same specificity coalesces;
contradictory roles or same-specificity role metadata fail closed without an
effective role. Type role evidence outranks assembly role evidence, and losing
role metadata is not merged. Six bounded context keys (`domain`,
`boundedContext`, `module`, `feature`, `platform`, and `runtime`) may enrich the
winning role through role properties or metadata-only attributes. Metadata-only
context composes from role evidence, type scope, then assembly defaults; equal
values coalesce, and same-scope conflicts omit the affected key while retaining
an otherwise valid role.

## Role catalog

Each row has: definition; intended static evidence; typical metadata; example;
existing vocabulary support tier; and exactly one v0.10 annotation disposition.
`Attr` means an explicit type or assembly attribute, `Base` means a base
type/interface fact, and `Ns/Path` means a namespace or path convention. The
row metadata lists examples; first-party attributes accept only the six bounded
context keys above.

### Layered/clean architecture

| Role | Definition; evidence; metadata; example | Tier | v0.10 annotation disposition |
| --- | --- | --- | --- |
| `DomainLayer` | Business rules; Ns/Attr; `domain`, `boundedContext`; Sales order rules. | Canonical | First-class annotation |
| `ApplicationLayer` | Use-case orchestration; Ns/Attr; `domain`, `feature`; Inventory service. | Canonical | First-class annotation |
| `InfrastructureLayer` | Technical implementations; Ns/Attr; `subsystem`, `adapter`; persistence. | Canonical | First-class annotation |
| `PresentationLayer` | User-facing boundary; Ns/Base/Attr; `platform`, `feature`; desktop UI. | Canonical | First-class annotation |
| `ApiLayer` | HTTP/RPC/message boundary; Ns/Attr; `platform`, `module`; Orders API. | Optional annotation candidate | First-class annotation |
| `PersistenceLayer` | Storage-facing code; Ns/Base/Attr; `subsystem`, `boundedContext`; SQL storage. | Optional annotation candidate | First-class annotation |
| `IntegrationLayer` | External-system boundary; Ns/Base/Attr; `adapter`, `direction`; payment gateway. | Optional annotation candidate | First-class annotation |
| `SharedKernel` | Deliberately shared domain concepts; Ns/assembly Attr; `owner`, `stability`; shared values. | Canonical | First-class annotation |
| `Common` | Broad reusable code without a narrower role; Ns/Path; `module`, `stability`; primitives. | Custom-mapping expected | Custom-mapping-only |
| `CompositionRoot` | Startup/composition boundary; Ns/known static convention; `platform`, `runtime`; host startup. | Optional annotation candidate | First-class annotation |

### DDD

| Role | Definition; evidence; metadata; example | Tier | v0.10 annotation disposition |
| --- | --- | --- | --- |
| `Entity` | Stable identity; Base/Attr/Ns; `domain`, `boundedContext`; Customer. | Canonical | First-class annotation |
| `AggregateRoot` | Aggregate consistency entry point; Base/Attr; `domain`, `feature`; Order. | Canonical | First-class annotation |
| `ValueObject` | Identity-free value; Base/Attr; `domain`, `boundedContext`; Money. | Canonical | First-class annotation |
| `DomainService` | Stateless domain operation; Base/Attr/Ns; `domain`, `feature`; pricing. | Canonical | First-class annotation |
| `DomainEvent` | Business state-transition fact; Base/Attr/Ns; `domain`, `feature`; OrderSubmitted. | Canonical | First-class annotation |
| `Repository` | Aggregate persistence abstraction; Base/Attr/Ns; `domain`, `adapter`; repository port. | Canonical | First-class annotation |
| `Specification` | Composable domain predicate; Base/Attr/Ns; `domain`, `feature`; eligibility rule. | Optional annotation candidate | First-class annotation |
| `Factory` | Named construction policy; Base/Attr/Ns; `domain`, `feature`; InvoiceFactory. | Optional annotation candidate | First-class annotation |
| `Policy` | Named business decision/rule; Base/Attr/Ns; `domain`, `stability`; CreditPolicy. | Custom-mapping expected | Custom-mapping-only |
| `Saga` | Long-running business process; Base/Attr/Ns; `boundedContext`, `feature`; fulfillment. | Examples-only | Deferred |
| `ProcessManager` | State-bearing process coordinator; Base/Attr/Ns; `boundedContext`, `feature`; shipment workflow. | Examples-only | Deferred |

### CQRS and Event Sourcing

| Role | Definition; evidence; metadata; example | Tier | v0.10 annotation disposition |
| --- | --- | --- | --- |
| `Command` | Request to change state; Base/Attr/Ns; `boundedContext`, `feature`; SubmitOrder. | Canonical | First-class annotation |
| `CommandHandler` | Handles a command; Base/Attr/Ns; `boundedContext`, `direction`; handler. | Canonical | First-class annotation |
| `Query` | Read request; Base/Attr/Ns; `boundedContext`, `feature`; FindInventory. | Canonical | First-class annotation |
| `QueryHandler` | Handles a query; Base/Attr/Ns; `boundedContext`, `direction`; query handler. | Canonical | First-class annotation |
| `Event` | Fact/notification contract; Base/Attr/Ns; `boundedContext`, `direction`; OrderAccepted. | Canonical | First-class annotation |
| `EventHandler` | Reacts to an event; Base/Attr/Ns; `boundedContext`, `direction`; reserve-stock handler. | Canonical | First-class annotation |
| `IntegrationEvent` | Cross-process event contract; Base/Attr/Ns; `platform`, `direction`; PaymentCaptured. | Optional annotation candidate | First-class annotation |
| `Projection` | Event/state to read representation; Base/Attr/Ns; `feature`, `adapter`; sales projection. | Examples-only | Deferred |
| `ReadModel` | Query-optimized representation; Ns/Attr; `boundedContext`, `feature`; inventory read model. | Optional annotation candidate | First-class annotation |
| `EventStore` | Event-stream persistence boundary; Base/Attr/Ns; `adapter`, `subsystem`; event store. | Examples-only | Deferred |
| `Snapshot` | Point-in-time event-sourced state; Ns/Attr/Path; `boundedContext`, `feature`; aggregate snapshot. | Examples-only | Deferred |

### Web/API and desktop/mobile UI

| Role | Definition; evidence; metadata; example | Tier | v0.10 annotation disposition |
| --- | --- | --- | --- |
| `Controller` | Request controller; Base/Ns/Attr; `platform`, `feature`; ASP.NET controller. | Optional annotation candidate | First-class annotation |
| `Endpoint` | Individual request/message endpoint; Base/Ns/Attr; `platform`, `direction`; minimal API. | Optional annotation candidate | First-class annotation |
| `RequestDto` | Boundary input shape; Ns/Attr/Path; `platform`, `feature`; CreateOrderRequest. | Canonical | First-class annotation |
| `ResponseDto` | Boundary output shape; Ns/Attr/Path; `platform`, `feature`; OrderResponse. | Canonical | First-class annotation |
| `ApiContract` | Explicit boundary contract; Base/Ns/Attr; `platform`, `stability`; public API. | Optional annotation candidate | First-class annotation |
| `Middleware` | Ordered pipeline component; Base/Ns/Attr; `platform`, `module`; correlation middleware. | Examples-only | Deferred |
| `Filter` | Boundary filter/enricher; Base/Ns/Attr; `platform`, `feature`; authorization filter. | Custom-mapping expected | Custom-mapping-only |
| `Validator` | Input/domain validation rules; Base/Ns/Attr; `feature`, `boundedContext`; order validator. | Canonical | First-class annotation |
| `Mapper` | Representation conversion; Base/Ns/Attr; `adapter`, `direction`; DTO mapper. | Custom-mapping expected | Custom-mapping-only |
| `View` | Visual surface; Base/Ns/Attr; `platform`, `feature`; WPF/MAUI view. | Optional annotation candidate | First-class annotation |
| `ViewModel` | Presentation state/commands; Base/Ns/Attr; `platform`, `feature`; inventory VM. | Optional annotation candidate | First-class annotation |
| `Presenter` | Application-to-view translator; Base/Ns/Attr; `platform`, `feature`; MVP presenter. | Examples-only | First-class annotation |
| `Model` | UI data representation; Ns/Attr; `platform`, `feature`; screen model. | Custom-mapping expected | Custom-mapping-only |
| `Page` | Navigation-addressable surface; Base/Ns/Attr; `platform`, `feature`; MAUI page. | Examples-only | Deferred |
| `Component` | Reusable UI unit; Ns/Attr; `platform`, `module`; Avalonia component. | Custom-mapping expected | Custom-mapping-only |
| `NavigationService` | UI navigation boundary; Base/Ns/Attr; `platform`, `direction`; mobile navigation. | Examples-only | Deferred |
| `UiService` | UI support service; Base/Ns/Attr; `platform`, `feature`; dialog service. | Custom-mapping expected | Custom-mapping-only |

`View`, `ViewModel`, `Presenter`, `Model`, `Page`, and `Component` span MVVM,
MVP, MVC, WPF, WinUI, Avalonia, and MAUI, but their exact meaning is project-
dependent. Prefer explicit mappings for them.

### Unity/client

| Role | Definition; evidence; metadata; example | Tier | v0.10 annotation disposition |
| --- | --- | --- | --- |
| `UnityRuntime` | Player/runtime code; assembly Attr/Ns; `platform`, `runtime`; gameplay assembly. | Optional annotation candidate | First-class annotation |
| `UnityEditor` | Editor-only tooling; assembly Attr/Ns; `platform`, `runtime`; importer. | Optional annotation candidate | First-class annotation |
| `Feature` | Coherent client feature; Ns/Path/Attr; `feature`, `module`; gameplay feature. | Custom-mapping expected | Custom-mapping-only |
| `System` | Focused client responsibility; Base/Ns/Attr; `feature`, `runtime`; gameplay system. | Custom-mapping expected | Custom-mapping-only |
| `MonoBehaviourAdapter` | Unity component adapter; Base/Ns/Attr; `platform`, `adapter`; scene adapter. | Examples-only | First-class annotation |
| `ScriptableObjectAsset` | Asset-backed configuration/data; Base/Ns/Attr; `platform`, `feature`; balance asset. | Examples-only | First-class annotation |
| `Installer` | Static composition entry point; Ns/Attr/Path; `platform`, `runtime`; client installer. | Examples-only | Deferred |
| `InputAdapter` | Input-to-intent adapter; Base/Ns/Attr; `platform`, `adapter`; controller input. | Optional annotation candidate | First-class annotation |
| `SceneAdapter` | Scene-to-application adapter; Base/Ns/Attr; `platform`, `adapter`; scene boundary. | Examples-only | First-class annotation |

### Infrastructure and cross-cutting

| Role | Definition; evidence; metadata; example | Tier | v0.10 annotation disposition |
| --- | --- | --- | --- |
| `Port` | Explicit inbound/outbound abstraction; Attr/interface/Ns; `domain`, `name`, `direction`; payment port. | Canonical | First-class annotation |
| `Adapter` | Concrete port implementation or boundary translator; Attr/interface/Ns; `domain`, `name`, `port`; Stripe adapter. | Canonical | First-class annotation |
| `PrimaryPort` | Inbound use-case boundary; Attr/interface/Ns; `domain`, `name`; command API. | Optional annotation candidate | Custom-mapping-only |
| `SecondaryPort` | Outbound dependency boundary; Attr/interface/Ns; `domain`, `name`; payment gateway port. | Optional annotation candidate | Custom-mapping-only |
| `AntiCorruptionLayer` | Explicit legacy/modern translation seam; Attr/Ns; `domain`, `adapter`; CRM translator. | Canonical | First-class annotation |
| `ExternalSystem`, `IntegrationAdapter`, `PersistenceAdapter` | Project-specific external and persistence boundary terminology; explicit YAML mapping; `domain`, `adapter`; ERP client. | Custom-mapping expected | Custom-mapping-only |

| Role | Definition; evidence; metadata; example | Tier | v0.10 annotation disposition |
| --- | --- | --- | --- |
| `DbContext` | Database session boundary; Base/Ns/Attr; `subsystem`, `adapter`; EF context. | Optional annotation candidate | First-class annotation |
| `RepositoryImplementation` | Repository port implementation; interface/Ns/Attr; `adapter`, `boundedContext`; SQL repo. | Canonical | First-class annotation |
| `ExternalClient` | External service/SDK client; Base/Ns/Attr; `adapter`, `direction`; payment client. | Canonical | First-class annotation |
| `MessageBusAdapter` | Message transport adapter; Base/Ns/Attr; `adapter`, `direction`; bus adapter. | Optional annotation candidate | First-class annotation |
| `FileSystemAdapter` | File-system boundary; Base/Ns/Attr; `adapter`, `platform`; document adapter. | Optional annotation candidate | First-class annotation |
| `ClockAdapter` | Time boundary; Base/Ns/Attr; `adapter`, `platform`; system clock. | Optional annotation candidate | First-class annotation |
| `TelemetryAdapter` | Telemetry export boundary; Base/Ns/Attr; `adapter`, `subsystem`; metrics exporter. | Examples-only | Deferred |
| `PersistenceModel` | Storage representation; Ns/Attr/Path; `subsystem`, `boundedContext`; row model. | Custom-mapping expected | Custom-mapping-only |
| `Migration` | Schema/data migration unit; Base/Ns/Path; `subsystem`, `boundedContext`; database migration. | Examples-only | Custom-mapping-only |
| `Logging` | Logging boundary/policy; Base/Ns/Attr; `subsystem`, `platform`; logging adapter. | Canonical | Custom-mapping-only |
| `Telemetry` | Metrics/tracing concern; Base/Ns/Attr; `subsystem`, `adapter`; tracing component. | Canonical | Custom-mapping-only |
| `Validation` | Cross-cutting validation concern; Base/Ns/Attr; `feature`, `boundedContext`; shared validation. | Canonical | Custom-mapping-only |
| `Mapping` | Cross-cutting conversion concern; Base/Ns/Attr; `direction`, `adapter`; mapping profile. | Custom-mapping expected | Custom-mapping-only |
| `Serialization` | Wire/storage conversion; Base/Ns/Attr; `platform`, `direction`; JSON adapter. | Optional annotation candidate | Custom-mapping-only |
| `Authorization` | Access-decision boundary; Base/Ns/Attr; `platform`, `feature`; API policy. | Examples-only | Custom-mapping-only |
| `Caching` | Cache boundary/policy; Base/Ns/Attr; `adapter`, `feature`; query cache. | Examples-only | Custom-mapping-only |
| `Configuration` | Configuration boundary; Base/Ns/Attr; `platform`, `runtime`; host configuration. | Optional annotation candidate | Custom-mapping-only |
| `Options` | Typed configuration values; Ns/Attr/Path; `feature`, `runtime`; payment options. | Custom-mapping expected | Custom-mapping-only |
| `ExceptionHandling` | Failure translation/recording; Base/Ns/Attr; `platform`, `feature`; API handler. | Examples-only | Custom-mapping-only |
| `BackgroundJob` | Scheduled/queued work; Base/Ns/Attr; `feature`, `direction`; reconciliation job. | Examples-only | Deferred |

## Metadata vocabulary

Canonical annotations support exactly `domain`, `boundedContext`, `module`,
`feature`, `platform`, and `runtime`, as optional string properties on role
attributes or as matching metadata-only attributes. Other metadata shown here
remains available through user-owned attributes and explicit YAML mappings; it
is not accepted by first-party annotation types.

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

The v0.10 contract approves an optional source-only `ArchLinterNet.Annotations`
NuGet package. It injects internal attribute definitions from NuGet `contentFiles`
for direct references by SDK-style authoring projects; it adds no annotation
runtime assembly, analyzer, or source generator. The package is planned, not
available in released tools. Tool support begins at v0.10.0. The repository
keeps a versioned manifest for its exact FQNs, targets, properties, package,
and generation contract.

Only the 49 first-class role entries in the v0.10 manifest have canonical role
attribute identities; each uses the FQN recorded in `roles[]`, following
`ArchLinterNet.Annotations.<FirstClassRole>Attribute`. Custom-mapping-only and
deferred catalog roles do not imply a built-in role FQN. For example, the
canonical form is `[AggregateRoot]` with optional named properties such as
`Domain = "Sales"` or `BoundedContext = "Orders"`. Role properties are
`Domain`, `BoundedContext`, `Module`, `Feature`, `Platform`, and `Runtime`;
values are exact non-empty strings. Metadata-only attributes (`Domain`,
`BoundedContext`, `Module`, `Feature`, `Platform`, and `Runtime`) take one
string value and support type or assembly scope. The metadata-only
`FeatureAttribute` supplies the `feature` context key; catalog role `Feature`
remains custom-mapping-only and has no role attribute FQN. `DomainLayer`,
`ApplicationLayer`, `InfrastructureLayer`, `PresentationLayer`, `ApiLayer`,
`PersistenceLayer`, `IntegrationLayer`, `SharedKernel`, `UnityRuntime`, and
`UnityEditor` are assembly-only; `CompositionRoot` supports type or assembly
scope; other first-class roles are type-only. Each package version's assembly
identity marker records both catalog generation and its exact package SemVer,
so annotation-aware tools can check compatibility from the target assembly
alone.

The package does not make adoption mandatory. Existing and future projects may
define user-owned attributes and map them by exact full type name:

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

The defined custom mapping extraction forms are `constructor[N]`,
`property:Name`, `const:Full.Type.NAME`, and literal scalar values. A same-simple-
name attribute in another namespace is not canonical. Exact YAML mappings for
user-owned attributes remain a supported path; YAML cannot remap a reserved
canonical FQN.

`ApiContract` expresses a type's primary semantic role only. Reviewed API
membership remains an independent policy selector decision under the #525
public API surface contract. An annotation by itself grants no dependency
permission or other policy authority.

A role-bearing assembly annotation contributes only at assembly source
precedence. The six metadata-only assembly attributes can provide defaults to
types that already have a higher-precedence role. Type-scope values override
assembly defaults; same-scope disagreement omits the affected metadata key and
is reported as a conflict. Metadata from a losing assembly role is not merged
into a winning type role.

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
- Respect the fixed source precedence. Legacy custom-only YAML mapping entries retain their reviewed first-declared rule; canonical role conflicts fail closed without ordering.
- Treat `Common`, `Model`, `Component`, `System`, and `Policy` as contextual unless explicitly mapped.
- Use exact role/metadata criteria. Do not write always-true selectors, broad exclusions, or policy generation that weakens reviewed YAML.
- Keep `reason` on broad overrides and every exclusion; explain the architectural intent.
- A conflict or unresolved evidence is a reviewable fact, not permission to guess.

The catalog does not add runtime dependencies, execute annotations, validate DI
registration, validate framework behavior, run plugins, or replace
project-specific YAML. Released tools through v0.9.1 do not recognize the
planned canonical annotation FQNs. Starting with the v0.10.0 annotation-aware
tool, the optional source-only package contributes static role/context
evidence; policy remains YAML-authored and YAML-first.
