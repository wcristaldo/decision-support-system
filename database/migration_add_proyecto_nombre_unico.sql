-- Gap real detectado: no habia ninguna validacion de nombre unico para
-- proyectos, ni en la API ni en la base -- se podian crear varios proyectos
-- con el mismo nombre sin ningun aviso. La API ya valida esto (ver
-- ProyectosController.Create/Update); este indice unico (case-insensitive)
-- es la garantia a nivel de base de datos.
CREATE UNIQUE INDEX idx_proyectos_nombre_unico ON proyectos (LOWER(nombre_proyecto));
