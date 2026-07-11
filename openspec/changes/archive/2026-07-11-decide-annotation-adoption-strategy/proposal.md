## Why

Issue #108 requires ArchLinterNet to decide how semantic role discovery
(#107's `classification.attributes`/`classification.assembly_attributes` YAML
mapping) can be adopted through code-level annotations without forcing
production projects to reference a binary ArchLinterNet annotation assembly.
The semantic-role-catalog spec already carries an open placeholder deferring
"a separate product and packaging decision" to this issue
(`openspec/specs/semantic-role-catalog/spec.md:70-75`), and the
semantic-classification-model spec asserts the no-binary-dependency boundary
without documenting the underlying trade-offs a policy author would need to
choose between adoption paths. This gap must close before any follow-up
extraction work (#109-#114) implements against the model.

## What Changes

- Decide and record that user-defined attributes mapped by full type name in
  YAML remain the sole supported annotation adoption path for this wave; no
  binary `ArchLinterNet.Annotations` package and no source-only annotation
  package are added, since neither has a demonstrated need yet.
- Document the trade-offs between a binary package, a source-only package,
  and user-owned attributes (dependency footprint, versioning burden, setup
  cost, control) in `docs/policy-format/semantic-classification.md`, closing
  the existing "No annotation package" bullet with an explicit recommended
  path and rationale.
- Close the packaging-decision placeholder in
  `openspec/specs/semantic-role-catalog/spec.md` with a MODIFIED requirement
  recording the resolution.
- Add a MODIFIED requirement to
  `openspec/specs/semantic-classification-model/spec.md` recording that
  user-owned attributes are the sole supported mechanism in this wave.
- Add compile-only fixtures under `tests/ArchLinterNet.Core.Tests/`
  demonstrating the recommended user-defined-attribute pattern, following the
  existing `*TestFixtures.cs` convention. No scanner/extractor exists yet, so
  these fixtures only prove the documented pattern compiles — they add no new
  runtime behavior or test assertions beyond that.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `semantic-role-catalog`: Resolve the first-wave annotation-package
  placeholder — no binary or source-only package ships; user-defined
  attributes remain the supported adoption path.
- `semantic-classification-model`: Record that no annotation package (binary
  or source-only) is shipped by this change and that user-owned attributes
  mapped by full type name are the sole supported mechanism.

## Impact

- Documentation: `docs/policy-format/semantic-classification.md`.
- Specs: `openspec/specs/semantic-role-catalog/spec.md`,
  `openspec/specs/semantic-classification-model/spec.md`.
- Tests: new compile-only fixture file(s) under
  `tests/ArchLinterNet.Core.Tests/`.
- No new project, package, schema field, or runtime/extraction behavior.
  Existing ArchLinterNet runtime behavior is unchanged.
