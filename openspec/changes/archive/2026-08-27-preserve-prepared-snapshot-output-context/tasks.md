## 1. Receipt-backed handoff

- [x] 1.1 Expose validation's final refreshed `ArchitectureRunnerPreparation` on the validation outcome and carry it through prepared graph and baseline requests.
- [x] 1.2 Materialize graph and baseline runners from that exact preparation, preserving fail-closed receipt verification without rediscovery or another build.
- [x] 1.3 Use one temporary project-level MSBuild driver for RID-specific preparation, where the SDK cannot build a solution with a runtime identifier.
- [x] 1.4 Update reviewed Core public API baselines for the intentional additive handoff contract.

## 2. Regression coverage

- [x] 2.1 Extend CLI and Core fake-composition tests to prove the same prepared selection reaches both graph projections and baseline debt.
- [x] 2.2 Add packaged ASP.NET change-snapshot acceptance coverage where CLI Release configuration and RID differ from policy defaults, including TFM inference from the selected RID output.

## 3. Validation and archive

- [x] 3.1 Run focused and affected Core/CLI tests, formatting, public API checks, and relevant lint. `make lint-architecture` was also attempted, but its temporary ensure-built solution was externally blocked by NuGet signature endpoint `NU1301`; the focused packaged and receipt-backed tests passed.
- [x] 3.2 Validate, synchronize, and archive the OpenSpec change.
