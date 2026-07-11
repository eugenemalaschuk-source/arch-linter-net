## Context

The semantic role catalog originally described `[assembly: BoundedContext("Billing")]` as a shared assembly context. The approved semantic-classification model requires every assembly-attribute mapping to carry a role and assigns one winning source's role and metadata to a type; it does not merge metadata from a lower-precedence source. The example therefore promised unsupported behavior.

## Goals / Non-Goals

**Goals:**

- Align every catalog example with the current classification model and schema.
- Explain that catalog roles are alternative classifications, not composable tags.
- Preserve role-bearing assembly classification as a documented, static use case.

**Non-Goals:**

- No metadata-only assembly mapping, cross-source metadata merging, new source tier, schema change, or extraction implementation.

## Decisions

### Defer metadata-only assembly context

Replace the `BoundedContext` assembly example with an assembly attribute that assigns both a role and any mapped metadata, such as `SharedKernel`. Its metadata applies only when the assembly-attribute source wins for a type. Metadata-only assembly context is explicitly deferred to a separate semantic-classification-model change that defines schema and merge behavior.

### State single-role semantics directly

The catalog will state that classification yields one role and the winning source's metadata. Authors who need a layer and domain context use metadata or existing namespace layers; they must not expect `DomainLayer` plus `AggregateRoot`, or `PresentationLayer` plus `Controller`, to accumulate on a single type.

### Keep unsupported evidence as future guidance

`asmdef` and package references remain useful discovery context, but are not among the approved six classification sources. The catalog will label them future evidence until a separate model decision adopts them.

## Risks / Trade-offs

- [Risk] Deferring metadata-only assembly context narrows the original example. → Mitigation: retain a valid role-bearing assembly use case and make the required future work explicit.
- [Risk] Single-role semantics can be less expressive than tagging. → Mitigation: use exact metadata and namespace layers today; introduce merging only through a reviewed model change.
