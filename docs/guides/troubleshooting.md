# Troubleshooting

Use this page when a validation run fails unexpectedly or produces no useful diagnostics.

## Exit code 2

Exit code `2` means validation could not complete normally.

Check:

- command-line arguments;
- policy path;
- YAML syntax;
- unknown contract IDs passed to `--contract`;
- unknown condition set passed to `--condition-set`;
- missing target assemblies;
- baseline entries that reference deleted contract IDs.

## Policy import failures

Import diagnostics include the selected root, the source that declared the
edge or conflicting value, its YAML path, and a concise import chain when
applicable.

- **Missing file or path-case mismatch:** resolve the path from the declaring
  document, use `/`, and match the on-disk casing exactly.
- **Cycle or duplicate import:** remove the reported back-edge or repeated
  canonical path. ArchLinterNet does not silently de-duplicate imports.
- **Composition conflict:** keep only one declaration of the keyed definition,
  singleton setting, or family/mode contract ID. Import order never provides
  override precedence.
- **Repository-boundary violation:** keep fragments inside the repository and
  avoid symlink or junction escapes.
- **Invalid fragment shape:** remove root-only `version`/`name`, baseline or
  unknown fields, and provide a mergeable section or non-empty nested import.
- **Editor reports root fields missing:** associate the imported file with
  `schema/dependencies.arch.fragment.schema.json`, regardless of its name.

See [Policy imports](../policy-format/imports.md#troubleshooting) for the full
path, schema, migration, and unsupported-behavior guide.

## Empty layer diagnostics

A layer with no matching project types may indicate:

- typo in the namespace;
- target assembly not loaded;
- missing assembly search path;
- layer represents an external SDK that should use `external: true` or `external_dependencies`.

For vendor/framework surfaces, prefer [external dependencies](../policy-format/external-dependencies.md).

## Policy passes but should fail

Check for false confidence:

- Does the layer namespace match real code?
- Are target assemblies configured and loadable?
- Is the relevant rule in `audit` instead of `strict`?
- Is a broad `ignored_violations` entry hiding the violation?
- Is the dependency only visible in a method body that requires source roots or condition sets?
- Did the YAML use unsupported fields that the runtime does not enforce?

Read [Supported capabilities and non-goals](../policy-format/supported-capabilities.md) before adding new fields.

## Method-body checks miss conditional code

If code is inside conditional compilation blocks, configure and select a condition set:

```yaml
analysis:
  condition_sets:
    editor: [UNITY_EDITOR]
```

```bash
arch-linter-net --condition-set editor
```

## JSON output is mixed with timings

Use shell redirection so JSON stays on stdout and timings go to stderr:

```bash
arch-linter-net --strict --json --timings \
  > architecture-violations.json \
  2> architecture-timings.txt
```

## NuGet links open internal docs

Package metadata should point to public product documentation and the repository only. Internal project docs such as backlog governance, OpenSpec archives, and maintenance-agent instructions must not be used as product documentation links.

See [NuGet package metadata](../reference/nuget-metadata.md).
