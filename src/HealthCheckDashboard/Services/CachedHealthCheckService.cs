using HealthCheckDashboard.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HealthCheckDashboard.Services;

/// <summary>
/// Reuses results for a short time so that many people viewing the dashboard
/// don't multiply the load on the services being checked.
/// </summary>
public class CachedHealthCheckService(
    HttpHealthCheckService inner,
    IMemoryCache cache,
    IOptionsMonitor<HealthCheckOptions> options) : IHealthCheckService
{
    private const string CacheKey = "health-check-results";
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public async Task<IReadOnlyList<ServiceHealth>> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var cacheSeconds = options.CurrentValue.CacheSeconds;
        if (cacheSeconds <= 0)
        {
            return await inner.GetHealthAsync(cancellationToken);
        }

        if (cache.TryGetValue(CacheKey, out IReadOnlyList<ServiceHealth>? cached) && cached is not null)
        {
            return cached;
        }

        // Only one request refreshes the results; others wait and then use them.
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(CacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            // Don't tie the shared refresh to one visitor's request being cancelled.
            var results = await inner.GetHealthAsync(CancellationToken.None);
            cache.Set(CacheKey, results, TimeSpan.FromSeconds(cacheSeconds));
            return results;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
