## Context

`analysis-cache/v1` is an opt-in local optimization for mode-specific validation work. Its
original cache-hit path became active only after the first implementation review, revealing that
an unkeyed content hash could validate a semantically poisoned entry, that some result-bearing
fields could not round-trip through the envelope, and that CLI and Testing used partially
different population/accounting paths. Cache data is untrusted optimization input: a miss,
reject, corruption event, or cancellation must always fall back safely and never make a failed
analysis appear to pass.

The design must preserve the existing architecture boundary: Core owns cache storage, envelope,
authorization, and instrumentation facts; CLI and Testing only translate their configured cache
options and report the shared facts. The implementation targets .NET 10 and its built-in
cryptography/filesystem primitives, with no new external dependency.

## Goals / Non-Goals

**Goals:**

- Authenticate cache entries with a cache-root-scoped HMAC key and reject hand-edited entries.
- Make a cache hit reconstruct every result-bearing `ValidationOutcome` field exactly.
- Bind reuse authorization to every result-affecting request dimension and a genuine project set.
- Apply the same safe path, cancellation, population, and profile-counter semantics in CLI and
  both Testing execution paths.
- Keep cache failure fail-closed: analysis recomputes rather than trusting unsafe or stale data.

**Non-Goals:**

- Protect against an attacker able to read and overwrite the HMAC key file as well as the cache
  root; this is a local trust-boundary control, not a machine-wide key-management system.
- Change validation finding identity, ordering, exit semantics, or the cache-disabled path.
- Add a remote/shared cache, key rotation, cache encryption, or persistent cache work beyond
  `analysis-cache/v1`.

## Decisions

### Root-scoped HMAC key, not an unkeyed digest

`AnalysisCacheHmacKeyStore` generates a 256-bit CSPRNG key once for each cache root and persists
it under `<root>/.keys/hmac-v1.key`, outside the sharded entry tree so clearing entries retains
the same trust root. First use is serialized/read-or-created so concurrent callers observe one
key. `AnalysisCacheContentDigest` signs canonical content with HMAC-SHA256 and validates tags with
`CryptographicOperations.FixedTimeEquals`.

- Alternative rejected: retaining SHA-256 and documenting cache content as trusted. A cache entry
  determines whether contracts execute, so an unauthenticated `Passed` value is not safe
  optimization data.
- Alternative rejected: a machine-global or checked-in key. A root-local key scopes trust to the
  local cache and avoids distributing a reusable secret.

### Authorization is a closed set of independent checks

`AnalysisCacheStore.TryGet` authorizes only after format/schema/tool identity, HMAC, successful
original completion, canonical key, and project-manifest eligibility all match. Project manifests
are compared as ordered-set equality after duplicate paths are rejected on both sides. The cache
key folds preprocessor symbols as an order-independent digest, baseline *content* (not path),
asmdef inclusion, and unmatched-ignore enforcement.

- Alternative rejected: relying on a key digest alone. Request dimensions or a duplicated forged
  manifest could otherwise make semantically different analysis reuse a stored outcome.

### Filesystem containment is enforced at the storage boundary

Every `AnalysisCacheStore` operation rejects a root or ancestor shard reparse point before file
I/O. Enumeration for inspect/clear does not follow linked directories. This applies independently
of cache mode, including `Auto`, because location-resolution checks do not protect a pre-created
auto root.

- Alternative rejected: validate only explicit cache paths. Store operations must be safe even
  when callers bypass location resolution or an existing auto root becomes unsafe.

### Cancellation wins over cache reuse and publication

The session token flows through policy digest creation and lookup. A hit observed before
cancellation cannot be accepted after cancellation is observed; normal evaluation then handles the
cancelled session. Population remains limited to completed, non-cancelled modes and checks the
token immediately before atomic rename.

- Alternative rejected: treat a completed cache read as authoritative despite cancellation. That
  could return an otherwise valid cached outcome for a request that the host has already cancelled.

### Shared support owns host parity and profile accounting

`ArchitectureValidationCacheSupport` is reused by Testing's independent and shared-snapshot
paths. CLI and Testing aggregate lookup and population reject reasons into the scalar `Rejects`,
and both classify corruption through the same helper. The envelope mapper and schema carry all
result-bearing outcome fields with closed-set metadata conversion.

- Alternative rejected: duplicate cache-population/profile code in each host. Separate host code
  had already drifted, leaving cache writes and corruption/reject evidence inconsistent.

## Risks / Trade-offs

- [Risk] HMAC protects entry edits but not a fully compromised cache root and key. → Mitigation:
  state the residual trust limit explicitly and continue to require all non-cryptographic
  authorization checks.
- [Risk] Strict reparse-point rejection can disable a user-selected cache on unusual filesystem
  layouts. → Mitigation: return a typed safe rejection and recompute analysis without changing
  validation results.
- [Risk] Additional key dimensions lower hit rate. → Mitigation: correctness takes precedence;
  deterministic content digests avoid false misses caused solely by checkout paths or symbol order.
- [Risk] A larger outcome envelope can exceed storage limits. → Mitigation: enforce identical
  size bounds before write and on read, so oversized data never becomes a persistent reject loop.

## Migration Plan

The cache remains disabled by default. Existing entries authenticated with the old unkeyed digest
do not satisfy the new HMAC verification and are rejected as untrusted; ordinary analysis then
recomputes and writes a new entry. No user action is required, and deleting an old cache tree is a
safe optional cleanup. Rollback is a code rollback that simply stops reading new entries; it does
not alter validation output or require a schema registry migration.

## Open Questions

None for `analysis-cache/v1`. Key rotation, stronger machine-level key protection, and shared or
remote cache trust are intentionally deferred to separate changes.
