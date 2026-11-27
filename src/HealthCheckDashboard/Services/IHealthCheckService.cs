using HealthCheckDashboard.Models;

namespace HealthCheckDashboard.Services;

public interface IHealthCheckService
{
    Task<IReadOnlyList<ServiceHealth>> GetHealthAsync(CancellationToken cancellationToken = default);
}
