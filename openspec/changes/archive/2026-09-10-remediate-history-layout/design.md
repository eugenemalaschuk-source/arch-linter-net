## Context

See [proposal.md](proposal.md). The frozen audit at
`929983eb985dab934f886cc2f8e25824ac854984` records these exact canonical identities and paths:

| Identity | Current path | Target path |
| --- | --- | --- |
| `HistoryDotNetEnrichmentUnavailableException` | `History/Enrichment/HistoryDotNetEnricher.cs` | `History/Enrichment/Exceptions/HistoryDotNetEnrichmentUnavailableException.cs` |
| `CanonicalJsonUnicodeException` | `History/Reporting/CanonicalJsonWriter.cs` | `History/Reporting/Exceptions/CanonicalJsonUnicodeException.cs` |
| `IHistoryDotNetFactProvider` | `History/Enrichment/IHistoryDotNetFactProvider.cs` | `History/Enrichment/Abstractions/IHistoryDotNetFactProvider.cs` |

`HistoryDotNetFactMaterialization` currently shares the provider-interface file. It must be split
into its own Enrichment source file, rather than being placed in the `Abstractions` directory.

## Goals / Non-Goals

**Goals:**

- Remove exactly the three frozen History audit-layout identities through source placement.
- Prove both recursive directory conventions still diagnose a representative misplaced type.
- Retain canonical History output and failure semantics through existing focused tests.

**Non-Goals:**

- Changing a namespace, accessibility, contract, policy control, exemption, baseline, or public API.
- Extracting new responsibilities, reformatting unrelated History code, or remediating any other
  audit finding.

## Decisions

### Preserve namespaces while relocating source declarations

The layout controls govern source folders, not namespaces. Keeping each declaration's namespace
unchanged is the smallest compatible correction: callers, reflection identities, serializer behavior,
and the reviewed API surface are unaffected. Changing namespaces to mirror folders would be broader
churn with no acceptance-criteria benefit.

### Split the mixed provider file before moving the interface

`Abstractions` is itself a governed purity directory. Moving its adjacent concrete materialization
class alongside the interface would create a replacement layout problem. The interface moves alone;
the materialization class receives its own existing-Enrichment file with unchanged namespace and
content.

### Use real-source self-policy mutation regressions

The existing self-policy test harness materializes temporary source under the actual Core source
roots and evaluates the genuine audit controls with `WithEnsureBuilt`. It proves the layout controls
fire without changing the policy they govern, and is preferred over a synthetic or policy-text-only
test.

## Risks / Trade-offs

- [File moves subtly change History behavior] → Keep declarations byte-for-byte equivalent apart
  from their file boundaries and run focused History tests plus canonical JSON coverage.
- [A future policy edit weakens the conventions] → Add focused exception and interface mutation
  regressions against the real source universe.
- [Concurrent remediation touches shared OpenSpec state] → Limit the shared decomposition change to
  its one #820 task hunk and archive this focused change separately after validation.
