using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace QuizManagement.Tests.ApiTests;

public class HealthApiTests
{
    [Theory]
    [InlineData(HealthStatus.Healthy, HttpStatusCode.OK)]
    [InlineData(HealthStatus.Degraded, HttpStatusCode.OK)]
    [InlineData(HealthStatus.Unhealthy, HttpStatusCode.ServiceUnavailable)]
    public async Task Get_ReturnsPublicJsonAndHealthStatusCode(HealthStatus status, HttpStatusCode expectedCode)
    {
        using var host = new ApiTestHost
        {
            ConfigureServices = services => services.AddSingleton<HealthCheckService>(
                new FakeHealthCheckService(status))
        };

        var response = await host.CreateClient().GetAsync("/api/health");

        Assert.Equal(expectedCode, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(status.ToString(), json.RootElement.GetProperty("status").GetString());
    }

    private sealed class FakeHealthCheckService(HealthStatus status) : HealthCheckService
    {
        public override Task<HealthReport> CheckHealthAsync(
            Func<HealthCheckRegistration, bool>? predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(new HealthReport(
                new Dictionary<string, HealthReportEntry>(), status, TimeSpan.Zero));
    }
}
