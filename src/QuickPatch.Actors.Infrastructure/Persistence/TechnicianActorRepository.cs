using Microsoft.EntityFrameworkCore;

using QuickPatch.Actors.Application.Abstractions;
using QuickPatch.Actors.Domain.Technicians;

namespace QuickPatch.Actors.Infrastructure.Persistence;

public sealed class TechnicianActorRepository(ActorsDbContext db) : ITechnicianActorRepository
{
    public Task<TechnicianActor?> FindAsync(Guid technicianId, CancellationToken cancellationToken) =>
        db.Technicians.SingleOrDefaultAsync(x => x.TechnicianId == technicianId, cancellationToken);

    public void Add(TechnicianActor technician) => db.Technicians.Add(technician);

    public async Task<TeamCount> CountTeamAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        // Envía los cambios pendientes dentro de la transacción abierta (no confirma) para contarlos con la base.
        await db.SaveChangesAsync(cancellationToken);
        var team = db.Technicians.Where(x => x.SupplierId == supplierId);
        var total = await team.CountAsync(cancellationToken);
        var approved = await team.CountAsync(x => x.VerificationStatus == VerificationStatus.Approved, cancellationToken);
        return new TeamCount(total, approved);
    }
}