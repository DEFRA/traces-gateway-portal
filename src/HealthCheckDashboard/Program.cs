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
    .ValidateDataAnnotations()
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
    var fakes = app.MapGroup("/dev/fake-services");
    fakes.MapGet("/healthy", () => Results.Text("Healthy"));
    fakes.MapGet("/healthy-json", () => Results.Json(new { status = "Healthy" }));
    fakes.MapGet("/degraded", () => Results.Text("Degraded"));
    fakes.MapGet("/slow", async () =>
    {
        await Task.Delay(1500);
        return Results.Text("Healthy");
    });
    fakes.MapGet("/unhealthy", () => Results.Text("Unhealthy", statusCode: StatusCodes.Status503ServiceUnavailable));
}

app.Run();
