# Changelog

Formato: [Keep a Changelog](https://keepachangelog.com/es/1.0.0/)

---

## [Unreleased]

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
