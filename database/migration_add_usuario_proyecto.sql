-- RF13: asociación usuario↔proyecto con restricción real de acceso.
-- Antes del alcance de esta migración, el acceso era 100% por rol (RF14),
-- sin ninguna restricción por proyecto — cualquier usuario con "ver_proyectos"
-- veía TODOS los proyectos del sistema. Esta tabla agrega esa segunda capa:
-- Administrador sigue viendo todo (sin fila en esta tabla = sin restricción,
-- ver DbClaimsTransformation.cs); el resto de los roles solo ve los proyectos
-- a los que fue asignado explícitamente.

CREATE TABLE usuario_proyecto (
    id_usuario_proyecto SERIAL      PRIMARY KEY,
    id_usuario          INTEGER     NOT NULL REFERENCES usuarios(id_usuario)   ON DELETE CASCADE,
    id_proyecto         INTEGER     NOT NULL REFERENCES proyectos(id_proyecto) ON DELETE CASCADE,
    fecha_asignacion    TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (id_usuario, id_proyecto)
);

-- Preserva el comportamiento actual del sistema: asocia a TODOS los usuarios
-- no-Administrador ya existentes con TODOS los proyectos ya existentes, para
-- no romper las cuentas de prueba sembradas en seed.sql ni los datos ya
-- cargados. De acá en más, un proyecto o usuario nuevo requiere asignación
-- explícita desde Gestión de Usuarios.
INSERT INTO usuario_proyecto (id_usuario, id_proyecto)
SELECT DISTINCT u.id_usuario, p.id_proyecto
FROM usuarios u
CROSS JOIN proyectos p
JOIN usuario_rol ur ON ur.id_usuario = u.id_usuario AND ur.estado = 'activo'
JOIN roles r ON r.id_rol = ur.id_rol AND r.nombre_rol <> 'Administrador'
ON CONFLICT (id_usuario, id_proyecto) DO NOTHING;
