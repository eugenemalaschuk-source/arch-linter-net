## 1. Core API and tests

- [x] 1.1 Add `ArchLinterNet.Core.Asmdef.AsmdefValidator` with the existing convenience signatures
- [x] 1.2 Delegate the facade to `ArchitectureEngine.ValidateAsmdef`
- [x] 1.3 Move facade behavior and strict-vs-audit delegation tests into `ArchLinterNet.Core.Tests`
- [x] 1.4 Preserve the public `contractPath` parameter name and cover named-argument source compatibility

## 2. Remove the redundant package boundary

- [x] 2.1 Remove `src/ArchLinterNet.Unity`
- [x] 2.2 Remove `tests/ArchLinterNet.Unity.Tests`
- [x] 2.3 Remove both projects from `ArchLinterNet.slnx`
- [x] 2.4 Remove the Unity friend assembly declaration from Core

## 3. Build, release, policy, and documentation

- [x] 3.1 Remove Unity project builds from make architecture targets
- [x] 3.2 Remove the standalone Unity pack step from the NuGet release workflow
- [x] 3.3 Update the self-architecture policy for Core, CLI, and Testing only
- [x] 3.4 Update installation guidance, AGENTS package layout, static facade inventory, and the active Core architecture blueprint
- [x] 3.5 Update active OpenSpec capabilities and archive this change record
- [x] 3.6 Add a pull-request package-validation workflow that requires Core, CLI, and Testing artifacts only

## 4. Validation

- [x] 4.1 Preserve existing asmdef-only public behavior through migrated tests
- [x] 4.2 Verify the branch diff contains no active project, package, workflow, policy, or blueprint dependency on `ArchLinterNet.Unity`
- [ ] 4.3 Run the repository validation gates in pull-request CI
- [ ] 4.4 Run package validation and verify exactly Core, CLI, and Testing NuGet artifacts are produced
