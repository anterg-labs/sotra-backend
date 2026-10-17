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

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Api.dll"]
