## Context

ArchLinterNet already has one policy loader that resolves imports, validates the
effective policy, assigns fallback contract IDs, expands source sets, and binds
typed portable provenance. It also has semantic classification, coverage, and
contract models. The CLI's `policy` command currently owns only static policy
checking, while graph and explain commands demonstrate the composed CLI/Core
runtime boundary.

Issue #118 needs an AI-safe summary of those existing facts before code is
changed. It must not become another policy parser, a second executable, or an
architecture analysis run.

## Goals / Non-Goals

**Goals:**

- Provide a versioned, typed Core result built from an effective policy and
  provenance already produced by the normal loader.
- Make it available as `arch-linter-net policy context` in deterministic JSON
  and concise Markdown formats.
- Include only declared, reviewable policy facts and bounded guidance that
  follows the repository's established AI policy-authoring documentation.
- Cover monolithic, modular-monolith, Unity/client-style, and imported-policy
  inputs with structural/snapshot-style tests.

**Non-Goals:**

- Project, assembly, source, dependency-graph, runtime, DI, or data-flow
  analysis.
- A new policy schema, parser, executable, policy suggestion engine, or policy
  auto-modification mechanism.
- Replacing policy documentation, full validation, or human architecture review.

## Decisions

### Core owns the export model and effective-policy projection

Add a small policy-context application service and public Engine/facade entry
point in Core. It loads with `IArchitecturePolicyDocumentLoader`, then projects
the already-composed `ArchitectureContractDocument` and its provenance into a
versioned immutable context model.

This keeps policy composition, validation, source-set expansion, fallback IDs,
and provenance single-sourced. Calling validation or graph setup would add
unneeded project/assembly work and make a pre-coding context command depend on
runtime build state.

Alternative considered: serializing the loaded YAML or introducing a CLI-only
parser. Both would expose unsupported raw details, make the schema unstable,
and risk disagreeing with effective policy behavior.

### The existing `policy` CLI family owns presentation

`policy context --policy <path> --format json|markdown` will dispatch through
the existing policy command module, a dedicated instance handler, and the
existing `ICliRuntime`/`ArchitectureEngine` seam. JSON and Markdown formatting
live in Core beside the typed context model so programmatic callers and CLI
receive the same deterministic content.

Alternative considered: a new top-level `context` command. It would separate
an effective-policy inspection operation from the existing policy surface and
would be less discoverable than `policy context`.

### Export a deliberate, stable policy summary

The JSON document uses `schema_version: 1` and a stable kind. Ordered arrays
contain policy identity and portable source provenance; layers with selectors
and exclusions; contract mode, family, ID, name, reason, selectors/context
facts and coverage scopes where applicable; semantic classification mappings,
roles, metadata keys, and discovered declared context values; and a short
reviewed guidance list. Collection values are ordinally sorted unless their
effective policy order is itself meaningful.

Markdown is rendered from the same model as a compact prompt-ready summary.
It has no illustrative dependency examples unless the policy itself declares a
deterministic relationship; guidance never invents a role, layer, exception, or
allowed dependency.

Alternative considered: emitting every raw YAML field. That would be verbose,
leak unsupported/deferred content, and cease to be compact agent context.

### Keep policy paths portable and guidance bounded

The projection accepts only typed provenance source paths already normalized by
the loader, and rejects/sanitizes rooted filesystem paths before presentation.
Guidance is reviewed, static text derived from `docs/ai/agent-guide.md` and
`docs/ai/semantic-role-governance.md`; it is never read dynamically from a
local checkout or generated from code/environment data.

Alternative considered: include physical policy paths and dynamically scrape
documents. This would expose local filesystem data and make the JSON vary with
the checkout rather than policy semantics.

## Risks / Trade-offs

- [A broad generic contract projection loses useful semantics] → project
  context selectors, semantic roles, coverage scopes, reasons, and contract
  IDs/names explicitly; test representative modular-monolith and Unity inputs.
- [New public Core types drift from reviewed API governance] → update the
  reviewed Core public-API snapshot only through its explicit lifecycle and
  include the snapshot check in validation.
- [Markdown and JSON diverge] → render both from the one typed context result
  and assert representative output structure/content in focused tests.
- [Output exposes machine paths] → use only loader provenance and add
  regression assertions that output has no absolute local path.
- [The command is mistaken for validation] → help text, Markdown, docs, and
  JSON description state that it summarizes policy and does not analyze code.

## Migration Plan

The feature is additive. Existing policies and validation command paths remain
unchanged. Consumers can adopt the Core API or `policy context` command when
they need pre-edit context; no policy migration or compatibility switch is
needed. Removing the new command later is a normal additive-API deprecation,
not a data migration.

## Open Questions

None. The output's v1 shape is intentionally compact and can be extended with
new optional fields in later schema versions without changing policy behavior.
