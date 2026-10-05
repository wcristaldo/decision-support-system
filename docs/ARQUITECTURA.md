# Arquitectura del sistema

Roshka DSS es una aplicación web de tres capas (presentación, lógica de negocio y acceso a datos) con una capa transversal de seguridad, empaquetada en tres contenedores Docker.

## Arquitectura física

```
Navegador ──HTTPS :443──▶ dss-frontend (Nginx + SPA React)
                              │  proxy /api/  (red interna dss-net)
                              ▼
                          dss-api (ASP.NET Core 10, Kestrel :5000, publicado solo en 127.0.0.1)
                              │  Npgsql / pg_dump
                              ▼
                          dss-db (PostgreSQL 15, sin puertos publicados)
```

- **dss-frontend** (imagen `nginx:alpine`): sirve la SPA compilada, termina TLS (1.2 y 1.3, HTTP/2, HSTS), redirige el puerto 80 a HTTPS y reenvía `/api/` a la API. Chequeo de salud: `GET /healthz`.
- **dss-api** (imagen `dotnet/aspnet:10.0`): API REST, servicio de respaldo en segundo plano e instalación de `pg_dump`. Chequeo de salud: `GET /api/health`. Los respaldos se guardan en el volumen `respaldos`.
- **dss-db** (imagen `postgres:15`): se inicializa con `schema.sql`, `seed.sql`, `suscripcion_schema.sql` y `suscripcion_seed.sql`. Chequeo de salud: `pg_isready`. Datos en el volumen `postgres_data`.

Servicios externos: servidor SMTP (alertas «No desplegar», códigos de recuperación y recibos), pasarelas de pago AdamsPay y PayPal, y `open.er-api.com` (tipo de cambio PYG/USD). Un cliente automatizado de la API (por ejemplo, un eventual pipeline de integración continua) puede enviar reportes a `POST /api/reports`; no forma parte del sistema.

## Capas

### Presentación — `frontend/`
SPA en React 18 con Vite. Páginas: Login (con recuperación de contraseña, aviso de privacidad y términos), Dashboard, Proyectos, Detalle de proyecto (umbrales, versiones, comparación), Cargar resultados, Análisis y métricas, Análisis de versión (recomendación y registro de decisión), Auditoría, Gestión de usuarios (con roles y permisos), Respaldo de base de datos, Suscripción y Mi perfil. El cliente HTTP (`services/api.js`, Axios) adjunta el JWT guardado en `sessionStorage` y vuelve al inicio de sesión ante un 401. El menú se arma según los permisos vigentes del usuario.

### Lógica de negocio — `backend/`
API REST con 16 controladores: `Auth`, `Usuarios`, `Roles`, `Proyectos`, `Versiones`, `ResultadosPrueba`, `Reports`, `Metricas`, `Recomendaciones`, `ReglaEvaluacion`, `DecisionesDespliegue`, `Analisis`, `Auditoria`, `Backup`, `Suscripcion` y `Health`.

15 servicios de dominio:

| Servicio | Responsabilidad |
|----------|-----------------|
| `RobotFrameworkParser` | Valida la estructura del `output.json` y extrae los conteos de pruebas |
| `IngestaResultadosService` | Flujo único de ingesta (carga manual y por API): validación, persistencia, métricas y recomendación |
| `MetricsCalculationService` | Tasa de éxito, tasa de fallo, cobertura de ejecución y tiempo de ejecución |
| `RecommendationEngine` | Compara las métricas con los umbrales (globales o del proyecto) y aplica la tolerancia de ±5 % |
| `AuthenticationService` | Hash BCrypt, emisión de JWT y códigos de recuperación de contraseña |
| `ProyectoAccesoService` | Control de acceso por proyecto asignado |
| `AuditoriaService` | Registro de auditoría (usuario, acción, entidad, IP) |
| `ActaPdfService`, `ReporteExportService`, `ReceiptService` | Acta de decisión, exportación del historial (PDF, Excel y CSV) y recibo electrónico |
| `DatabaseBackupService` | Respaldo periódico con `pg_dump` (tarea en segundo plano) |
| `EmailService` | Correos mediante SMTP (MailKit) |
| `SuscripcionService`, `AdamsPayService`, `PayPalService` | Planes, límites, pagos y confirmaciones de las pasarelas |

### Acceso a datos
Entity Framework Core 8 con Npgsql (`ApplicationDbContext`, 21 entidades). El esquema se define con scripts SQL planos en `database/` (no se usan migraciones de EF Core).

## Seguridad

- **Autenticación**: JWT firmado con HMAC-SHA256 (60 minutos por defecto); contraseñas con BCrypt (factor de costo 12); la clave `Jwt:Key` debe tener 32 caracteres o más o la aplicación no arranca.
- **Autorización**: 12 permisos agrupados por módulo, asignados a cuatro roles; cada endpoint declara su política y los permisos se leen de la base en cada petición, de modo que revocarlos surte efecto sin esperar a que expire el token. Control adicional por proyecto asignado (el Administrador accede a todos).
- **Rutas públicas**: inicio de sesión, recuperación de contraseña, verificación de salud, consulta de planes y notificaciones de las pasarelas de pago (la de AdamsPay se valida con HMAC; la de PayPal solo activa un pedido existente identificado por su `orderId`).
- **Limitación de tasa**: 8 intentos por dirección IP cada 5 minutos en inicio de sesión y recuperación de contraseña (HTTP 429).
- **Auditoría inmutable**: un trigger de PostgreSQL rechaza `UPDATE`, `DELETE` y `TRUNCATE` sobre la tabla `auditoria`.
- **Comunicaciones**: HTTPS en Nginx (TLS 1.2 y 1.3, HSTS), CORS con lista blanca de orígenes, API publicada solo en `127.0.0.1`, base de datos sin puertos publicados.
- **Secretos**: variables de entorno (`.env`) y `appsettings.Development.json`, ambos fuera del repositorio.

## Flujo principal

1. El Analista QA (o un cliente automatizado de la API, si el plan lo permite) envía el `output.json` de una versión.
2. El sistema valida el archivo, registra el resultado y calcula las cuatro métricas.
3. El motor de reglas compara las métricas con los umbrales y emite la recomendación (Desplegar, Revisar o No desplegar); con «No desplegar» y un plan que lo incluya, se envía una alerta por correo.
4. El Líder Técnico revisa el análisis y registra su decisión (Aprobado o Rechazado); si contradice la recomendación, la justificación debe tener al menos 20 caracteres y la decisión se marca como excepción.
5. Todo queda en la auditoría; el acta de decisión puede descargarse en PDF.
