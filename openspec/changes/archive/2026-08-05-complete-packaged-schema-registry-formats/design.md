## Context

The Core package already embeds a manifest-backed registry and six implemented release contracts, including normalized finding. The source schemas and public producers for persistent cache entries and profiling documents have since landed, but their resources remain outside the manifest, embedded-resource list, contentFiles list, and consistency tests.

## Goals / Non-Goals

**Goals:**

- Add the two exact existing cache/profile source schemas to the immutable 0.5.1 manifest and package distribution while retaining normalized-finding coverage.
- Validate output produced through current public paths against registry bytes and prove a packed CLI/Core can discover those bytes offline.
- Keep the source, manifest, embedded resources, content files, documentation, and capabilities inventory executable and synchronized.

**Non-Goals:**

- Change schema shapes, writer behavior, or compatibility semantics.
- Add remote schema retrieval, mutable IDs, or automatic document migration.
- Rework the existing registry foundation or its prior entries.

## Decisions

1. **Extend the existing `PackagedSchemaRegistry` manifest model.** The manifest already provides logical identity, release-qualified `$id`, SHA-256, ownership, and read/write claims, while `TryRead` verifies embedded bytes. Adding entries rather than a new registry preserves the installed package as the single source of truth.

2. **Ship the existing schema bytes unchanged.** The source files already carry immutable 0.5.1 `$id` values and real-output tests. Copying no schema or introducing a generated derivative avoids byte divergence between source, embedded resource, contentFiles, and `schema print`.

3. **Extend the current registry integration tests.** Generate each sample with the appropriate public producer, validate it against the resource returned by `TryRead`, and assert registry/package/manifest consistency. Add an offline packed-tool smoke test only through local build artifacts, never a network source.

4. **Preserve actual support boundaries.** The existing finding and cache readers retain explicit compatibility checks; profile is a generated-output writer contract without a public reader.

## Risks / Trade-offs

- **Digest drift after schema edits** → manifest and tests fail immediately, requiring an intentional digest update.
- **Tests accidentally validate a hand-written fixture** → samples are created through current public formatter, cache store/serializer, and profile output paths.
- **Package-resource omission** → package inspection compares embedded resource and `contentFiles` artifacts with the manifest.
- **Offline smoke flakiness** → restore/build happens before isolated offline invocation; the smoke itself receives no repository-relative schema path or network dependency.

## Migration Plan

The change is additive for release 0.5.1: installed packages gain two discoverable logical IDs. Existing IDs and bytes, including normalized-finding, remain unchanged. Rollback consists of restoring the preceding package version; no persisted document is rewritten.

## Open Questions

None; format owners and their source contracts are already merged.
