-- Detalle por prueba de cada resultado cargado: pruebas fallidas con su motivo, las más lentas y el
-- desglose por módulo, extraídos del output.json de Robot Framework al momento de la carga.
-- Una fila por resultado (id_resultado es la clave). Los resultados cargados antes de esta migración, o
-- sin el archivo completo, simplemente no tienen fila: el sistema muestra solo las métricas agregadas.
-- Es idempotente: se puede ejecutar más de una vez sin efecto.

CREATE TABLE IF NOT EXISTS resultado_detalle (
    id_resultado   INTEGER   PRIMARY KEY REFERENCES resultados_prueba(id_resultado) ON DELETE CASCADE,
    detalle_json   TEXT      NOT NULL,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
