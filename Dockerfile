# Follows the CDP .NET template (DEFRA/cdp-dotnet-backend-template), using DEFRA's .NET base images.
ARG PARENT_VERSION=2.1.9-dotnet10.0
# CDP frontends listen on 3000.
ARG PORT=3000

# ---- Build ----
FROM defradigital/dotnetcore-development:${PARENT_VERSION} AS build
ARG PARENT_VERSION
LABEL uk.gov.defra.ffc.parent-image=defradigital/dotnetcore-development:${PARENT_VERSION}

WORKDIR /home/dotnet

# Restore first so the package layer is cached between source-only changes.
COPY --chown=dotnet:dotnet src/HealthCheckDashboard/HealthCheckDashboard.csproj src/HealthCheckDashboard/
RUN dotnet restore src/HealthCheckDashboard/HealthCheckDashboard.csproj

COPY --chown=dotnet:dotnet src/ src/
RUN dotnet publish src/HealthCheckDashboard/HealthCheckDashboard.csproj \
    -c Release \
    -o /home/dotnet/publish \
    --no-restore \
    /p:UseAppHost=false

# ---- Production ----
FROM defradigital/dotnetcore:${PARENT_VERSION} AS production
ARG PARENT_VERSION
LABEL uk.gov.defra.ffc.parent-image=defradigital/dotnetcore:${PARENT_VERSION}

# CDP PLATFORM HEALTHCHECK REQUIREMENT: curl.
# icu-libs/icu-data-full keep real cultures (en-GB); tzdata lets the app show UK times.
USER root
RUN apk add --no-cache curl icu-libs icu-data-full tzdata
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

WORKDIR /app
COPY --from=build /home/dotnet/publish .
USER dotnet

ENV TZ="Europe/London"
ENV ASPNETCORE_ENVIRONMENT=Production
# TLS is terminated by the platform; trust X-Forwarded-* so the app sees the original scheme and host.
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

ARG PORT
ENV PORT=${PORT}
ENV ASPNETCORE_HTTP_PORTS=${PORT}
EXPOSE ${PORT}

ENTRYPOINT ["dotnet", "HealthCheckDashboard.dll"]
