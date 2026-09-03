namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Runtime-only additions layered onto a <see cref="GitVersionedAdoptionFixture"/> copy of
/// `modular-consumer` for the v0.8 full-cycle Checkpoint B scenario. Nothing here is checked into
/// the shared `modular-consumer` fixture itself: every other scenario that reuses that checked-in
/// fixture must keep seeing its original, unmodified, cleanly-passing policy.
/// </summary>
internal static class V08FullCycleFragmentContent
{
    // A declared topology over the fixture's existing layers (subject_kind: type observes each
    // type; `modules` and `composition_host` reuse the layer keys already declared in
    // fragments/layers.yml, so no assembly/namespace list is duplicated here). The `modules`
    // outgoing-component-count metric backs the enforced budget in the strict_metric_budgets
    // contract below.
    internal const string TopologyAndMetrics = """
        topology:
          mode: exhaustive
          subject_kind: type
          scope:
            selectors:
              - layer: shared_abstractions
              - layer: modules
              - layer: composition_host
          nodes:
            - id: shared-abstractions
              mappings:
                - layer: shared_abstractions
            - id: modules
              mappings:
                - layer: modules
            - id: composition-host
              mappings:
                - layer: composition_host
          allowed_edges:
            - from: modules
              to: shared-abstractions
            - from: composition-host
              to: shared-abstractions
            - from: composition-host
              to: modules

        metrics:
          - id: modules-outgoing
            kind: outgoing_component_count
            topology_node: modules
        """;

    // One contracts: block (a YAML document cannot repeat a top-level key, so the exposure
    // contract and the metric budget below must share this single mapping rather than each
    // declaring their own contracts: key). Sourced directly from src/**/*.csproj-resolved
    // assemblies (no reviewed public-API surface needed for this proof): M01's exported
    // ModuleContracts type must not disclose the module's internal state type, even nested inside
    // a generic wrapper. The budget's maximum is 0: the modules component's one legitimate
    // dependency (modules -> shared-abstractions, enforced separately by the fixture's own
    // pre-existing modules-reference-abstractions-only contract) already gives an outgoing count
    // of 1, so this proves budget enforcement fires without needing a project-reference change
    // (an M01 -> composition_host reference would create a circular project reference, since
    // Synthetic.Composition already references every module project).
    internal const string Contracts = """
        contracts:
          strict_contract_surface_exposure:
            - id: m01-contracts-do-not-expose-internal-state
              name: m01-contracts-do-not-expose-internal-state
              source:
                assemblies: [Synthetic.Modules.M01]
                types_matching:
                  name_suffix: Contracts
              forbidden:
                - namespace: Synthetic.Modules.M01.Internal
              reason: A module's exported contract surface must not disclose its internal state type, even nested inside a generic wrapper.
          strict_metric_budgets:
            - id: modules-outgoing-limit
              metric: modules-outgoing
              maximum: 0
        """;

    internal const string ExternalEvidence = """
        external_evidence:
          - id: v08-static-analysis
            format: sarif
            required: true
            tool: V08 Synthetic Analyzer
            run: v08-full-cycle
            require_repository: true
            require_revision: true
            require_scope: true
        """;

    // The forbidden internal type: exported through ModuleContracts.GetSnapshot's
    // IReadOnlyList<ModuleInternalState> return type, matching the recursive
    // generic-wrapper-position exposure shape the contract-surface-exposure family targets.
    internal const string ModuleInternalStateSource = """
        namespace Synthetic.Modules.M01.Internal;

        /// <summary>Internal module state that must never leak through the module's exported contract surface.</summary>
        public sealed class ModuleInternalState
        {
            public string Snapshot => "internal";
        }
        """;

    internal const string ModuleContractsSource = """
        using Synthetic.Modules.M01.Internal;

        namespace Synthetic.Modules.M01;

        /// <summary>
        /// The module's exported contract surface. GetSnapshot deliberately returns
        /// IReadOnlyList&lt;ModuleInternalState&gt;, nesting the forbidden internal type inside a
        /// generic wrapper position -- the shape the v0.8 recursive contract-surface exposure proof
        /// targets.
        /// </summary>
        public static class ModuleContracts
        {
            public static System.Collections.Generic.IReadOnlyList<ModuleInternalState> GetSnapshot()
                => System.Array.Empty<ModuleInternalState>();
        }
        """;

    // An empty, structurally-valid baseline: `health` unconditionally requires --baseline, but a
    // clean fixture (or one whose violations must stay deliberately unreviewed) has nothing to
    // carry. Matches the shape CliIntegrationTests.Health.cs already uses for the same purpose.
    internal const string EmptyBaseline = "version: 2\nbaseline: {}\n";
}
