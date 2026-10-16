-- Migración: agrupar en auditoría los cambios aplicados juntos (ej. un intercambio de aulas A<->B) (2026-10-05)
-- Solo agrega una columna: no modifica ni borra datos existentes.
BEGIN;

ALTER TABLE auditoria_cambios ADD COLUMN IF NOT EXISTS lote UUID;
CREATE INDEX IF NOT EXISTS idx_auditoria_lote ON auditoria_cambios(lote);

COMMIT;
