using QuickPatch.Actors.Domain.Suppliers;

namespace QuickPatch.Actors.Application.Abstractions;

/// <summary>Proveedores del directorio (tabla <c>suppliers</c>).</summary>
public interface ISupplierRepository
{
    Task<Supplier?> FindAsync(Guid supplierId, CancellationToken cancellationToken);

    void Add(Supplier supplier);
}