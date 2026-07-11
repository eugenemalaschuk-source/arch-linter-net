## Context

`openspec/specs/semantic-classification-model/spec.md` already fixes the YAML schema and vocabulary for `classification.attributes`/`classification.assembly_attributes`, the four-form metadata extraction syntax, canonicalization domains, precedence order, and conflict-resolution rules. No C# binding or extraction logic exists yet (confirmed: no `Classification` property on `ArchitectureContractDocument`, no extraction/reader types anywhere in `src/`). `ArchLinterNet.Core/Scanning/ArchitectureAttributeUsageScanner.cs` and `ArchitectureTypeScanner.cs` establish the existing reflection-safety conventions (catch `TypeLoadException`/`FileNotFoundException`/`CustomAttributeFormatException` around every reflection call, never let a malformed assembly crash a scan) that new code follows.

## Goals / Non-Goals

**Goals:**
- Bind `classification.attributes` and `classification.assembly_attributes` from YAML into typed C# models on `ArchitectureContractDocument`.
- Extract role/metadata from type-level and assembly-level attributes matched by full type name, with no dependency on any ArchLinterNet-provided attribute package.
- Implement the four fixed metadata-extraction forms and the three canonical value domains exactly as specified.
- Implement `type_attribute > assembly_attribute` precedence, same-tier declaration-order conflict resolution, repeated-instance metadata-order resolution, and uniform evidence-extraction-failure handling.
- Produce a queryable per-type classification result (role + metadata + recorded conflict/failure facts) that later work (#110+, #111/#112) can consume without reshaping.

**Non-Goals:**
- `inheritance`, `namespace`, `path` classification sources (later issues).
- `classification.overrides`, `classification.exclusions`, `layers.<name>.selector` matching/consumption (later issues; schema-valid no-ops for now).
- `yaml_override` source (depends on `overrides`, out of scope here).
- Any new ArchLinterNet-provided annotation/attribute package (decided against in #108).
- Wiring classification results into any contract family's pass/fail evaluation — this issue only produces facts, per the reviewed design's "classification produces facts consumed by other constructs" framing.

## Decisions

**Model shape.** Add `ArchitectureClassificationConfiguration` with `Attributes: List<ArchitectureAttributeClassificationMapping>` and `AssemblyAttributes: List<ArchitectureAttributeClassificationMapping>`, wired as `ArchitectureContractDocument.Classification` (`[YamlMember(Alias = "classification")]`, defaulting to an empty configuration so existing policies are unaffected). `ArchitectureAttributeClassificationMapping` carries `Attribute` (full type name string), `Role` (string), and `Metadata` (`Dictionary<string, string>` of raw, unparsed extraction expressions — parsing into one of the four forms happens at extraction time, not load time, keeping the model a thin YAML mirror like every other contract model in this file).
Alternative considered: parse metadata expressions into a discriminated form at load time. Rejected — every other model in `ArchitectureContractModels.cs` is a plain YAML mirror; expression parsing belongs in the extraction engine alongside the reflection work it serves, consistent with how `ArchitectureAttributeUsageScanner` takes raw string lists rather than pre-parsed matchers.

**Extraction engine placement and shape.** Add `ArchitectureAttributeRoleExtractor` (or similarly named) under `src/ArchLinterNet.Core/Scanning/`, taking a resolved `ArchitectureClassificationConfiguration`, a scanned `Type`, and its declaring `Assembly`, and returning a per-type result: assigned `Role` (nullable — no match is not an error), canonicalized `Metadata` dictionary, and lists of recorded `conflict`/evidence-extraction-failure facts for diagnostics. It reuses `Type.GetCustomAttributesData()` / `Assembly.GetCustomAttributesData()` and the same defensive try/catch envelope (`TypeLoadException`, `FileNotFoundException`, `CustomAttributeFormatException`) as `ArchitectureAttributeUsageScanner`.

**Matching mechanics.** For each mapping list (`Attributes`, `AssemblyAttributes`), iterate `CustomAttributeData` entries against every configured mapping in declaration order, matching `AttributeType` full name (via the existing `ArchitectureTypeNames.SafeFullName` helper) against `Attribute`. Multiple `CustomAttributeData` instances of one matched attribute type are grouped and resolved by their position in the `CustomAttributeData` list (spec: "metadata order", i.e. `GetCustomAttributesData()` return order, which reflects declaration order on the member) — first instance's extracted role/metadata wins; instances are compared for full role+metadata equality before being flagged as a conflict, per spec.

**Metadata extraction dispatch.** A single function inspects each `metadata.<key>` string prefix in the fixed order (`constructor[`, `property:`, `const:`, else literal) and dispatches to:
- `constructor[<index>]` → index into `CustomAttributeData.ConstructorArguments` (already compiler-resolved, defaults included, per spec).
- `property:<Name>` → look up `<Name>` only in `CustomAttributeData.NamedArguments` (never the attribute type's declared properties/defaults, per spec — this is why extraction reads `CustomAttributeData` rather than instantiating the attribute).
- `const:<Full.Type.NAME>` → resolve the named field via `Type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)` and require `FieldInfo.IsLiteral && !FieldInfo.IsInitOnly` (the CLR's own definition of a compile-time `const`, which excludes `static readonly`); read via `GetRawConstantValue()`.
- literal → the YAML scalar string itself.
Each path funnels its raw CLR/YAML value through one canonicalization function producing `string | bool | decimal | null` (null = evidence-extraction failure), per the fixed three-domain rule (enum → declared member name, ambiguous-alias enum → failure, `System.Type` → `FullName`, every numeric primitive → `decimal`, unsupported shapes → failure).

**Precedence and conflicts as data, not exceptions.** Every conflict and evidence-extraction failure is recorded on the result object, never thrown — matching the spec's "role assignment... SHALL still proceed unaffected" and giving later diagnostics work (not in this issue's scope) a structured fact list to report from, rather than requiring a second pass later to add error handling.
Alternative considered: throw on conflict/failure and let callers catch. Rejected — spec explicitly requires role assignment to proceed past a metadata failure, which a throw-based model cannot express without exception-driven control flow for an expected, common case.

**Assembly-level scope.** `assembly_attributes` are extracted once per `Assembly` (not per type) and looked up per-type by the type's declaring assembly when resolving the two-source precedence, avoiding redundant reflection work across every type in a large assembly.

## Risks / Trade-offs

- **Reflection over untrusted/third-party assemblies** could throw unexpected exception types beyond the three already guarded. Mitigation: mirror `ArchitectureAttributeUsageScanner`'s catch set exactly; if acceptance testing surfaces a new exception type from real fixtures, add it to the same guarded set rather than a broad `catch (Exception)`.
- **`const:` resolution ambiguity** (nested types, non-public fields across assembly boundaries) could silently resolve the wrong field if `Full.Type.NAME` parsing is naive. Mitigation: split strictly on the last `.` for the field name and resolve the type prefix via `Type.GetType`/assembly scan consistent with how other full-type-name lookups in this codebase resolve types; treat any resolution ambiguity as an evidence-extraction failure per spec rather than guessing.
- **Enum alias detection** requires enumerating all declared members of the enum type to check for duplicate underlying values, which is more work than `Enum.GetName`. Mitigation: this is a per-canonicalization-call cost bounded by the enum's own member count, negligible versus the surrounding reflection scan.

## Migration Plan

Additive only: no existing YAML field or model changes shape. Policies without a `classification` section are unaffected (default empty configuration). No rollback concerns beyond reverting the change.

## Open Questions

None — the referenced spec fixes every behavioral question this issue's scope touches; anything not fixed there (diagnostics surface/CLI output format) is deferred as out of scope, matching the issue's "produces facts" framing.
