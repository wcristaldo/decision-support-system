-- Crear base de datos
CREATE DATABASE "DecisionSupport" ENCODING 'UTF8';

-- Conectar a la base de datos
\c "DecisionSupport";

-- Tabla de usuarios
CREATE TABLE usuarios (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    email VARCHAR(100) UNIQUE NOT NULL,
    contrasena_hash VARCHAR(255) NOT NULL,
    activo BOOLEAN DEFAULT true,
    fecha_creacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    fecha_actualizacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabla de roles
CREATE TABLE roles (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(50) UNIQUE NOT NULL,
    descripcion TEXT,
    fecha_creacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabla de relación usuarios-roles
CREATE TABLE usuarios_roles (
    usuario_id INTEGER NOT NULL,
    rol_id INTEGER NOT NULL,
    PRIMARY KEY (usuario_id, rol_id),
    FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE CASCADE,
    FOREIGN KEY (rol_id) REFERENCES roles(id) ON DELETE CASCADE
);

-- Tabla de permisos
CREATE TABLE permisos (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(100) UNIQUE NOT NULL,
    descripcion TEXT,
    fecha_creacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabla de relación roles-permisos
CREATE TABLE roles_permisos (
    rol_id INTEGER NOT NULL,
    permiso_id INTEGER NOT NULL,
    PRIMARY KEY (rol_id, permiso_id),
    FOREIGN KEY (rol_id) REFERENCES roles(id) ON DELETE CASCADE,
    FOREIGN KEY (permiso_id) REFERENCES permisos(id) ON DELETE CASCADE
);

-- Tabla de proyectos
CREATE TABLE proyectos (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(200) NOT NULL,
    descripcion TEXT,
    estado VARCHAR(50) DEFAULT 'activo',
    fecha_creacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    fecha_actualizacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabla de versiones
CREATE TABLE versiones (
    id SERIAL PRIMARY KEY,
    proyecto_id INTEGER NOT NULL,
    numero_version VARCHAR(50) NOT NULL,
    descripcion TEXT,
    fecha_creacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (proyecto_id) REFERENCES proyectos(id) ON DELETE CASCADE,
    UNIQUE(proyecto_id, numero_version)
);

-- Tabla de resultados de prueba
CREATE TABLE resultados_prueba (
    id SERIAL PRIMARY KEY,
    version_id INTEGER NOT NULL,
    nombre_prueba VARCHAR(200) NOT NULL,
    tipo_prueba VARCHAR(50),
    estado VARCHAR(50),
    tiempo_ejecucion DECIMAL(10, 2),
    fecha_ejecucion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (version_id) REFERENCES versiones(id) ON DELETE CASCADE
);

-- Tabla de métricas
CREATE TABLE metricas (
    id SERIAL PRIMARY KEY,
    version_id INTEGER NOT NULL,
    nombre_metrica VARCHAR(100) NOT NULL,
    valor DECIMAL(10, 4),
    unidad VARCHAR(50),
    fecha_calculo TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (version_id) REFERENCES versiones(id) ON DELETE CASCADE
);

-- Tabla de evaluaciones
CREATE TABLE evaluaciones (
    id SERIAL PRIMARY KEY,
    version_id INTEGER NOT NULL,
    puntuacion_calidad DECIMAL(5, 2),
    puntuacion_riesgo DECIMAL(5, 2),
    observaciones TEXT,
    fecha_evaluacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (version_id) REFERENCES versiones(id) ON DELETE CASCADE
);

-- Tabla de reglas de evaluación
CREATE TABLE reglas_evaluacion (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    descripcion TEXT,
    condicion_json JSONB,
    activa BOOLEAN DEFAULT true,
    fecha_creacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabla de recomendaciones
CREATE TABLE recomendaciones (
    id SERIAL PRIMARY KEY,
    version_id INTEGER NOT NULL,
    tipo_recomendacion VARCHAR(100),
    descripcion TEXT,
    confianza DECIMAL(5, 2),
    fecha_generacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (version_id) REFERENCES versiones(id) ON DELETE CASCADE
);

-- Tabla de decisiones de despliegue
CREATE TABLE decisiones_despliegue (
    id SERIAL PRIMARY KEY,
    version_id INTEGER NOT NULL,
    usuario_id INTEGER NOT NULL,
    decision VARCHAR(50),
    justificacion TEXT,
    fecha_decision TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (version_id) REFERENCES versiones(id) ON DELETE CASCADE,
    FOREIGN KEY (usuario_id) REFERENCES usuarios(id)
);

-- Tabla de auditoría
CREATE TABLE auditoria (
    id SERIAL PRIMARY KEY,
    usuario_id INTEGER,
    entidad VARCHAR(50),
    accion VARCHAR(50),
    datos_anteriores JSONB,
    datos_nuevos JSONB,
    fecha_evento TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE SET NULL
);

-- Crear índices
CREATE INDEX idx_usuarios_email ON usuarios(email);
CREATE INDEX idx_versiones_proyecto ON versiones(proyecto_id);
CREATE INDEX idx_resultados_version ON resultados_prueba(version_id);
CREATE INDEX idx_metricas_version ON metricas(version_id);
CREATE INDEX idx_evaluaciones_version ON evaluaciones(version_id);
CREATE INDEX idx_recomendaciones_version ON recomendaciones(version_id);
CREATE INDEX idx_decisiones_version ON decisiones_despliegue(version_id);
CREATE INDEX idx_auditoria_usuario ON auditoria(usuario_id);
CREATE INDEX idx_auditoria_fecha ON auditoria(fecha_evento);

-- Insertar roles iniciales
INSERT INTO roles (nombre, descripcion) VALUES
('Administrador', 'Acceso total al sistema'),
('Analista', 'Puede analizar resultados y generar recomendaciones'),
('Gerente', 'Puede tomar decisiones de despliegue'),
('Visualizador', 'Acceso de solo lectura');

-- Insertar permisos iniciales
INSERT INTO permisos (nombre, descripcion) VALUES
('crear_proyecto', 'Crear nuevos proyectos'),
('editar_proyecto', 'Editar proyectos existentes'),
('eliminar_proyecto', 'Eliminar proyectos'),
('cargar_pruebas', 'Cargar resultados de pruebas'),
('generar_recomendaciones', 'Generar recomendaciones automáticas'),
('tomar_decision_despliegue', 'Tomar decisiones sobre despliegues'),
('ver_auditoria', 'Ver registro de auditoría'),
('gestionar_usuarios', 'Crear y modificar usuarios');

-- Asignar permisos a roles
INSERT INTO roles_permisos (rol_id, permiso_id) SELECT r.id, p.id FROM roles r, permisos p WHERE r.nombre = 'Administrador';
INSERT INTO roles_permisos (rol_id, permiso_id)
SELECT r.id, p.id FROM roles r, permisos p
WHERE r.nombre = 'Analista' AND p.nombre IN ('cargar_pruebas', 'generar_recomendaciones');
INSERT INTO roles_permisos (rol_id, permiso_id)
SELECT r.id, p.id FROM roles r, permisos p
WHERE r.nombre = 'Gerente' AND p.nombre IN ('generar_recomendaciones', 'tomar_decision_despliegue', 'ver_auditoria');
