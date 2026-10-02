-- Migración: tabla aula alineada con el Excel uade_aulas.xlsx (2026-10-02)
-- Backup previo: backups/uade_aulas_antes_import_aulas_2026-10-02.dump
BEGIN;

ALTER TABLE aula ADD COLUMN IF NOT EXISTS piso_texto VARCHAR(50);
ALTER TABLE aula ADD COLUMN IF NOT EXISTS numero_aula VARCHAR(50);

-- Sedes reales: Monserrat, Recoleta, Belgrano, Pinamar
INSERT INTO sede (codigo, nombre) VALUES
    ('RECOLETA', 'UADE Recoleta'),
    ('PINAMAR', 'UADE Pinamar')
ON CONFLICT (codigo) DO NOTHING;

-- FLORES era una sede de prueba: se borran sus clases (con asignaciones y docentes) y sus aulas.
DELETE FROM asignacion WHERE clase_id IN (SELECT id FROM clase WHERE sede_id = (SELECT id FROM sede WHERE codigo = 'FLORES'));
DELETE FROM clase_docente WHERE clase_id IN (SELECT id FROM clase WHERE sede_id = (SELECT id FROM sede WHERE codigo = 'FLORES'));
DELETE FROM clase WHERE sede_id = (SELECT id FROM sede WHERE codigo = 'FLORES');
DELETE FROM aula WHERE sede_id = (SELECT id FROM sede WHERE codigo = 'FLORES');
DELETE FROM sede WHERE codigo = 'FLORES';

COMMIT;
