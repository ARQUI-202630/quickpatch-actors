using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuickPatch.Actors.Infrastructure.Persistence;

/// <summary>Fábrica para <c>dotnet ef</c> (generar migraciones). No se conecta a ninguna base.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ActorsDbContext>
{
    public ActorsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ActorsDbContext>().UseNpgsql("Host=localhost;Database=db_actors").Options);
}