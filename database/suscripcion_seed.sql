-- =============================================================
-- suscripcion_seed.sql
-- Datos iniciales: 3 planes comerciales del sistema SAD-Roshka.
-- Ejecutar DESPUES de suscripcion_schema.sql
-- Fuente: Tabla 14 de la tesis (Planes comerciales SAD-Roshka)
-- =============================================================

INSERT INTO planes_suscripcion (
    nombre, precio_mensual,
    max_proyectos, max_usuarios, max_evaluaciones_mes, max_tamano_archivo_mb,
    historial_dias,
    exportar_pdf, exportar_excel, dashboard_avanzado, auditoria_detallada,
    notificaciones_email, notificaciones_slack,
    api_publica, integracion_cicd, webhooks,
    soporte_prioritario, estado
) VALUES
-- ── Plan Básico ──────────────────────────────────────────────────────────
(
    'Básico', 400000.00,
    1, 3, 100, 5,
    30,
    FALSE, FALSE, FALSE, FALSE,
    FALSE, FALSE,
    FALSE, FALSE, FALSE,
    FALSE, 'activo'
),
-- ── Plan Profesional ─────────────────────────────────────────────────────
(
    'Profesional', 1000000.00,
    5, 10, 500, 20,
    NULL,           -- historial completo
    TRUE, FALSE, TRUE, FALSE,
    TRUE, FALSE,
    FALSE, FALSE, FALSE,
    FALSE, 'activo'
),
-- ── Plan Empresarial ─────────────────────────────────────────────────────
(
    'Empresarial', 2500000.00,
    NULL, NULL, NULL, 100,    -- sin límites
    NULL,           -- historial completo
    TRUE, TRUE, TRUE, TRUE,
    TRUE, TRUE,
    TRUE, TRUE, TRUE,
    TRUE, 'activo'
)
ON CONFLICT (nombre) DO NOTHING;

-- ── Suscripción inicial (Plan Básico, estado activo para dev/staging) ────
-- Comentar en producción real; el Admin debe pagar desde la UI.
INSERT INTO suscripciones (id_plan, estado, fecha_inicio, fecha_vencimiento)
SELECT
    p.id,
    'activa',
    NOW(),
    NOW() + INTERVAL '30 days'
FROM planes_suscripcion p
WHERE p.nombre = 'Básico'
AND NOT EXISTS (SELECT 1 FROM suscripciones WHERE estado = 'activa')
LIMIT 1;
