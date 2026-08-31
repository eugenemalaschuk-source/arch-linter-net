## Context

See proposal.md for the motivation. Project discovery has both a legacy
simple-name lookup used by general policy compatibility and a newer exact
artifact-to-project binding used by metric evaluation. The former still
collapses distinct project outputs that have the same assembly name, while the
latter is not populated for ordinary explicit-target measurement.

## Goals / Non-Goals

**Goals:**

- Preserve all discovered project output artifacts needed to evaluate project
  metric trust.
- Fail closed for a project metric whenever a simple output name represents
  more than one distinct project artifact.
- Prepare exact project-output evidence for explicit-target project metrics
  without changing ordinary validation or requiring `--ensure-built`.

**Non-Goals:**

- Replace legacy simple-name policy selectors outside measurement.
- Change assembly resolution precedence for non-metric analysis.
- Build, copy, or otherwise mutate project outputs during ordinary measurement.

## Decisions

### Carry duplicate-output-name evidence alongside the exact artifact index

The metadata index will derive a set of output assembly simple names that map
to more than one normalized artifact path from discovery's per-project output
evidence. Exact artifact lookup remains necessary but is insufficient on its
own: a pre-existing simple-name resolution path could already have selected one
of several artifacts. Metric project-owner trust will therefore require both a
unique exact artifact owner and absence of a duplicate output-name ambiguity.

This is deliberately metric-only. Replacing the legacy simple-name lookup
globally would change established policy behavior and exceeds this correction.

### Resolve project outputs only when explicit-target metrics need them

Runner setup will extend the existing output-resolution decision to include
project-unit metric definitions and metric projections over a project topology.
The normal explicit `target_assemblies` shortcut remains unchanged when no
metric needs project ownership. Discovery only reads existing output metadata;
build preparation remains controlled by its existing explicit mode. The snapshot
preflight is skipped for ordinary measurement's isolated project metrics:
build receipts are not required measurement facts, and stream-loaded artifacts
do not have an `Assembly.Location` from which legacy preflight could recover
their build evidence. Validation and explicit build-preparation workflows retain
their current receipt enforcement.

### Preserve artifact-derived topology identity

The existing metric-only topology projection remains the sole place that maps
resolved assembly instances to normalized project paths. The added ambiguity
guard is evaluated before a metric accepts a project contributor, so no legacy
simple-name identity can restore trust after projection.

## Risks / Trade-offs

- Duplicate assembly names cause conservative unassessability even if the
  currently resolved artifact is otherwise unique. → This is required because
  discovery/probing order cannot be a trust boundary; users can make the
  output identity unique.
- Extra project-output inspection occurs for explicit-target project metrics.
  → It is limited to the existing configured projects and does not build or
  write outputs.

## Migration Plan

No persisted data migration is required. Existing policies with duplicate
project output assembly names will receive unassessable project metrics until
their project outputs are made unambiguous; non-project metrics retain their
current behavior.
