using Microsoft.Extensions.DependencyInjection;

using QuickPatch.Actors.Application.Clients;
using QuickPatch.Actors.Application.Technicians;

namespace QuickPatch.Actors.Application;

/// <summary>Capa de aplicación: casos de uso que aplican los eventos consumidos (SDD 6.3).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TechnicianEventsHandler>();
        services.AddScoped<ServiceRequestCreatedHandler>();
        return services;
    }
}