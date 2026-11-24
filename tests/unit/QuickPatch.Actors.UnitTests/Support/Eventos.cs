using QuickPatch.Actors.Application.Events;

namespace QuickPatch.Actors.UnitTests.Support;

/// <summary>Sobres de prueba de los eventos que consume Actors.</summary>
public static class Eventos
{
    public static readonly DateTimeOffset T0 = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);

    public static EventEnvelope<TechnicianRegisteredData> Registrado(
        Guid tenant, Guid tecnico, Guid? proveedor, string estado = "pendiente", DateTimeOffset? cuando = null) =>
        Sobre(tenant, EventTypes.TechnicianRegistered, new TechnicianRegisteredData(tecnico, Guid.NewGuid(), proveedor, estado, cuando ?? T0));

    public static EventEnvelope<TechnicianVerificationChangedData> Verificado(
        Guid tenant, Guid tecnico, string estado, DateTimeOffset cuando) =>
        Sobre(tenant, EventTypes.TechnicianVerificationChanged, new TechnicianVerificationChangedData(tecnico, Guid.NewGuid(), "pendiente", estado, cuando));

    public static EventEnvelope<ServiceRequestCreatedData> SolicitudCreada(Guid tenant, Guid cliente, DateTimeOffset cuando) =>
        Sobre(tenant, EventTypes.ServiceRequestCreated, new ServiceRequestCreatedData(Guid.NewGuid(), cliente, cuando));

    private static EventEnvelope<T> Sobre<T>(Guid tenant, string tipo, T data) =>
        new(Guid.NewGuid(), tipo, 1, T0, Guid.NewGuid(), tenant, "pruebas", data);
}