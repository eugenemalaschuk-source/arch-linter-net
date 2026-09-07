# Immutable canonical evidence bundle — 2026-09-07

This directory preserves the complete, machine-readable canonical evidence used by the [self-architecture health baseline](../../self-architecture-health-baseline.md). It is a repository-backed copy of the exact CI outputs, rather than a pointer to an expiring Actions artifact.

## Producer identity

| Field | Value |
| --- | --- |
| Repository tree | `929983eb985dab934f886cc2f8e25824ac854984` |
| Producer | PR [#799](https://github.com/eugenemalaschuk-source/arch-linter-net/pull/799), workflow run [34141855753, attempt 1](https://github.com/eugenemalaschuk-source/arch-linter-net/actions/runs/34141855753) |
| Execution identity | `pr-799-91c4b258570ef6debaed7e39a3edf1532b6891d2` |
| CLI/configuration | `net10.0`; `architecture/dependencies.arch.yml` blob `1f6bc009aba9263d9e34bad284c4bb8e1770e506`; imported `architecture/policy/audit-conventions.arch.yml` blob `2d0211f73ba50eb9fa2c04d91e691e3dd623b71d` |
| Modes | strict snapshot/gate and audit inventory; strict Health and before/after change report |
| Result | strict `pass`; audit has 41 reviewable layout diagnostics; Health `degrading` / Gate `pass` |

The CI producer ran the CLI with `--policy architecture/dependencies.arch.yml --mode strict --ensure-built`, supplying an explicit empty v3 baseline when no baseline file existed. The change report compares strict snapshots and records the execution identity above. The exact workflow is `.github/workflows/ci.yml` at the recorded repository tree.

## Payload encoding and verification

Each `*.gz.base64` file is the complete original output, gzip-compressed and base64-encoded only to keep this text-based repository patch reviewable. Decode with `base64 -D < file.gz.base64 | gzip -dc > file` on macOS or `base64 --decode < file.gz.base64 | gzip -dc > file` on GNU systems; this must produce the stated original SHA-256. The encoded-file SHA-256 protects the repository copy itself. No JSON has been redacted, reordered, or regenerated.

| Original output | CI artifact SHA-256 | Repository encoded-file SHA-256 |
| --- | --- | --- |
| `architecture-strict.json` | `54db4fabb24067013f22eb4aad272785ce75c2b31e8460a79486b61e4d9d4c67` | `7de05fc9c14eb0bfded38789a036e95b494c77f14401bddc0386d79fb6fcdf9b` |
| `architecture-audit.json` | `cba0f86e9e6f3fc1fe893982ca6975b82801cb6db74f2f6e5a58d90c4583f80a` | `4746ee434b2b683e7fda3115e2770c25b3e44c3899187fad55f454946716c0e1` |
| `architecture-health.json` | `ccbd7b5e44bede54458b1f6667e776ccfa892f50dc6b7b32c783d34965f6618a` | `cb03a2fde3910758bc9a62367df143ca023a0369e802b072079f691e5fb7c63f` |
| `architecture-change.json` | `f71dc7254604781aed65f64da92ec7dbca2aaa9253e5c84f4f3d1964485cae63` | `6c5db02d444e17fcc691db0abf5cfbf5b3c3eecc1149305a932d8d65ff02e6da` |
| `architecture-coverage.md` | `e7bf8eca6e0763e8721413f1f5613b6146051af789f96277f180cacea9131b54` | `df0cc5f350827b6c6441413e4cea3e5327f06ae03034d13cd17e81807870765d` |

The corresponding immutable Actions artifact digests are retained as independent transport receipts:

| Output | Artifact ID | Artifact digest |
| --- | ---: | --- |
| strict | `10026270794` | `23779bb5b4f51bc61bc64473ed836133bde9ea716e6703ed1afc63a50d2f7f10` |
| audit | `10026271481` | `90911058cbb94d7d6bc2ac3689dc521de7026da8892c46b786d8abde5e7404de` |
| health | `10026272857` | `e0d71f31b830b8aee6eb28d6916ca5397ba753b6fe632ce79457bf2bc1c19504` |
| coverage | `10026272172` | `36337e672d427a61ec5ac856fb585a5d3211b5c6f1adc57ee98731ecac98795a` |
| change | `10026273578` | `2e00330adc9df0aba19f83f5f133ba1ea7bdead8bf3dea8392a226a428e4cea3` |

These transport artifacts are scheduled to expire on 2026-12-06; their expiration will not remove the repository-backed evidence above.
