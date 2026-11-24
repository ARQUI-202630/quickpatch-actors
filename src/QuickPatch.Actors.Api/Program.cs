using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using QuickPatch.Actors.Application;
using QuickPatch.Actors.Infrastructure;
using QuickPatch.Transversal.Http;

var builder = WebApplication.CreateBuilder(args);

// Logs en JSON con scopes, para Loki (SCRUM-323).
builder.Logging.AddQuickPatchJsonConsole();

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Probes de k3s (Documento de Infraestructura, sección 5.8).
// live: el proceso responde; ready: además, PostgreSQL responde.
// Actors no publica endpoints de negocio: es un servicio de eventos (ADR-025).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(QuickPatch.Actors.Infrastructure.DependencyInjection.ReadyTag),
});

app.Run();

/// <summary>Punto de entrada; público para las pruebas con WebApplicationFactory.</summary>
public partial class Program;