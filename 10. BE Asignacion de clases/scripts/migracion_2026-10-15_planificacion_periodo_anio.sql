-- planificacion_excel: el período y el año dejan de ser números sueltos (cuatrimestre 1/2 con CHECK y año entero)
-- y pasan a vincularse con las tablas `periodo` y `anio`, que son las que alimentan los desplegables.
-- Idempotente: si ya se aplicó, no hace nada. Backup previo:
--   backups/uade_aulas_antes_planificacion_periodo_anio_2026-10-15.dump
BEGIN;

ALTER TABLE planificacion_excel ADD COLUMN IF NOT EXISTS periodo_id integer REFERENCES periodo(id);
ALTER TABLE planificacion_excel ADD COLUMN IF NOT EXISTS anio_id integer REFERENCES anio(id);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'public' AND table_name = 'planificacion_excel' AND column_name = 'cuatrimestre') THEN
        -- cuatrimestre 1 y 2 coinciden con periodo.id 1 ("1er Cuatr") y 2 ("2do Cuatr")
        UPDATE planificacion_excel pe SET periodo_id = p.id
        FROM periodo p WHERE pe.periodo_id IS NULL AND p.id = pe.cuatrimestre;

        -- anio.anio es texto ('2026')
        UPDATE planificacion_excel pe SET anio_id = a.id
        FROM anio a WHERE pe.anio_id IS NULL AND TRIM(a.anio) = pe.anio::text;

        ALTER TABLE planificacion_excel DROP COLUMN cuatrimestre; -- arrastra su CHECK (1, 2)
        ALTER TABLE planificacion_excel DROP COLUMN anio;
    END IF;
END $$;

COMMIT;
