using System.Diagnostics.CodeAnalysis;

namespace QuickPatch.Actors.Application.Events;

/// <summary><c>data</c> de <c>technician.registered</c> v1.</summary>
public sealed record TechnicianRegisteredData(
    Guid TechnicianId, Guid SpecialtyId, Guid? ProviderId, string VerificationStatus, DateTimeOffset UpdatedAt);

/// <summary><c>data</c> de <c>technician.verification-changed</c> v1; Actors no guarda el motivo.</summary>
public sealed record TechnicianVerificationChangedData(
    Guid TechnicianId, Guid SpecialtyId, string PreviousStatus, string VerificationStatus, DateTimeOffset UpdatedAt);

/// <summary>Lo que un mensaje de técnico debe traer para aplicarse; si no lo cumple se descarta sin reintentar.</summary>
public static class TechnicianEventsContract
{
    public static bool IsWellFormed([NotNullWhen(true)] EventEnvelope<TechnicianRegisteredData>? envelope) =>
        envelope?.Data is { } data
        && IsValidEnvelope(envelope, EventTypes.TechnicianRegistered)
        && data.TechnicianId != Guid.Empty
        && data.ProviderId != Guid.Empty
        && Domain.Technicians.VerificationStatus.IsKnown(data.VerificationStatus);

    public static bool IsWellFormed([NotNullWhen(true)] EventEnvelope<TechnicianVerificationChangedData>? envelope) =>
        envelope?.Data is { } data
        && IsValidEnvelope(envelope, EventTypes.TechnicianVerificationChanged)
        && data.TechnicianId != Guid.Empty
        && Domain.Technicians.VerificationStatus.IsKnown(data.VerificationStatus);

    private static bool IsValidEnvelope<T>(EventEnvelope<T> envelope, string type) =>
        envelope.EventType == type && envelope.EventVersion == 1 && envelope.EventId != Guid.Empty && envelope.TenantId != Guid.Empty;
}