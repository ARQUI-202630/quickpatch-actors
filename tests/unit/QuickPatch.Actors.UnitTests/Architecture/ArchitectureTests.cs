using System.Reflection;

using NetArchTest.Rules;

using QuickPatch.Actors.Application.Technicians;
using QuickPatch.Actors.Domain.Technicians;
using QuickPatch.Actors.Infrastructure.Persistence;

namespace QuickPatch.Actors.UnitTests.Architecture;

/// <summary>
/// Reglas de dependencia entre capas del SDD 10.3.2 (SCRUM-648), las mismas que en los otros servicios .NET. Revisan lo que
/// el compilador no ve: el API referencia a Infrastructure para la composición, pero solo <c>Program</c> puede usarla.
/// </summary>
public sealed class ArchitectureTests
{
    private const string ApiNs = "QuickPatch.Actors.Api";
    private const string ApplicationNs = "QuickPatch.Actors.Application";
    private const string InfrastructureNs = "QuickPatch.Actors.Infrastructure";

    private static readonly string[] Tecnologia =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Confluent.Kafka",
        "Microsoft.AspNetCore",
        "QuickPatch.Transversal",
    ];

    private static readonly Assembly Domain = typeof(TechnicianActor).Assembly;
    private static readonly Assembly Application = typeof(TechnicianEventsHandler).Assembly;
    private static readonly Assembly Infrastructure = typeof(ActorsDbContext).Assembly;
    private static readonly Assembly Api = Assembly.Load("QuickPatch.Actors.Api");

    [Fact]
    public void Domain_NoDependeDeOtrasCapasNiDeTecnologia() =>
        AssertOk(Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny([ApplicationNs, InfrastructureNs, ApiNs, .. Tecnologia]).GetResult());

    [Fact]
    public void Application_NoDependeDeInfrastructureApiNiTecnologia() =>
        AssertOk(Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOnAny([InfrastructureNs, ApiNs, .. Tecnologia]).GetResult());

    [Fact]
    public void Infrastructure_NoDependeDelApi() =>
        AssertOk(Types.InAssembly(Infrastructure).ShouldNot()
            .HaveDependencyOnAny(ApiNs, "Microsoft.AspNetCore").GetResult());

    [Fact]
    public void Api_SoloLaRaizDeComposicionConoceInfrastructure() =>
        AssertOk(Types.InAssembly(Api).That().DoNotHaveName("Program")
            .ShouldNot().HaveDependencyOnAny(InfrastructureNs).GetResult());

    [Fact]
    public void Consumidores_SoloDelegan_NoConocenRepositorios() =>
        AssertOk(Types.InAssembly(Infrastructure).That().ResideInNamespace(InfrastructureNs + ".Messaging")
            .ShouldNot().HaveDependencyOnAny(InfrastructureNs + ".Persistence", ApplicationNs + ".Abstractions").GetResult());

    private static void AssertOk(TestResult result) =>
        Assert.True(
            result.IsSuccessful,
            "Viola SDD 10.3.2: " + string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []));
}