using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

namespace QuickPatch.Actors.UnitTests;

public class HealthEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    // /health/ready consulta PostgreSQL: se prueba en tests/integration.
    [Fact]
    public async Task Live_RespondeOkSinDependencias()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}