-- Gap real detectado: no habia ninguna validacion de numero de version unico
-- dentro de un mismo proyecto, ni en la API ni en la base -- se podian crear
-- varias versiones "1.0.0" para el mismo proyecto sin ningun aviso. La API
-- ya valida esto (ver VersionesController.Create/Update); este indice unico
-- compuesto (case-insensitive) es la garantia a nivel de base de datos.
CREATE UNIQUE INDEX idx_versiones_numero_unico ON versiones (id_proyecto, LOWER(nombre_version));
