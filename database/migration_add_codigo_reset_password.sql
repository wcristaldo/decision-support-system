-- Autoservicio de restablecimiento de contraseña ("¿Olvidaste tu contraseña?"
-- en Login): código numérico de 6 dígitos, vigencia corta, un solo uso.
CREATE TABLE codigo_reset_password (
  id_codigo        SERIAL PRIMARY KEY,
  id_usuario       INT NOT NULL REFERENCES usuarios(id_usuario) ON DELETE CASCADE,
  codigo           VARCHAR(6) NOT NULL,
  fecha_creacion   TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  fecha_expiracion TIMESTAMP NOT NULL,
  usado            BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX idx_codigo_reset_usuario ON codigo_reset_password (id_usuario, usado);
