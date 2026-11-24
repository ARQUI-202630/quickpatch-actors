using Microsoft.EntityFrameworkCore;

using QuickPatch.Actors.Application.Abstractions;
using QuickPatch.Actors.Domain.Clients;

namespace QuickPatch.Actors.Infrastructure.Persistence;

public sealed class ClientActorRepository(ActorsDbContext db) : IClientActorRepository
{
    public Task<ClientActor?> FindAsync(Guid clientId, CancellationToken cancellationToken) =>
        db.Clients.SingleOrDefaultAsync(x => x.ClientId == clientId, cancellationToken);

    public void Add(ClientActor client) => db.Clients.Add(client);
}