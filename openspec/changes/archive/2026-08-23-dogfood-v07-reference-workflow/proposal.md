## Why

ArchLinterNet's v0.7 release delivered real-repository forensics and AI-first
drift-control capabilities, but external adopters do not yet have one cohesive,
reproducible example that demonstrates how to apply both workflows safely to a
non-trivial repository. The immutable `v0.7.0` tag now makes it possible to
publish public-safe evidence based on a fixed release range rather than a
development candidate.

## What Changes

- Add an evergreen public reference workflow that uses ArchLinterNet itself as
  the example repository, with the exact commands, inputs, and advisory versus
  authoritative boundaries for release forensics and AI-assisted drift control.
- Commit deterministic, public-safe evidence for the `v0.6.5` to `v0.7.0`
  release range, including canonical artifact identity, material-finding
  classifications, and explicit self-policy applicability decisions.
- Demonstrate representative real comparisons for policy context, architecture
  changes, policy weakening, remediation guidance, and known-versus-new debt
  gating without weakening the policy or rewriting debt for presentation.
- Add the reference page to public MkDocs navigation while retaining release
  identifiers only as reproducibility evidence, not as the page identity.

## Capabilities

### New Capabilities

- `self-dogfood-reference-workflow`: a reproducible, public-safe reference for
  applying the shipped forensics and drift-control workflows to a real
  repository and interpreting their evidence responsibly.

### Modified Capabilities

- `docs-site`: require public navigation to the evergreen self-dogfood
  reference page.

## Impact

- Affected documentation: a new public guide, public navigation, and a
  contributor-facing deterministic evidence record.
- Affected systems: the existing CLI and policy files are exercised read-only;
  their product semantics, public APIs, architecture policy, and debt baseline
  are unchanged.
- Dependencies: uses the released `arch-linter-net` tool and the repository's
  existing OpenSpec and documentation validation workflows; introduces no new
  runtime dependency.
