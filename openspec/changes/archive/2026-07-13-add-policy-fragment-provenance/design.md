## Context

The import resolver already assigns graph roles and deterministic portable source
identities, and the composer already sees every source YAML node before it serializes
one effective document. That information is currently discarded at the serialization
boundary. Downstream validators and checkers therefore receive only POCOs and cannot
identify the fragment that owns a bad layer, analysis setting, contract, or ignored
violation. The change must preserve existing monolithic-policy behavior, the one-path
loader API, family-registry boundaries, and additive JSON/SARIF compatibility.

## Goals / Non-Goals

**Goals:**

- Preserve graph-derived source identity, role, authored import edge, import chain,
  source order, YAML path, contract family, and contract ID in typed records.
- Keep one provenance index on the resolved document and bind it to deserialized
  object identities without adding filesystem properties to every family DTO.
- Produce typed import/validation diagnostics with a primary policy location and
  related locations, including both declarations for conflicts.
- Enrich validation, policy-consistency, ignored-violation, human, CI JSON, and SARIF
  output through shared model/formatter seams.
- Preserve current exception messages/types and serialized fields for monolithic
  policies where consumers already depend on them; new fields are additive.

**Non-Goals:**

- Changing import grammar, graph limits, composition order, or conflict semantics.
- Introducing filename-based roles, multiple roots, remote imports, or templates.
- Adding source fields to every contract-family POCO or creating a new adapter layer.
- Redesigning source-code diagnostic locations or SARIF result scope.

## Decisions

### Build provenance while composing YAML nodes

The resolver will enrich each parsed source with a typed descriptor containing the
explicit root identity, canonical portable source path, graph role, authored import
edge, import chain, and source ordinal. The composer will return a composition result
containing both effective YAML and a typed map from effective YAML paths to original
source/YAML locations. It will register mapping values, list entries, scalar settings,
contracts, and nested ignored violations while it copies nodes.

This uses information already available at the only lossless boundary. Reconstructing
origins after serialization was rejected because repeated list indices and merged maps
no longer reveal which source owned them. Storing paths on every DTO was rejected
because it couples every current and future contract family to the filesystem.

### Bind a typed side index after deserialization

`ArchitectureContractDocument` will expose one YAML-ignored provenance index. The index
publishes typed source descriptors and node locations, and internally binds effective
paths to deserialized layers, contract objects, and ignored-violation objects using
reference identity. Contract list discovery uses existing YAML aliases/registry-shaped
enumeration rather than a family switch, so a new family does not require provenance
or formatter changes.

Monolithic policies receive an equivalent root-only index built directly from their raw
YAML without routing them through import-only role/schema checks. This preserves the
legacy permissive loading path while giving all consumers a common model.

### Use typed diagnostic locations at shared boundaries

Import and composed-schema failures will carry an `ArchitecturePolicyDiagnostic` with
one primary location, zero or more related locations, and an import chain. Existing
stable import categories remain the programmatic failure discriminator. Post-
deserialization semantic validators will track the typed object currently being
validated; imported-policy failures are wrapped with its bound location while the
original error text remains intact.

Runtime `ArchitectureViolation`, `ArchitectureDiagnostic`, policy-consistency, and
unmatched-ignore models gain common typed policy-origin properties. Configuration and
contract execution are enriched centrally from the document index, avoiding edits to
every checker or family payload. This is common metadata, not a family-specific
nullable property bag, and remains consistent with the typed-payload direction from
issue #214.

### Render policy origin additively

Human output appends a compact `policy: path:yaml.path` suffix and includes related
origins when present. CI JSON adds typed `policy_location` and
`related_policy_locations` objects. SARIF retains existing physical/logical code
locations and adds policy definitions as `relatedLocations` with portable artifact
URIs and YAML-path messages. Existing field names, diagnostic categories, ordering
keys, and SARIF rule IDs remain unchanged.

### Keep all entry points on the existing resolved model

CLI, Testing, graph, explain, baseline, and application services continue to call
`IArchitecturePolicyDocumentLoader.Load(string)`. Enrichment happens in the loader,
analysis session, executor, and shared formatters, so adapters do not learn about the
import graph and cannot diverge in role or path handling.

## Risks / Trade-offs

- [Effective and original list indices can diverge] -> Register both paths at copy time
  and bind objects only after final ordering is known.
- [Validator exceptions historically carry only text] -> Track the current typed owner
  during each existing validator loop and wrap only imported-policy failures; do not
  parse error messages to infer ownership.
- [Monolithic behavior could change if routed through import schemas] -> Build a
  root-only provenance index without applying import-only parsing/composition stages.
- [Absolute paths could leak through diagnostics] -> Public descriptors expose only
  canonical portable paths; absolute/physical paths stay internal to resolution.
- [Additive metadata could destabilize ordering] -> Preserve existing sort keys and
  order related policy locations by composed source ordinal and YAML path.

## Migration Plan

No policy or API migration is required. The new document and diagnostic properties are
additive and YAML-ignored. Rollback consists of removing the side index and additive
formatter fields; policy files and existing output fields remain valid.

## Open Questions

None. The source-of-truth issue and #280/#281 design settle roles, paths, composition,
and compatibility constraints.
