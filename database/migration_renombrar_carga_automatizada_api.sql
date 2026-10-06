-- Renombra la columna planes_suscripcion.integracion_cicd a carga_automatizada_api
-- (la funcionalidad es la carga automatizada de reportes mediante la API, POST /api/reports).
-- Idempotente: no hace nada si la columna ya tiene el nombre nuevo.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_name = 'planes_suscripcion' AND column_name = 'integracion_cicd') THEN
        ALTER TABLE planes_suscripcion RENAME COLUMN integracion_cicd TO carga_automatizada_api;
    END IF;
END $$;
