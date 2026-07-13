# Negative Policy Import Cases

These YAML files are candidate #281 fixtures. Each case is evaluated from the
named root; individual fragment filenames have no semantic role.

| Case | Root | Expected failure |
| --- | --- | --- |
| Root-only fragment field | `root-fragment-shape.yml` | `fragment-with-root-field.yml` declares forbidden `version`. |
| Duplicate layer | `root-duplicate-layer.yml` | Both fragments own `layers.domain`; the diagnostic names both sources. |
| Duplicate contract ID | `root-duplicate-id.yml` | Both fragments add case-insensitively equal IDs to `strict_dependencies`. |
| Cycle | `root-cycle.yml` | The chain is `root-cycle.yml`, `cycle-a.yml`, `cycle-b.yml`, `cycle-a.yml`. |
| Boundary escape | `root-boundary.yml` | The relative target resolves outside the repository boundary. |
| Unsupported expression | `root-glob.yml` | A glob entry is rejected before filesystem expansion. |
| Backslash separator | `root-backslash.yml` | Windows-style separator is rejected before host path resolution. |
| Drive/UNC/device form | `root-platform-native.yml` | Drive, UNC, and device forms are rejected by portable grammar. |
| Reserved basename with extension | `root-reserved-basename.yml` | `NUL.yml`, `COM1.arch.yml`, `LPT¹.yaml`, and `NUL.tar.gz` are rejected before filesystem resolution on every host. |
| Absolute path | `root-absolute.yml` | Unix absolute path is rejected by portable grammar. |
| Depth limit | generated fixture | A 17-edge chain fails before reading the depth-17 target. |
| File-count limit | generated fixture | Root plus 256 completed files fails before reading file 257. |

The limit cases should be generated in a temporary filesystem fixture rather
than committed as hundreds of nearly empty files.
