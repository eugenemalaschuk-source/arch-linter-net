# Layout Convention Contracts

Layout convention contracts select source files by folder segment, namespace segment, and file-name prefix/suffix, then validate the declared types in each matched file against type-kind, naming, file/type-name-matching, and matching-interface-counterpart expectations. This governs *layout conventions* — Services folders containing concrete service classes, Interfaces folders containing only interfaces, a file's primary type matching its file name — not dependency direction, which the [dependency](dependency.md), [allow-only](allow-only.md), and [layer order](layers.md) families already cover.

Groups:

- `strict_layout_conventions`
- `audit_layout_conventions`

## Example

```yaml
contracts:
  strict_layout_conventions:
    - id: services-are-concrete-classes
      name: services-folder-must-contain-concrete-services
      files_matching:
        folder_segment: Services
      require_type_kind: class
      required_name_suffix: Service
      require_matching_interface:
        name_prefix: I
      reason: Every service class needs a matching interface, and the Services folder must not accumulate misplaced interfaces.

  audit_layout_conventions:
    - id: interfaces-are-interfaces
      name: interfaces-folder-must-not-contain-concrete-types
      files_matching:
        folder_segment: Interfaces
      forbid_type_kind: class
```

## When to use

Use layout convention contracts when a rule is about *where a file lives and what it declares*, derived from deterministic source-file and declared-type facts, not from a dependency edge:

- Application Services files must contain concrete service types;
- Application Interfaces files must contain service interfaces, not implementations;
- interface namespaces/folders must not contain concrete implementation classes;
- services namespaces/folders must not contain interfaces;
- a service class should have a matching `I`-prefixed interface somewhere in the codebase;
- a file's primary declared type should match its file name.

## Semantics

### Selecting files

`files_matching` selects candidate source files using only these fields, all optional, combined with **AND** semantics (an unset/empty field is not applied):

- `folder_segment` — the file's path (relative to the repository root) must contain this exact folder segment.
- `namespace_segment` — the declared type's namespace must contain this exact dot-separated segment.
- `file_name_suffix` / `file_name_prefix` — match the file name (without extension), ordinal.

There is no regex or expression-language selector on these fields. At least one must be populated, or policy loading fails — an unselective matcher would otherwise match every source file.

Folder-segment and file-name selector fields require deterministic source-file data (see [Source facts and unavailable data](#source-facts-and-unavailable-data) below); a declared type with no resolved source file can never match those fields. `namespace_segment` still works from reflection-derived namespace facts even without source enrichment.

### An optional CEL refinement

`files_matching.when` is an optional [ArchLinter CEL Profile v1](../reference/yaml-schema.md) boolean predicate that further narrows *which declared types* in an already-selected file are checked, evaluated against the same closed `subject` context every selector-backed `when` uses (`subject.role`, `subject.kind`, `subject.sourcePaths`, `subject.sourceDirectoryPrefixes`, etc.):

```yaml
files_matching:
  folder_segment: Services
  when: subject.role == 'ApplicationService'
```

`when` is compiled at policy-load time and fails the load on any compile diagnostic, exactly like every other approved `when` location in this tool.

### Expectations

All optional; declare at least one, or policy loading fails as a configuration error:

- `require_type_kind` / `forbid_type_kind` — the matched file's declared types must (or must not) include a type of this kind (`class`, `interface`, `struct`, `enum`, `record`, `delegate`).
- `required_name_suffix` / `required_name_prefix` / `forbidden_name_suffix` / `forbidden_name_prefix` — check each matched declared type's simple name, same semantics as [type placement](type-placement.md).
- `require_type_name_matches_file_name` — the matched file must declare at least one type whose simple name equals the file name (without extension).
- `require_matching_interface` — every matched concrete class must have a corresponding interface (`name_prefix` + class name, default prefix `I`) declared somewhere in the analyzed source. Ambiguous candidates (more than one interface with the expected name) are reported as unresolved rather than picked implicitly.

### Source facts and unavailable data

Layout convention contracts read from the same deterministic source-file and declared-type fact index as [type placement](type-placement.md)'s namespace resolution. If a contract is declared but the run has **no** source-enriched declared-type facts at all (for example, `analysis.source_roots` is not configured), validation emits one explicit diagnostic explaining that path-based layout checks are unavailable for this run, instead of silently reporting zero violations.

### Violations and ignored violations

Diagnostics identify the matched file, the contract, and whichever of expected/actual type kind, expected/actual name, file/type-name mismatch, or expected/actual counterpart applied. `ignored_violations` entries use the same `source_type`/`forbidden_reference`/`reason` shape as other contract families.

## Scope: what's not covered here

- No regex or expression-language selectors beyond the bounded `when` refinement described above.
- No runtime dependency-injection resolution.
- No standalone "unmapped folder" discovery that scans the whole repository for folders not covered by any configured contract — declare `audit_layout_conventions` entries for the folders you want drift-checked.
- No configurable counterpart naming beyond a single prefix — `require_matching_interface` supports `name_prefix` only.
