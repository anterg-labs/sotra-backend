# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Se copian primero los proyectos y se restauran los paquetes: mientras no
# cambien las dependencias, Docker reutiliza esta capa y no los vuelve a bajar.
COPY SOTRA-BACKEND/SOTRA-BACKEND.csproj SOTRA-BACKEND/
COPY Api/Api.csproj Api/

RUN dotnet restore Api/Api.csproj

COPY . .

RUN dotnet publish Api/Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app
# CVE-2026-109518: la imagen base todavía trae apt 2.8.3. Quitar cuando la publique corregida.
RUN apt-get update \
    && apt-get install -y --no-install-recommends --only-upgrade apt libapt-pkg6.0t64 \
    && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /var/lib/sotra/errores && chmod 1777 /var/lib/sotra/errores

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Api.dll"]
