namespace HealthCheckDashboard.Models;

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}

/// <summary>
/// The result of checking one service. This is what gets rendered to the browser,
/// so it deliberately does not include the service's URL or any raw error details.
/// </summary>
public record ServiceHealth(
    string Name,
    HealthStatus Status,
    TimeSpan? ResponseTime,
    DateTimeOffset CheckedAt,
    string? Description = null);
