# Unity-style topology review fixture

This fixture mirrors the assembly-definition layout of a Unity client without requiring the
Unity editor or a generated `Library/` directory:

```text
TopologyReview.Unity.Editor -> TopologyReview.Unity.Gameplay -> TopologyReview.Unity.Runtime
```

The runtime assembly is platform-neutral, gameplay references runtime, and editor tooling is
editor-only. The `.asmdef` manifests are the authoritative Unity-style assembly boundary; the
`*.arch.yml` files show the same boundaries as a topology review input.

`capture.arch.yml` has no declared topology and is suitable for a first, review-only capture.
`declared.arch.yml` maps the three asmdef assemblies and includes a retired node and edge so
stale-declaration evidence is visible. `declared-unmapped.arch.yml` intentionally leaves the
editor assembly unmapped. These variants make mapping, relationship, unmapped, and stale review
categories explicit without making a generated candidate authoritative.

The capture/diff/verify lifecycle is:

```text
arch-linter-net topology capture --policy capture.arch.yml --subject-kind assembly --format json
arch-linter-net topology diff --policy declared.arch.yml --format json
arch-linter-net topology verify --policy declared.arch.yml --mode audit --format json
```

Unity must first export/compile the assemblies and make them available through the selected
assembly search path for a live analysis. The fixture's checked-in `.asmdef` files are not
rewritten by these commands, and neither capture nor diff approves a candidate. A reviewer
must hand-author and review any topology declaration.
