# Contenedor "api" (arquitectura física del Capítulo IV).
# Build multi-stage: SDK para compilar, runtime ASP.NET para ejecutar.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DecisionSupportAPI.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl: chequeo de salud del contenedor; postgresql-client: pg_dump para el respaldo periódico (RNF12)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl postgresql-client \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "DecisionSupportAPI.dll"]
