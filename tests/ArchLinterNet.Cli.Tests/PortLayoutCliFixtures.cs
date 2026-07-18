// Sales/Catalog/LegacyCrm-style fixtures for CLI-level end-to-end tests of the port-boundary,
// anti-corruption-layer, and layout-convention contract families, mirroring
// ContextualContractCliFixtures.cs. Kept in the CLI test project so the policy YAML in
// PortLayoutCliTests.cs can target "ArchLinterNet.Cli.Tests" as target_assemblies and have these
// types resolved from the running test process's own already-loaded entry assembly.
namespace PortLayoutCliFixtures
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PortLayoutDomainMarkerAttribute : Attribute
    {
        public PortLayoutDomainMarkerAttribute(string module)
        {
            Module = module;
        }

        public string Module { get; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PortLayoutApplicationMarkerAttribute : Attribute
    {
        public PortLayoutApplicationMarkerAttribute(string module)
        {
            Module = module;
        }

        public string Module { get; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PortLayoutPortMarkerAttribute : Attribute
    {
        public PortLayoutPortMarkerAttribute(string module)
        {
            Module = module;
        }

        public string Module { get; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PortLayoutAdapterMarkerAttribute : Attribute
    {
        public PortLayoutAdapterMarkerAttribute(string module)
        {
            Module = module;
        }

        public string Module { get; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PortLayoutAclMarkerAttribute : Attribute
    {
        public PortLayoutAclMarkerAttribute(string module)
        {
            Module = module;
        }

        public string Module { get; }
    }

    // Sales -> Catalog port-seam scenario.
    [PortLayoutPortMarker("Catalog")]
    public sealed class PortLayoutCatalogPort;

    [PortLayoutDomainMarker("Catalog")]
    public sealed class PortLayoutCatalogItem;

    [PortLayoutAdapterMarker("Catalog")]
    public sealed class PortLayoutCatalogAdapter;

    [PortLayoutApplicationMarker("Sales")]
    public sealed class PortLayoutSalesUsesCatalogPort
    {
        public PortLayoutCatalogPort Port { get; } = null!;
    }

    [PortLayoutApplicationMarker("Sales")]
    public sealed class PortLayoutSalesReferencesCatalogDomainDirectly
    {
        public PortLayoutCatalogItem Item { get; } = null!;
    }

    // LegacyCrm anti-corruption-layer scenario.
    [PortLayoutAclMarker("LegacyCrm")]
    public sealed class PortLayoutLegacyCrmAcl;

    [PortLayoutAdapterMarker("LegacyCrm")]
    public sealed class PortLayoutLegacyCrmDatabaseAdapter;

    [PortLayoutDomainMarker("LegacyCrm")]
    public sealed class PortLayoutLegacyCrmUsesAcl
    {
        public PortLayoutLegacyCrmAcl Acl { get; } = null!;
    }

    [PortLayoutDomainMarker("LegacyCrm")]
    public sealed class PortLayoutLegacyCrmReferencesDatabaseDirectly
    {
        public PortLayoutLegacyCrmDatabaseAdapter Adapter { get; } = null!;
    }
}

// Layout-convention fixtures: types below are ALSO written to matching on-disk .cs files at test
// time (PortLayoutCliTests.WriteSourceFile), because layout convention selectors require real
// source-file facts (folder segment, file name) that only exist once a matching file is scanned
// from analysis.source_roots. See LayoutConventionContractTests.cs for the same Core-level pattern.
namespace PortLayoutCliFixtures.Services
{
    public sealed class PortLayoutOrderService;

    public sealed class PortLayoutPaymentService;
}

namespace PortLayoutCliFixtures.Interfaces
{
    public interface IPortLayoutOrderService;
}

namespace PortLayoutCliFixtures.Handlers
{
    public sealed class PortLayoutWhenNarrowedTarget;

    public sealed class PortLayoutWhenIgnoredSibling;
}
