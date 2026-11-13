using Confluent.Kafka;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using QuickPatch.Actors.Application;
using QuickPatch.Actors.Application.Abstractions;
using QuickPatch.Actors.Infrastructure.Messaging;
using QuickPatch.Actors.UnitTests.Support;

namespace QuickPatch.Actors.UnitTests.Infrastructure;

public sealed class ConsumerTests : IDisposable
{
    private readonly IConsumer<string, string> kafka =
        new ConsumerBuilder<string, string>(new ConsumerConfig { BootstrapServers = "localhost:1", GroupId = "pruebas" }).Build();

    public void Dispose() => kafka.Dispose();

    private static ServiceProvider Services(InMemoryActors? store)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        if (store is not null)
        {
            services.AddSingleton<ITenantUnitOfWork>(store);
            services.AddSingleton<ITechnicianActorRepository>(store);
            services.AddSingleton<ISupplierRepository>(store);
            services.AddSingleton<IClientActorRepository>(store);
            services.AddSingleton<IProcessedEventRepository>(store);
        }

        return services.BuildServiceProvider();
    }

    private static string Sobre(string tipo, Guid tenant, string data) =>
        $$"""
        {"eventId":"{{Guid.NewGuid()}}","eventType":"{{tipo}}","eventVersion":1,"occurredAt":"2026-11-12T15:00:00Z",
         "correlationId":"{{Guid.NewGuid()}}","tenantId":"{{tenant}}","producer":"pruebas","data":{{data}}}
        """;

    private static string Registro(Guid tenant, Guid tecnico, string estado = "pendiente", string tipo = "technician.registered") =>
        Sobre(tipo, tenant, $$"""
            {"technicianId":"{{tecnico}}","specialtyId":"{{Guid.NewGuid()}}","providerId":"{{Guid.NewGuid()}}",
             "verificationStatus":"{{estado}}","updatedAt":"2026-11-12T15:00:00Z"}
            """);

    private static string Verificacion(Guid tenant, Guid tecnico, string estado) =>
        Sobre("technician.verification-changed", tenant, $$"""
            {"technicianId":"{{tecnico}}","specialtyId":"{{Guid.NewGuid()}}","previousStatus":"pendiente",
             "verificationStatus":"{{estado}}","reason":null,"updatedAt":"2026-11-12T16:00:00Z"}
            """);

    private static string Solicitud(Guid tenant, Guid cliente) =>
        Sobre("service-request.created", tenant, $$"""
            {"serviceRequestId":"{{Guid.NewGuid()}}","clientId":"{{cliente}}","categoryId":"{{Guid.NewGuid()}}",
             "description":"No se guarda","location":{"address":"No se guarda"},"createdAt":"2026-11-12T15:00:00Z"}
            """);

    [Fact]
    public async Task ConsumidorDeTecnicos_AplicaLosDosTopics_YDescartaLoQueNoCumpleElContrato()
    {
        var store = new InMemoryActors();
        await using var services = Services(store);
        var consumer = new TechnicianEventsConsumer(
            services.GetRequiredService<IServiceScopeFactory>(), kafka, NullLogger<TechnicianEventsConsumer>.Instance);
        var (tenant, tecnico) = (Guid.NewGuid(), Guid.NewGuid());

        Assert.True(await consumer.TryProcessAsync("technician.registered", Registro(tenant, tecnico), default));
        Assert.True(await consumer.TryProcessAsync("technician.verification-changed", Verificacion(tenant, tecnico, "aprobado"), default));
        Assert.True(await consumer.TryProcessAsync("technician.registered", "{no es json", default));
        Assert.True(await consumer.TryProcessAsync("technician.registered", "null", default));
        Assert.True(await consumer.TryProcessAsync("technician.registered", Registro(tenant, Guid.NewGuid(), "otro"), default));
        Assert.True(await consumer.TryProcessAsync("technician.registered", Registro(Guid.Empty, Guid.NewGuid()), default));
        Assert.True(await consumer.TryProcessAsync("technician.registered", Registro(tenant, Guid.NewGuid(), tipo: "otro.evento"), default));
        Assert.True(await consumer.TryProcessAsync("technician.verification-changed", Verificacion(tenant, Guid.Empty, "aprobado"), default));
        Assert.True(await consumer.TryProcessAsync("otro.topic", Registro(tenant, Guid.NewGuid()), default));

        Assert.True(Assert.Single(store.Technicians).IsApproved);
    }

    [Fact]
    public async Task ConsumidorDeSolicitudes_GuardaSoloElConteo_YDescartaLoQueNoCumpleElContrato()
    {
        var store = new InMemoryActors();
        await using var services = Services(store);
        var consumer = new ServiceRequestCreatedConsumer(
            services.GetRequiredService<IServiceScopeFactory>(), kafka, NullLogger<ServiceRequestCreatedConsumer>.Instance);
        var tenant = Guid.NewGuid();

        Assert.True(await consumer.TryProcessAsync(Solicitud(tenant, Guid.NewGuid()), default));
        Assert.True(await consumer.TryProcessAsync("{no es json", default));
        Assert.True(await consumer.TryProcessAsync(Solicitud(tenant, Guid.Empty), default));

        Assert.Equal(1, Assert.Single(store.Clients).RequestCount);
    }

    [Fact]
    public async Task Consumidores_SiFallaLaBase_PidenReintentar()
    {
        await using var services = Services(null);
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var tecnicos = new TechnicianEventsConsumer(scopes, kafka, NullLogger<TechnicianEventsConsumer>.Instance);
        var solicitudes = new ServiceRequestCreatedConsumer(scopes, kafka, NullLogger<ServiceRequestCreatedConsumer>.Instance);

        Assert.False(await tecnicos.TryProcessAsync("technician.registered", Registro(Guid.NewGuid(), Guid.NewGuid()), default));
        Assert.False(await solicitudes.TryProcessAsync(Solicitud(Guid.NewGuid(), Guid.NewGuid()), default));
    }
}