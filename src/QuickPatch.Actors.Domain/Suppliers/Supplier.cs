namespace QuickPatch.Actors.Domain.Suppliers;

/// <summary>
/// Proveedor (Supplier, SDD 6.14): usuario con rol <c>proveedor</c> que tiene un equipo de técnicos (RF-16). Actors no
/// administra el equipo, que sigue en Identity (DD, P-02): lleva su tamaño y cuántos técnicos están aprobados.
/// </summary>
public sealed class Supplier
{
    private Supplier()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid SupplierId { get; private set; }

    public int TechnicianCount { get; private set; }

    public int ApprovedTechnicianCount { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Supplier Create(Guid tenantId, Guid supplierId, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || supplierId == Guid.Empty)
        {
            throw new ArgumentException("El tenant y el proveedor son obligatorios.");
        }

        return new Supplier { TenantId = tenantId, SupplierId = supplierId, UpdatedAt = now };
    }

    /// <summary>Fija el tamaño del equipo con el conteo del directorio, para que un evento repetido no lo infle.</summary>
    public void Recount(int technicians, int approved, DateTimeOffset now)
    {
        if (technicians < 0 || approved < 0 || approved > technicians)
        {
            throw new ArgumentException("El conteo del equipo no es válido.");
        }

        TechnicianCount = technicians;
        ApprovedTechnicianCount = approved;
        UpdatedAt = now;
    }
}