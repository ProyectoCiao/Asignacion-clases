-- Migración: finalización automática y reapertura excepcional de la planificación (2026-10-06)
--
-- Nuevo ciclo de vida (calculado en Models/Periodo.cs, no se guarda):
--   ahora <  fecha_congelamiento (o nula)    -> EN_EDICION
--   ahora <  fecha_fin_inscripcion (o nula)  -> INSCRIPCION_ABIERTA   (edición con motivo; la IA sugiere acá)
--   resto, sin reabierta_en                  -> FINALIZADA            (automática al cerrar la inscripción)
--   resto, con reabierta_en                  -> REABIERTA             (excepción manual hasta cerrarla)
-- Desaparece INSCRIPCION_CERRADA y la finalización manual (finalizada_en/finalizada_por).
-- El historial de reaperturas queda en auditoria_cambios; acá solo la reapertura vigente.
BEGIN;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'periodo' AND column_name = 'finalizada_en')
       AND EXISTS (SELECT 1 FROM periodo WHERE finalizada_en IS NOT NULL) THEN
        RAISE EXCEPTION 'Hay períodos con finalizada_en cargada: revisarlos antes de quitar la columna.';
    END IF;
END $$;

ALTER TABLE periodo DROP COLUMN IF EXISTS finalizada_en;
ALTER TABLE periodo DROP COLUMN IF EXISTS finalizada_por;

ALTER TABLE periodo ADD COLUMN IF NOT EXISTS reabierta_en     TIMESTAMPTZ;
ALTER TABLE periodo ADD COLUMN IF NOT EXISTS reabierta_por    VARCHAR(255);
ALTER TABLE periodo ADD COLUMN IF NOT EXISTS reabierta_motivo TEXT;

-- Solo se reabre lo que se finalizó (requiere fin de inscripción) y siempre con usuario y motivo
ALTER TABLE periodo DROP CONSTRAINT IF EXISTS ck_periodo_reapertura;
ALTER TABLE periodo ADD CONSTRAINT ck_periodo_reapertura
    CHECK (reabierta_en IS NULL
           OR (fecha_fin_inscripcion IS NOT NULL AND reabierta_por IS NOT NULL AND reabierta_motivo IS NOT NULL));

COMMIT;
