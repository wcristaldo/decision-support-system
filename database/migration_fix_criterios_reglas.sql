-- =============================================================
-- migration_fix_criterios_reglas.sql
--
-- BUG CRÍTICO encontrado en la base real: las reglas de evaluación
-- vigentes usan nombres de "criterio" que NO coinciden con los
-- nombres de métrica que MetricsCalculationService realmente calcula
-- (tasa_exito, cobertura, tasa_fallo, tiempo_ejecucion — ver
-- Services/MetricsCalculationService.cs). Como consecuencia,
-- RecommendationEngine nunca encuentra coincidencia para ninguna
-- regla y TODA evaluación termina en "desplegar" sin importar los
-- resultados reales de las pruebas — el motor de recomendación
-- (RF07-RF09, núcleo de la tesis) estaba efectivamente inerte.
--
-- Este script realinea los criterios existentes a los nombres reales
-- de métrica. El umbral de "tiempo_respuesta_ms" (3000, pensado como
-- milisegundos de tiempo de respuesta de API) no es comparable con
-- tiempo_ejecucion (segundos de duración de la suite de pruebas) —
-- son conceptos distintos — así que se reemplaza por el default
-- documentado en la tesis y en seed.sql: 120 segundos. La regla
-- "errores_criticos" no corresponde a ninguna métrica que el sistema
-- calcule hoy, así que se desactiva en vez de renombrarse a algo
-- inexistente.
--
-- Idempotente: puede ejecutarse más de una vez sin efectos adicionales.
-- =============================================================

UPDATE reglas_evaluacion SET criterio = 'tasa_exito'
    WHERE criterio = 'porcentaje_exito';

UPDATE reglas_evaluacion SET criterio = 'cobertura'
    WHERE criterio = 'cobertura_porcentaje';

UPDATE reglas_evaluacion SET criterio = 'tasa_fallo'
    WHERE criterio = 'porcentaje_fallo';

UPDATE reglas_evaluacion SET criterio = 'tiempo_ejecucion', umbral = 120.00
    WHERE criterio = 'tiempo_respuesta_ms';

UPDATE reglas_evaluacion SET estado = 'inactivo'
    WHERE criterio = 'errores_criticos';

-- Verificación
SELECT id_regla, nombre_regla, criterio, umbral, estado, id_proyecto
FROM reglas_evaluacion
ORDER BY id_proyecto NULLS FIRST, criterio;
