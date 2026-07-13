# Policy Import Architecture and Implementation Reference

This note translates issue #280 and the `policy-import-composition` OpenSpec
capability into implementation boundaries for #281 and #282. Runtime graph
resolution, composition, and schemas are implemented by #281; provenance-rich
downstream diagnostics remain assigned to #282.

## Scope

#281 owns local graph resolution, role-aware parsing, deterministic composition,
root and fragment schemas, and behavior/regression tests. #282 owns provenance
retention and its use in configuration diagnostics and machine-readable output.

The design does not add multiple roots, remote or package resolution, globs,
directory scanning, optional imports, overrides, templates, interpolation,
scripting, cross-file anchors, or baseline imports. It does not redesign the
CLI, Testing adapter, contract family DTOs, validation pipeline, or execution
catalog.

## Pipeline

```text
one explicit policy path
        |
        v
repository boundary (resolved once)
        |
        v
import resolver ----- canonical identity / stack / limits
        |
        v
role-aware parser --- root or fragment shape + explicit-field presence
        |
        v
composer ------------ maps / lists / singletons / family registry
        |
        v
ArchitectureContractDocument + provenance index
        |
        v
effective-policy schema -> fallback IDs -> validator pipeline -> execution
```

`IArchitecturePolicyDocumentLoader.Load(string policyPath)` remains the
orchestration seam. CLI, Testing, validation, graph, explain, baseline, and
asmdef-facing callers must continue to pass one path and receive one effective
document. No adapter should learn about the import graph.

## Resolver boundary

The resolver receives the explicit root path and the repository boundary
resolved for that run. It returns parsed sources in depth-first pre-order. It
owns only filesystem and graph concerns:

- relative path resolution from the declaring document;
- rejection of absolute, URI-like, glob, interpolation, and invalid entries;
- exact-case verification;
- physical canonicalization through links and junctions;
- repository-boundary enforcement;
- active-stack cycle detection;
- completed-file and portable case-insensitive duplicate detection;
- the depth-16 and file-count-256 limits;
- first-error traversal order.

The current `IArchitectureFileSystem` supports reads and existence checks but
does not expose exact directory entry casing, regular-file identity, link
resolution, or physical canonicalization. #281 adds a minimal fakeable seam
for those concrete safety requirements. Extending the existing filesystem seam
or adding a focused path-identity collaborator is justified only by those four
missing operations; it must not become a general virtual filesystem or import
provider extension point.

The resolver must not parse contract families or apply merge rules. It must not
enumerate directories because v1 accepts explicit paths only.

## Parser boundary

Each source is parsed independently with a graph-assigned role. The parser must
preserve two things ordinary POCO deserialization loses:

1. whether a scalar/singleton field was explicitly present or merely received a
   CLR default;
1. the YAML path for every contributed node.

A focused parsed-source model can hold the deserialized
`ArchitectureContractDocument`, its role and source descriptor, and an
explicit-path/provenance index derived from `YamlStream`. This avoids adding
filesystem properties to every contract DTO and works with the loader's current
raw-YAML validation approach.

Shape validation runs before composition. The root schema requires identity;
it records whether each field was explicit but does not require sections a
fragment can contribute. The fragment schema rejects root-only and unknown
fields. Family-specific semantic validators do not run per fragment because
they need the complete definition graph.

The parser also retains raw `classification.path`, `classification.overrides`,
and `classification.exclusions` nodes. These fields are intentionally absent
from the bound C# classification model, so ordinary DTO deserialization alone
would lose `path`'s current deferred-support signal.

## Composer boundary

The composer receives the ordered parsed sources and creates one
`ArchitectureContractDocument`:

- union keyed maps and fail on a repeated key;
- append ordered lists without sorting or de-duplication;
- use explicit-field presence to fail repeated singletons before CLR defaults
  are applied;
- copy each contract and its nested ignored violations as one atomic node;
- assign defaults only after all explicit values are composed.

For classification, append the model-bound lists and singleton-check
`precedence`. Retain raw `path`, `overrides`, and `exclusions` entries with
provenance and source order. Aggregate all raw `path` entries into the one
`ClassificationPathDeferred` notice; preserve `overrides` and `exclusions` as
deferred no-ops rather than silently dropping them.

Contract family composition must enumerate
`ArchitectureContractFamilyBindings.All`. A new contract family already adds
one binding under `Contracts/Families`; it must automatically participate in
strict/audit list composition. Do not add a new family switch to the loader or
composer. This preserves the #210 validator pipeline and #216 family-owned DTO
model.

Fallback IDs are assigned only after composition. The existing
`DuplicateIdValidator` then checks every binding and preserves its established
key: family plus strict/audit mode, compared case-insensitively. A duplicate
across fragments in one group fails; the same ID in another family or mode
remains compatible.

## Provenance boundary

Each source descriptor contains:

- the explicit root identity;
- canonical repository-relative source path using `/`;
- original authored import path and declaring source, when imported;
- YAML path;
- composed source ordinal.

#282 should attach provenance through a side index or focused composition
wrapper, not new filesystem fields on every family POCO. Object identity can
address mapping/list nodes; explicit owner-plus-field or composed YAML paths
can address scalar settings. The chosen model must survive fallback ID
assignment and validator execution.

Conflict errors are produced by the composer and include both descriptors.
Existing family validators continue to own semantic validation; their thrown
configuration errors need a provenance-aware mapping boundary so the final
diagnostic includes the invalid node's source. Checkers still produce structured
violations only and must not call filesystem or formatter adapters.

Machine-readable provenance should use canonical repository-relative paths,
not host-absolute paths, to keep output reproducible. Ordering follows composed
source ordinal and existing within-family order.

## Schema boundary

#281 publishes a root schema and a fragment schema from shared definitions:

- root source: required identity and optional policy sections plus `imports`;
- fragment: only mergeable sections plus `imports`, with at least one useful
  contribution;
- both: closed top-level property sets and the same contract-family definitions.

After composition, #281 validates against the full effective-policy schema,
which requires `version`, `name`, `layers`, `analysis`, and `contracts` exactly
as the current production schema does. The effective schema is not substituted
for either source-role schema.

Runtime chooses the schema from graph role. Editor documentation may show an
inline YAML language-server schema directive. Filename associations are optional
convenience only and must not be used by runtime tests.

The production root schema now accepts `imports`, and the fragment schema is
published separately for explicit editor selection.

## Validation order

The implementation should preserve this observable order:

1. Validate an import entry against the portable grammar before resolving its
   target.
1. Validate canonical identity, boundary, duplicate/cycle state, and graph
   limits before reading the next file.
1. Parse and validate the source role/shape.
1. Compose in depth-first pre-order and fail on the first composition conflict.
1. Validate the composed effective document against the full policy schema.
1. Assign fallback contract IDs.
1. Run the existing `ArchitecturePolicyDocumentValidatorPipeline` once in its
   current fixed order.
1. Continue through existing setup and execution services.

This order prevents a later semantic error from hiding an earlier deterministic
graph or composition error and preserves current first-failure behavior after a
document is composed.

## Test matrix

Tests should use the existing fake-infrastructure style for resolver/composer
units and real-file integration fixtures for loader and application behavior.
The candidate YAML files under `policy-import-examples` can seed the integration
fixtures after runtime support exists.

| Owner | Area | Case | Required assertion |
| --- | --- | --- | --- |
| #281 | Compatibility | Existing monolithic policy at its current name | Effective document and validation results are unchanged. |
| #281 | Compatibility | Monolithic policy with arbitrary filename | Filename does not change loading or execution. |
| #281 | Entry model | CLI, Testing adapter, validation, graph, explain | Each continues to accept exactly one root path. |
| #281 | Naming | `arch.yml` plus `*.arch.yml` | Recommended names work without special behavior. |
| #281 | Naming | Arbitrary root, fragment name, and extension | Results equal the recommended-name graph. |
| #281 | Root | Root with inline sections and imports | Both contribute to one document. |
| #281 | Validation phases | Root omits sections supplied by fragments | Source shape passes; full effective schema passes before IDs/validators. |
| #281 | Validation phases | Entire graph omits layers, analysis, or contracts | Full effective schema fails before IDs/validators. |
| #281 | Fragment shape | Every allowed top-level section | Each is accepted independently in a fragment. |
| #281 | Fragment shape | Empty fragment | Rejected unless `imports` is non-empty. |
| #281 | Fragment shape | `version`, `name`, baseline key, unknown key | Rejected with role, field, and source. |
| #281 | Import syntax | Empty, null, mapping, absolute, URI, glob, environment expression | Rejected before target read. |
| #281 | Import grammar | `/`-separated NFC relative path | Has the same segments on Windows, Linux, and macOS. |
| #281 | Import grammar | Backslash, drive, UNC/device, leading slash, empty segment, invalid character, or interpolation token | Rejected before host path APIs resolve it. |
| #281 | Import grammar | `NUL.yml`, `COM1.arch.yml`, `LPT¹.yaml`, and `NUL.tar.gz` | Each is rejected as a reserved basename before filesystem resolution on Windows, Linux, and macOS. |
| #281 | Resolution | Import relative to root | Resolves from root directory. |
| #281 | Resolution | Nested import relative to fragment | Resolves from fragment directory, not root directory. |
| #281 | Ordering | Root imports A/B and A imports C/D | Source order is root, A, C, D, B. |
| #281 | Ordering | YAML `imports` key moved within document | Resulting order is unchanged. |
| #281 | Ordering | Files created/enumerated in different order | Resulting order is unchanged. |
| #281 | Keyed maps | Unique layers, external groups, packages, condition sets | Union contains every key. |
| #281 | Keyed maps | Repeated identical or different key, including root versus fragment | Fails and never applies precedence. |
| #281 | Lists | Legacy layers and every analysis list | Values append in composed source order. |
| #281 | Lists | Classification mapping lists | Authored mapping order is preserved. |
| #281 | Contracts | Representative strict/audit family lists | Entries append through registry bindings. |
| #281 | Contracts | A newly registered test family | Composer requires no family-specific branch. |
| #281 | Contracts | Ignored violations in imported contract | Nested list remains attached and ordered. |
| #281 | Contract IDs | Fallback IDs across fragments | IDs are assigned after composition. |
| #281 | Contract IDs | Same case-insensitive ID in one family/mode | Rejected after composition in that family/mode group. |
| #281 | Contract IDs | Same ID in different family or mode | Accepted for compatibility. |
| #281 | Singletons | One explicit scalar in any source | Value is retained. |
| #281 | Singletons | Equal or different repeated scalar | Rejected with both sources. |
| #281 | Defaults | Singleton omitted everywhere | Existing CLR/product default applies after composition. |
| #281 | Paths | `.` and `..` remain physically in boundary | Canonical in-bound file loads. |
| #281 | Paths | Lexical escape | Rejected before read. |
| #281 | Paths | Symlink/junction escape | Rejected after physical canonicalization. |
| #281 | Paths | Directory, missing file, or non-regular file | Deterministic configuration error. |
| #281 | Portability | Authored case mismatch on case-insensitive filesystem | Rejected with expected on-disk casing. |
| #281 | Portability | Two Linux files differ only by case | Portable identity collision is rejected. |
| #281 | Duplicate | Same file via normalized, link, or case alias | Rejected; file is not silently de-duplicated. |
| #281 | Cycle | Self-cycle and multi-node cycle | Ordered active-stack chain is reported. |
| #281 | Limits | Exactly depth 16 and exactly 256 files | Accepted. |
| #281 | Limits | Depth 17 and file 257 | Rejected before reading over-limit target. |
| #281 | Schema | Root schema with imports and arbitrary filename | Accepted. |
| #281 | Schema | Fragment schema with every allowed section | Accepted. |
| #281 | Schema | Root-only or unknown fragment field | Rejected. |
| #281 | Schema | Explicit editor schema directive | Works without filename association. |
| #281 | CEL handoff | Imported node contains approved expression field | Value reaches normal CEL validation; no import capabilities exist. |
| #281 | Baseline | Policy import resembles or points to baseline content | Rejected as fragment shape; baseline workflow remains separate. |
| #282 | Provenance | Keyed and list nodes from each source | Root, canonical source, YAML path, and ordinal are retained. |
| #282 | Provenance | Map or singleton conflict | Diagnostic includes both source descriptors. |
| #282 | Provenance | Existing family validator rejects imported node | Final diagnostic identifies fragment and YAML path. |
| #282 | Provenance | Duplicate contract ID across fragments | Diagnostic identifies both contracts. |
| #282 | Classification raw metadata | Root plus fragments declare `classification.path` | Deferred notice count is aggregated and sources remain available. |
| #282 | Classification raw metadata | Fragments declare `overrides` or `exclusions` | Entries retain provenance/order and remain deferred no-ops. |
| #282 | Output | Human, JSON, and SARIF where applicable | Paths are canonical repository-relative values. |
| #282 | Output | Same graph on Windows and Linux | Diagnostic ordering and portable paths are identical. |
| #282 | Integration | CLI and Testing adapter imported-policy failure | Both expose the same origin details and effective behavior. |

## Handoff acceptance

#281 is complete only when import and monolithic policies produce the same
effective model for equivalent content, all schema and graph-safety cases pass,
and the repository acceptance gate is green. #282 is complete only when every
composed configuration node needed by diagnostics has provenance, supported
outputs expose portable paths, ordering is deterministic, and acceptance remains
green.
