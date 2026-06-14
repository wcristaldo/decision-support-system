-- =============================================================
-- Decision Support System — DSS
-- Schema basado en DER del anteproyecto (UNIDA, 2026)
-- PostgreSQL 15+
-- =============================================================

-- -------------------------------------------------------------
-- DROP: en orden inverso de dependencias
-- -------------------------------------------------------------
DROP TABLE IF EXISTS auditoria              CASCADE;
DROP TABLE IF EXISTS decisiones_despliegue  CASCADE;
DROP TABLE IF EXISTS evaluacion_regla       CASCADE;
DROP TABLE IF EXISTS recomendaciones        CASCADE;
DROP TABLE IF EXISTS evaluaciones           CASCADE;
DROP TABLE IF EXISTS metricas               CASCADE;
DROP TABLE IF EXISTS resultados_prueba      CASCADE;
DROP TABLE IF EXISTS reglas_evaluacion      CASCADE;
DROP TABLE IF EXISTS versiones              CASCADE;
DROP TABLE IF EXISTS proyectos              CASCADE;
DROP TABLE IF EXISTS usuario_rol            CASCADE;
DROP TABLE IF EXISTS rol_permiso            CASCADE;
DROP TABLE IF EXISTS permisos               CASCADE;
DROP TABLE IF EXISTS roles                  CASCADE;
DROP TABLE IF EXISTS usuarios               CASCADE;


-- =============================================================
-- 1. USUARIOS
-- =============================================================
CREATE TABLE usuarios (
    id_usuario          SERIAL          PRIMARY KEY,
    nombre              VARCHAR(100)    NOT NULL,
    apellido            VARCHAR(100),
    email               VARCHAR(150)    UNIQUE NOT NULL,
    password_hash       VARCHAR(255)    NOT NULL,
    estado              VARCHAR(20)     NOT NULL DEFAULT 'activo'
                            CHECK (estado IN ('activo','inactivo')),
    fecha_creacion      TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_ultimo_acceso TIMESTAMP
);


-- =============================================================
-- 2. ROLES
-- =============================================================
CREATE TABLE roles (
    id_rol      SERIAL       PRIMARY KEY,
    nombre_rol  VARCHAR(50)  UNIQUE NOT NULL,
    descripcion VARCHAR(255),
    estado      VARCHAR(20)  NOT NULL DEFAULT 'activo'
                    CHECK (estado IN ('activo','inactivo'))
);


-- =============================================================
-- 3. PERMISOS
-- =============================================================
CREATE TABLE permisos (
    id_permiso      SERIAL       PRIMARY KEY,
    nombre_permiso  VARCHAR(100) UNIQUE NOT NULL,
    descripcion     VARCHAR(255),
    modulo          VARCHAR(100)
);


-- =============================================================
-- 4. ROL_PERMISO  (roles <-> permisos)
-- =============================================================
CREATE TABLE rol_permiso (
    id_rol_permiso  SERIAL  PRIMARY KEY,
    id_rol          INTEGER NOT NULL REFERENCES roles(id_rol)        ON DELETE CASCADE,
    id_permiso      INTEGER NOT NULL REFERENCES permisos(id_permiso) ON DELETE CASCADE,
    UNIQUE (id_rol, id_permiso)
);


-- =============================================================
-- 5. USUARIO_ROL  (usuarios <-> roles)
-- =============================================================
CREATE TABLE usuario_rol (
    id_usuario_rol   SERIAL      PRIMARY KEY,
    id_usuario       INTEGER     NOT NULL REFERENCES usuarios(id_usuario) ON DELETE CASCADE,
    id_rol           INTEGER     NOT NULL REFERENCES roles(id_rol)        ON DELETE CASCADE,
    fecha_asignacion TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    estado           VARCHAR(20) NOT NULL DEFAULT 'activo'
                         CHECK (estado IN ('activo','inactivo')),
    UNIQUE (id_usuario, id_rol)
);


-- =============================================================
-- 6. PROYECTOS
-- =============================================================
CREATE TABLE proyectos (
    id_proyecto     SERIAL       PRIMARY KEY,
    nombre_proyecto VARCHAR(150) NOT NULL,
    descripcion     VARCHAR(255),
    tipo_solucion   VARCHAR(100),
    estado          VARCHAR(20)  NOT NULL DEFAULT 'activo'
                        CHECK (estado IN ('activo','inactivo','archivado')),
    fecha_creacion  TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP
);


-- =============================================================
-- 7. VERSIONES
-- =============================================================
CREATE TABLE versiones (
    id_version     SERIAL      PRIMARY KEY,
    id_proyecto    INTEGER     NOT NULL REFERENCES proyectos(id_proyecto) ON DELETE CASCADE,
    nombre_version VARCHAR(50) NOT NULL,
    descripcion    VARCHAR(255),
    fecha_version  TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    estado_version VARCHAR(20) NOT NULL DEFAULT 'pendiente'
                       CHECK (estado_version IN ('pendiente','en_evaluacion','aprobada','rechazada','desplegada'))
);


-- =============================================================
-- 8. REGLAS_EVALUACION
-- =============================================================
CREATE TABLE reglas_evaluacion (
    id_regla            SERIAL        PRIMARY KEY,
    nombre_regla        VARCHAR(100)  NOT NULL,
    descripcion         VARCHAR(255),
    criterio            VARCHAR(255)  NOT NULL,
    umbral              DECIMAL(10,2),
    estado              VARCHAR(20)   NOT NULL DEFAULT 'activo'
                            CHECK (estado IN ('activo','inactivo')),
    fecha_creacion      TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    id_usuario_creacion INTEGER       REFERENCES usuarios(id_usuario) ON DELETE SET NULL
);


-- =============================================================
-- 9. RESULTADOS_PRUEBA
-- =============================================================
CREATE TABLE resultados_prueba (
    id_resultado      SERIAL       PRIMARY KEY,
    id_version        INTEGER      NOT NULL REFERENCES versiones(id_version)  ON DELETE CASCADE,
    id_usuario_carga  INTEGER      REFERENCES usuarios(id_usuario)            ON DELETE SET NULL,
    nombre_archivo    VARCHAR(255) NOT NULL,
    formato_archivo   VARCHAR(20)  NOT NULL DEFAULT 'json',
    ruta_archivo      VARCHAR(255),
    fecha_carga       TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    estado_validacion VARCHAR(30)  NOT NULL DEFAULT 'pendiente'
                          CHECK (estado_validacion IN ('pendiente','valido','invalido')),
    observaciones     VARCHAR(255)
);


-- =============================================================
-- 10. METRICAS
-- =============================================================
CREATE TABLE metricas (
    id_metrica     SERIAL        PRIMARY KEY,
    id_resultado   INTEGER       NOT NULL REFERENCES resultados_prueba(id_resultado) ON DELETE CASCADE,
    nombre_metrica VARCHAR(100)  NOT NULL,
    valor_metrica  DECIMAL(10,2),
    unidad         VARCHAR(50),
    fecha_calculo  TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP
);


-- =============================================================
-- 11. EVALUACIONES
-- =============================================================
CREATE TABLE evaluaciones (
    id_evaluacion      SERIAL       PRIMARY KEY,
    id_resultado       INTEGER      NOT NULL REFERENCES resultados_prueba(id_resultado) ON DELETE CASCADE,
    fecha_evaluacion   TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    estado_evaluacion  VARCHAR(30)  NOT NULL DEFAULT 'en_proceso'
                           CHECK (estado_evaluacion IN ('en_proceso','completada','fallida')),
    resumen_evaluacion VARCHAR(255)
);


-- =============================================================
-- 12. EVALUACION_REGLA  (evaluaciones <-> reglas)
-- =============================================================
CREATE TABLE evaluacion_regla (
    id_evaluacion_regla SERIAL       PRIMARY KEY,
    id_evaluacion       INTEGER      NOT NULL REFERENCES evaluaciones(id_evaluacion)       ON DELETE CASCADE,
    id_regla            INTEGER      NOT NULL REFERENCES reglas_evaluacion(id_regla)       ON DELETE CASCADE,
    resultado_regla     VARCHAR(30)  CHECK (resultado_regla IN ('cumple','no_cumple','no_aplica')),
    observacion         VARCHAR(255)
);


-- =============================================================
-- 13. RECOMENDACIONES
-- =============================================================
CREATE TABLE recomendaciones (
    id_recomendacion   SERIAL       PRIMARY KEY,
    id_evaluacion      INTEGER      NOT NULL REFERENCES evaluaciones(id_evaluacion) ON DELETE CASCADE,
    tipo_recomendacion VARCHAR(30)  NOT NULL
                           CHECK (tipo_recomendacion IN ('desplegar','no_desplegar','desplegar_con_observaciones')),
    justificacion      VARCHAR(255),
    fecha_generacion   TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP
);


-- =============================================================
-- 14. DECISIONES_DESPLIEGUE
-- =============================================================
CREATE TABLE decisiones_despliegue (
    id_decision        SERIAL       PRIMARY KEY,
    id_recomendacion   INTEGER      REFERENCES recomendaciones(id_recomendacion) ON DELETE SET NULL,
    id_usuario_decisor INTEGER      REFERENCES usuarios(id_usuario)              ON DELETE SET NULL,
    decision_final     VARCHAR(30)  NOT NULL
                           CHECK (decision_final IN ('aprobado','rechazado','postergado')),
    comentario         VARCHAR(255),
    fecha_decision     TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP
);


-- =============================================================
-- 15. AUDITORIA
-- =============================================================
CREATE TABLE auditoria (
    id_auditoria         SERIAL       PRIMARY KEY,
    id_usuario           INTEGER      REFERENCES usuarios(id_usuario) ON DELETE SET NULL,
    entidad_afectada     VARCHAR(100),
    id_registro_afectado INTEGER,
    accion               VARCHAR(50)  NOT NULL,
    detalle              VARCHAR(255),
    fecha_evento         TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ip_origen            VARCHAR(45)
);


-- =============================================================
-- INDICES para rendimiento
-- =============================================================
CREATE INDEX idx_usuario_rol_usuario  ON usuario_rol(id_usuario);
CREATE INDEX idx_usuario_rol_rol      ON usuario_rol(id_rol);
CREATE INDEX idx_versiones_proyecto   ON versiones(id_proyecto);
CREATE INDEX idx_resultados_version   ON resultados_prueba(id_version);
CREATE INDEX idx_metricas_resultado   ON metricas(id_resultado);
CREATE INDEX idx_evaluaciones_result  ON evaluaciones(id_resultado);
CREATE INDEX idx_recomend_evaluacion  ON recomendaciones(id_evaluacion);
CREATE INDEX idx_decisiones_recomend  ON decisiones_despliegue(id_recomendacion);
CREATE INDEX idx_auditoria_usuario    ON auditoria(id_usuario);
CREATE INDEX idx_auditoria_entidad    ON auditoria(entidad_afectada, id_registro_afectado);
