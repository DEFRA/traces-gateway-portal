using System.ComponentModel.DataAnnotations;

namespace HealthCheckDashboard.Services;

/// <summary>
/// Bound from the "HealthChecks" configuration section (appsettings.json or environment variables,
/// e.g. HealthChecks__Services__0__Url).
/// </summary>
public class HealthCheckOptions
{
    public const string SectionName = "HealthChecks";

    /// <summary>Replaced in service URLs with the CDP environment name, e.g. dev or prod.</summary>
    public const string EnvironmentToken = "{environment}";

    /// <summary>How long to wait for each service before marking it unhealthy.</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>How long results are reused before the services are checked again.</summary>
    [Range(0, 3600)]
    public int CacheSeconds { get; set; } = 20;

    /// <summary>A healthy response slower than this is shown as degraded.</summary>
    [Range(1, 120_000)]
    public int DegradedThresholdMilliseconds { get; set; } = 1000;

    public List<MonitoredService> Services { get; set; } = [];

    public void ApplyEnvironment(string? environment)
    {
        if (string.IsNullOrWhiteSpace(environment))
        {
            return;
        }

        foreach (var service in Services)
        {
            service.Url = service.Url.Replace(EnvironmentToken, environment.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
        }
    }
}

public class MonitoredService
{
    [Required]
    public string Name { get; set; } = "";

    /// <summary>The health endpoint. Only ever used server-side; never rendered to the page.</summary>
    [Required, Url]
    public string Url { get; set; } = "";
}
