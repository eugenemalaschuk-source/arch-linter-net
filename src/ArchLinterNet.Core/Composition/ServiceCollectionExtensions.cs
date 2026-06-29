using ArchLinterNet.Core.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace ArchLinterNet.Core.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArchLinterNetCore(this IServiceCollection services)
    {
        services.AddSingleton<IArchitectureValidationApplicationService, ArchitectureValidationApplicationService>();
        services.AddSingleton<IArchitectureBaselineApplicationService, ArchitectureBaselineApplicationService>();
        return services;
    }
}
