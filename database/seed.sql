-- =====================================================
-- Roles y Permisos para DSS
-- =====================================================

-- Limpiar roles y permisos existentes (opcional)
-- DELETE FROM usuarios_roles;
-- DELETE FROM roles_permisos;
-- DELETE FROM permisos;
-- DELETE FROM roles;

-- =====================================================
-- 1. INSERTAR ROLES
-- =====================================================

INSERT INTO roles (nombre, descripcion) VALUES
('ADMIN', 'Administrador del sistema con acceso total'),
('LIDER_TECNICO', 'Líder técnico que supervisa evaluaciones y toma decisiones'),
('QA', 'Especialista de QA que carga resultados de pruebas'),
('GERENTE', 'Gerente que visualiza dashboards y reportes ejecutivos');

-- =====================================================
-- 2. INSERTAR PERMISOS
-- =====================================================

-- Permisos de Sistema
INSERT INTO permisos (nombre, descripcion) VALUES
('SYSTEM_MANAGE_USERS', 'Gestionar usuarios del sistema'),
('SYSTEM_MANAGE_ROLES', 'Gestionar roles y permisos'),
('SYSTEM_VIEW_AUDIT', 'Ver auditoría y trazabilidad del sistema'),
('SYSTEM_MANAGE_CONFIG', 'Gestionar configuración del sistema');

-- Permisos de Proyectos
INSERT INTO permisos (nombre, descripcion) VALUES
('PROJECT_CREATE', 'Crear nuevos proyectos'),
('PROJECT_READ', 'Ver proyectos'),
('PROJECT_UPDATE', 'Editar proyectos'),
('PROJECT_DELETE', 'Eliminar proyectos');

-- Permisos de Pruebas
INSERT INTO permisos (nombre, descripcion) VALUES
('TEST_UPLOAD_RESULTS', 'Cargar resultados de pruebas'),
('TEST_VIEW_RESULTS', 'Ver resultados de pruebas'),
('TEST_ANALYZE', 'Analizar resultados de pruebas');

-- Permisos de Evaluaciones
INSERT INTO permisos (nombre, descripcion) VALUES
('EVALUATION_CREATE_RULES', 'Crear reglas de evaluación'),
('EVALUATION_RUN', 'Ejecutar evaluaciones'),
('EVALUATION_VIEW', 'Ver resultados de evaluaciones');

-- Permisos de Decisiones
INSERT INTO permisos (nombre, descripcion) VALUES
('DECISION_CREATE', 'Crear decisiones de despliegue'),
('DECISION_APPROVE', 'Aprobar decisiones de despliegue'),
('DECISION_EXECUTE', 'Ejecutar decisiones de despliegue'),
('DECISION_VIEW', 'Ver decisiones de despliegue');

-- Permisos de Recomendaciones
INSERT INTO permisos (nombre, descripcion) VALUES
('RECOMMENDATION_VIEW', 'Ver recomendaciones IA'),
('RECOMMENDATION_OVERRIDE', 'Anular recomendaciones IA');

-- Permisos de Reportes
INSERT INTO permisos (nombre, descripcion) VALUES
('REPORT_VIEW_DASHBOARD', 'Ver dashboard ejecutivo'),
('REPORT_EXPORT', 'Exportar reportes'),
('REPORT_SCHEDULE', 'Agendar reportes automáticos');

-- =====================================================
-- 3. ASIGNAR PERMISOS A ROLES
-- =====================================================

-- ADMIN: TODOS los permisos
INSERT INTO roles_permisos (rol_id, permiso_id)
SELECT r.id, p.id FROM roles r, permisos p WHERE r.nombre = 'ADMIN';

-- LIDER_TECNICO: Supervisión y decisiones
INSERT INTO roles_permisos (rol_id, permiso_id)
SELECT r.id, p.id FROM roles r, permisos p
WHERE r.nombre = 'LIDER_TECNICO' AND p.nombre IN (
  'PROJECT_CREATE', 'PROJECT_READ', 'PROJECT_UPDATE',
  'TEST_VIEW_RESULTS', 'TEST_ANALYZE',
  'EVALUATION_CREATE_RULES', 'EVALUATION_RUN', 'EVALUATION_VIEW',
  'DECISION_CREATE', 'DECISION_APPROVE', 'DECISION_EXECUTE', 'DECISION_VIEW',
  'RECOMMENDATION_VIEW', 'RECOMMENDATION_OVERRIDE',
  'SYSTEM_VIEW_AUDIT'
);

-- QA: Cargar pruebas y ver análisis
INSERT INTO roles_permisos (rol_id, permiso_id)
SELECT r.id, p.id FROM roles r, permisos p
WHERE r.nombre = 'QA' AND p.nombre IN (
  'PROJECT_READ',
  'TEST_UPLOAD_RESULTS', 'TEST_VIEW_RESULTS', 'TEST_ANALYZE',
  'EVALUATION_VIEW'
);

-- GERENTE: Dashboard y reportes
INSERT INTO roles_permisos (rol_id, permiso_id)
SELECT r.id, p.id FROM roles r, permisos p
WHERE r.nombre = 'GERENTE' AND p.nombre IN (
  'PROJECT_READ',
  'TEST_VIEW_RESULTS',
  'EVALUATION_VIEW',
  'DECISION_VIEW',
  'RECOMMENDATION_VIEW',
  'REPORT_VIEW_DASHBOARD', 'REPORT_EXPORT'
);

-- =====================================================
-- 4. CREAR USUARIOS DE PRUEBA (opcional)
-- =====================================================

-- Admin
INSERT INTO usuarios (nombre, email, contrasena_hash, activo) VALUES
('Administrador', 'admin@roshka.com', '$2b$10$example_hash_admin', true);

-- Líder Técnico
INSERT INTO usuarios (nombre, email, contrasena_hash, activo) VALUES
('Juan García', 'juan.garcia@roshka.com', '$2b$10$example_hash_tech_leader', true);

-- QA
INSERT INTO usuarios (nombre, email, contrasena_hash, activo) VALUES
('María López', 'maria.lopez@roshka.com', '$2b$10$example_hash_qa', true);

-- Gerente
INSERT INTO usuarios (nombre, email, contrasena_hash, activo) VALUES
('Carlos Pérez', 'carlos.perez@roshka.com', '$2b$10$example_hash_manager', true);

-- =====================================================
-- 5. ASIGNAR ROLES A USUARIOS
-- =====================================================

INSERT INTO usuarios_roles (usuario_id, rol_id)
SELECT u.id, r.id FROM usuarios u, roles r
WHERE u.email = 'admin@roshka.com' AND r.nombre = 'ADMIN';

INSERT INTO usuarios_roles (usuario_id, rol_id)
SELECT u.id, r.id FROM usuarios u, roles r
WHERE u.email = 'juan.garcia@roshka.com' AND r.nombre = 'LIDER_TECNICO';

INSERT INTO usuarios_roles (usuario_id, rol_id)
SELECT u.id, r.id FROM usuarios u, roles r
WHERE u.email = 'maria.lopez@roshka.com' AND r.nombre = 'QA';

INSERT INTO usuarios_roles (usuario_id, rol_id)
SELECT u.id, r.id FROM usuarios u, roles r
WHERE u.email = 'carlos.perez@roshka.com' AND r.nombre = 'GERENTE';
