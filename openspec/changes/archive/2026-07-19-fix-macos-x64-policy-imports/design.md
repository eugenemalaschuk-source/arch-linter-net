## Context

`ArchitecturePolicyPathResolver` currently uses platform-specific native file
metadata to identify policy sources. Its Darwin declaration assumes an ABI
layout that differs on Intel macOS, causing valid imported policies to fail.
The resolver already calculates exact and physically canonical paths before
graph traversal, and graph state separately preserves case-insensitive portable
identities.

## Goals / Non-Goals

**Goals:**

- Support the same root-plus-fragment policy graph on macOS x86_64, macOS
  arm64, Linux, and Windows.
- Preserve path-boundary, exact-case, symlink, regular-file, duplicate, cycle,
  and deterministic traversal protections.
- Produce typed root diagnostics for selected-root failures and provenance-rich
  fragment diagnostics for imported failures.

**Non-Goals:**

- Redesigning import syntax, composition rules, schemas, CLI options, or the
  Testing API.
- Adding remote imports, glob expansion, multiple roots, or filesystem
  abstractions beyond this resolver.

## Decisions

### Use `getattrlist` file identity and vnode type on macOS

After resolving each link segment, the macOS resolver obtains `ATTR_CMN_DEVID`,
`ATTR_CMN_FILEID`, and `ATTR_CMN_OBJTYPE` through `getattrlist`. The device and
file ID form the graph identity, preserving duplicate detection for hard-link
aliases; vnode type must be `VREG` before the source is opened. The response
buffer has an explicit four-byte packing contract, independent of the host
processor's `stat` ABI. Physical canonicalization continues to collapse
normalized, case, and symbolic-link aliases; the existing portable identity
set remains case-insensitive.

### Classify native failures by platform error domain

Windows error codes and POSIX `errno` values are classified independently:
missing-path and access-denied values retain their typed categories, while
other native failures use the append-only `PlatformFailure` category and carry
the native error domain and code. Directories and special files fail as
`SourceShape` before YAML reads. No operation opens arbitrary special files
merely to establish identity.

### Describe root failures as root-policy failures

The graph resolver will catch path-resolution exceptions for the selected
policy and enrich them with a root descriptor derived from the user-selected
path. Imported failures remain enriched at the declaring `imports[n]` edge and
retain the root-based import chain.

## Risks / Trade-offs

- [Native metadata differs by OS] -> Isolate native calls and classify Win32
  and POSIX errors separately; tests exercise each mapping and real macOS jobs
  exercise hard links and special files on x86_64 and arm64.
- [File IDs vary by filesystem] -> Pair macOS file IDs with device IDs and
  preserve exact-path, physical-boundary, and portable identities separately.
- [Root path may not be repository-relative when resolution fails] -> The
  fallback descriptor uses a normalized selected path and marks it explicitly
  as the root, preserving role clarity without leaking unrelated paths.
