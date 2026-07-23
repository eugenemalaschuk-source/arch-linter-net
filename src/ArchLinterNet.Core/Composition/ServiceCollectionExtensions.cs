using ArchLinterNet.Core.Asmdef;
using ArchLinterNet.Core.Asmdef.Abstractions;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Discovery.Abstractions;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Graph.Abstractions;
using ArchLinterNet.Core.IO;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Resolution.Abstractions;
using ArchLinterNet.Core.Scanning;
using ArchLinterNet.Core.Scanning.Abstractions;
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
        services.AddSingleton<IArchitectureSarifFormatter, ArchitectureSarifFormatter>();
        services.AddSingleton<IArchitectureRepositoryRootResolver, ArchitectureRepositoryRootResolver>();
        services.AddSingleton<IConditionSetResolutionService, ConditionSetResolutionService>();
        services.AddSingleton<IArchitectureSolutionParser, ArchitectureSolutionParser>();
        services.AddSingleton<IArchitectureProjectFileParser, ArchitectureProjectFileParser>();
        services.AddSingleton<IArchitectureProjectDiscoveryService>(sp => new ArchitectureProjectDiscoveryService(
            sp.GetRequiredService<IArchitectureFileSystem>(),
            sp.GetRequiredService<IArchitectureSolutionParser>(),
            sp.GetRequiredService<IArchitectureProjectFileParser>()));
        services.AddSingleton<IArchitectureProjectRoslynContextResolver, ArchitectureProjectRoslynContextResolver>();
        services.AddSingleton<IArchitectureFrameworkReferenceEvaluator, ArchitectureFrameworkReferenceEvaluator>();
        services.AddSingleton<IArchitectureAssemblyResolutionService, ArchitectureAssemblyResolutionService>();
        services.AddSingleton<IArchitectureAsmdefScanner, ArchitectureAsmdefScanner>();
        services.AddSingleton<IArchitectureSourceScanner, ArchitectureSourceScanner>();
        services.AddSingleton<IArchitectureExternalDependencyIlScanner, ArchitectureExternalDependencyIlScanner>();
        services.AddSingleton<IArchitectureIlMethodBodyScanner, ArchitectureIlMethodBodyScanner>();
        services.AddSingleton<IArchitectureRunnerSetupService, ArchitectureRunnerSetupService>();
        services.AddSingleton<ArchitectureContractHandlerRegistry>();
        services.AddSingleton<IArchitectureContractHandlerRegistry>(ResolveHandlerRegistry);
        services.AddSingleton<IArchitectureContractExecutor, ArchitectureContractExecutor>();
        services.AddSingleton<IArchitectureValidationApplicationService, ArchitectureValidationApplicationService>();
        services.AddSingleton<IArchitectureBaselineApplicationService, ArchitectureBaselineApplicationService>();
        services.AddSingleton<IAsmdefValidationService, AsmdefValidationService>();
        services.AddSingleton<IArchitectureGraphFormatter, ArchitectureGraphFormatter>();
        services.AddSingleton<IArchitectureGraphApplicationService, ArchitectureGraphApplicationService>();
        services.AddSingleton<IArchitectureExplainApplicationService, ArchitectureExplainApplicationService>();
        return services;
    }

    private static ArchitectureContractHandlerRegistry ResolveHandlerRegistry(IServiceProvider sp)
    {
        return sp.GetRequiredService<ArchitectureContractHandlerRegistry>();
    }
}
