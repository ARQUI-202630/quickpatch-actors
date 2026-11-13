using QuickPatch.Actors.Domain.Clients;

namespace QuickPatch.Actors.Application.Abstractions;

/// <summary>Clientes del directorio (tabla <c>client_actors</c>).</summary>
public interface IClientActorRepository
{
    Task<ClientActor?> FindAsync(Guid clientId, CancellationToken cancellationToken);

    void Add(ClientActor client);
}