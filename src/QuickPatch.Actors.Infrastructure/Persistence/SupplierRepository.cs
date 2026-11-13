using Microsoft.EntityFrameworkCore;

using QuickPatch.Actors.Application.Abstractions;
using QuickPatch.Actors.Domain.Suppliers;

namespace QuickPatch.Actors.Infrastructure.Persistence;

public sealed class SupplierRepository(ActorsDbContext db) : ISupplierRepository
{
    public Task<Supplier?> FindAsync(Guid supplierId, CancellationToken cancellationToken) =>
        db.Suppliers.SingleOrDefaultAsync(x => x.SupplierId == supplierId, cancellationToken);

    public void Add(Supplier supplier) => db.Suppliers.Add(supplier);
}