using System.Diagnostics.CodeAnalysis;

namespace QuickPatch.Actors.Application.Events;

/// <summary>
/// <c>data</c> de <c>service-request.created</c> v1, solo con lo que Actors usa: ni la descripción ni la ubicación
/// (RN-SR13).
/// </summary>
public sealed record ServiceRequestCreatedData(Guid ServiceRequestId, Guid ClientId, DateTimeOffset CreatedAt);

/// <summary>Lo que el mensaje debe traer para aplicarse; si no lo cumple se descarta sin reintentar.</summary>
public static class ServiceRequestCreatedContract
{
    public static bool IsWellFormed([NotNullWhen(true)] EventEnvelope<ServiceRequestCreatedData>? envelope) =>
        envelope?.Data is { } data
        && envelope.EventType == EventTypes.ServiceRequestCreated
        && envelope.EventVersion == 1
        && envelope.EventId != Guid.Empty
        && envelope.TenantId != Guid.Empty
        && data.ServiceRequestId != Guid.Empty
        && data.ClientId != Guid.Empty;
}