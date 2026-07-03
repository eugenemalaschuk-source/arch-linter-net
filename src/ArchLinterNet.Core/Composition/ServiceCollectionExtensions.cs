using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;
using ArchLinterNet.Core.Validation;
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
        services.AddSingleton<IArchitectureProjectDiscoveryService, ArchitectureProjectDiscoveryService>();
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
        services.AddSingleton<IArchitectureContractExecutor, ArchitectureContractExecutor>();
        services.AddSingleton<IArchitectureValidationApplicationService, ArchitectureValidationApplicationService>();
        services.AddSingleton<IArchitectureBaselineApplicationService, ArchitectureBaselineApplicationService>();
        return services;
    }
}
