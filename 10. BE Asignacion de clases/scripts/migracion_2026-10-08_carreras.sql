-- Migración: reemplazar la tabla carrera por carreras (2026-10-08)
--
-- carreras (codigo_carrera, nombre_carrera, facultad) es el maestro de carreras, cargado desde el Excel.
-- Venía sin clave y con 342 filas repetidas (copias exactas, una por plan de estudios): se dejan 359,
-- una por carrera, y codigo_carrera pasa a ser la clave primaria.
-- alumno y materia_carrera dejan de usar carrera_id (int -> carrera.id) y pasan a codigo_carrera
-- (varchar -> carreras.codigo_carrera). Ambas estaban vacías: no hay datos que convertir.
BEGIN;

DO $$
DECLARE hay BOOLEAN;
BEGIN
    -- SQL dinámico: si la columna ya no existe (segunda corrida), la consulta no se compila
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'alumno' AND column_name = 'carrera_id') THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM alumno WHERE carrera_id IS NOT NULL)' INTO hay;
        IF hay THEN
            RAISE EXCEPTION 'alumno tiene carrera_id cargado: convertir esos datos antes de migrar.';
        END IF;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'materia_carrera' AND column_name = 'carrera_id')
       AND EXISTS (SELECT 1 FROM materia_carrera) THEN
        RAISE EXCEPTION 'materia_carrera tiene filas: convertir esos datos antes de migrar.';
    END IF;
END $$;

-- 1. carreras: una fila por código y clave primaria
DELETE FROM carreras a
USING carreras b
WHERE a.codigo_carrera = b.codigo_carrera
  AND a.ctid > b.ctid;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'carreras'::regclass AND contype = 'p') THEN
        ALTER TABLE carreras ADD CONSTRAINT carreras_pkey PRIMARY KEY (codigo_carrera);
    END IF;
END $$;

-- 2. materia_carrera: carrera_id -> codigo_carrera
ALTER TABLE materia_carrera DROP COLUMN IF EXISTS carrera_id;   -- arrastra su FK, su UNIQUE y su índice
ALTER TABLE materia_carrera ADD COLUMN IF NOT EXISTS codigo_carrera VARCHAR(20) NOT NULL
    REFERENCES carreras(codigo_carrera);
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'materia_carrera_materia_codigo_codigo_carrera_key') THEN
        ALTER TABLE materia_carrera ADD CONSTRAINT materia_carrera_materia_codigo_codigo_carrera_key
            UNIQUE (materia_codigo, codigo_carrera);
    END IF;
END $$;
CREATE INDEX IF NOT EXISTS idx_materia_carrera_carrera ON materia_carrera (codigo_carrera);

-- 3. alumno: carrera_id -> codigo_carrera
ALTER TABLE alumno DROP COLUMN IF EXISTS carrera_id;            -- arrastra su FK y su índice
ALTER TABLE alumno ADD COLUMN IF NOT EXISTS codigo_carrera VARCHAR(20)
    REFERENCES carreras(codigo_carrera);
CREATE INDEX IF NOT EXISTS idx_alumno_carrera ON alumno (codigo_carrera);

-- 4. Ya nadie depende de carrera
DROP TABLE IF EXISTS carrera;

COMMIT;
