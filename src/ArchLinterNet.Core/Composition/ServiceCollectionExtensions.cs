using ArchLinterNet.Core.Asmdef;
using ArchLinterNet.Core.Asmdef.Abstractions;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Discovery.Abstractions;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Resolution.Abstractions;
using ArchLinterNet.Core.Scanning;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Core.Validation.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ArchLinterNet.Core.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArchLinterNetCore(this IServiceCollection services)
    {
        services.AddSingleton<IArchitectureFileSystem, ArchitectureFileSystem>();
        services.AddSingleton<IArchitectureEnvironment, ArchitectureEnvironment>();
        services.AddSingleton<IArchitectureAssemblyLoader, ArchitectureAssemblyLoader>();
        services.AddSingleton<IRoslynCompilationFactory, RoslynCompilationFactory>();
        services.AddSingleton<IArchitecturePolicyDocumentLoader, ArchitecturePolicyDocumentLoader>();
        services.AddSingleton<IArchitectureBaselineLoadingService, ArchitectureBaselineLoadingService>();
        services.AddSingleton<IArchitectureBaselineGenerator, ArchitectureBaselineGenerator>();
        services.AddSingleton<IArchitectureDiagnosticFormatter, ArchitectureDiagnosticFormatter>();
        services.AddSingleton<IArchitectureRepositoryRootResolver, ArchitectureRepositoryRootResolver>();
        services.AddSingleton<IConditionSetResolutionService, ConditionSetResolutionService>();
        services.AddSingleton<IArchitectureSolutionParser, ArchitectureSolutionParser>();
        services.AddSingleton<IArchitectureProjectFileParser, ArchitectureProjectFileParser>();
        services.AddSingleton<IArchitectureProjectDiscoveryService>(sp => new ArchitectureProjectDiscoveryService(
            sp.GetRequiredService<IArchitectureFileSystem>(),
            sp.GetRequiredService<IArchitectureSolutionParser>(),
            sp.GetRequiredService<IArchitectureProjectFileParser>()));
        services.AddSingleton<IArchitectureAssemblyResolutionService, ArchitectureAssemblyResolutionService>();
        services.AddSingleton<IArchitectureAsmdefScanner, ArchitectureAsmdefScanner>();
        services.AddSingleton<IArchitectureSourceScanner, ArchitectureSourceScanner>();
        services.AddSingleton<IArchitectureExternalDependencyIlScanner, ArchitectureExternalDependencyIlScanner>();
        services.AddSingleton<IArchitectureIlMethodBodyScanner, ArchitectureIlMethodBodyScanner>();
        services.AddSingleton<IArchitectureRunnerSetupService, ArchitectureRunnerSetupService>();
        services.AddSingleton<IArchitectureContractHandler, DependencyContractHandler>();
        services.AddSingleton<IArchitectureContractHandler, LayerContractHandler>();
        services.AddSingleton<IArchitectureContractHandler, AllowOnlyContractHandler>();
        services.AddSingleton<IArchitectureContractHandler, CycleContractHandler>();
        services.AddSingleton<IArchitectureContractHandler, AcyclicSiblingContractHandler>();
        services.AddSingleton<IArchitectureContractHandler, MethodBodyContractHandler>();
        services.AddSingleton<IArchitectureContractHandler, AsmdefContractHandler>();
        services.AddSingleton<IArchitectureContractHandler, IndependenceContractHandler>();
        services.AddSingleton<IArchitectureContractHandler, ProtectedContractHandler>();
        services.AddSingleton<IArchitectureContractHandler, ExternalContractHandler>();
        services.AddSingleton<IArchitectureContractHandler, CoverageContractHandler>();
        services.AddSingleton<ArchitectureContractHandlerRegistry>();
        services.AddSingleton<IArchitectureContractHandlerRegistry>(ResolveHandlerRegistry);
        services.AddSingleton<IArchitectureContractExecutor, ArchitectureContractExecutor>();
        services.AddSingleton<IArchitectureValidationApplicationService, ArchitectureValidationApplicationService>();
        services.AddSingleton<IArchitectureBaselineApplicationService, ArchitectureBaselineApplicationService>();
        services.AddSingleton<IAsmdefValidationService, AsmdefValidationService>();
        return services;
    }

    private static ArchitectureContractHandlerRegistry ResolveHandlerRegistry(IServiceProvider sp)
    {
        return sp.GetRequiredService<ArchitectureContractHandlerRegistry>();
    }
}
