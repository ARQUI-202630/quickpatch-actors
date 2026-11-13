using Confluent.Kafka;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using QuickPatch.Actors.Application.Abstractions;
using QuickPatch.Actors.Infrastructure.Messaging;
using QuickPatch.Actors.Infrastructure.Options;
using QuickPatch.Actors.Infrastructure.Persistence;

namespace QuickPatch.Actors.Infrastructure;

/// <summary>
/// Capa de infraestructura: PostgreSQL (EF Core) y Kafka (SDD 6.3). Implementa los puertos de Application. Actors no tiene
/// REST hacia el gateway (ADR-025) ni publica eventos: arma el directorio de actores del tenant con lo que consume.
/// </summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "Actors";
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.Section));
        services.AddDbContext<ActorsDbContext>(options => options.UseNpgsql(configuration.GetConnectionString(ConnectionStringName)));

        services.AddScoped<ITenantUnitOfWork, TenantUnitOfWork>();
        services.AddScoped<ITechnicianActorRepository, TechnicianActorRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IClientActorRepository, ClientActorRepository>();
        services.AddScoped<IProcessedEventRepository, ProcessedEventRepository>();

        services.AddHealthChecks().AddDbContextCheck<ActorsDbContext>("postgresql", tags: [ReadyTag]);

        var kafka = configuration.GetSection(KafkaOptions.Section).Get<KafkaOptions>() ?? new KafkaOptions();
        if (kafka.Enabled)
        {
            // Un consumidor de Kafka por familia de eventos, en el mismo grupo: cada uno confirma sus propios offsets.
            services.AddHostedService(sp => new TechnicianEventsConsumer(
                sp.GetRequiredService<IServiceScopeFactory>(), BuildConsumer(kafka), sp.GetRequiredService<ILogger<TechnicianEventsConsumer>>()));
            services.AddHostedService(sp => new ServiceRequestCreatedConsumer(
                sp.GetRequiredService<IServiceScopeFactory>(), BuildConsumer(kafka), sp.GetRequiredService<ILogger<ServiceRequestCreatedConsumer>>()));
        }

        return services;
    }

    private static IConsumer<string, string> BuildConsumer(KafkaOptions kafka) =>
        new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            GroupId = kafka.ConsumerGroupId,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        }).Build();
}