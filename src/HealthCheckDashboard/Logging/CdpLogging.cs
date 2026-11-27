using Elastic.Serilog.Enrichers.Web;
using Serilog;

namespace HealthCheckDashboard.Logging;

/// <summary>
/// Serilog setup for the CDP platform, matching trade-gateway's CdpLogging.
/// The sink and formatter come from the "Serilog" configuration section: ECS JSON (what CDP's log pipeline expects)
/// in appsettings.json, and readable text in appsettings.Development.json.
/// </summary>
public static class CdpLogging
{
    public static void Configuration(HostBuilderContext ctx, IServiceProvider services, LoggerConfiguration config)
    {
        var traceIdHeader = ctx.Configuration.GetValue<string>("TraceHeader");
        var serviceVersion = Environment.GetEnvironmentVariable("SERVICE_VERSION") ?? "";

        config
            .ReadFrom.Configuration(ctx.Configuration)
            .Enrich.WithEcsHttpContext(services.GetRequiredService<IHttpContextAccessor>())
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service.version", serviceVersion);

        if (!string.IsNullOrWhiteSpace(traceIdHeader))
        {
            config.Enrich.WithCorrelationId(traceIdHeader);
        }
    }
}
