using QuickPatch.Actors.Application.Abstractions;
using QuickPatch.Actors.Domain.Clients;
using QuickPatch.Actors.Domain.Suppliers;
using QuickPatch.Actors.Domain.Technicians;

namespace QuickPatch.Actors.UnitTests.Support;

/// <summary>Puertos de Application en memoria, filtrados por el tenant de la transacción como lo haría RLS.</summary>
public sealed class InMemoryActors : ITenantUnitOfWork, ITechnicianActorRepository, ISupplierRepository, IClientActorRepository, IProcessedEventRepository
{
    private Guid tenant;

    public List<TechnicianActor> Technicians { get; } = [];

    public List<Supplier> Suppliers { get; } = [];

    public List<ClientActor> Clients { get; } = [];

    public HashSet<Guid> Processed { get; } = [];

    public async Task<T> ExecuteAsync<T>(Guid tenantId, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        tenant = tenantId;
        return await work(cancellationToken);
    }

    public Task<TechnicianActor?> FindAsync(Guid technicianId, CancellationToken cancellationToken) =>
        Task.FromResult(Technicians.SingleOrDefault(x => x.TenantId == tenant && x.TechnicianId == technicianId));

    public void Add(TechnicianActor technician) => Technicians.Add(technician);

    public Task<TeamCount> CountTeamAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        var team = Technicians.Where(x => x.TenantId == tenant && x.SupplierId == supplierId).ToList();
        return Task.FromResult(new TeamCount(team.Count, team.Count(x => x.IsApproved)));
    }

    Task<Supplier?> ISupplierRepository.FindAsync(Guid supplierId, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.SingleOrDefault(x => x.TenantId == tenant && x.SupplierId == supplierId));

    public void Add(Supplier supplier) => Suppliers.Add(supplier);

    Task<ClientActor?> IClientActorRepository.FindAsync(Guid clientId, CancellationToken cancellationToken) =>
        Task.FromResult(Clients.SingleOrDefault(x => x.TenantId == tenant && x.ClientId == clientId));

    public void Add(ClientActor client) => Clients.Add(client);

    public Task<bool> ExistsAsync(Guid eventId, CancellationToken cancellationToken) => Task.FromResult(Processed.Contains(eventId));

    public void Add(Guid eventId, Guid tenantId, string eventType, DateTimeOffset processedAt) => Processed.Add(eventId);
}