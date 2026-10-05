-- Registro de auditoría inmutable a nivel de base de datos: solo admite inserciones.
-- La única modificación permitida es la que aplica la propia clave foránea si se eliminara un usuario
-- (ON DELETE SET NULL sobre id_usuario); cualquier otro UPDATE, DELETE o TRUNCATE se rechaza.
CREATE OR REPLACE FUNCTION fn_auditoria_inmutable() RETURNS trigger AS $$
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.id_usuario IS NULL AND OLD.id_usuario IS NOT NULL
           AND (NEW.id_auditoria, NEW.entidad_afectada, NEW.id_registro_afectado, NEW.accion,
                NEW.detalle, NEW.fecha_evento, NEW.ip_origen)
               IS NOT DISTINCT FROM
               (OLD.id_auditoria, OLD.entidad_afectada, OLD.id_registro_afectado, OLD.accion,
                OLD.detalle, OLD.fecha_evento, OLD.ip_origen)
        THEN
            RETURN NEW;
        END IF;
    END IF;
    RAISE EXCEPTION 'Los registros de auditoría no pueden modificarse ni eliminarse (operación %)', TG_OP;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_auditoria_inmutable ON auditoria;
CREATE TRIGGER trg_auditoria_inmutable
    BEFORE UPDATE OR DELETE ON auditoria
    FOR EACH ROW EXECUTE FUNCTION fn_auditoria_inmutable();

DROP TRIGGER IF EXISTS trg_auditoria_sin_truncate ON auditoria;
CREATE TRIGGER trg_auditoria_sin_truncate
    BEFORE TRUNCATE ON auditoria
    FOR EACH STATEMENT EXECUTE FUNCTION fn_auditoria_inmutable();
