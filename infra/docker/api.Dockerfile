# PMPlatform.Api image (TASK-014). Build context: repository root. Build: docker compose -f infra/docker/docker-compose.yml build api
#
# Stage 1 restores against the project files alone so that a source edit does not invalidate the package layer,
# then publishes with the repository's analyzers and warnings-as-errors (Directory.Build.props). The SDK image tag
# tracks .NET 10 LTS (ADR-002 §4.2.1); global.json's rollForward: latestPatch accepts its patch level.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
# .editorconfig: the image compiles under the same analyzer rules as CI, including the generated-code exemption
# for Persistence/Migrations (TASK-024).
COPY global.json .editorconfig ./
COPY src/backend/Directory.Build.props src/backend/Directory.Packages.props src/backend/PMPlatform.slnx src/backend/
COPY src/backend/PMPlatform.Domain/PMPlatform.Domain.csproj src/backend/PMPlatform.Domain/
COPY src/backend/PMPlatform.Application/PMPlatform.Application.csproj src/backend/PMPlatform.Application/
COPY src/backend/PMPlatform.Infrastructure/PMPlatform.Infrastructure.csproj src/backend/PMPlatform.Infrastructure/
COPY src/backend/PMPlatform.Api/PMPlatform.Api.csproj src/backend/PMPlatform.Api/
RUN dotnet restore src/backend/PMPlatform.Api/PMPlatform.Api.csproj
COPY src/backend/ src/backend/
# TASK-027: the seed and data-integrity scripts are embedded in PMPlatform.Infrastructure (`seed`, `validate-data-integrity`).
COPY db/seed/ db/seed/
RUN dotnet publish src/backend/PMPlatform.Api/PMPlatform.Api.csproj --configuration Release --no-restore --output /app

# Stage 2: runtime only. curl is installed for the compose health check; the image otherwise ships nothing but the
# published output. Runs as the image's non-root `app` user on port 8080 (ASPNETCORE_HTTP_PORTS default).
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "PMPlatform.Api.dll"]
