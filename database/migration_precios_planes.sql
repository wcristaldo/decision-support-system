-- =============================================================
-- migration_precios_planes.sql
-- Actualización de planes según Tabla 14 de la tesis y
-- Presupuesto_RoshkaDSS_2026.xlsx (Hoja "Precios")
--
-- PRECIOS:
--   Básico:       400.000 → 250.000 Gs./mes
--   Profesional: 1.000.000 → 500.000 Gs./mes
--   Empresarial: 2.500.000 → 900.000 Gs./mes
--
-- USUARIOS:
--   Básico:       3 → 10 usuarios
--   Profesional: 10 → 25 usuarios
--   Empresarial: sin límite (sin cambio)
--
-- Ejecutar en producción y staging antes del próximo ciclo de facturación.
-- =============================================================

-- Precios
UPDATE planes_suscripcion SET precio_mensual = 250000.00  WHERE nombre = 'Básico';
UPDATE planes_suscripcion SET precio_mensual = 500000.00  WHERE nombre = 'Profesional';
UPDATE planes_suscripcion SET precio_mensual = 900000.00  WHERE nombre = 'Empresarial';

-- Límites de usuarios
UPDATE planes_suscripcion SET max_usuarios   = 10  WHERE nombre = 'Básico';
UPDATE planes_suscripcion SET max_usuarios   = 25  WHERE nombre = 'Profesional';

-- Límites de proyectos
UPDATE planes_suscripcion SET max_proyectos  =  3  WHERE nombre = 'Básico';
UPDATE planes_suscripcion SET max_proyectos  = 10  WHERE nombre = 'Profesional';

-- Verificación
SELECT nombre, precio_mensual, max_proyectos, max_usuarios, max_evaluaciones_mes
FROM planes_suscripcion
ORDER BY precio_mensual;
