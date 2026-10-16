-- Eliminar columnas de texto
ALTER TABLE aulas 
    DROP COLUMN aula_sede,
    DROP COLUMN aula_edificio;

-- Agregar restricciones Foreign Key
ALTER TABLE aulas
    ADD CONSTRAINT fk_aulas_sedes FOREIGN KEY (sede_id) REFERENCES sedes(id) ON DELETE RESTRICT,
    ADD CONSTRAINT fk_aulas_edificios FOREIGN KEY (edificio_id) REFERENCES edificios(id) ON DELETE RESTRICT;