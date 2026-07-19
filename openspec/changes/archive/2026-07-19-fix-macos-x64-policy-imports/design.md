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

### Use physical canonical paths as macOS file identities

After resolving each link segment and validating the target is a regular file,
the macOS resolver will use the physical canonical path as the graph identity.
This removes the Darwin `stat` declaration while retaining the established,
tested Windows and Linux identity paths. Canonicalization continues to collapse
normalized, case, and symbolic-link aliases; the existing portable identity
set remains case-insensitive. Native inode identity was considered for macOS,
but it requires a platform-specific layout and is not necessary for the
supported authored-import aliases.

### Retain managed regular-file validation

The resolver will validate a canonical target through managed file metadata and
the existing exact-path existence check. Directories and unreadable/non-file
targets continue to fail before YAML reads. No operation opens arbitrary
special files merely to establish identity.

### Describe root failures as root-policy failures

The graph resolver will catch path-resolution exceptions for the selected
policy and enrich them with a root descriptor derived from the user-selected
path. Imported failures remain enriched at the declaring `imports[n]` edge and
retain the root-based import chain.

## Risks / Trade-offs

- [Hard-link aliases have distinct canonical paths] -> Existing portable path
  identity still rejects authored case aliases and link aliases; coverage will
  preserve hard-link duplicate detection where the platform exposes it.
- [Managed metadata can vary by filesystem] -> Tests exercise ordinary files,
  directories, boundary escapes, and links; platform-native acceptance remains
  part of the release matrix.
- [Root path may not be repository-relative when resolution fails] -> The
  fallback descriptor uses a normalized selected path and marks it explicitly
  as the root, preserving role clarity without leaking unrelated paths.
