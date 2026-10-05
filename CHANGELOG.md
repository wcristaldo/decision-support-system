# Changelog

Formato: [Keep a Changelog](https://keepachangelog.com/es/1.0.0/)

---

## [Unreleased]

### Added
- Registro de "override" en las decisiones de despliegue (RF12/RF13): se marca automáticamente cuando la decisión final contradice la recomendación del sistema (aprobar algo "no apto" o rechazar algo "apto"), exige una justificación mínima de 20 caracteres en ese caso y lo muestra como excepción visible en el historial — evidencia concreta de que "el sistema asiste, no decide"
- Indicador de adherencia a las recomendaciones (`GET /api/decisionesDespliegue/adherencia`) con panel dedicado en el Dashboard: % histórico de decisiones que siguieron la recomendación automática del sistema
- Acta de decisión de despliegue en PDF (`GET /api/decisionesDespliegue/{id}/acta`, QuestPDF): documento único que consolida proyecto, versión, métricas evaluadas, recomendación del sistema y decisión final, incluyendo aviso cuando la decisión fue un override
- Auditoría de cambios en umbrales de calidad: `ReglaEvaluacionController` ahora registra creación, actualización y eliminación de reglas (antes no auditaba nada); nuevas entidades "Regla de evaluación" y "Decisión de despliegue" en el filtro de Auditoría
- RBAC real por permisos (`gestionar_usuarios`, `ver_proyectos`, `cargar_resultados`, `ejecutar_evaluacion`, `ver_evaluacion`, `gestionar_reglas`, `registrar_decision`, `ver_decisiones`, `ver_auditoria`, etc.), activando el claim `permission` del JWT que ya se emitía pero nunca se validaba — `[Authorize]` agregado a los 7 controllers que no lo tenían (`Proyectos`, `Versiones`, `ResultadosPrueba`, `Metricas`, `Recomendaciones`, `DecisionesDespliegue`, `Analisis`)
- Umbrales de calidad configurables **por proyecto** (RF07/RF08/CU-03): `reglas_evaluacion.id_proyecto` (nullable, override sobre la regla global), endpoints `GET/PUT/DELETE /api/reglaEvaluacion/proyecto/{id}`, panel "Umbrales de calidad" en `DetalleProyecto.jsx`
- Endpoint `POST /api/reports` (CU-05, ingesta CI/CD automatizada), reutilizando el pipeline de ingesta vía `IIngestaResultadosService`. Recibe el archivo `output.json` real de Robot Framework (`multipart/form-data`: `versionId`, `archivo`, `observaciones`), lo valida y extrae las métricas en el servidor (`RobotFrameworkParser`, mismas reglas que la carga manual) y responde con las métricas extraídas y la recomendación generada; antes esperaba las métricas ya calculadas por el cliente
- Gráfico de tendencia histórica (Recharts) en Análisis y Métricas (RF10)
- Filtros por fecha y usuario responsable en el historial de análisis (RF11); nueva columna "Responsable" (requiere trackear `UsuarioCargaId`, antes siempre nulo)
- `docker-compose.yml` + Dockerfiles (`backend/Dockerfile`, `frontend/Dockerfile` + `nginx.conf`) — arquitectura física del Capítulo IV
- Proyecto `DecisionSupportAPI.Tests` (xUnit) con los casos CP-U01 a CP-U04 de la Tabla 21, corriendo contra `RecommendationEngine` real
- Columnas redimensionables (estilo Excel) en las 5 tablas del sistema, con ancho persistido en `localStorage`

### Security
- HTTPS en el despliegue con Docker: el Nginx del contenedor frontend termina TLS (puerto 443) con el certificado de `./certs` (`tls.crt`/`tls.key`) o, si no se provee, con uno autofirmado generado al iniciar; el puerto HTTP solo redirige a HTTPS
- La API solo se publica en `127.0.0.1` y la base de datos no publica puertos fuera de la red interna de Docker
- La IP real del cliente se toma de `X-Forwarded-For` cuando la API está detrás de Nginx (`Proxy:ConfiarEncabezadosReenviados`), para que el límite de intentos de inicio de sesión y la IP de la auditoría correspondan al usuario y no al proxy
- Registro de auditoría inmutable a nivel de base de datos: un trigger rechaza `UPDATE`, `DELETE` y `TRUNCATE` sobre `auditoria` (`migration_auditoria_inmutable.sql`)
- Dependencias con vulnerabilidades conocidas actualizadas: Npgsql (EF Core provider 8.0.11), System.IdentityModel.Tokens.Jwt 7.1.2, MailKit 4.18.1, axios 1.20.0 y form-data 4.0.6. Quedan dos avisos moderados de react-router que requieren migrar a la versión 7
- La imagen de la API no incluye la carpeta `backups/` (volcados de la base con datos)

### Changed
- La carga automática desde el pipeline CI/CD (`POST /api/reports`) queda sujeta a la funcionalidad «Carga automática desde CI/CD» del plan activo (402 si no está incluida); la carga manual desde la interfaz sigue disponible en todos los planes
- Se quita «Alertas Slack/Teams» de los planes: el sistema solo envía alertas por correo electrónico ante una recomendación «No desplegar» (`migration_quitar_notificaciones_slack.sql`)
- Pantalla de inicio de sesión: aviso de privacidad visible con enlaces a la Política de Privacidad y a los Términos y Condiciones; mensaje específico «Demasiados intentos» cuando se supera el límite de intentos (HTTP 429)
- Docker Compose: chequeos de salud de los tres servicios, arranque ordenado por estado «healthy», `pg_dump` incluido en la imagen de la API y volumen persistente para los respaldos (RNF12)

### Fixed
- **Motor de recomendación inerte**: los criterios de `reglas_evaluacion` en la base real (`porcentaje_exito`, `cobertura_porcentaje`, etc.) no coincidían con los nombres de métrica que genera `MetricsCalculationService` (`tasa_exito`, `cobertura`, etc.) — ninguna regla aplicaba nunca y todo resultado terminaba en "desplegar" sin importar los datos reales. Corregido en la base (`migration_fix_criterios_reglas.sql`) y en `seed.sql`
- Hashing de contraseñas migrado de SHA-256 (sin sal) a BCrypt factor 12 (RNF04), con migración transparente de hashes legado en el primer login exitoso tras el despliegue
- `RecommendationEngine.GenerateRecommendationsAsync` reevaluaba TODOS los resultados de una versión al cargar uno nuevo, pisando la fecha de evaluación de los anteriores
- `DecisionesDespliegueController` nunca registraba `UsuarioDecisorId`; `ResultadosPruebaController`/`ReportsController` nunca registraban `UsuarioCargaId` — ambos rotos por falta de autenticación, ahora se leen del JWT
- `AuditoriaController`/`AuthController.ResetPassword`: `Forbid("mensaje")` lanzaba `InvalidOperationException` (500) en vez de devolver 403 — uso incorrecto de la sobrecarga de `Forbid` con un string de esquema de autenticación inexistente
- Endpoint de debug `POST /api/auth/debug/hash` (devolvía el hash de cualquier contraseña sin autenticación) eliminado
- `seed.sql`: `CROSS JOIN` de reglas de evaluación referenciaba `admin@roshka.com`, que no existe (`wcristaldo@roshka.com` es el real) — una base sembrada desde cero nunca insertaba las reglas
- Bug de paginación en Auditoría (Anterior/Siguiente saltaban a primera/última página)
- Fecha de "Análisis y Métricas" ahora muestra fecha+hora (antes solo fecha, indistinguible entre cargas del mismo día)
- Doble sistema de auditoría (`AuditService` + `AuditoriaService`) escribía dos filas por cada acción sobre un proyecto, una de ellas siempre con usuario `null` y sin IP; unificado en `AuditoriaService` (captura usuario real + IP) en todos los controllers, `AuditService` eliminado
- Badge de tipo de proyecto en `DetalleProyecto.jsx` mostraba siempre "-" (leía `proyecto.tipo`, un campo inexistente; el real es `tipoSolucion`)
- Contraste insuficiente (texto blanco sobre azul claro, ~2.7:1) en los botones primarios de casi toda la aplicación (Proyectos, Usuarios, Login, Auditoría, Roles y Permisos, Mi Perfil, Detalle de Proyecto, Carga de Resultados, Análisis)
- Paginación de Auditoría rota: "Anterior"/"Siguiente" saltaban siempre a la primera/última página en vez de la anterior/siguiente
- Botón "Eliminar" de un proyecto quedaba trabado en "Eliminando…" si la API fallaba (faltaba `finally`)
- `colSpan` incorrecto en la fila vacía de la tabla de usuarios (5 en vez de 6 columnas)
- Columna `comentario` de `decisiones_despliegue` limitada a 255 caracteres en la base de datos pero a 1000 en el frontend
- Métricas no enteras en Análisis y en la comparativa de versiones se mostraban con tres decimales («38.500 segundos», que se lee como treinta y ocho mil); ahora usan dos decimales, igual que los porcentajes y el detalle técnico
- Colección de Postman (`dss-roshka.postman_collection.json`): nueva primera carpeta «Casos de prueba de la tesis (CP-05 a CP-14)» con las solicitudes y los tests usados como evidencia; el form-data de ingesta usa la ruta relativa `json-tests/ci-cd/output.json`
- Nombres de proyecto invisibles en el listado de Proyectos: `.proy-td-link` tenía `max-width: 0` (recorte total del contenido); corregido a `100%` en `Proyectos.css`, junto con el ancho de las columnas descripción/acciones en `Proyectos.jsx` que lo compensaba mal. Bug real, visible también en la evidencia de la tesis y causa de varias fallas en la suite Robot Framework
- Checkbox de "Proyectos asignados" en la edición de usuario (`UserManagement.jsx`) desalineado con el nombre del proyecto por falta de `flex: 0 0 auto` en el input

### Removed
- Módulo de "API REST pública" y "Webhooks salientes" (API keys, webhooks salientes, pestaña "API y Webhooks" en Suscripción): no formaba parte del alcance definido en el anteproyecto ni en el libro de tesis
- `PagoparService.cs`: código muerto, ningún endpoint ni módulo del sistema lo invocaba (el módulo de suscripción usa AdamsPay y PayPal)

### Changed
- `ReglaEvaluacionController`, `ResultadosPruebaController` y `AuthController` migrados de checks de rol manuales (`User.IsInRole`) a políticas de autorización declarativas

---

## [0.3.0] — 2026-06-09

### Added
- Gestión de usuarios: `UsuariosController`, cambio y reseteo de contraseña
- Página `UserManagement` en el frontend con estilos

### Changed
- Refactorización de estructura del proyecto (eliminar doble anidamiento)
- `appsettings.json` sin credenciales hardcodeadas
- `.gitignore` actualizado con patrones profesionales

---

## [0.2.0] — 2026-06-08

### Added
- Backend completo: Auth, Proyectos, Versiones, Métricas, Recomendaciones, Decisiones
- Motor de recomendaciones (`RecommendationEngine`)
- Servicio de auditoría (`AuditService`)
- Frontend React + Vite con todas las páginas

---

## [0.1.0] — 2026-06-07

### Added
- Estructura inicial del proyecto
- Esquema de base de datos PostgreSQL con RBAC
