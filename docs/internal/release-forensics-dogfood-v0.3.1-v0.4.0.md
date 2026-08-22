# Release Architecture Forensics dogfood: v0.3.1 to v0.4.0

This is the repository-safe release-closure dogfood record for #244. It uses
only the public ArchLinterNet repository and a packaged local tool built from
the recorded source revision; it contains no adopter or private-repository data.

## Reproduction identity

| Field | Value |
| --- | --- |
| Command | `dotnet arch-linter-net history analyze --from v0.3.1 --to v0.4.0 --policy architecture/dependencies.arch.yml --format json` |
| Source revision | `9c7d638719aa5879cbdd9361fedbae5332e2e70f` |
| Tool version | `0.1.0` |
| History semantics | `v1` |
| Object format | `sha1` |
| Authored `from` / resolved commit | `v0.3.1` / `e75ddd13ac387f1fa0c5333e9a9412f864bb968d` |
| Authored `to` / resolved commit | `v0.4.0` / `c3c920166ef64dd30dc17d542552c0a3946d763a` |
| Effective configuration identity | SHA-256 `2749d3874541169da8d14b947ebf75ac261256e4f12c45dedac8c8fd133a10c2` of `jq -c .analysis.historyAnalysisConfiguration` |
| Canonical Git-only JSON SHA-256 | `65a33f97557f6f0feec98f72465a969a72cbbaa290670d777e800bc65becf5b2` |
| Range evidence | 21 commits, 344 logical files, 0 excluded merges, 0 exact-rename candidates/components |

The command intentionally passes separate `--from` and `--to` operands. It
does not use `v0.3.1...v0.4.0` or another Git revision expression as one
operand.

The tool package was built from the source revision with:

```bash
dotnet pack ArchLinterNet.slnx --configuration Release --no-restore --output <temporary-nupkg-directory>
```

and installed into the repository-local .NET tool manifest only for this run.
The manifest was restored to its tracked version afterward.

## Determinism and enrichment evidence

| Run | Result |
| --- | --- |
| Default Git-only run | Successful canonical JSON; enrichment `not_requested` |
| `LC_ALL=C TZ=Pacific/Auckland` Git-only rerun | Byte-identical to the default run; SHA-256 remains `65a33f…f5b2` |
| `--enrich-dotnet` historical-range run | Successful canonical JSON; enrichment `unavailable` with reason `revision_mismatch` and provenance for resolved `to` |
| Git-level comparison | `jq -c 'del(.enrichment)'` hashes to `fb7601f907763bed37d82ad902e3694942724fab93fa7afcc8fe47032bcbb214` for both the Git-only and unavailable-enrichment reports |

The full unavailable-enrichment JSON has a distinct SHA-256
`795124b3796d812589eea889805bdc38a34e8afa1e23405384b85d8de4efe535`,
as expected because only its reserved enrichment projection differs. The
focused `HistoryDotNetEnricherTests.AvailableAndUnavailableEnrichmentChangeOnlyTheReservedReportProjection`
fixture additionally proves the successful `available` path cannot change
Git-level evidence, findings, scores, ranks, or candidate ordering.

## Findings and manual comparison

The effective default profile leaves all paths in the explicit `unknown`
category, producing 344 hotspot, 344 bottleneck, and 344 OCP-pressure
investigations. This is a useful release-wide signal, not an assertion that
every file needs a refactor. No `Gtheta` co-change cluster qualified at the
configured threshold.

The first canonical hotspot/candidate identities are:

| Canonical path | Hotspot score |
| --- | ---: |
| `src/ArchLinterNet.Core/Composition/ServiceCollectionExtensions.cs` | `0.865372715` |
| `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs` | `0.860962802` |
| `schema/dependencies.arch.schema.json` | `0.846390605` |
| `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` | `0.845545896` |
| `src/ArchLinterNet.Core/Execution/ArchitectureContractCatalog.cs` | `0.830443745` |

Manual review of the 21 commits shows an intentionally broad contract-family
delivery: assembly, package, external, project, public-API, attribute,
inheritance, composition, baseline, CLI/testing, and SARIF work. The leading
composition, policy-loading, schema, contract-model, and catalog findings match
those shared implementation and wiring surfaces. They are accepted
investigations, not defects: the range deliberately added many features there.

There were no observed merge, exact-rename, NUL/blob, or invalid-UTF-8 events
in this range, so this dogfood run cannot quantify their limit-specific false
positive or false-negative rate. The following intentional v1 limits remain
explicitly accepted and are covered by focused conformance vectors:

- only literal `HEAD`, full IDs, exact `refs/...`, and unique tag/head
  shorthand resolve; revision expressions and DWIM are not accepted;
- selected author/message metadata and Git paths require strict UTF-8;
- TaskKeys are bounded, namespaced canonical identities rather than arbitrary
  issue parsing;
- one baseline identity per pathname can conflate delete/re-add generations;
- merge file deltas are excluded, which can understate merge-resolution edits;
- only local exact same-blob rename candidates collapse; rename-with-edit is
  intentionally missed;
- ambiguous DAG or lifecycle-broken rename components never collapse identity;
- NUL, non-blob, and non-line events have zero line churn with explicit status.

No tuning was applied. The recorded default profile changed configuration only
through its existing reviewed values; no v1 semantic invariant or `Gtheta`
separation was relaxed for this run.
