# Internal Documentation

This directory contains internal project-maintenance documentation for the `arch-linter-net` repository.

It is intentionally excluded from the public MkDocs/GitHub Pages product site.

## Internal docs

- [Documentation boundary](documentation-boundary.md) — public product docs vs internal project docs.

- [Backlog governance and issue authoring](backlog-governance.md) — internal issue and backlog rules.

- [Core architecture blueprint](core-architecture-blueprint.md) — target Core module graph, application/composition seams, and state-ownership rules for the architecture-health refactor (#132/#133).

- [CEL engine architecture blueprint](cel-engine-architecture.md) — processing pipeline, component ownership, extension-direction matrix, and prohibited shortcuts for the `ArchLinterNet.CEL` engine (#322/#324).

- [CEL upstream corpus mining manifest](cel-corpus-mining-manifest.md) — provenance-aware classification of upstream CEL test corpora reviewed for parser/tokenizer hardening, and what was adapted or deferred (#338).

- [CEL policy model blueprint](cel-policy-model.md) — explicit `when` fields,
  typed Core fact contexts, fail-closed semantics, and worked examples for the
  future policy-expression surface (#162/#163).

- [Policy import format draft](policy-import-format-draft.md) - approved format decisions implemented by issue #281.

- [Policy import architecture and implementation reference](policy-import-architecture.md) - resolver/composer/provenance boundaries and the #281/#282 test matrix.

- [Policy import design examples](policy-import-examples/README.md) - fixture-ready positive and negative YAML examples.

## Publishing rule

Only public product documentation should appear in `mkdocs.yml` navigation and GitHub Pages output.

Internal docs may remain visible in GitHub as Markdown files, but they must not be linked from NuGet.org as product documentation and must not appear in the published site navigation.

## Public docs live elsewhere

Use the public documentation home for product usage:

- [Product documentation home](../index.md)
- [Getting started](../getting-started/index.md)
- [Policy format](../policy-format/index.md)
- [Contracts](../contracts/index.md)
- [AI policy authoring](../ai/index.md)
