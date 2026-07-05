## Context

The prior hardening pass (`harden-assembly-dependency-contracts`) added `dependency_depth` to both assembly-axis families with load-time rejection of `transitive`. Review found two gaps: the JSON schema still advertised `transitive` as valid, and the rejection only lived in `ArchitecturePolicyDocumentLoader`, not in the `ArchitectureAnalysisSession` check methods themselves.

## Goals / Non-Goals

**Goals:**
- Make the JSON schema an accurate contract: only `direct` is a valid `dependency_depth` value for these two families.
- Make the direct-only invariant hold regardless of entry point — YAML-loaded or programmatically constructed `ArchitectureContractDocument`.

**Non-Goals:**
- Implementing transitive traversal.
- Any change to `assembly_allow_only` evaluation semantics or `assembly_independence`.

## Decisions

**1. Narrow the schema enum instead of leaving `transitive` documented as "accepted but rejected."**
A JSON schema that accepts a value the loader immediately rejects is misleading to schema-validating editors/tools. Narrowing the enum to `["direct"]` makes IDE-level validation and the loader agree.

**2. Add the guard as a shared private helper called from both `Check...` methods, rather than duplicating the check inline.**
Both methods need the identical check and error message; a single `RequireDirectDependencyDepth(contractName, depth)` helper avoids duplicating the message text and keeps the two methods' structure symmetric with how `BuildAssemblyLookup` is already shared between them.

## Risks / Trade-offs

- [Risk] None beyond what the original hardening pass already accepted — this closes a gap rather than introducing new behavior for well-formed (loader-validated) policies.
