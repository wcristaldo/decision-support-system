# DSS — Decision Support System

Sistema de apoyo a la toma de decisiones para el despliegue de software.  
Analiza resultados de pruebas automatizadas y genera recomendaciones sobre si un release está listo para producción.

Desarrollado como proyecto de tesis para **Roshka S.A.**

## Stack

| Capa | Tecnología |
|------|-----------|
| Frontend | React 18 + Vite |
| Backend | ASP.NET Core 10 Web API |
| Base de datos | PostgreSQL 15 |
| Autenticación | JWT + RBAC |

## Estructura

```
decision-support-system/
├── backend/          ASP.NET Core API
│   ├── Controllers/
│   ├── Models/
│   ├── DTOs/
│   ├── Services/
│   ├── Data/
│   └── Program.cs
├── frontend/         React + Vite SPA
│   └── src/
│       ├── components/
│       ├── pages/
│       ├── services/
│       └── styles/
├── database/
│   ├── schema.sql    Esquema inicial
│   └── seed.sql      Roles y datos base
├── docs/
│   ├── ARQUITECTURA.md
│   └── PLAN_IMPLEMENTACION.md
├── .env.example
└── CHANGELOG.md
```

## Inicio rápido

### Requisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [PostgreSQL 15+](https://www.postgresql.org/)

### Base de datos
```bash
psql -U postgres -f database/schema.sql
psql -U postgres -f database/seed.sql
```

### Backend
```bash
cd backend
# Copiar y editar configuración
cp appsettings.json appsettings.Development.json
# Editar appsettings.Development.json con tus credenciales locales
dotnet restore
dotnet run
# API en http://localhost:5000  |  Swagger en http://localhost:5000/swagger
```

### Frontend
```bash
cd frontend
cp .env.example .env
npm install
npm run dev
# App en http://localhost:5173
```

## Roles

| Rol | Descripción |
|-----|-------------|
| `ADMIN` | Acceso total al sistema |
| `LIDER_TECNICO` | Supervisión y decisiones de despliegue |
| `QA` | Carga de resultados de pruebas |
| `GERENTE` | Acceso a reportes ejecutivos |

## Ramas

| Rama | Propósito |
|------|-----------|
| `main` | Código estable |
| `develop` | Integración de features |
| `feature/*` | Nueva funcionalidad |
| `fix/*` | Corrección de bugs |
