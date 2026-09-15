# ClinicQ container image: SDK build stage, ASP.NET runtime stage.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the layer is cached while only sources change.
COPY ClinicQ.sln ./
COPY src/ClinicQ.Domain/ClinicQ.Domain.csproj src/ClinicQ.Domain/
COPY src/ClinicQ.Web/ClinicQ.Web.csproj src/ClinicQ.Web/
COPY tests/ClinicQ.Tests/ClinicQ.Tests.csproj tests/ClinicQ.Tests/
RUN dotnet restore src/ClinicQ.Web/ClinicQ.Web.csproj

COPY . .
RUN dotnet publish src/ClinicQ.Web/ClinicQ.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

# Local-disk lab report storage when Azure Blob is not configured.
RUN mkdir -p /app/storage && chown -R $APP_UID /app/storage
USER $APP_UID

COPY --from=build /app/publish ./

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD ["dotnet", "--info"]

ENTRYPOINT ["dotnet", "ClinicQ.Web.dll"]
