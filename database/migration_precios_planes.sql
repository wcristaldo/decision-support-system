-- =============================================================
-- migration_precios_planes.sql
-- Actualización de precios de planes según Presupuesto_SAD_Roshka_2026.xlsx
-- Hoja "Precios" → Fila "PRECIO DE VENTA DEFINIDO (Gs./mes)"
--
-- Básico:       400.000 → 250.000 Gs./mes
-- Profesional: 1.000.000 → 500.000 Gs./mes
-- Empresarial: 2.500.000 → 900.000 Gs./mes
--
-- Ejecutar en producción y staging antes del próximo ciclo de facturación.
-- =============================================================

UPDATE planes_suscripcion SET precio_mensual = 250000.00  WHERE nombre = 'Básico';
UPDATE planes_suscripcion SET precio_mensual = 500000.00  WHERE nombre = 'Profesional';
UPDATE planes_suscripcion SET precio_mensual = 900000.00  WHERE nombre = 'Empresarial';

-- Verificación
SELECT nombre, precio_mensual FROM planes_suscripcion ORDER BY precio_mensual;
