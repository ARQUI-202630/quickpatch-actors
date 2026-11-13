namespace QuickPatch.Actors.Domain.Technicians;

/// <summary>
/// Técnico en el directorio de actores del tenant: a qué proveedor pertenece, su especialidad y su estado de verificación.
/// Es una proyección de los eventos de Identity: los dos topics llegan por separado, así que un cambio con
/// <c>updatedAt</c> anterior al guardado se descarta (mismo criterio que la réplica de Matching, ADR-017).
/// </summary>
public sealed class TechnicianActor
{
    private TechnicianActor()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid TechnicianId { get; private set; }

    /// <summary>Proveedor a cuyo equipo pertenece; nulo si es independiente o si aún no llegó <c>technician.registered</c>.</summary>
    public Guid? SupplierId { get; private set; }

    public Guid SpecialtyId { get; private set; }

    public string VerificationStatus { get; private set; } = Technicians.VerificationStatus.Pending;

    /// <summary>Si ya se aplicó <c>technician.registered</c>, la única fuente del proveedor.</summary>
    public bool Registered { get; private set; }

    public DateTimeOffset StatusUpdatedAt { get; private set; }

    public bool IsApproved => VerificationStatus == Technicians.VerificationStatus.Approved;

    public static TechnicianActor Create(Guid tenantId, Guid technicianId, Guid specialtyId, string status, DateTimeOffset updatedAt)
    {
        if (tenantId == Guid.Empty || technicianId == Guid.Empty)
        {
            throw new ArgumentException("El tenant y el técnico son obligatorios.");
        }

        EnsureKnown(status);
        return new TechnicianActor
        {
            TenantId = tenantId,
            TechnicianId = technicianId,
            SpecialtyId = specialtyId,
            VerificationStatus = status,
            StatusUpdatedAt = updatedAt,
        };
    }

    /// <summary>
    /// Aplica el registro. El proveedor se toma siempre, porque solo este evento lo trae; la especialidad y el estado, solo si
    /// el registro no es más viejo que lo guardado. Devuelve el proveedor anterior para recontar su equipo si cambió.
    /// </summary>
    public Guid? ApplyRegistration(Guid? supplierId, Guid specialtyId, string status, DateTimeOffset updatedAt)
    {
        EnsureKnown(status);
        var previousSupplier = SupplierId;
        SupplierId = supplierId;
        Registered = true;
        if (updatedAt >= StatusUpdatedAt)
        {
            SpecialtyId = specialtyId;
            VerificationStatus = status;
            StatusUpdatedAt = updatedAt;
        }

        return previousSupplier;
    }

    /// <summary>Aplica un cambio de verificación; devuelve false si es más viejo que el estado guardado.</summary>
    public bool ApplyVerification(Guid specialtyId, string status, DateTimeOffset updatedAt)
    {
        EnsureKnown(status);
        if (updatedAt < StatusUpdatedAt)
        {
            return false;
        }

        SpecialtyId = specialtyId;
        VerificationStatus = status;
        StatusUpdatedAt = updatedAt;
        return true;
    }

    private static void EnsureKnown(string status)
    {
        if (!Technicians.VerificationStatus.IsKnown(status))
        {
            throw new ArgumentException($"Estado de verificación desconocido: {status}.", nameof(status));
        }
    }
}