using System.Text.Json;

using Confluent.Kafka;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using QuickPatch.Actors.Application.Clients;
using QuickPatch.Actors.Application.Events;
using QuickPatch.Transversal.Messaging;

namespace QuickPatch.Actors.Infrastructure.Messaging;

/// <summary>
/// Consumidor de <c>service-request.created</c> v1. Solo transporte: deserializa, descarta lo que no cumple el contrato y
/// delega en <see cref="ServiceRequestCreatedHandler"/>. Confirma el offset después de aplicar.
/// </summary>
public sealed partial class ServiceRequestCreatedConsumer(
    IServiceScopeFactory scopes,
    IConsumer<string, string> consumer,
    ILogger<ServiceRequestCreatedConsumer> logger)
    : KafkaConsumerService(consumer, [EventTypes.ServiceRequestCreated], logger)
{
    protected override Task<bool> ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken) =>
        TryProcessAsync(result.Message.Value, cancellationToken);

    /// <summary>Aplica un mensaje; devuelve false si hay que reintentarlo.</summary>
    public async Task<bool> TryProcessAsync(string value, CancellationToken cancellationToken)
    {
        EventEnvelope<ServiceRequestCreatedData>? envelope;
        try
        {
            envelope = EventSerializer.Deserialize<EventEnvelope<ServiceRequestCreatedData>>(value);
        }
        catch (JsonException)
        {
            envelope = null;
        }

        if (!ServiceRequestCreatedContract.IsWellFormed(envelope))
        {
            LogInvalidMessage(logger);
            return true;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ServiceRequestCreatedHandler>().HandleAsync(envelope, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogProcessingFailed(logger, envelope.EventId, ex);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Mensaje de service-request.created que no cumple el contrato; se descarta.")]
    private static partial void LogInvalidMessage(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falló el procesamiento del evento {EventId}; se reintentará.")]
    private static partial void LogProcessingFailed(ILogger logger, Guid eventId, Exception exception);
}