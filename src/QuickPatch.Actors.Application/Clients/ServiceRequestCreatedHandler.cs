using QuickPatch.Actors.Application.Abstractions;
using QuickPatch.Actors.Application.Events;
using QuickPatch.Actors.Domain.Clients;

namespace QuickPatch.Actors.Application.Clients;

/// <summary>
/// Al consumir <c>service-request.created</c> suma la solicitud al cliente en el directorio del tenant, una vez por
/// <c>eventId</c> (ADR-007).
/// </summary>
public sealed class ServiceRequestCreatedHandler(
    ITenantUnitOfWork unitOfWork,
    IClientActorRepository clients,
    IProcessedEventRepository processedEvents,
    TimeProvider clock)
{
    public Task<HandlingOutcome> HandleAsync(EventEnvelope<ServiceRequestCreatedData> envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var data = envelope.Data;
        return unitOfWork.ExecuteAsync(
            envelope.TenantId,
            async ct =>
            {
                if (await processedEvents.ExistsAsync(envelope.EventId, ct))
                {
                    return HandlingOutcome.Duplicate;
                }

                processedEvents.Add(envelope.EventId, envelope.TenantId, envelope.EventType, clock.GetUtcNow());
                var client = await clients.FindAsync(data.ClientId, ct);
                if (client is null)
                {
                    client = ClientActor.Create(envelope.TenantId, data.ClientId, data.CreatedAt);
                    clients.Add(client);
                }

                client.RecordRequest(data.CreatedAt);
                return HandlingOutcome.Applied;
            },
            cancellationToken);
    }
}