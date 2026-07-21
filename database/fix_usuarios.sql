-- =============================================================
-- fix_usuarios.sql
-- Alinea roles y usuarios de la BD a lo definido en la tesis.
--
-- Roles válidos (RF10): Administrador | Analista QA |
--                        Líder Técnico | Gerente QA
--
-- Ejecutar en pgAdmin sobre la base decision_support_db.
-- =============================================================

-- ── 1. Eliminar todos los usuarios no-admin y sus asignaciones ─────────────
DELETE FROM usuario_rol
WHERE id_usuario IN (
    SELECT id_usuario FROM usuarios
    WHERE email <> 'admin@roshka.com'
);

DELETE FROM usuarios
WHERE email <> 'admin@roshka.com';

-- ── 2. Eliminar rol incorrecto "Desarrollador" ────────────────────────────
DELETE FROM rol_permiso
WHERE id_rol IN (SELECT id_rol FROM roles WHERE nombre_rol = 'Desarrollador');

DELETE FROM roles WHERE nombre_rol = 'Desarrollador';

-- ── 3. Insertar roles correctos si no existen ─────────────────────────────
INSERT INTO roles (nombre_rol, descripcion, estado)
VALUES
    ('Analista QA',
     'Carga reportes de prueba y consulta métricas e indicadores de sus proyectos asignados.',
     'activo'),
    ('Gerente QA',
     'Supervisa las métricas globales del proceso y consulta el historial de decisiones (acceso de lectura).',
     'activo')
ON CONFLICT (nombre_rol) DO NOTHING;

-- Asignar permisos a Analista QA
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
  )
ON CONFLICT (id_rol, id_permiso) DO NOTHING;

-- Asignar permisos a Gerente QA
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
  )
ON CONFLICT (id_rol, id_permiso) DO NOTHING;

-- ── 4. Insertar usuarios correctos según la tesis ────────────────────────
--    Contraseñas (SHA-256):
--      lider@roshka.com    →  Lider2026!
--      analista@roshka.com →  Analista2026!
--      gerente@roshka.com  →  Gerente2026!

INSERT INTO usuarios (nombre, apellido, email, password_hash, estado, fecha_creacion)
VALUES
    ('Carlos',  'Martínez', 'lider@roshka.com',
     'b38d86c738c217f1f4defd35387e1f79f272ec74e69ef2ed780cee6050f68f64',
     'activo', NOW()),
    ('Ana',     'López',    'analista@roshka.com',
     '025f2eb2238c0859a9abd20c0e12a5dbfc7a7fb7d26d4274a29dba85ea4bd4e3',
     'activo', NOW()),
    ('María',   'González', 'gerente@roshka.com',
     'bfc703cf6551ccc8f506a64522af86ce8c691c0cec46814663ca9f7f41f14443',
     'activo', NOW());

-- ── 5. Asignar roles a los nuevos usuarios ───────────────────────────────
INSERT INTO usuario_rol (id_usuario, id_rol, estado, fecha_asignacion)
SELECT u.id_usuario, r.id_rol, 'activo', NOW()
FROM usuarios u, roles r
WHERE u.email = 'lider@roshka.com' AND r.nombre_rol = 'Líder Técnico';

INSERT INTO usuario_rol (id_usuario, id_rol, estado, fecha_asignacion)
SELECT u.id_usuario, r.id_rol, 'activo', NOW()
FROM usuarios u, roles r
WHERE u.email = 'analista@roshka.com' AND r.nombre_rol = 'Analista QA';

INSERT INTO usuario_rol (id_usuario, id_rol, estado, fecha_asignacion)
SELECT u.id_usuario, r.id_rol, 'activo', NOW()
FROM usuarios u, roles r
WHERE u.email = 'gerente@roshka.com' AND r.nombre_rol = 'Gerente QA';

-- ── 6. Verificar resultado ───────────────────────────────────────────────
SELECT u.id_usuario, u.nombre, u.apellido, u.email, u.estado, r.nombre_rol AS rol
FROM usuarios u
LEFT JOIN usuario_rol ur ON u.id_usuario = ur.id_usuario AND ur.estado = 'activo'
LEFT JOIN roles r ON ur.id_rol = r.id_rol
ORDER BY u.id_usuario;
