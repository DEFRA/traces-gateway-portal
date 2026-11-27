using System.Diagnostics;
using System.Net;
using System.Text.Json;
using HealthCheckDashboard.Models;
using Microsoft.Extensions.Options;

namespace HealthCheckDashboard.Services;

/// <summary>
/// Calls each configured service's health endpoint from the server, in parallel.
/// Errors are logged in full but only a short, generic description is returned for display,
/// so hostnames and exception details never reach the browser.
/// </summary>
public class HttpHealthCheckService(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<HealthCheckOptions> options,
    TimeProvider timeProvider,
    ILogger<HttpHealthCheckService> logger) : IHealthCheckService
{
    public const string HttpClientName = "HealthChecks";

    public async Task<IReadOnlyList<ServiceHealth>> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.CurrentValue;
        var checks = settings.Services.Select(service => CheckAsync(service, settings, cancellationToken));
        return await Task.WhenAll(checks);
    }

    private async Task<ServiceHealth> CheckAsync(MonitoredService service, HealthCheckOptions settings, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var client = httpClientFactory.CreateClient(HttpClientName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await client.GetAsync(service.Url, HttpCompletionOption.ResponseContentRead, timeoutCts.Token);
            stopwatch.Stop();
            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);

            var status = InterpretResponse(response.StatusCode, body);
            string? description = null;

            if (status == HealthStatus.Unhealthy)
            {
                description = $"Returned HTTP {(int)response.StatusCode}";
            }
            else if (status == HealthStatus.Degraded)
            {
                description = "Service reported it is degraded";
            }
            else if (stopwatch.ElapsedMilliseconds > settings.DegradedThresholdMilliseconds)
            {
                status = HealthStatus.Degraded;
                description = $"Response took longer than {settings.DegradedThresholdMilliseconds:N0} ms";
            }

            if (status != HealthStatus.Healthy)
            {
                logger.LogWarning("Health check for {Service} at {Url} was {Status} (HTTP {StatusCode}, {ElapsedMs} ms)",
                    service.Name, service.Url, status, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);
            }

            return new ServiceHealth(service.Name, status, stopwatch.Elapsed, Now(), description);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Health check for {Service} at {Url} timed out after {Timeout}", service.Name, service.Url, timeout);
            return new ServiceHealth(service.Name, HealthStatus.Unhealthy, null, Now(), $"No response within {settings.TimeoutSeconds} seconds");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Health check for {Service} at {Url} failed", service.Name, service.Url);
            return new ServiceHealth(service.Name, HealthStatus.Unhealthy, null, Now(), "Could not connect");
        }
    }

    /// <summary>
    /// Non-2xx is unhealthy. For 2xx, honours a status reported in the body, which covers
    /// ASP.NET Core health checks (plain text "Healthy"/"Degraded"/"Unhealthy", or JSON with a "status" property).
    /// </summary>
    private static HealthStatus InterpretResponse(HttpStatusCode statusCode, string body)
    {
        if ((int)statusCode is < 200 or > 299)
        {
            return HealthStatus.Unhealthy;
        }

        var reported = body.Trim();

        if (reported.StartsWith('{'))
        {
            try
            {
                using var json = JsonDocument.Parse(reported);
                if (json.RootElement.TryGetProperty("status", out var statusProperty) && statusProperty.ValueKind == JsonValueKind.String)
                {
                    reported = statusProperty.GetString() ?? "";
                }
            }
            catch (JsonException)
            {
                // Not JSON we understand; fall back to the HTTP status code.
            }
        }

        return reported.ToLowerInvariant() switch
        {
            "degraded" => HealthStatus.Degraded,
            "unhealthy" => HealthStatus.Unhealthy,
            _ => HealthStatus.Healthy
        };
    }

    private DateTimeOffset Now() => timeProvider.GetUtcNow();
}
