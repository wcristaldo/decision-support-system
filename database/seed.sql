-- =============================================================
-- Decision Support System — DSS
-- Datos iniciales (seed)
-- Ejecutar DESPUES de schema.sql
-- =============================================================


-- =============================================================
-- ROLES
-- =============================================================
INSERT INTO roles (nombre_rol, descripcion, estado) VALUES
('Administrador',   'Acceso total al sistema. Gestiona usuarios, roles y configuración.',        'activo'),
('Analista',        'Carga resultados de pruebas y consulta evaluaciones.',                      'activo'),
('Gerente',         'Visualiza dashboards, consulta recomendaciones y registra decisiones.',     'activo'),
('Visualizador',    'Acceso de solo lectura a proyectos y evaluaciones.',                        'activo');


-- =============================================================
-- PERMISOS  (agrupados por módulo)
-- =============================================================
INSERT INTO permisos (nombre_permiso, descripcion, modulo) VALUES
-- Usuarios
('gestionar_usuarios',      'Crear, editar y desactivar usuarios',              'Usuarios'),
('ver_usuarios',            'Consultar listado y detalle de usuarios',          'Usuarios'),
-- Proyectos
('gestionar_proyectos',     'Crear y editar proyectos',                         'Proyectos'),
('ver_proyectos',           'Consultar proyectos y versiones',                  'Proyectos'),
-- Pruebas
('cargar_resultados',       'Cargar archivos JSON de resultados de pruebas',    'Pruebas'),
('ver_resultados',          'Consultar resultados y métricas',                  'Pruebas'),
-- Evaluación
('ejecutar_evaluacion',     'Disparar proceso de evaluación de versión',        'Evaluacion'),
('ver_evaluacion',          'Consultar evaluaciones y recomendaciones',         'Evaluacion'),
('gestionar_reglas',        'Crear y editar reglas de evaluación',              'Evaluacion'),
-- Decisiones
('registrar_decision',      'Registrar decisión final de despliegue',           'Decisiones'),
('ver_decisiones',          'Consultar historial de decisiones',                'Decisiones'),
-- Auditoría
('ver_auditoria',           'Consultar registros de auditoría',                 'Auditoria');


-- =============================================================
-- ROL_PERMISO — asignación de permisos por rol
-- =============================================================

-- Administrador: todos los permisos
INSERT INTO rol_permiso (id_rol, id_permiso)
SELECT r.id_rol, p.id_permiso
FROM roles r, permisos p
WHERE r.nombre_rol = 'Administrador';

-- Analista
INSERT INTO rol_permiso (id_rol, id_permiso)
SELECT r.id_rol, p.id_permiso
FROM roles r, permisos p
WHERE r.nombre_rol = 'Analista'
  AND p.nombre_permiso IN (
    'ver_proyectos',
    'cargar_resultados',
    'ver_resultados',
    'ejecutar_evaluacion',
    'ver_evaluacion',
    'ver_decisiones'
  );

-- Gerente
INSERT INTO rol_permiso (id_rol, id_permiso)
SELECT r.id_rol, p.id_permiso
FROM roles r, permisos p
WHERE r.nombre_rol = 'Gerente'
  AND p.nombre_permiso IN (
    'ver_proyectos',
    'ver_resultados',
    'ver_evaluacion',
    'registrar_decision',
    'ver_decisiones'
  );

-- Visualizador
INSERT INTO rol_permiso (id_rol, id_permiso)
SELECT r.id_rol, p.id_permiso
FROM roles r, permisos p
WHERE r.nombre_rol = 'Visualizador'
  AND p.nombre_permiso IN (
    'ver_proyectos',
    'ver_resultados',
    'ver_evaluacion',
    'ver_decisiones'
  );


-- =============================================================
-- USUARIOS DE PRUEBA
-- Contraseñas hasheadas con SHA-256 (mismo algoritmo del backend)
--
--   admin@roshka.com     →  Admin2026!
--   analista@roshka.com  →  Analista2026!
--   gerente@roshka.com   →  Gerente2026!
-- =============================================================
INSERT INTO usuarios (nombre, apellido, email, password_hash, estado) VALUES
('Administrador', 'Sistema',   'admin@roshka.com',    '04445e6487736590d1ef50186b414e737e0164683cbbec64e00e73c000fd3bef', 'activo'),
('Carlos',        'López',     'analista@roshka.com', '025f2eb2238c0859a9abd20c0e12a5dbfc7a7fb7d26d4274a29dba85ea4bd4e3', 'activo'),
('María',         'González',  'gerente@roshka.com',  'bfc703cf6551ccc8f506a64522af86ce8c691c0cec46814663ca9f7f41f14443', 'activo');


-- =============================================================
-- USUARIO_ROL — asignación de roles a usuarios
-- =============================================================
INSERT INTO usuario_rol (id_usuario, id_rol)
SELECT u.id_usuario, r.id_rol
FROM usuarios u, roles r
WHERE u.email = 'admin@roshka.com' AND r.nombre_rol = 'Administrador';

INSERT INTO usuario_rol (id_usuario, id_rol)
SELECT u.id_usuario, r.id_rol
FROM usuarios u, roles r
WHERE u.email = 'analista@roshka.com' AND r.nombre_rol = 'Analista';

INSERT INTO usuario_rol (id_usuario, id_rol)
SELECT u.id_usuario, r.id_rol
FROM usuarios u, roles r
WHERE u.email = 'gerente@roshka.com' AND r.nombre_rol = 'Gerente';


-- =============================================================
-- REGLAS DE EVALUACION iniciales (configurables desde el sistema)
-- =============================================================
INSERT INTO reglas_evaluacion (nombre_regla, descripcion, criterio, umbral, estado, id_usuario_creacion)
SELECT
    r.nombre_regla, r.descripcion, r.criterio, r.umbral, 'activo', u.id_usuario
FROM (VALUES
    ('Tasa de éxito mínima',     'El porcentaje de pruebas exitosas debe superar el umbral',   'porcentaje_exito',    80.00),
    ('Errores críticos cero',    'No se permiten errores de severidad crítica',                 'errores_criticos',     0.00),
    ('Tiempo de respuesta',      'El tiempo promedio de respuesta no debe superar el umbral',   'tiempo_respuesta_ms', 3000.00),
    ('Cobertura mínima',         'La cobertura de pruebas debe alcanzar el umbral definido',    'cobertura_porcentaje', 70.00),
    ('Pruebas fallidas máximas', 'El porcentaje de pruebas fallidas no debe superar el umbral', 'porcentaje_fallo',    20.00)
) AS r(nombre_regla, descripcion, criterio, umbral)
CROSS JOIN (SELECT id_usuario FROM usuarios WHERE email = 'admin@roshka.com') u;
