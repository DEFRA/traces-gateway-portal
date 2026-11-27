using HealthCheckDashboard.Components;
using HealthCheckDashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// CDP sets PORT for each service (frontends default to 3000). Honour it when present;
// otherwise ASPNETCORE_HTTP_PORTS (set in the Dockerfile) or launchSettings.json applies.
if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port))
{
    builder.WebHost.UseUrls($"http://+:{port}");
}

builder.Services.AddRazorComponents();

// Health checks of other services. All calls are made server-side; the browser only receives rendered HTML.
builder.Services.AddOptions<HealthCheckOptions>()
    .Bind(builder.Configuration.GetSection(HealthCheckOptions.SectionName))
    // CDP sets ENVIRONMENT (dev, test, perf-test, prod...). Service URLs use an {environment} token for it.
    // Read the variable itself: configuration["ENVIRONMENT"] also picks up ASPNETCORE_ENVIRONMENT ("Production").
    .PostConfigure(options => options.ApplyEnvironment(Environment.GetEnvironmentVariable("ENVIRONMENT")))
    .ValidateDataAnnotations()
    .Validate(options => options.Services.TrueForAll(s => !s.Url.Contains(HealthCheckOptions.EnvironmentToken)),
        $"A health check URL contains {HealthCheckOptions.EnvironmentToken} but the ENVIRONMENT variable is not set.")
    .ValidateOnStart();
builder.Services.AddHttpClient(HttpHealthCheckService.HttpClientName, client =>
{
    // Per-service timeouts are applied in HttpHealthCheckService; this is only a backstop.
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<HttpHealthCheckService>();
builder.Services.AddSingleton<IHealthCheckService, CachedHealthCheckService>();

// This dashboard's own health endpoint, for load balancer / container probes.
builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// No UseHttpsRedirection: the container serves plain HTTP and the platform terminates TLS in front of it.
// ASPNETCORE_FORWARDEDHEADERS_ENABLED (Dockerfile) makes the app see the original https scheme.

app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapRazorComponents<App>();

if (app.Environment.IsDevelopment())
{
    // Fake downstream services so the dashboard has something to call locally.
    // Referenced from appsettings.Development.json. Never mapped outside Development.
    // Responses mimic the ASP.NET Core health checks JSON that /health/all returns.
    var fakes = app.MapGroup("/dev/fake-services");
    fakes.MapGet("/trade-gateway/health/all", () => Results.Json(new
    {
        status = "Healthy",
        entries = new { mongo = new { status = "Healthy" }, sqs = new { status = "Healthy" } }
    }));
    fakes.MapGet("/trade-gateway-publisher/health/all", () => Results.Json(new
    {
        status = "Degraded",
        entries = new { mongo = new { status = "Healthy" }, sns = new { status = "Degraded" } }
    }));
}

app.Run();
