using System.Net;
using System.Text.Json;

using Confluent.Kafka;
using Confluent.Kafka.Admin;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

using Npgsql;

using QuickPatch.Actors.Infrastructure.Persistence;

using Testcontainers.Kafka;
using Testcontainers.PostgreSql;

namespace QuickPatch.Actors.IntegrationTests;

/// <summary>
/// PostgreSQL 16 y Kafka reales, con las migraciones, los roles del DD 10.2 y el servicio como <c>actors_app</c> (sin
/// BYPASSRLS).
/// </summary>
public sealed class ActorsEnvironment : IAsyncLifetime
{
    public static readonly string[] Topics = ["technician.registered", "technician.verification-changed", "service-request.created"];

    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("db_actors").Build();
    private readonly KafkaContainer kafka = new KafkaBuilder("confluentinc/cp-kafka:7.7.1").Build();

    public string AdminConnectionString => postgres.GetConnectionString();

    public string AppConnectionString => new NpgsqlConnectionStringBuilder(AdminConnectionString)
    {
        Username = "actors_app",
        Password = "app-pruebas",
    }.ConnectionString;

    public string KafkaServers => kafka.GetBootstrapAddress();

    public WebApplicationFactory<Program> App { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(postgres.StartAsync(), kafka.StartAsync());
        await using (var db = new ActorsDbContext(new DbContextOptionsBuilder<ActorsDbContext>().UseNpgsql(AdminConnectionString).Options))
        {
            await db.Database.MigrateAsync();
        }

        await ExecuteAsync(AdminConnectionString, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "db", "roles.sql")));
        await ExecuteAsync(AdminConnectionString, "ALTER ROLE actors_app LOGIN PASSWORD 'app-pruebas';");
        using (var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = KafkaServers }).Build())
        {
            await admin.CreateTopicsAsync(Topics.Select(t => new TopicSpecification { Name = t, NumPartitions = 1, ReplicationFactor = 1 }));
        }

        App = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:Actors", AppConnectionString);
            b.UseSetting("Kafka:BootstrapServers", KafkaServers);
            b.UseSetting("Kafka:ConsumerGroupId", "actors-pruebas");
        });
        _ = App.Server;
    }

    public async Task DisposeAsync()
    {
        if (App is not null)
        {
            await App.DisposeAsync();
        }

        await Task.WhenAll(postgres.DisposeAsync().AsTask(), kafka.DisposeAsync().AsTask());
    }

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<long> CountAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? 0 : Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Cuenta con el rol del servicio y el tenant fijado en la sesión, como lo hace la aplicación.</summary>
    public static async Task<long> CountAsTenantAsync(string connectionString, Guid tenant, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var set = new NpgsqlCommand("SELECT set_config('app.current_tenant', @t, false)", connection))
        {
            set.Parameters.AddWithValue("t", tenant.ToString());
            await set.ExecuteScalarAsync();
        }

        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Publica un evento con el sobre común (DD 8.4.1), como lo hacen Identity y ServiceRequest.</summary>
    public async Task PublishAsync(string topic, Guid tenant, string key, object data)
    {
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = KafkaServers }).Build();
        var value = JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid(),
            eventType = topic,
            eventVersion = 1,
            occurredAt = DateTimeOffset.UtcNow,
            correlationId = Guid.NewGuid(),
            tenantId = tenant,
            producer = "pruebas",
            data,
        });
        await producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = value });
    }

    /// <summary>Espera hasta que la lectura devuelva el valor esperado o se acabe el tiempo.</summary>
    public static async Task<long> EventuallyAsync(Func<Task<long>> read, long expected, int seconds = 45)
    {
        ArgumentNullException.ThrowIfNull(read);
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        long value;
        do
        {
            value = await read();
            if (value == expected)
            {
                return value;
            }

            await Task.Delay(250);
        }
        while (DateTime.UtcNow < deadline);
        return value;
    }
}

[CollectionDefinition(Name)]
public sealed class EnvironmentDefinition : ICollectionFixture<ActorsEnvironment>
{
    public const string Name = "actors";
}

/// <summary>
/// SCRUM-640 de punta a punta: los eventos de Identity y ServiceRequest llegan por Kafka y Actors arma el directorio del
/// tenant bajo RLS, sin ver los datos de otro tenant.
/// </summary>
[Collection(EnvironmentDefinition.Name)]
public class ActorsFlowTests(ActorsEnvironment env)
{
    [Fact]
    public async Task Eventos_ArmanElDirectorio_ConElEquipoDelProveedorYLosClientes()
    {
        var (tenant, proveedor, cliente) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var t0 = DateTimeOffset.UtcNow;
        foreach (var tecnico in new[] { a, b })
        {
            await env.PublishAsync("technician.registered", tenant, tecnico.ToString(), new
            {
                technicianId = tecnico,
                specialtyId = Guid.NewGuid(),
                providerId = proveedor,
                verificationStatus = "pendiente",
                updatedAt = t0,
            });
        }

        await env.PublishAsync("technician.verification-changed", tenant, a.ToString(), new
        {
            technicianId = a,
            specialtyId = Guid.NewGuid(),
            previousStatus = "pendiente",
            verificationStatus = "aprobado",
            reason = (string?)null,
            updatedAt = t0.AddSeconds(1),
        });
        for (var i = 0; i < 2; i++)
        {
            await env.PublishAsync("service-request.created", tenant, Guid.NewGuid().ToString(), new
            {
                serviceRequestId = Guid.NewGuid(),
                clientId = cliente,
                categoryId = Guid.NewGuid(),
                description = "fuga",
                location = new { address = "Calle 1" },
                createdAt = t0.AddMinutes(i),
            });
        }

        Task<long> Admin(string sql) => ActorsEnvironment.CountAsync(env.AdminConnectionString, sql, ("t", tenant), ("p", proveedor), ("c", cliente));
        Assert.Equal(1, await ActorsEnvironment.EventuallyAsync(
            () => Admin("SELECT approved_technician_count FROM suppliers WHERE tenant_id = @t AND supplier_id = @p"), 1));
        Assert.Equal(2, await Admin("SELECT technician_count FROM suppliers WHERE tenant_id = @t AND supplier_id = @p"));
        Assert.Equal(2, await ActorsEnvironment.EventuallyAsync(
            () => Admin("SELECT request_count FROM client_actors WHERE tenant_id = @t AND client_id = @c"), 2));

        // RLS: el rol del servicio, con otro tenant en la sesión, no ve nada; sin tenant tampoco.
        var otro = Guid.NewGuid();
        Assert.Equal(0, await ActorsEnvironment.CountAsTenantAsync(env.AppConnectionString, otro, "SELECT count(*) FROM technician_actors"));
        Assert.Equal(2, await ActorsEnvironment.CountAsTenantAsync(env.AppConnectionString, tenant, "SELECT count(*) FROM technician_actors"));
        Assert.Equal(0, await ActorsEnvironment.CountAsync(env.AppConnectionString, "SELECT count(*) FROM suppliers"));
    }

    [Fact]
    public async Task Ready_RespondeCuandoPostgreSqlResponde()
    {
        using var client = env.App.CreateClient();

        var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}