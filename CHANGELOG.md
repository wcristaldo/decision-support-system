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
- Endpoint `POST /api/reports` (CU-05, ingesta CI/CD automatizada), reutilizando el pipeline de ingesta vía `IIngestaResultadosService`
- Gráfico de tendencia histórica (Recharts) en Análisis y Métricas (RF10)
- Filtros por fecha y usuario responsable en el historial de análisis (RF11); nueva columna "Responsable" (requiere trackear `UsuarioCargaId`, antes siempre nulo)
- `docker-compose.yml` + Dockerfiles (`backend/Dockerfile`, `frontend/Dockerfile` + `nginx.conf`) — arquitectura física del Capítulo IV
- Proyecto `DecisionSupportAPI.Tests` (xUnit) con los casos CP-U01 a CP-U04 de la Tabla 21, corriendo contra `RecommendationEngine` real
- Columnas redimensionables (estilo Excel) en las 5 tablas del sistema, con ancho persistido en `localStorage`

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

### Removed
- Módulo de "API REST pública" y "Webhooks salientes" (API keys, webhooks salientes, pestaña "API y Webhooks" en Suscripción): no formaba parte del alcance definido en el anteproyecto ni en el libro de tesis

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
