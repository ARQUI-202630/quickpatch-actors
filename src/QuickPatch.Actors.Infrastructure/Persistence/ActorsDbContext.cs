using Microsoft.EntityFrameworkCore;

using QuickPatch.Actors.Domain.Clients;
using QuickPatch.Actors.Domain.Suppliers;
using QuickPatch.Actors.Domain.Technicians;
using QuickPatch.Transversal.Messaging;

namespace QuickPatch.Actors.Infrastructure.Persistence;

/// <summary>
/// Base de Actors Service (<c>db_actors</c>, ADR-014): el directorio de actores de negocio de cada tenant, armado con
/// eventos (DD 5.31 a 5.33), y los eventos aplicados. Actors no publica eventos, así que no tiene Outbox. RLS y los permisos
/// los agrega la migración inicial.
/// </summary>
public sealed class ActorsDbContext(DbContextOptions<ActorsDbContext> options) : DbContext(options)
{
    public DbSet<TechnicianActor> Technicians => Set<TechnicianActor>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<ClientActor> Clients => Set<ClientActor>();

    public DbSet<ProcessedEventRecord> ProcessedEvents => Set<ProcessedEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<TechnicianActor>(e =>
        {
            e.ToTable("technician_actors", t => t.HasCheckConstraint(
                "ck_technician_actors_status", "verification_status IN ('pendiente', 'aprobado', 'rechazado', 'suspendido')"));
            e.HasKey(x => new { x.TenantId, x.TechnicianId });
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.TechnicianId).HasColumnName("technician_id");
            e.Property(x => x.SupplierId).HasColumnName("supplier_id");
            e.Property(x => x.SpecialtyId).HasColumnName("specialty_id");
            e.Property(x => x.VerificationStatus).HasColumnName("verification_status").HasMaxLength(20);
            e.Property(x => x.Registered).HasColumnName("registered");
            e.Property(x => x.StatusUpdatedAt).HasColumnName("status_updated_at");
            e.Ignore(x => x.IsApproved);
            e.HasIndex(x => new { x.TenantId, x.SupplierId }, "ix_technician_actors_supplier");
        });

        modelBuilder.Entity<Supplier>(e =>
        {
            e.ToTable("suppliers", t => t.HasCheckConstraint(
                "ck_suppliers_counts", "technician_count >= 0 AND approved_technician_count BETWEEN 0 AND technician_count"));
            e.HasKey(x => new { x.TenantId, x.SupplierId });
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.SupplierId).HasColumnName("supplier_id");
            e.Property(x => x.TechnicianCount).HasColumnName("technician_count");
            e.Property(x => x.ApprovedTechnicianCount).HasColumnName("approved_technician_count");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<ClientActor>(e =>
        {
            e.ToTable("client_actors", t => t.HasCheckConstraint(
                "ck_client_actors_requests", "request_count >= 1 AND first_request_at <= last_request_at"));
            e.HasKey(x => new { x.TenantId, x.ClientId });
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.ClientId).HasColumnName("client_id");
            e.Property(x => x.RequestCount).HasColumnName("request_count");
            e.Property(x => x.FirstRequestAt).HasColumnName("first_request_at");
            e.Property(x => x.LastRequestAt).HasColumnName("last_request_at");
        });

        // Tabla transversal con el mapeo común (ADR-020); la migración sigue siendo de este servicio.
        modelBuilder.MapProcessedEvents();
    }
}