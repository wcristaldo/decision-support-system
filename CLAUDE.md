# CLAUDE.md — Contexto del proyecto Roshka DSS

> Documento de contexto para Claude Code. Leerlo completo antes de cualquier tarea.

---

## 1. Qué es este sistema

**Roshka DSS** (Decision Support System) es el proyecto de tesis de grado de **Willian Samuel Cristaldo** (UNIDA, Ingeniería en Sistemas, 2026). Es un sistema SaaS que ayuda a equipos de software a decidir si una versión está lista para despliegue, analizando resultados de pruebas automatizadas y generando recomendaciones automáticas.

**Tutor:** (revisar tesis para nombre exacto)  
**Empresa:** Roshka S.A.  
**Repositorio:** `https://github.com/wcristaldo/decision-support-system`  
**Ramas activas:** `main` (estable) y `develop` (desarrollo activo)

---

## 2. Stack tecnológico

| Capa | Tecnología |
|---|---|
| Frontend | React 18 + Vite 5, React Router v6, Axios |
| Backend | ASP.NET Core (.NET 10 / net10.0) Web API |
| Base de datos | PostgreSQL 15+ |
| ORM | Entity Framework Core 8 + Npgsql |
| Autenticación | JWT (Bearer) + RBAC |
| PDF | QuestPDF 2024.10.4 (Community license) |
| Email | MailKit 4.7.1 (SMTP Gmail con App Password) |
| Pagos | AdamsPay + PayPal Orders v2 (sandbox) |
| Cotización | open.er-api.com (USD→PYG, 1h cache con IMemoryCache) |
| CSS | Custom CSS (NO usar Bootstrap ni ningún framework CSS) |

---

## 3. Estructura del repositorio

```
decision-support-system/
├── backend/
│   ├── Controllers/          # API endpoints
│   ├── DTOs/                 # Data Transfer Objects
│   ├── Data/
│   │   └── ApplicationDbContext.cs
│   ├── Models/               # Entidades EF Core
│   ├── Services/             # Lógica de negocio
│   ├── appsettings.json      # Config (solo placeholders CHANGE_ME)
│   ├── appsettings.Development.json   # ⚠️ NUNCA commitear — contiene credenciales reales
│   └── DecisionSupportAPI.csproj
├── frontend/
│   └── src/
│       ├── components/       # Sidebar, NotificationModal, PaymentModal
│       ├── pages/            # Una página por ruta
│       ├── services/api.js   # Axios con baseURL y JWT header
│       └── styles/           # CSS por componente/página
├── database/
│   ├── schema.sql            # Schema principal
│   ├── suscripcion_schema.sql
│   ├── seed.sql
│   └── suscripcion_seed.sql
├── docs/
│   ├── ARQUITECTURA.md
│   └── PLAN_IMPLEMENTACION.md
├── json-tests/               # JSONs de ejemplo para pruebas
└── dss-roshka.postman_collection.json
```

---

## 4. Módulos y controllers

### Backend — Controllers

| Controller | Ruta base | Propósito |
|---|---|---|
| `AuthController` | `/api/auth` | Login, change-password, reset-password |
| `ProyectosController` | `/api/proyectos` | CRUD proyectos |
| `VersionesController` | `/api/versiones` | CRUD versiones |
| `ResultadosPruebaController` | `/api/resultados-prueba` | Carga JSON de resultados |
| `MetricasController` | `/api/metricas` | Cálculo y consulta de métricas |
| `RecomendacionesController` | `/api/recomendaciones` | Motor de recomendaciones |
| `DecisionesDespliegueController` | `/api/decisiones` | Registro de decisiones |
| `AnalisisController` | `/api/analisis` | Historial de análisis |
| `ReglaEvaluacionController` | `/api/reglas-evaluacion` | CRUD reglas |
| `RolesController` | `/api/roles` | Gestión de roles |
| `UsuariosController` | `/api/usuarios` | CRUD usuarios + gestión de roles |
| `AuditoriaController` | `/api/auditoria` | Log de auditoría |
| `SuscripcionController` | `/api/suscripcion` | Planes, pagos, webhooks |
| `HealthController` | `/api/health` | Health check |

### Backend — Services

| Servicio | Responsabilidad |
|---|---|
| `AuthenticationService` | JWT generation/validation, password hashing |
| `MetricsCalculationService` | Calcula tasa_exito, tasa_fallo, cobertura, tiempo_ejecucion |
| `RecommendationEngine` | Motor: DESPLEGAR / REVISAR / NO_DESPLEGAR con tolerancia ±5% |
| `AuditService` / `AuditoriaService` | Registro de acciones de usuario |
| `SuscripcionService` | Verificación de límites de plan (proyectos, usuarios, evaluaciones, tamaño archivo) |
| `AdamsPayService` | Integración con AdamsPay webhook (HMAC validation) |
| `PayPalService` | PayPal Orders v2 sandbox, cotización PYG/USD con cache 1h |
| `EmailService` | Envío de recibos (HTML+PDF adjunto) y alertas "No Apto" vía Gmail SMTP |
| `ReceiptService` | Generación de PDF recibo con QuestPDF (7 secciones según formato tutor) |

### Frontend — Páginas (src/pages/)

| Archivo | Ruta | Descripción |
|---|---|---|
| `Login.jsx` | `/login` | Autenticación |
| `Dashboard.jsx` | `/` | Panel principal |
| `Proyectos.jsx` | `/proyectos` | Lista de proyectos con fechaCreacion+hora |
| `DetalleProyecto.jsx` | `/proyectos/:id` | Versiones y acciones de un proyecto |
| `CargarResultados.jsx` | `/cargar-resultados` | Upload de JSON de resultados de prueba |
| `AnalisisMetricas.jsx` | `/analisis` | Historial global de análisis |
| `AnalisisVersion.jsx` | `/versiones/:id/analisis` | Análisis detallado de una versión |
| `UserManagement.jsx` | `/usuarios` | CRUD usuarios con fechaCreacion+hora |
| `Auditoria.jsx` | `/auditoria` | Log de auditoría |
| `Suscripcion.jsx` | `/suscripcion` | Estado, planes, historial de pagos con filtros+paginación |

---

## 5. Base de datos — Entidades principales

### schema.sql (tablas principales)
```
usuarios          → id_usuario, nombre, apellido, email, password_hash, estado, fecha_creacion
roles             → id_rol, nombre_rol, descripcion, estado
permisos          → id_permiso, nombre_permiso, modulo
rol_permiso       → (rol ↔ permiso)
usuario_rol       → (usuario ↔ rol), fecha_asignacion, estado
proyectos         → id_proyecto, nombre_proyecto, descripcion, tipo_solucion, estado, fecha_creacion
versiones         → id_version, id_proyecto, nombre_version, estado_version (pendiente/en_evaluacion/aprobada/rechazada/desplegada)
reglas_evaluacion → id_regla, nombre_metrica, operador, valor_umbral, estado
resultados_prueba → id, id_version, nombre_archivo, contenido_json, fecha_carga
metricas          → id, id_resultado, nombre_metrica, valor_metrica
evaluaciones      → id, id_resultado, resultado_evaluacion (cumple/no_cumple/revisar)
evaluacion_regla  → (evaluacion ↔ regla), resultado
recomendaciones   → id, id_evaluacion, tipo_recomendacion (DESPLEGAR/REVISAR/NO_DESPLEGAR), descripcion
decisiones_despliegue → id, id_version, id_usuario, decision, justificacion, fecha_decision
auditoria         → id, id_usuario, accion, entidad, id_entidad, detalles, fecha
```

### suscripcion_schema.sql (módulo de pagos)
```
planes_suscripcion   → id, nombre, precio_mensual (en Gs.), limites (max_proyectos, max_usuarios, etc.), funcionalidades booleanas
suscripciones        → id, id_plan, estado (pendiente/activa/vencida/cancelada), fecha_inicio, fecha_vencimiento
pagos_suscripcion    → id, id_suscripcion, id_plan, monto, estado (pendiente/aprobado/rechazado/revertido), referencia, fecha_pago
```

### Planes actuales (según tesis)
| Plan | Precio/mes | Proyectos | Usuarios | Evaluaciones/mes |
|---|---|---|---|---|
| Básico | Gs. 250.000 | 3 | 10 | 20 |
| Profesional | Gs. 500.000 | 10 | 25 | 100 |
| Empresarial | Gs. 900.000 | ilimitado | ilimitado | ilimitada |

---

## 6. Motor de recomendaciones

Métricas calculadas: `tasa_exito`, `tasa_fallo`, `cobertura`, `tiempo_ejecucion`

Reglas:
- `mayor_igual`: tasa_exito, cobertura → valor ≥ umbral
- `menor_igual`: tasa_fallo, tiempo_ejecucion → valor ≤ umbral
- Tolerancia ±5%: si está dentro del 5% del umbral → REVISAR (no fallo directo)

Resultado final por versión:
- Sin `no_cumple` → **DESPLEGAR**
- Con algún `no_cumple` → **NO_DESPLEGAR**
- Sin `no_cumple` pero con algún `revisar` → **REVISAR**

---

## 7. Módulo de pagos

### AdamsPay
- Webhook HMAC-SHA256 validation en `POST /api/suscripcion/webhook-adamspay`
- Genera recibo PDF y envía email al confirmar pago

### PayPal (sandbox)
- Merchant sandbox: `comercio@roshka.com` (cuenta US, necesaria para Paraguay)
- App: "Roshka DSS" en PayPal Developer Dashboard
- Flujo: `POST /api/suscripcion/iniciar-pago-paypal` → redirecta a PayPal → regresa con `?pp_status=success&token=ORDER_ID` → `POST /api/suscripcion/paypal-capture`
- Conversión PYG→USD: `open.er-api.com/v6/latest/USD` con cache 1h, fallback a config `PayPal:TasaPygUsd`

### Email de recibo
- SMTP: Gmail (App Password)
- Dirección: `soporte-dss@roshka.com` (visible en recibo y email)
- HTML con tabla de datos + PDF adjunto
- PDF: 7 secciones según formato tutor (identificación, emisor/pagador, detalle concepto, tabla importes en letras, forma de pago checkboxes, trazabilidad, leyenda)
- Formas de pago en recibo: solo 3 → Tarjeta de crédito/débito, AdamsPay, PayPal

---

## 8. Configuración local (appsettings.Development.json)

Este archivo NUNCA se commitea al repo. Está en `.gitignore`.  
Contiene las credenciales reales:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=DecisionSupport;Username=postgres;Password=R0shka!2026"
  },
  "Jwt": { "Key": "<clave real 32+ chars>" },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "Username": "<cuenta gmail real>",
    "Password": "<app password gmail>"
  },
  "AdamsPay": {
    "ApiKey": "<key real>",
    "ApiSecret": "<secret real>",
    "WebhookSecret": "<secret webhook>"
  },
  "PayPal": {
    "ClientId": "<client id sandbox>",
    "Secret": "<secret sandbox>",
    "BaseUrl": "https://api-m.sandbox.paypal.com"
  }
}
```

---

## 9. Reglas de seguridad y restricciones (NO negociables)

1. **`appsettings.Development.json` NUNCA se commitea** — tiene la contraseña real de BD `R0shka!2026`
2. **`appsettings.json` solo tiene placeholders** `CHANGE_ME` — nunca credenciales reales
3. **No usar Bootstrap** — solo CSS custom (archivos en `frontend/src/styles/`)
4. **No commitear archivos que referencien "Kiwi"** (nombre de proyecto interno previo)
5. **El nombre del sistema es "Roshka DSS"** — no "SAD", no "SAD-Roshka", no "AdamsPay" donde no corresponda
6. **Contraseña de BD** no va en ningún archivo versionado

---

## 10. Roles del sistema

| Rol | Acceso |
|---|---|
| `Administrador` | Acceso total, gestión de usuarios, suscripción |
| `Líder Técnico` | Supervisión, decisiones de despliegue |
| `QA` | Carga de resultados de prueba |
| `Gerente` | Reportes ejecutivos, solo lectura |
| `Desarrollador` | Acceso básico a proyectos y versiones |

---

## 11. Flujo de desarrollo

- Trabajar en rama `develop`
- Commits semánticos: `feat:`, `fix:`, `refactor:`, `security:`, `chore:`
- Merge a `main` cuando hay cambios estables que pushear
- **No crear ramas `feature/*`** nuevas sin necesidad (las anteriores ya fueron eliminadas)

---

## 12. Cambios implementados en sesiones anteriores

Todos comiteados y pusheados a `main` y `develop`:

- ✅ Precios actualizados según Presupuesto_RoshkaDSS_2026 (Gs. 250k / 500k / 900k)
- ✅ Renombrado completo de SAD/SAD-Roshka → Roshka DSS en todo el sistema
- ✅ Integración PayPal sandbox (Orders v2) + selector de pasarela (PaymentModal)
- ✅ Cotización automática USD/PYG desde open.er-api.com con cache 1h y fallback
- ✅ Suscripción: `AddMonths(1)` en lugar de `AddDays(30)` para vencimiento correcto
- ✅ Días restantes: comparación por fecha local sin truncación de hora
- ✅ Recibo PDF rediseñado: 7 secciones formato tutor con QuestPDF
- ✅ Email: método de pago dinámico (no hardcodeado "AdamsPay")
- ✅ Email footer: `soporte-dss@roshka.com` (eliminado "Powered by AdamsPay")
- ✅ Recibo: solo 3 formas de pago (Tarjeta, AdamsPay, PayPal)
- ✅ Historial de pagos: paginación (5/10/15/20 por página, default 10) + filtros (estado, plan, desde/hasta)
- ✅ Filtro fecha historial: comparación por fecha local (fix desfase UTC↔local)
- ✅ Gestión de Usuarios: columna "Creado el" con fecha+hora
- ✅ Proyectos: columna "Creado el" con fecha+hora
- ✅ UserManagement.jsx: eliminado `<td>` duplicado (causaba error de sintaxis en Vite)
- ✅ Security: passwords hardcodeadas en Postman collection reemplazadas por variables `{{password}}`
- ✅ Ramas `feature/*` eliminadas (estaban 2 meses sin actividad)

---

## 13. Libro de tesis

El documento principal de tesis está en:
```
C:\Users\William Cristaldo\Documents\Tesis 2026\Taller de Tesis\Tesis_Cristaldo_Willian.docx
```

Otros documentos relevantes en esa carpeta:
- `Presupuesto_RoshkaDSS_2026.xlsx` — precios y presupuesto del sistema
- `Clase_Capitulos_IV_VI_UNIDA.pdf` — guía de capítulos
- `Encuestas_PreTest_PostTest_RoshkaDSS.docx` — instrumentos de validación
- `Instrumento_C_Cuestionario_Validacion.docx` — cuestionario de validación

---

## 14. Cómo levantar el sistema localmente

```bash
# Base de datos
psql -U postgres -f database/schema.sql
psql -U postgres -f database/seed.sql
psql -U postgres -f database/suscripcion_schema.sql
psql -U postgres -f database/suscripcion_seed.sql

# Backend (desde /backend)
dotnet restore
dotnet run
# API en http://localhost:5000 | Swagger en http://localhost:5000/swagger

# Frontend (desde /frontend)
npm install
npm run dev
# App en http://localhost:3000
```

---

## 15. Credenciales de prueba del sistema

Las siguientes son credenciales del sistema DSS (no de servicios externos):

| Email | Contraseña | Rol |
|---|---|---|
| wcristaldo@roshka.com | (contraseña actual del admin) | Administrador |
| lider@roshka.com | Lider2026! | Líder Técnico |
| analista@roshka.com | Analista2026! | QA |
| gerente@roshka.com | Gerente2026! | Gerente |

> ⚠️ **Importante:** Las contraseñas de lider/analista/gerente estuvieron expuestas brevemente en el repo (Postman collection). Si el sistema está en producción con esas credenciales, cambiarlas desde el panel Admin → Gestión de Usuarios.
> La contraseña del administrador (`wcristaldo@roshka.com`) fue cambiada manualmente — no figura en el repo.
