using Microsoft.Extensions.Logging;

using QuickPatch.Actors.Application.Abstractions;
using QuickPatch.Actors.Application.Events;
using QuickPatch.Actors.Domain.Suppliers;
using QuickPatch.Actors.Domain.Technicians;

namespace QuickPatch.Actors.Application.Technicians;

/// <summary>
/// Mantiene el directorio de técnicos y proveedores con los eventos de Identity, bajo el tenant del evento y una vez por
/// <c>eventId</c> (ADR-007). Cuando cambia el equipo de un proveedor, lo recuenta en la misma transacción.
/// </summary>
public sealed partial class TechnicianEventsHandler(
    ITenantUnitOfWork unitOfWork,
    ITechnicianActorRepository technicians,
    ISupplierRepository suppliers,
    IProcessedEventRepository processedEvents,
    TimeProvider clock,
    ILogger<TechnicianEventsHandler> logger)
{
    public Task<HandlingOutcome> HandleAsync(EventEnvelope<TechnicianRegisteredData> envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var data = envelope.Data;
        return ApplyOnceAsync(envelope.EventId, envelope.TenantId, envelope.EventType, async (now, ct) =>
        {
            var technician = await technicians.FindAsync(data.TechnicianId, ct);
            if (technician is null)
            {
                technician = TechnicianActor.Create(envelope.TenantId, data.TechnicianId, data.SpecialtyId, data.VerificationStatus, data.UpdatedAt);
                technicians.Add(technician);
            }

            var previousSupplier = technician.ApplyRegistration(data.ProviderId, data.SpecialtyId, data.VerificationStatus, data.UpdatedAt);
            if (previousSupplier is { } before && before != data.ProviderId)
            {
                await RecountAsync(envelope.TenantId, before, now, ct);
            }

            if (data.ProviderId is { } supplierId)
            {
                await RecountAsync(envelope.TenantId, supplierId, now, ct);
            }

            LogRegistered(logger, data.TechnicianId, data.ProviderId);
            return HandlingOutcome.Applied;
        }, cancellationToken);
    }

    public Task<HandlingOutcome> HandleAsync(EventEnvelope<TechnicianVerificationChangedData> envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var data = envelope.Data;
        return ApplyOnceAsync(envelope.EventId, envelope.TenantId, envelope.EventType, async (now, ct) =>
        {
            var technician = await technicians.FindAsync(data.TechnicianId, ct);
            if (technician is null)
            {
                // Llegó antes que el registro: el técnico entra sin proveedor y el registro lo completa después.
                technicians.Add(TechnicianActor.Create(envelope.TenantId, data.TechnicianId, data.SpecialtyId, data.VerificationStatus, data.UpdatedAt));
                return HandlingOutcome.Applied;
            }

            if (!technician.ApplyVerification(data.SpecialtyId, data.VerificationStatus, data.UpdatedAt))
            {
                LogStale(logger, envelope.EventId, data.TechnicianId);
                return HandlingOutcome.Stale;
            }

            if (technician.SupplierId is { } supplierId)
            {
                await RecountAsync(envelope.TenantId, supplierId, now, ct);
            }

            return HandlingOutcome.Applied;
        }, cancellationToken);
    }

    private Task<HandlingOutcome> ApplyOnceAsync(
        Guid eventId,
        Guid tenantId,
        string eventType,
        Func<DateTimeOffset, CancellationToken, Task<HandlingOutcome>> apply,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(
            tenantId,
            async ct =>
            {
                if (await processedEvents.ExistsAsync(eventId, ct))
                {
                    return HandlingOutcome.Duplicate;
                }

                var now = clock.GetUtcNow();
                processedEvents.Add(eventId, tenantId, eventType, now);
                return await apply(now, ct);
            },
            cancellationToken);

    private async Task RecountAsync(Guid tenantId, Guid supplierId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var supplier = await suppliers.FindAsync(supplierId, cancellationToken);
        if (supplier is null)
        {
            supplier = Supplier.Create(tenantId, supplierId, now);
            suppliers.Add(supplier);
        }

        var team = await technicians.CountTeamAsync(supplierId, cancellationToken);
        supplier.Recount(team.Technicians, team.Approved, now);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Técnico {TechnicianId} en el directorio con el proveedor {SupplierId}.")]
    private static partial void LogRegistered(ILogger logger, Guid technicianId, Guid? supplierId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Evento {EventId} del técnico {TechnicianId} más viejo que el estado guardado; se descarta.")]
    private static partial void LogStale(ILogger logger, Guid eventId, Guid technicianId);
}