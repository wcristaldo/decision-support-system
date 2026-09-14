-- Bug real: recomendaciones.justificacion se arma en el motor de recomendacion
-- (RecommendationEngine.cs) concatenando el resumen mas la observacion de cada
-- regla evaluada. Con 3 o mas reglas (el sistema seedea 4 reglas globales) el
-- texto resultante supera facilmente los 255 caracteres del VARCHAR original,
-- y el INSERT completo fallaba con "el valor es demasiado largo para el tipo
-- character varying(255)" -- la API devolvia el mensaje generico "Los datos
-- enviados no son validos." sin explicar la causa real. Se amplia a TEXT
-- (sin limite arbitrario) ya que es contenido generado por el sistema, no
-- texto libre de un usuario que necesite un tope de validacion.
ALTER TABLE recomendaciones ALTER COLUMN justificacion TYPE TEXT;
