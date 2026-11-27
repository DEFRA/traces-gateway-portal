# traces-gateway-portal

[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=DEFRA_traces-gateway-portal&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=DEFRA_traces-gateway-portal)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=DEFRA_traces-gateway-portal&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=DEFRA_traces-gateway-portal)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=DEFRA_traces-gateway-portal&metric=coverage)](https://sonarcloud.io/summary/new_code?id=DEFRA_traces-gateway-portal)

A Blazor (.NET 10) frontend for the Core Delivery Platform (CDP), built with the
[GOV.UK Design System](https://design-system.service.gov.uk/).

- [Requirements](#requirements)
- [Project structure](#project-structure)
- [Configuration](#configuration)
  - [Health checks](#health-checks)
  - [Proxy](#proxy)
- [Local development](#local-development)
- [Docker](#docker)
  - [Docker Compose](#docker-compose)
- [GitHub workflows](#github-workflows)
  - [Dependabot](#dependabot)
  - [SonarCloud](#sonarcloud)
- [GOV.UK Frontend](#govuk-frontend)
- [Licence](#licence)

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Docker, to build and run the container image

## Project structure

| Path | Contents |
| --- | --- |
| `TracesGatewayPortal.slnx` | Solution file |
| `src/HealthCheckDashboard/Components/Pages` | One Razor component per page |
| `src/HealthCheckDashboard/Components/GovUk` | Razor wrappers for GOV.UK Design System components |
| `src/HealthCheckDashboard/Components/Layout` | Page layout: skip link, service navigation, phase banner, footer |
| `src/HealthCheckDashboard/Services` | Server-side health checks of other services |
| `src/HealthCheckDashboard/wwwroot` | Static files, including GOV.UK Frontend CSS, JS, fonts and images |

Pages use static server-side rendering: all code, including calls to other services, runs on the server
and the browser only receives HTML.

## Configuration

Settings come from `appsettings.json`, `appsettings.{Environment}.json` and environment variables.
Use `__` for nesting in environment variables, for example `HealthChecks__TimeoutSeconds=5`.

| Setting | Default | Purpose |
| --- | --- | --- |
| `PORT` | `3000` in the container | Port the service listens on |
| `ASPNETCORE_ENVIRONMENT` | `Production` in the container | `Development` enables the fake services below |

### Health checks

The Service health page checks every service in the `HealthChecks:Services` list in
[appsettings.json](src/HealthCheckDashboard/appsettings.json). These are currently:

| Service | URL |
| --- | --- |
| Trade Gateway | `https://trade-gateway.{environment}.cdp-int.defra.cloud/health/all` |
| Trade Gateway Publisher | `https://trade-gateway-publisher.{environment}.cdp-int.defra.cloud/health/all` |

`{environment}` is replaced with the `ENVIRONMENT` variable, which CDP sets (`dev`, `test`, `prod` and so on).
The app won't start if a URL contains `{environment}` and `ENVIRONMENT` isn't set.

If a service returns ASP.NET Core health check JSON with `entries`, the page names the checks that
aren't healthy, for example "Degraded: sns".

To override a URL, or add a service, use environment variables:

```bash
HealthChecks__Services__0__Url="http://localhost:8085/health/all"
HealthChecks__Services__2__Name="Another service"
HealthChecks__Services__2__Url="https://another-service.{environment}.cdp-int.defra.cloud/health"
```

| Setting | Default | Purpose |
| --- | --- | --- |
| `HealthChecks__TimeoutSeconds` | `5` | How long to wait for each service |
| `HealthChecks__CacheSeconds` | `20` | How long results are reused before checking again |
| `HealthChecks__DegradedThresholdMilliseconds` | `1000` | Healthy responses slower than this show as degraded |

The URLs are only used server-side and are never sent to the browser. In `Development`, the app serves fake
services under `/dev/fake-services/...`, which `appsettings.Development.json` points at.

The service's own health endpoint is `/health`, which the platform uses to check the service is up.

### Logging

Logging uses Serilog, set up the same way as trade-gateway ([CdpLogging.cs](src/HealthCheckDashboard/Logging/CdpLogging.cs)):

- When deployed (`appsettings.json`), logs are written to the console as ECS JSON (`EcsTextFormatter`), which is
  the format CDP's log pipeline expects. In `Development`, they're plain text.
- Each entry includes `service.version` (from `SERVICE_VERSION`, set by CDP), the HTTP request details, and
  `CorrelationId` from the `x-cdp-request-id` header (`TraceHeader` setting).
- The `x-cdp-request-id` header is passed on to the services the health checks call, so a request can be traced
  across services.
- Log levels are set in the `Serilog` section, for example `Serilog__MinimumLevel__Default=Debug`.

### Proxy

CDP configures `HTTP_PROXY`, `HTTPS_PROXY` and `NO_PROXY` when the service is deployed. .NET's `HttpClient` reads
these automatically, so outbound calls go through the forward proxy with no extra configuration.

## Local development

```bash
dotnet run --project src/HealthCheckDashboard
```

Then open <http://localhost:5183>.

## Docker

The [Dockerfile](Dockerfile) follows the CDP .NET template and uses DEFRA's base images:
`defradigital/dotnetcore-development` to build and `defradigital/dotnetcore` to run. The production image
includes `curl`, which the platform health check needs, and runs as the non-root `dotnet` user on port 3000.

```bash
docker build --tag traces-gateway-portal .
docker run -p 3000:3000 traces-gateway-portal
```

### Docker Compose

```bash
docker compose up --build -d
```

This starts the portal on <http://localhost:3000>, plus local floci (AWS), Redis and MongoDB, which the portal
does not use yet.

## GitHub workflows

| Workflow | When it runs | What it does |
| --- | --- | --- |
| `check-pull-request.yml` | Pull requests to `main` | Builds with warnings as errors, checks for vulnerable NuGet packages, test-builds the Docker image |
| `publish.yml` | Pushes to `main` | Builds, then builds and publishes the image with `DEFRA/cdp-build-action` |
| `publish-hotfix.yml` | Manually | Builds, then publishes a hotfix image |

### Dependabot

To enable Dependabot, rename [.github/example.dependabot.yml](.github/example.dependabot.yml) to
`.github/dependabot.yml`. It updates NuGet packages, the Docker base images and GitHub Actions.

### SonarCloud

Instructions for setting up SonarCloud are in
[how-to/sonarcloud.md](https://github.com/DEFRA/cdp-documentation/blob/main/how-to/sonarcloud.md).
The SonarCloud steps in the workflows are commented out until it is set up.

## GOV.UK Frontend

GOV.UK Frontend's compiled CSS, JavaScript, fonts and images are checked in under `src/HealthCheckDashboard/wwwroot`
(currently version 5.14.0). To upgrade, copy `dist/govuk/govuk-frontend.min.css`, `govuk-frontend.min.js` and the
`assets` folder from the new [govuk-frontend](https://www.npmjs.com/package/govuk-frontend) npm package.

## Licence

THIS INFORMATION IS LICENSED UNDER THE CONDITIONS OF THE OPEN GOVERNMENT LICENCE found at:

<http://www.nationalarchives.gov.uk/doc/open-government-licence/version/3>

The following attribution statement MUST be cited in your products and applications when using this information.

> Contains public sector information licensed under the Open Government license v3

### About the licence

The Open Government Licence (OGL) was developed by the Controller of Her Majesty's Stationery Office (HMSO) to enable
information providers in the public sector to license the use and re-use of their information under a common open
licence.

It is designed to encourage use and re-use of information freely and flexibly, with only a few conditions.
