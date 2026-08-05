## Context

ArchLinterNet 0.5.1 has completed its policy, identity, output, build-state,
cache, profile, concurrency, cancellation, and packaged-schema slices. Their
contracts are authoritative in existing OpenSpec specifications and are partly
described in scattered user pages. Issue #367 must turn that material into an
adopter-facing contract without creating an alternate implementation or
overclaiming cross-platform evidence.

The audience includes a minimal new repository, an established 0.5.0 policy
with imports and a legacy baseline, conventional and multi-host solutions,
direct CLI and Testing users, CI maintainers, and non-interactive/offline or
resource-constrained environments. All public examples must use synthetic
identities.

## Goals / Non-Goals

**Goals:**

- Give each adopter profile a searchable, copy-pasteable 0.5.1 path.
- Keep the paths status-correct: wrappers invoke the product once, preserve
  stdout/stderr, and return its exact exit code.
- Make safety boundaries explicit: no automatic baseline/API approval, no
  network dependency for installed schema discovery, and no cache or
  parallelism requirement for a simple policy.
- Keep release claims and terminology aligned with the installed public
  surface, source schemas, capability manifest, and existing reference pages.
- Verify the documentation structure and its load-bearing command vocabulary
  in repository tests; execute the platform-independent external-consumer
  smoke path against locally packed artifacts in the release gate.

**Non-Goals:**

- Add a product command, alter CLI/Test API semantics, or implement consumer
  orchestration in ArchLinterNet.
- Generate a policy for an adopter or write/approve a baseline or API snapshot
  on its behalf.
- Claim a platform, provider, performance result, or offline guarantee beyond
  the existing evidence and package contract.

## Decisions

### One canonical guide with focused reference pages

Create a `0.5.1 migration guide` as the primary decision tree: new adopters
start at the minimal policy while 0.5.0 adopters follow the explicit upgrade
workflow. Keep command syntax and format details in the CLI, output, schema,
baseline, API, Testing, and CI reference pages, linking to them instead of
copying competing prose. This makes the migration path discoverable while
retaining one source for each detailed contract.

Alternative considered: fold all release material into the existing adoption
page. Rejected because it would mix a short introductory guide with every
compatibility transition and make the two adopter paths difficult to search.

### Checked-in reference examples, not product-owned wrappers

Place POSIX, PowerShell, Make, Task, Tilt, and provider-neutral snippets in a
dedicated reference page and make their rules explicit. They locate an already
pinned tool, pass arguments as structured values, call it once, leave standard
streams alone, and propagate the exact exit status. Examples remain consumer
templates; the product does not take ownership of a caller's build system.

Alternative considered: ship executable wrapper files. Rejected because those
would imply a supported orchestration layer and introduce path/install policy
not requested by the product contract. The release gate copies and executes
the documented snippets in synthetic consumer fixtures instead.

### Documentation claims are tied to existing contracts

Use the installed `schema list`/`schema print` commands as the offline source
of truth; link cache, profile, concurrency, cancellation, output, and exit
claims to their owning public references. Distinguish validation `--report`
sinks from command-owned `--output` artifacts. State that Checkpoint A is
internal evidence and 0.5.1 is the only public stabilization release.

Alternative considered: publish a static list of schema URLs or a capability
matrix only. Rejected because mutable repository URLs are not installed
release authority and a matrix alone does not give an adopter executable
commands.

### Lightweight structural documentation tests

Add tests that locate the new guides through the MkDocs navigation and verify
the canonical commands and safety statements are present. The tests are not a
substitute for the packed-artifact gate: they prevent accidental removal or
drift, while #366 owns end-to-end package, shell, and platform evidence.

## Risks / Trade-offs

- [Reference examples drift from CLI syntax] → Test required command tokens
  and run the normal acceptance suite; the final packed-artifact gate executes
  copied commands.
- [Documentation overclaims support] → State the observed support scope and
  distinguish release gate evidence from Checkpoint A.
- [Duplicated semantics diverge] → Use a canonical guide plus links to owning
  reference pages rather than duplicating exhaustive option tables.
- [Shell examples mishandle arguments or errors] → Use shell arrays/PowerShell
  arrays, no `eval` or constructed command string, and an explicit
  `$LASTEXITCODE` return.

## Migration Plan

1. Add the canonical migration and entrypoint reference pages, then link them
   from navigation and existing adoption, installation, CI, schema, output,
   Testing, capability, and release-reference pages.
2. Add structural tests for discoverability, required command forms, release
   boundary, and safety constraints.
3. Run formatting, acceptance, OpenSpec validation, archive the change, and
   open the issue-closing PR. No runtime rollback is needed; removing the
   documentation change reverts the public guidance independently of product
   behavior.

## Open Questions

None. The implementation and schema slices named by #367 are complete; this
change documents their established public behavior.
