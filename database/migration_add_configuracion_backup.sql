-- RNF12: mecanismo de respaldo periódico de la BD, configurable por el
-- Administrador (intervalo y carpeta destino), con historial de ejecuciones.
-- Fila única (siempre id=1) — es configuración global del sistema, no por usuario.

CREATE TABLE configuracion_backup (
    id                     SERIAL      PRIMARY KEY,
    intervalo_horas        INTEGER     NOT NULL DEFAULT 24 CHECK (intervalo_horas >= 1),
    carpeta_destino        VARCHAR(500) NOT NULL DEFAULT 'backups',
    activo                 BOOLEAN     NOT NULL DEFAULT true,
    fecha_ultima_ejecucion TIMESTAMP   NULL
);

INSERT INTO configuracion_backup (intervalo_horas, carpeta_destino, activo)
VALUES (24, 'backups', true);
