using System.Text.Json;

using Confluent.Kafka;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using QuickPatch.Actors.Application.Events;
using QuickPatch.Actors.Application.Technicians;
using QuickPatch.Transversal.Messaging;

namespace QuickPatch.Actors.Infrastructure.Messaging;

/// <summary>
/// Consumidor de <c>technician.registered</c> y <c>technician.verification-changed</c> v1. Solo transporte: deserializa,
/// descarta lo que no cumple el contrato y delega en <see cref="TechnicianEventsHandler"/>. Confirma el offset después de
/// aplicar; un error de base se reintenta.
/// </summary>
public sealed partial class TechnicianEventsConsumer(
    IServiceScopeFactory scopes,
    IConsumer<string, string> consumer,
    ILogger<TechnicianEventsConsumer> logger)
    : KafkaConsumerService(consumer, [EventTypes.TechnicianRegistered, EventTypes.TechnicianVerificationChanged], logger)
{
    protected override Task<bool> ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken) =>
        TryProcessAsync(result.Topic, result.Message.Value, cancellationToken);

    /// <summary>Aplica un mensaje del topic dado; devuelve false si hay que reintentarlo.</summary>
    public async Task<bool> TryProcessAsync(string topic, string value, CancellationToken cancellationToken)
    {
        try
        {
            if (topic == EventTypes.TechnicianRegistered)
            {
                var envelope = EventSerializer.Deserialize<EventEnvelope<TechnicianRegisteredData>>(value);
                return !TechnicianEventsContract.IsWellFormed(envelope)
                    ? Discard(topic)
                    : await ApplyAsync(envelope.EventId, h => h.HandleAsync(envelope, cancellationToken));
            }

            if (topic == EventTypes.TechnicianVerificationChanged)
            {
                var envelope = EventSerializer.Deserialize<EventEnvelope<TechnicianVerificationChangedData>>(value);
                return !TechnicianEventsContract.IsWellFormed(envelope)
                    ? Discard(topic)
                    : await ApplyAsync(envelope.EventId, h => h.HandleAsync(envelope, cancellationToken));
            }

            return Discard(topic);
        }
        catch (JsonException)
        {
            return Discard(topic);
        }
    }

    private async Task<bool> ApplyAsync(Guid eventId, Func<TechnicianEventsHandler, Task> apply)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await apply(scope.ServiceProvider.GetRequiredService<TechnicianEventsHandler>());
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogProcessingFailed(logger, eventId, ex);
            return false;
        }
    }

    private bool Discard(string topic)
    {
        LogInvalidMessage(logger, topic);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Mensaje de {Topic} que no cumple el contrato; se descarta.")]
    private static partial void LogInvalidMessage(ILogger logger, string topic);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falló el procesamiento del evento {EventId}; se reintentará.")]
    private static partial void LogProcessingFailed(ILogger logger, Guid eventId, Exception exception);
}