-- =============================================================
-- suscripcion_schema.sql
-- Módulo de suscripciones SaaS del sistema SAD-Roshka.
-- Ejecutar DESPUES de schema.sql
-- Modelo de negocio: Tabla 13 y 14 de la tesis (Lean Canvas)
-- =============================================================

-- ── 1. PLANES DE SUSCRIPCIÓN ──────────────────────────────────────────────
--    Tabla 14 de la tesis: Básico / Profesional / Empresarial
CREATE TABLE IF NOT EXISTS planes_suscripcion (
    id                      SERIAL          PRIMARY KEY,
    nombre                  VARCHAR(50)     NOT NULL UNIQUE,
    precio_mensual          DECIMAL(15,2)   NOT NULL,           -- en guaraníes
    -- Límites cuantitativos
    max_proyectos           INTEGER,                            -- NULL = ilimitado
    max_usuarios            INTEGER,                            -- NULL = ilimitado
    max_evaluaciones_mes    INTEGER,                            -- NULL = ilimitado
    max_tamano_archivo_mb   INTEGER,
    -- Historial
    historial_dias          INTEGER,                            -- NULL = completo
    -- Funcionalidades de reporting
    exportar_pdf            BOOLEAN         NOT NULL DEFAULT FALSE,
    exportar_excel          BOOLEAN         NOT NULL DEFAULT FALSE,
    dashboard_avanzado      BOOLEAN         NOT NULL DEFAULT FALSE,
    auditoria_detallada     BOOLEAN         NOT NULL DEFAULT FALSE,
    -- Notificaciones e integraciones
    notificaciones_email    BOOLEAN         NOT NULL DEFAULT FALSE,
    notificaciones_slack    BOOLEAN         NOT NULL DEFAULT FALSE,
    api_publica             BOOLEAN         NOT NULL DEFAULT FALSE,
    integracion_cicd        BOOLEAN         NOT NULL DEFAULT FALSE,
    webhooks                BOOLEAN         NOT NULL DEFAULT FALSE,
    -- Soporte
    soporte_prioritario     BOOLEAN         NOT NULL DEFAULT FALSE,
    -- Meta
    estado                  VARCHAR(20)     NOT NULL DEFAULT 'activo',
    fecha_creacion          TIMESTAMP       NOT NULL DEFAULT NOW()
);

-- ── 2. SUSCRIPCIÓN ACTIVA ─────────────────────────────────────────────────
--    Una sola suscripción activa por despliegue del sistema.
--    El Administrador gestiona el plan contratado.
CREATE TABLE IF NOT EXISTS suscripciones (
    id                      SERIAL          PRIMARY KEY,
    id_plan                 INTEGER         NOT NULL REFERENCES planes_suscripcion(id),
    estado                  VARCHAR(20)     NOT NULL DEFAULT 'pendiente',
    --   pendiente  → pago iniciado, aún no confirmado
    --   activa     → pago confirmado, acceso habilitado
    --   vencida    → período vencido sin renovación
    --   cancelada  → cancelada manualmente
    fecha_inicio            TIMESTAMP,
    fecha_vencimiento       TIMESTAMP,
    fecha_creacion          TIMESTAMP       NOT NULL DEFAULT NOW(),
    -- Datos de tarjeta catastrada en Pagopar (pagos recurrentes)
    pagopar_id_cliente      INTEGER,        -- identificador enviado a Pagopar
    pagopar_id_tarjeta      VARCHAR(20)     -- id numérico de tarjeta (resultado.tarjeta)
);

-- ── 3. HISTORIAL DE PAGOS ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS pagos_suscripcion (
    id                      SERIAL          PRIMARY KEY,
    id_suscripcion          INTEGER         NOT NULL REFERENCES suscripciones(id),
    id_plan                 INTEGER         NOT NULL REFERENCES planes_suscripcion(id),
    monto                   DECIMAL(15,2)   NOT NULL,
    estado                  VARCHAR(20)     NOT NULL DEFAULT 'pendiente',
    --   pendiente  → pedido creado en Pagopar
    --   aprobado   → pago confirmado por webhook
    --   rechazado  → pago rechazado
    --   revertido  → pago reversado por Pagopar
    -- Datos Pagopar checkout
    pagopar_id_pedido_comercio  VARCHAR(100),   -- nuestro ID único de pedido
    pagopar_hash_pedido         VARCHAR(255),   -- hash retornado por Pagopar
    pagopar_numero_pedido       VARCHAR(100),   -- número pedido Pagopar
    pagopar_respuesta           TEXT,           -- JSON completo de respuesta/webhook
    fecha_pago              TIMESTAMP,
    fecha_creacion          TIMESTAMP       NOT NULL DEFAULT NOW()
);

-- ── Índices ───────────────────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_suscripciones_estado
    ON suscripciones(estado);

CREATE INDEX IF NOT EXISTS idx_pagos_suscripcion_suscripcion
    ON pagos_suscripcion(id_suscripcion);

CREATE INDEX IF NOT EXISTS idx_pagos_suscripcion_hash
    ON pagos_suscripcion(pagopar_hash_pedido);
