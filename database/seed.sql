-- =============================================================
-- Decision Support System — DSS
-- Datos iniciales (seed)
-- Ejecutar DESPUES de schema.sql
-- =============================================================


-- =============================================================
-- ROLES  (RF10 de la tesis: Administrador, Analista QA,
--          Líder Técnico, Gerente QA)
-- =============================================================
INSERT INTO roles (nombre_rol, descripcion, estado) VALUES
('Administrador', 'Acceso total al sistema. Gestiona usuarios, roles y configuración.',                                             'activo'),
('Analista QA',   'Carga reportes de prueba y consulta métricas e indicadores de sus proyectos asignados.',                        'activo'),
('Líder Técnico', 'Consulta el dashboard, revisa recomendaciones automáticas y registra la decisión formal de despliegue.',        'activo'),
('Gerente QA',    'Supervisa las métricas globales del proceso y consulta el historial de decisiones (acceso de lectura).',        'activo');


-- =============================================================
-- PERMISOS  (agrupados por módulo)
-- =============================================================
INSERT INTO permisos (nombre_permiso, descripcion, modulo) VALUES
-- Usuarios
('gestionar_usuarios',   'Crear, editar y desactivar usuarios',                    'Usuarios'),
('ver_usuarios',         'Consultar listado y detalle de usuarios',                'Usuarios'),
-- Proyectos
('gestionar_proyectos',  'Crear y editar proyectos y versiones',                   'Proyectos'),
('ver_proyectos',        'Consultar proyectos y versiones',                        'Proyectos'),
-- Pruebas
('cargar_resultados',    'Cargar archivos JSON de resultados de pruebas',          'Pruebas'),
('ver_resultados',       'Consultar resultados y métricas de calidad',             'Pruebas'),
-- Evaluación
('ejecutar_evaluacion',  'Disparar proceso de evaluación de versión',              'Evaluacion'),
('ver_evaluacion',       'Consultar evaluaciones y recomendaciones',               'Evaluacion'),
('gestionar_reglas',     'Crear y editar reglas de evaluación (umbrales)',         'Evaluacion'),
-- Decisiones
('registrar_decision',   'Registrar decisión final de despliegue',                 'Decisiones'),
('ver_decisiones',       'Consultar historial de decisiones',                      'Decisiones'),
-- Auditoría
('ver_auditoria',        'Consultar registros de auditoría del sistema',           'Auditoria');


-- =============================================================
-- ROL_PERMISO — asignación de permisos por rol
-- =============================================================

-- Administrador: todos los permisos
INSERT INTO rol_permiso (id_rol, id_permiso)
SELECT r.id_rol, p.id_permiso
FROM roles r, permisos p
WHERE r.nombre_rol = 'Administrador';

-- Líder Técnico
INSERT INTO rol_permiso (id_rol, id_permiso)
SELECT r.id_rol, p.id_permiso
FROM roles r, permisos p
WHERE r.nombre_rol = 'Líder Técnico'
  AND p.nombre_permiso IN (
    'ver_proyectos',
    'ver_resultados',
    'ver_evaluacion',
    'registrar_decision',
    'ver_decisiones',
    'ver_auditoria',
    'cargar_resultados',
    'ejecutar_evaluacion'
  );

-- Analista QA: carga resultados, consulta métricas y evaluaciones
INSERT INTO rol_permiso (id_rol, id_permiso)
SELECT r.id_rol, p.id_permiso
FROM roles r, permisos p
WHERE r.nombre_rol = 'Analista QA'
  AND p.nombre_permiso IN (
    'ver_proyectos',
    'cargar_resultados',
    'ver_resultados',
    'ejecutar_evaluacion',
    'ver_evaluacion',
    'ver_decisiones'
  );

-- Gerente QA: acceso de lectura a métricas globales e historial de decisiones
INSERT INTO rol_permiso (id_rol, id_permiso)
SELECT r.id_rol, p.id_permiso
FROM roles r, permisos p
WHERE r.nombre_rol = 'Gerente QA'
  AND p.nombre_permiso IN (
    'ver_proyectos',
    'ver_resultados',
    'ver_evaluacion',
    'ver_decisiones',
    'ver_auditoria'
  );


-- =============================================================
-- USUARIOS DE PRUEBA
-- Contraseñas hasheadas con SHA-256 (algoritmo del backend)
--
--   admin@roshka.com    →  Admin2026!
--   lider@roshka.com    →  Lider2026!
--   analista@roshka.com →  Analista2026!
--   gerente@roshka.com  →  Gerente2026!
-- =============================================================
INSERT INTO usuarios (nombre, apellido, email, password_hash, estado) VALUES
('Administrador', 'Sistema',   'admin@roshka.com',    '04445e6487736590d1ef50186b414e737e0164683cbbec64e00e73c000fd3bef', 'activo'),
('Carlos',        'Martínez',  'lider@roshka.com',    'b38d86c738c217f1f4defd35387e1f79f272ec74e69ef2ed780cee6050f68f64', 'activo'),
('Ana',           'López',     'analista@roshka.com', '025f2eb2238c0859a9abd20c0e12a5dbfc7a7fb7d26d4274a29dba85ea4bd4e3', 'activo'),
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
WHERE u.email = 'lider@roshka.com' AND r.nombre_rol = 'Líder Técnico';

INSERT INTO usuario_rol (id_usuario, id_rol)
SELECT u.id_usuario, r.id_rol
FROM usuarios u, roles r
WHERE u.email = 'analista@roshka.com' AND r.nombre_rol = 'Analista QA';

INSERT INTO usuario_rol (id_usuario, id_rol)
SELECT u.id_usuario, r.id_rol
FROM usuarios u, roles r
WHERE u.email = 'gerente@roshka.com' AND r.nombre_rol = 'Gerente QA';


-- =============================================================
-- REGLAS DE EVALUACIÓN iniciales (configurables desde el sistema)
--
-- Los criterios deben coincidir con los nombres de métricas que
-- almacena MetricsCalculationService:
--   tasa_exito       → % pruebas exitosas   (tipo: mayor_igual)
--   cobertura        → % cobertura código   (tipo: mayor_igual)
--   tasa_fallo       → % pruebas fallidas   (tipo: menor_igual)
--   tiempo_ejecucion → segundos ejecución   (tipo: menor_igual)
--
-- Umbrales alineados con casos de prueba CP-U01/CP-U04 de la tesis.
-- =============================================================
INSERT INTO reglas_evaluacion (nombre_regla, descripcion, criterio, umbral, estado, id_usuario_creacion)
SELECT
    r.nombre_regla, r.descripcion, r.criterio, r.umbral, 'activo', u.id_usuario
FROM (VALUES
    ('Tasa de éxito mínima',
     'El porcentaje de pruebas exitosas debe ser mayor o igual al umbral configurado.',
     'tasa_exito',        90.00),
    ('Cobertura de código mínima',
     'La cobertura de código de la suite de pruebas debe alcanzar el umbral mínimo.',
     'cobertura',         80.00),
    ('Tasa de fallo máxima',
     'El porcentaje de pruebas fallidas no debe superar el umbral configurado.',
     'tasa_fallo',        10.00),
    ('Tiempo de ejecución máximo',
     'El tiempo total de ejecución del pipeline no debe exceder el umbral en segundos.',
     'tiempo_ejecucion', 120.00)
) AS r(nombre_regla, descripcion, criterio, umbral)
CROSS JOIN (SELECT id_usuario FROM usuarios WHERE email = 'admin@roshka.com') u;
