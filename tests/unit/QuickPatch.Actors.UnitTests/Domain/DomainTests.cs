using QuickPatch.Actors.Domain.Clients;
using QuickPatch.Actors.Domain.Suppliers;
using QuickPatch.Actors.Domain.Technicians;

namespace QuickPatch.Actors.UnitTests.Domain;

public class DomainTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Tecnico_DescartaLaVerificacionMasVieja()
    {
        var tecnico = TechnicianActor.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), VerificationStatus.Approved, T0);

        Assert.False(tecnico.ApplyVerification(Guid.NewGuid(), VerificationStatus.Suspended, T0.AddMinutes(-1)));
        Assert.True(tecnico.IsApproved);
        Assert.True(tecnico.ApplyVerification(Guid.NewGuid(), VerificationStatus.Suspended, T0.AddMinutes(1)));
        Assert.Equal(VerificationStatus.Suspended, tecnico.VerificationStatus);
    }

    [Fact]
    public void Tecnico_ElRegistroAtrasadoFijaElProveedorPeroNoElEstado()
    {
        var tecnico = TechnicianActor.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), VerificationStatus.Approved, T0);
        var proveedor = Guid.NewGuid();

        var anterior = tecnico.ApplyRegistration(proveedor, Guid.NewGuid(), VerificationStatus.Pending, T0.AddMinutes(-5));

        Assert.Null(anterior);
        Assert.Equal(proveedor, tecnico.SupplierId);
        Assert.True(tecnico.Registered);
        Assert.Equal(VerificationStatus.Approved, tecnico.VerificationStatus);
    }

    [Fact]
    public void Tecnico_RechazaEstadosYIdentificadoresInvalidos()
    {
        Assert.Throws<ArgumentException>(() => TechnicianActor.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), VerificationStatus.Pending, T0));
        Assert.Throws<ArgumentException>(() => TechnicianActor.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "otro", T0));
        var tecnico = TechnicianActor.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), VerificationStatus.Pending, T0);
        Assert.Throws<ArgumentException>(() => tecnico.ApplyVerification(Guid.NewGuid(), "otro", T0));
        Assert.Throws<ArgumentException>(() => tecnico.ApplyRegistration(null, Guid.NewGuid(), "otro", T0));
    }

    [Fact]
    public void Proveedor_FijaElConteoYRechazaUnoImposible()
    {
        var proveedor = Supplier.Create(Guid.NewGuid(), Guid.NewGuid(), T0);

        proveedor.Recount(3, 2, T0.AddMinutes(1));

        Assert.Equal((3, 2, T0.AddMinutes(1)), (proveedor.TechnicianCount, proveedor.ApprovedTechnicianCount, proveedor.UpdatedAt));
        Assert.Throws<ArgumentException>(() => proveedor.Recount(1, 2, T0));
        Assert.Throws<ArgumentException>(() => proveedor.Recount(-1, 0, T0));
        Assert.Throws<ArgumentException>(() => Supplier.Create(Guid.NewGuid(), Guid.Empty, T0));
    }

    [Fact]
    public void Cliente_SumaSolicitudesYAguantaElDesorden()
    {
        var cliente = ClientActor.Create(Guid.NewGuid(), Guid.NewGuid(), T0);

        cliente.RecordRequest(T0);
        cliente.RecordRequest(T0.AddDays(2));
        cliente.RecordRequest(T0.AddDays(-1));

        Assert.Equal(3, cliente.RequestCount);
        Assert.Equal(T0.AddDays(-1), cliente.FirstRequestAt);
        Assert.Equal(T0.AddDays(2), cliente.LastRequestAt);
        Assert.Throws<ArgumentException>(() => ClientActor.Create(Guid.NewGuid(), Guid.Empty, T0));
    }
}