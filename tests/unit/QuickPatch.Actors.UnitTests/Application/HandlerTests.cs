using Microsoft.Extensions.Logging.Abstractions;

using QuickPatch.Actors.Application.Clients;
using QuickPatch.Actors.Application.Events;
using QuickPatch.Actors.Application.Technicians;
using QuickPatch.Actors.UnitTests.Support;

using static QuickPatch.Actors.UnitTests.Support.Eventos;

namespace QuickPatch.Actors.UnitTests.Application;

public class HandlerTests
{
    private readonly InMemoryActors store = new();

    private TechnicianEventsHandler Tecnicos() =>
        new(store, store, store, store, TimeProvider.System, NullLogger<TechnicianEventsHandler>.Instance);

    [Fact]
    public async Task Registro_ArmaElEquipoDelProveedor_YLaAprobacionLoRecuenta()
    {
        var tenant = Guid.NewGuid();
        var proveedor = Guid.NewGuid();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());

        await Tecnicos().HandleAsync(Registrado(tenant, a, proveedor), default);
        await Tecnicos().HandleAsync(Registrado(tenant, b, proveedor), default);
        var resultado = await Tecnicos().HandleAsync(Verificado(tenant, a, "aprobado", T0.AddMinutes(1)), default);

        Assert.Equal(HandlingOutcome.Applied, resultado);
        var equipo = Assert.Single(store.Suppliers);
        Assert.Equal((2, 1), (equipo.TechnicianCount, equipo.ApprovedTechnicianCount));
    }

    [Fact]
    public async Task EventoRepetido_NoSeAplicaDosVeces()
    {
        var evento = Registrado(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(HandlingOutcome.Applied, await Tecnicos().HandleAsync(evento, default));
        Assert.Equal(HandlingOutcome.Duplicate, await Tecnicos().HandleAsync(evento, default));
        Assert.Equal(1, Assert.Single(store.Suppliers).TechnicianCount);
    }

    [Fact]
    public async Task VerificacionAntesDelRegistro_ElRegistroCompletaElProveedor()
    {
        var tenant = Guid.NewGuid();
        var (tecnico, proveedor) = (Guid.NewGuid(), Guid.NewGuid());

        await Tecnicos().HandleAsync(Verificado(tenant, tecnico, "aprobado", T0.AddMinutes(5)), default);
        await Tecnicos().HandleAsync(Registrado(tenant, tecnico, proveedor, "pendiente", T0), default);

        var t = Assert.Single(store.Technicians);
        Assert.Equal(proveedor, t.SupplierId);
        Assert.True(t.IsApproved);
        Assert.Equal((1, 1), (store.Suppliers[0].TechnicianCount, store.Suppliers[0].ApprovedTechnicianCount));
    }

    [Fact]
    public async Task VerificacionAtrasada_SeDescarta()
    {
        var tenant = Guid.NewGuid();
        var tecnico = Guid.NewGuid();
        await Tecnicos().HandleAsync(Registrado(tenant, tecnico, null, "aprobado", T0), default);

        Assert.Equal(HandlingOutcome.Stale, await Tecnicos().HandleAsync(Verificado(tenant, tecnico, "suspendido", T0.AddMinutes(-1)), default));
        Assert.True(Assert.Single(store.Technicians).IsApproved);
        Assert.Empty(store.Suppliers);
    }

    [Fact]
    public async Task CambioDeProveedor_RecuentaLosDosEquipos()
    {
        var tenant = Guid.NewGuid();
        var tecnico = Guid.NewGuid();
        var (antes, despues) = (Guid.NewGuid(), Guid.NewGuid());

        await Tecnicos().HandleAsync(Registrado(tenant, tecnico, antes), default);
        await Tecnicos().HandleAsync(Registrado(tenant, tecnico, despues, cuando: T0.AddMinutes(1)), default);

        Assert.Equal(0, store.Suppliers.Single(x => x.SupplierId == antes).TechnicianCount);
        Assert.Equal(1, store.Suppliers.Single(x => x.SupplierId == despues).TechnicianCount);
    }

    [Fact]
    public async Task Tenants_NoSeMezclan()
    {
        var proveedor = Guid.NewGuid();

        await Tecnicos().HandleAsync(Registrado(Guid.NewGuid(), Guid.NewGuid(), proveedor), default);
        await Tecnicos().HandleAsync(Registrado(Guid.NewGuid(), Guid.NewGuid(), proveedor), default);

        Assert.Equal(2, store.Suppliers.Count);
        Assert.All(store.Suppliers, s => Assert.Equal(1, s.TechnicianCount));
    }

    [Fact]
    public async Task SolicitudCreada_SumaAlClienteUnaVezPorEvento()
    {
        var handler = new ServiceRequestCreatedHandler(store, store, store, TimeProvider.System);
        var (tenant, cliente) = (Guid.NewGuid(), Guid.NewGuid());
        var primera = SolicitudCreada(tenant, cliente, T0);

        Assert.Equal(HandlingOutcome.Applied, await handler.HandleAsync(primera, default));
        Assert.Equal(HandlingOutcome.Duplicate, await handler.HandleAsync(primera, default));
        await handler.HandleAsync(SolicitudCreada(tenant, cliente, T0.AddHours(3)), default);

        var c = Assert.Single(store.Clients);
        Assert.Equal((2, T0, T0.AddHours(3)), (c.RequestCount, c.FirstRequestAt, c.LastRequestAt));
    }
}