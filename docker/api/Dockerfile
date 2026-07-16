# syntax=docker/dockerfile:1.7

ARG DOTNET_VERSION=8.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-bookworm-slim AS restore
WORKDIR /src

COPY AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Core/AqualLifeStyle.Core.csproj src/AqualLifeStyle.Core/
COPY AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Application/AqualLifeStyle.Application.csproj src/AqualLifeStyle.Application/
COPY AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.EntityFrameworkCore/AqualLifeStyle.EntityFrameworkCore.csproj src/AqualLifeStyle.EntityFrameworkCore/
COPY AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Core/AqualLifeStyle.Web.Core.csproj src/AqualLifeStyle.Web.Core/
COPY AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Web.Host/AqualLifeStyle.Web.Host.csproj src/AqualLifeStyle.Web.Host/
COPY AqualLifeStyle/9.4.2/aspnet-core/src/AqualLifeStyle.Migrator/AqualLifeStyle.Migrator.csproj src/AqualLifeStyle.Migrator/

RUN dotnet restore src/AqualLifeStyle.Web.Host/AqualLifeStyle.Web.Host.csproj \
    && dotnet restore src/AqualLifeStyle.Migrator/AqualLifeStyle.Migrator.csproj

FROM restore AS publish
COPY AqualLifeStyle/9.4.2/aspnet-core/ ./
RUN dotnet publish src/AqualLifeStyle.Web.Host/AqualLifeStyle.Web.Host.csproj \
      --configuration Release --no-restore --output /out/api \
    && dotnet publish src/AqualLifeStyle.Migrator/AqualLifeStyle.Migrator.csproj \
      --configuration Release --no-restore --output /out/migrator

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-bookworm-slim AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=publish --chown=$APP_UID:$APP_UID /out/api/ ./
COPY --from=publish --chown=$APP_UID:$APP_UID /out/migrator/ ./migrator/

USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=8s --start-period=30s --retries=3 \
  CMD ["dotnet", "AqualLifeStyle.Web.Host.dll", "--health-check"]

ENTRYPOINT ["dotnet", "AqualLifeStyle.Web.Host.dll"]
