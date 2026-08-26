-- Ejecutar en Turso (turso db shell talleresrmp-ariasjuarezalexandro)
-- o desde la consola web de Turso

-- Campos nuevos en Mantenimiento
ALTER TABLE Mantenimiento ADD COLUMN Fotos TEXT;
ALTER TABLE Mantenimiento ADD COLUMN Descripcion TEXT;
ALTER TABLE Mantenimiento ADD COLUMN Precio REAL;

-- Eliminar campo Estado
ALTER TABLE Mantenimiento DROP COLUMN Estado;

-- Campo EsServicio en MantenimientoProducto
-- Si ya ejecutaste la migración anterior con EsDescripcion, ejecuta las dos líneas siguientes:
--   ALTER TABLE MantenimientoProducto DROP COLUMN EsDescripcion;
--   ALTER TABLE MantenimientoProducto ADD COLUMN EsServicio INTEGER DEFAULT 0;
-- Si aún no ejecutaste ninguna migración en MantenimientoProducto, solo ejecuta:
ALTER TABLE MantenimientoProducto ADD COLUMN EsServicio INTEGER DEFAULT 0;
