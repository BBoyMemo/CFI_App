# syntax=docker/dockerfile:1

# Multi-stage build: the SDK image (large, has compilers) only exists during build; the
# image that actually ships is the small ASP.NET runtime image with nothing else in it.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first and restore before copying source, so an unchanged
# dependency set reuses the Docker layer cache instead of re-downloading every build.
COPY CfiApp.slnx ./
COPY src/CfiApp.Domain/CfiApp.Domain.csproj src/CfiApp.Domain/
COPY src/CfiApp.Application/CfiApp.Application.csproj src/CfiApp.Application/
COPY src/CfiApp.Infrastructure/CfiApp.Infrastructure.csproj src/CfiApp.Infrastructure/
COPY src/CfiApp.Api/CfiApp.Api.csproj src/CfiApp.Api/
RUN dotnet restore src/CfiApp.Api/CfiApp.Api.csproj

COPY Directory.Build.props ./
COPY src/ src/
RUN dotnet publish src/CfiApp.Api/CfiApp.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The base image has no curl; HEALTHCHECK below needs it. Installed as root, before the
# user switch, and the apt cache is removed in the same layer so the image stays small.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# The .NET runtime images already ship a low-privilege "app" user for exactly this
# purpose - a compromised process should not own the container - so none is created here.
USER app

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Liveness only checks the process is up, not the database - the same distinction the
# app's own /health/live and /health/ready endpoints make, and the reason this probes
# /health/live rather than /health/ready.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "CfiApp.Api.dll"]
