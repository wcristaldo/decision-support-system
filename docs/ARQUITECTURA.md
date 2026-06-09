# Arquitectura del Sistema

## Descripción General
El Sistema de Apoyo a la Toma de Decisiones para el Despliegue de Software sigue una arquitectura cliente-servidor de tres capas con separación clara de responsabilidades.

## Capas Arquitectónicas

### 1. Capa de Presentación (Frontend)
- **Tecnología**: React + Vite
- **Ubicación**: `/frontend`
- **Responsabilidades**:
  - Interfaz de usuario interactiva
  - Validación de entrada en cliente
  - Gestión de estado local
  - Consumo de API REST
  - Autenticación (manejo de tokens JWT)

**Componentes Principales**:
- `Dashboard` - Panel principal del sistema
- `Login` - Autenticación de usuarios
- Formularios de carga de pruebas
- Visualización de métricas y recomendaciones

### 2. Capa de Lógica de Negocio (Backend)
- **Tecnología**: ASP.NET Core Web API
- **Ubicación**: `/backend/DecisionSupportAPI`
- **Responsabilidades**:
  - Procesamiento de reglas de negocio
  - Cálculo de métricas
  - Generación de recomendaciones
  - Autenticación y autorización (JWT + RBAC)
  - Validación de datos
  - Operaciones CRUD

**Controladores Principales**:
- `AuthController` - Autenticación y autorización
- `ProjectsController` - Gestión de proyectos
- `TestResultsController` - Carga de resultados de pruebas
- `MetricsController` - Cálculo de métricas
- `RecommendationsController` - Generación de recomendaciones
- `DecisionsController` - Registro de decisiones de despliegue

**Servicios Principales**:
- `AuthenticationService` - Manejo de JWT
- `MetricsCalculationService` - Cálculo de métricas de calidad
- `RecommendationEngine` - Motor de generación de recomendaciones
- `AuditService` - Registro de auditoría

### 3. Capa de Persistencia (Base de Datos)
- **Tecnología**: PostgreSQL
- **Ubicación**: `/docs/database`
- **ORM**: Entity Framework Core
- **Responsabilidades**:
  - Almacenamiento de datos
  - Integridad referencial
  - Índices para optimización
  - Registro de auditoría

**Entidades Principales**:
- `Usuarios` - Información de usuarios
- `Roles` - Roles del sistema
- `Proyectos` - Proyectos de software
- `Versiones` - Versiones de proyecto
- `ResultadosPrueba` - Resultados de pruebas automatizadas
- `Métricas` - Métricas de calidad
- `Evaluaciones` - Evaluaciones de versiones
- `Recomendaciones` - Recomendaciones automáticas
- `DecisionesDespliegue` - Decisiones de despliegue

## Flujo de Datos

```
[Frontend React]
       |
       | HTTP/REST
       |
    [Gateway/CORS]
       |
[ASP.NET Core API]
       |
   [Services]
       |
   [Repositories]
       |
   [DbContext]
       |
  [PostgreSQL]
```

## Flujos Principales

### 1. Flujo de Autenticación
```
1. Usuario ingresa credenciales → Frontend
2. Frontend → POST /api/auth/login → Backend
3. Backend valida credenciales
4. Backend genera JWT
5. Backend → Token JWT → Frontend
6. Frontend almacena token en localStorage
7. Frontend incluye token en Authorization header
```

### 2. Flujo de Carga de Resultados de Pruebas
```
1. Usuario carga archivo de resultados → Frontend
2. Frontend valida formato JSON
3. Frontend → POST /api/testresults → Backend
4. Backend valida estructura
5. Backend almacena en BD
6. Backend dispara cálculo de métricas
7. Backend retorna confirmación
8. Frontend notifica al usuario
```

### 3. Flujo de Generación de Recomendaciones
```
1. Sistema detecta nueva versión
2. Backend calcula métricas
3. Backend aplica reglas de evaluación
4. Backend ejecuta motor de recomendaciones
5. Backend almacena recomendaciones
6. Backend notifica frontend
7. Frontend muestra recomendaciones a usuario
```

## Patrones de Diseño

### Backend
- **MVC** (Model-View-Controller) con controladores REST
- **Repository Pattern** para acceso a datos
- **Dependency Injection** para inyección de dependencias
- **Service Layer** para lógica de negocio

### Frontend
- **Component-Based Architecture** con React
- **Container/Presentational Components**
- **Custom Hooks** para lógica reutilizable
- **Context API** para estado global (opcional)

## Seguridad

### Autenticación
- JWT (JSON Web Tokens)
- Tokens con expiración configurable
- Refresh tokens para renovación

### Autorización
- RBAC (Role-Based Access Control)
- Permisos granulares por rol
- Middleware de autorización en backend

### Encriptación
- Contraseñas hasheadas con bcrypt
- Conexión HTTPS en producción
- Datos sensibles encriptados

## Escalabilidad

### Consideraciones
- Connection pooling en base de datos
- Caching de resultados frecuentes
- Índices optimizados
- Paginación en listados
- Soporte para 20+ usuarios concurrentes (inicial)

## Monitoreo y Logging

### Backend
- Logging de operaciones
- Tracking de errores
- Auditoría de acciones de usuario
- Métricas de rendimiento

### Frontend
- Logging en consola (desarrollo)
- Tracking de errores
- Analytics de uso (opcional)
