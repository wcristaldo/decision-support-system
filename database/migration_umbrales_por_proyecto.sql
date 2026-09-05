-- =============================================================
-- migration_umbrales_por_proyecto.sql
-- RF07/RF08/CU-03 de la tesis: umbrales de calidad configurables
-- POR PROYECTO (no solo globales).
--
-- Agrega id_proyecto (nullable) a reglas_evaluacion:
--   id_proyecto = NULL  → regla global (umbral por defecto)
--   id_proyecto = N     → override específico del proyecto N
--
-- El motor de recomendación (RecommendationEngine) resuelve, para
-- cada criterio, la regla del proyecto si existe; si no, cae al
-- valor global. Idempotente: se puede ejecutar más de una vez.
-- =============================================================

ALTER TABLE reglas_evaluacion
    ADD COLUMN IF NOT EXISTS id_proyecto INTEGER
        REFERENCES proyectos(id_proyecto) ON DELETE CASCADE;

CREATE INDEX IF NOT EXISTS idx_reglas_evaluacion_proyecto
    ON reglas_evaluacion(id_proyecto, criterio);

-- Evita duplicar un override para el mismo proyecto+criterio.
-- (No aplica a las reglas globales, que tienen id_proyecto NULL.)
CREATE UNIQUE INDEX IF NOT EXISTS uq_reglas_evaluacion_proyecto_criterio
    ON reglas_evaluacion(id_proyecto, criterio)
    WHERE id_proyecto IS NOT NULL;

-- Verificación
SELECT id_regla, nombre_regla, criterio, umbral, id_proyecto
FROM reglas_evaluacion
ORDER BY id_proyecto NULLS FIRST, criterio;
