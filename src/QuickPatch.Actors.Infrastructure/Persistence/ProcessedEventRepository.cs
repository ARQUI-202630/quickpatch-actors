using QuickPatch.Actors.Application.Abstractions;
using QuickPatch.Transversal.Messaging;

namespace QuickPatch.Actors.Infrastructure.Persistence;

/// <summary>Idempotencia de los consumidores (RN-EV1) con la implementación común de ADR-020.</summary>
public sealed class ProcessedEventRepository(ActorsDbContext db) : ProcessedEventStore<ActorsDbContext>(db), IProcessedEventRepository;