using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace ArchLinterNet.Core.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArchLinterNetCore(this IServiceCollection services)
    {
        services.AddSingleton<IArchitecturePolicyDocumentLoader, ArchitecturePolicyDocumentLoader>();
        services.AddSingleton<IArchitectureBaselineLoadingService, ArchitectureBaselineLoadingService>();
        services.AddSingleton<IArchitectureRepositoryRootResolver, ArchitectureRepositoryRootResolver>();
        services.AddSingleton<IConditionSetResolutionService, ConditionSetResolutionService>();
        services.AddSingleton<IArchitectureProjectDiscoveryService, ArchitectureProjectDiscoveryService>();
        services.AddSingleton<IArchitectureAssemblyResolutionService, ArchitectureAssemblyResolutionService>();
        services.AddSingleton<IArchitectureRunnerSetupService, ArchitectureRunnerSetupService>();
        services.AddSingleton<IArchitectureValidationApplicationService, ArchitectureValidationApplicationService>();
        services.AddSingleton<IArchitectureBaselineApplicationService, ArchitectureBaselineApplicationService>();
        return services;
    }
}
