-- Migración: planificación inicial cruda y baja del modelo anterior (2026-10-08)
--
-- Decisión del usuario: las pestañas quedan desvinculadas de las tablas hasta que cree las nuevas
-- (Mapa, Aulas, Materias, Docentes). Se borran todas las tablas salvo carreras.
-- La planificación inicial se sube una sola vez desde el Excel y se guarda tal cual (sin reglas ni vínculos)
-- en planificacion_excel; después se ajusta desde el programa.
BEGIN;

-- Sin CASCADE: si apareciera algo que dependa de estas tablas (vista, FK desde otra tabla), falla y no borra nada
DROP TABLE IF EXISTS
    auditoria_cambios, asignacion, clase_docente, clase, planificacion, periodo,
    inscripcion_interes, alumno, materia_carrera, docente_materia, docente,
    demanda_snapshot, aula, sede;

CREATE TABLE IF NOT EXISTS planificacion_excel (
    id             SERIAL PRIMARY KEY,
    nombre_archivo VARCHAR(255) NOT NULL,
    subido_en      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    subido_por     VARCHAR(255) NOT NULL,
    encabezados    JSONB        NOT NULL,  -- ["Materia", "Sede", ...] en el orden del Excel
    filas          JSONB        NOT NULL   -- [["A123", "Monserrat", ...], ...] como texto, tal como se ve en el Excel
);
-- Período al que corresponde (lo elige el usuario al subir; no viene en el Excel)
ALTER TABLE planificacion_excel ADD COLUMN IF NOT EXISTS anio         INT;
ALTER TABLE planificacion_excel ADD COLUMN IF NOT EXISTS cuatrimestre INT CHECK (cuatrimestre IN (1, 2));

COMMIT;
