using QuickPatch.Actors.Domain.Technicians;

namespace QuickPatch.Actors.Application.Abstractions;

/// <summary>Técnicos del directorio (tabla <c>technician_actors</c>), siempre bajo el tenant de la transacción.</summary>
public interface ITechnicianActorRepository
{
    Task<TechnicianActor?> FindAsync(Guid technicianId, CancellationToken cancellationToken);

    void Add(TechnicianActor technician);

    /// <summary>Tamaño del equipo del proveedor y cuántos están aprobados, contando los cambios aún no guardados.</summary>
    Task<TeamCount> CountTeamAsync(Guid supplierId, CancellationToken cancellationToken);
}

/// <summary>Conteo del equipo de un proveedor.</summary>
public readonly record struct TeamCount(int Technicians, int Approved);