-- Bug real: el motor de recomendacion (RecommendationEngine.cs) escribe
-- resultado_regla = 'revisar' para cualquier metrica que caiga en la zona de
-- tolerancia (+-5%), pero el CHECK original de evaluacion_regla solo permitia
-- 'cumple'/'no_cumple'/'no_aplica'. Cualquier resultado de prueba cuyas
-- metricas cayeran en la zona de tolerancia rechazaba el INSERT completo con
-- una violacion de CHECK constraint, y la API devolvia el mensaje generico
-- "Los datos enviados no son validos." sin explicar la causa real.
ALTER TABLE evaluacion_regla DROP CONSTRAINT evaluacion_regla_resultado_regla_check;
ALTER TABLE evaluacion_regla ADD CONSTRAINT evaluacion_regla_resultado_regla_check
    CHECK (resultado_regla IN ('cumple','no_cumple','no_aplica','revisar'));
