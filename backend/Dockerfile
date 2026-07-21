# =============================================================
# Dockerfile — SAD-Roshka Backend (.NET 10)
# Deploy en Railway: https://railway.app
# =============================================================

# ── Etapa 1: build ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar csproj y restaurar dependencias (capa cacheada)
COPY ["backend/DecisionSupportAPI.csproj", "backend/"]
RUN dotnet restore "backend/DecisionSupportAPI.csproj"

# Copiar todo el código del backend y publicar
COPY backend/ backend/
WORKDIR /src/backend
RUN dotnet publish "DecisionSupportAPI.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Etapa 2: runtime ──────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Exponer el puerto que Railway asigna dinámicamente via $PORT
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production

# El CMD usa shell form para que $PORT se expanda en runtime
CMD dotnet DecisionSupportAPI.dll
