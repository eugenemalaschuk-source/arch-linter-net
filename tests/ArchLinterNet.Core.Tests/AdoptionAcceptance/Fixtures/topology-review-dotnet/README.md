# .NET topology review fixture

This fixture is a small server/library topology with a real project and assembly graph:

```text
TopologyReview.Server -> TopologyReview.Application -> TopologyReview.Domain
TopologyReview.Infrastructure -> TopologyReview.Domain
```

`capture.arch.yml` deliberately has no `topology` section. It is the input for a review-only
capture before a declaration exists. `declared.arch.yml` maps all four assemblies and enables
stale-declaration evidence. `declared-unmapped.arch.yml` intentionally leaves the infrastructure
assembly unmapped so diff output can show an unmapped subject separately from a relationship
finding.

The three policy files are inputs, not generated output. Capture, diff, and verify may print or
write evidence beside this fixture, but they must never replace any of these policy files. A
reviewer chooses which candidate subjects, relationships, and out-of-scope decisions to hand
author in a policy; the capture operation does not approve or merge those decisions.

Typical lifecycle (after restoring/building the solution):

```text
arch-linter-net topology capture --policy capture.arch.yml --subject-kind assembly --format json
arch-linter-net topology diff --policy declared.arch.yml --format json
arch-linter-net topology verify --policy declared.arch.yml --mode strict --format json
```

The command examples intentionally do not pass a policy path as an output destination. Keep
capture and diff artifacts in a separate review/artifacts directory.
