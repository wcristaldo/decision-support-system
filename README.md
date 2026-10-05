# Roshka DSS — Sistema de apoyo a la toma de decisiones para el despliegue de software

Aplicación web que recibe los resultados de pruebas automatizadas (archivo `output.json` de Robot Framework), calcula métricas de calidad, las compara con umbrales configurables por proyecto y emite una recomendación de despliegue (**Desplegar**, **Revisar** o **No desplegar**). La decisión final la toma una persona (Líder Técnico) con una justificación registrada, y todo queda auditado.

Desarrollado como trabajo final de grado (Ingeniería en Sistemas, UNIDA, 2026) para **Roshka S.A.**

## Funcionalidades

- Gestión de proyectos y versiones, con acceso restringido por proyecto asignado.
- Carga de resultados desde la interfaz web y por la API (`POST /api/reports`, sujeta al plan contratado), con validación estructural del `output.json`.
- Cálculo de métricas (tasa de éxito, tasa de fallo, cobertura de ejecución y tiempo de ejecución), umbrales globales y por proyecto, y margen de tolerancia de ±5 %.
- Recomendación de despliegue, registro de la decisión (con justificación de al menos 20 caracteres cuando contradice la recomendación), indicador de adherencia y acta de decisión en PDF.
- Panel principal con tendencia de calidad, historial con filtros y exportación (PDF, Excel y CSV según el plan) y comparación de versiones.
- Autenticación JWT, control de acceso por roles y permisos (RBAC), recuperación de contraseña con código, límite de intentos de inicio de sesión y auditoría inmutable (trigger en base de datos).
- Respaldo periódico configurable de la base de datos, módulo de suscripción por planes (AdamsPay y PayPal) y recibo electrónico en PDF.

## Stack

| Capa | Tecnología |
|------|-----------|
| Frontend | React 18, Vite 5, React Router 6, Recharts, Axios |
| Backend | ASP.NET Core 10 (API REST), Entity Framework Core 8 + Npgsql |
| Base de datos | PostgreSQL 15 (21 tablas) |
| Seguridad | JWT, BCrypt (factor de costo 12), políticas de autorización por permiso |
| Reportes y correo | QuestPDF, ClosedXML, MailKit |
| Despliegue | Docker Compose (frontend con Nginx, API y base de datos), HTTPS con TLS 1.2/1.3 |
| Pruebas | xUnit (164 pruebas), Coverlet, Postman, Apache JMeter, Robot Framework |

## Estructura

```
decision-support-system/
├── backend/                     API ASP.NET Core (Controllers, Services, Models, DTOs, Data)
├── DecisionSupportAPI.Tests/    Pruebas unitarias (xUnit)
├── frontend/                    SPA React + Vite (pages, components, services, styles) y nginx.conf
├── database/                    schema.sql, seed.sql, suscripcion_schema.sql, suscripcion_seed.sql y migration_*.sql
├── json-tests/                  Archivos JSON de los casos de prueba (CP-01 a CP-07) y output.json de ejemplo
├── docs/ARQUITECTURA.md         Arquitectura lógica, física y de seguridad
├── dss-roshka.postman_collection.json   Colección de Postman con los casos de prueba de la tesis
├── docker-compose.yml           Orquestación de los tres contenedores
├── .env.example                 Variables de entorno (copiar a .env)
└── CHANGELOG.md
```

## Inicio rápido con Docker Compose (recomendado)

Requisitos: Docker y Docker Compose.

```bash
cp .env.example .env
# Editar .env: POSTGRES_PASSWORD y JWT_KEY (32 caracteres como mínimo) son obligatorias
docker compose up --build -d
docker compose ps        # los tres servicios deben quedar "healthy"
```

- Aplicación: `https://localhost` (el puerto 80 redirige a HTTPS). Si `./certs` está vacío se genera un certificado autofirmado; en producción se copian `tls.crt` y `tls.key` en `./certs`.
- API: solo en `127.0.0.1:5000` (el acceso normal es a través de Nginx). Verificación de salud: `GET /api/health`.
- Los cuatro scripts de inicialización de la base (`schema.sql`, `seed.sql`, `suscripcion_schema.sql`, `suscripcion_seed.sql`) se aplican automáticamente al crear el volumen de datos.
- Los respaldos se guardan en el volumen `respaldos`.

## Instalación manual (desarrollo)

Requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/download), [Node.js 20+](https://nodejs.org/) y [PostgreSQL 15+](https://www.postgresql.org/).

```bash
# Base de datos
psql -U postgres -c 'CREATE DATABASE "DecisionSupport";'
psql -U postgres -d DecisionSupport -f database/schema.sql
psql -U postgres -d DecisionSupport -f database/seed.sql
psql -U postgres -d DecisionSupport -f database/suscripcion_schema.sql
psql -U postgres -d DecisionSupport -f database/suscripcion_seed.sql
```

```bash
# Backend (API en http://localhost:5000, Swagger en /swagger solo en desarrollo)
cd backend
cp appsettings.json appsettings.Development.json   # completar la cadena de conexión y Jwt:Key (32+ caracteres)
dotnet restore
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

```bash
# Frontend (http://localhost:3000, con proxy de /api hacia el backend)
cd frontend
npm install
npm run dev
```

Los scripts `database/migration_*.sql` aplican cambios incrementales sobre bases creadas con versiones anteriores del esquema; una instalación nueva no los necesita. El esquema se administra con scripts SQL planos (no se usan migraciones de EF Core).

## Configuración

| Clave | Uso |
|-------|-----|
| `ConnectionStrings:DefaultConnection` | Cadena de conexión a PostgreSQL |
| `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpirationMinutes` | Tokens JWT (la clave debe tener 32 caracteres o más; vigencia de 60 minutos por defecto) |
| `Cors:AllowedOrigins`, `Frontend:BaseUrl` | Orígenes permitidos y URL base del frontend |
| `Email:SmtpHost`, `Email:SmtpPort`, `Email:Username`, `Email:Password`, `Email:FromAddress`, `Email:FromName` | Envío de códigos de recuperación, alertas «No desplegar» y recibos |
| `AdamsPay:*`, `PayPal:*` | Pasarelas de pago del módulo de suscripción |
| `Backup:PgDumpPath` | Ruta al ejecutable `pg_dump` (el intervalo y la carpeta de destino se configuran desde la interfaz) |
| `Proxy:ConfiarEncabezadosReenviados` | Indica que la API opera detrás de Nginx y toma la IP real del encabezado `X-Forwarded-For` |

Los secretos nunca se versionan: `appsettings.Development.json` y `.env` están en `.gitignore`.

## Roles y permisos

El sistema tiene cuatro roles; cada uno tiene asignado un subconjunto de doce permisos (por ejemplo `gestionar_usuarios`, `cargar_resultados`, `registrar_decision`, `ver_auditoria`), que el Administrador puede ajustar desde la interfaz.

| Rol | Descripción |
|-----|-------------|
| Administrador | Acceso total: usuarios, roles y permisos, proyectos, umbrales, respaldo y auditoría |
| Analista QA | Carga resultados de pruebas y consulta métricas de sus proyectos asignados |
| Líder Técnico | Revisa las recomendaciones, registra la decisión de despliegue y consulta la auditoría |
| Gerente QA | Acceso de lectura: panel de decisión, historiales y auditoría |

`database/seed.sql` crea los roles, los permisos, los umbrales globales y usuarios de prueba (contraseñas con hash BCrypt). Cambiar esas credenciales antes de usar el sistema fuera de un entorno de pruebas.

## Pruebas

```bash
dotnet test DecisionSupportAPI.Tests                                 # 164 pruebas unitarias
dotnet test DecisionSupportAPI.Tests --collect:"XPlat Code Coverage" # con cobertura
```

La colección de Postman (`dss-roshka.postman_collection.json`) reúne los casos de integración y humo; el archivo `json-tests/ci-cd/output.json` es un ejemplo real de reporte de Robot Framework para probar la ingesta.

## Ramas

| Rama | Propósito |
|------|-----------|
| `main` | Código estable |
| `develop` | Integración de funcionalidades |
| `feature/*` | Nueva funcionalidad |
| `fix/*` | Corrección de errores |

Convenciones de trabajo en [CONTRIBUTING.md](CONTRIBUTING.md); cambios del sistema en [CHANGELOG.md](CHANGELOG.md).
