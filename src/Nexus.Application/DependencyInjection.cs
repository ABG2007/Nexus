using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.Services;
using Nexus.Core.Services;

namespace Nexus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNexusApplication(this IServiceCollection services)
    {
        services.AddSingleton<CommandInterpreter>();
        services.AddSingleton<TabManager>();
        services.AddScoped<NavigationService>();
        services.AddSingleton<AiOrchestrator>();
        return services;
    }
}