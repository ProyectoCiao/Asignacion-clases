-- Migración: Excel de demanda guardado (2026-10-08)
--
-- Provisorio: la demanda real va a salir de una tabla externa leída por API. Mientras tanto el backoffice sube
-- un Excel y se guarda tal cual (una sola fila: cada subida reemplaza la anterior) para no perderlo si se cierra
-- el programa. Los totales por columna se calculan en la API.
BEGIN;

CREATE TABLE IF NOT EXISTS demanda_excel (
    id             SERIAL PRIMARY KEY,
    nombre_archivo VARCHAR(255) NOT NULL,
    subido_en      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    subido_por     VARCHAR(255) NOT NULL,
    encabezados    JSONB        NOT NULL,  -- ["legajo", "Carrera", ...] en el orden del Excel
    filas          JSONB        NOT NULL   -- [["1146251", "CONTADOR PÚBLICO", ...], ...] como texto
);

COMMIT;
