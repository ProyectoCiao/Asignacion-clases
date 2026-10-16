-- Migración: ciclo de vida de la planificación y log de cambios del backoffice (2026-10-05)
-- Solo agrega columnas y tablas: no modifica ni borra datos existentes.
--
-- La planificación de un cuatrimestre es su fila en `periodo`. El estado NO se guarda: se calcula
-- a partir de estas fechas (ver Models/Periodo.cs):
--   finalizada_en cargada              -> FINALIZADA
--   ahora <  fecha_congelamiento       -> EN_EDICION           (o sin fecha cargada)
--   ahora <  fecha_fin_inscripcion     -> INSCRIPCION_ABIERTA  (o sin fecha cargada)
--   resto                              -> INSCRIPCION_CERRADA
BEGIN;

ALTER TABLE periodo ADD COLUMN IF NOT EXISTS fecha_congelamiento   TIMESTAMPTZ;
ALTER TABLE periodo ADD COLUMN IF NOT EXISTS fecha_fin_inscripcion TIMESTAMPTZ;
ALTER TABLE periodo ADD COLUMN IF NOT EXISTS finalizada_en         TIMESTAMPTZ;
ALTER TABLE periodo ADD COLUMN IF NOT EXISTS finalizada_por        VARCHAR(255);
-- EXCEL: carga inicial desde el Excel (una sola vez). COPIA: copiada de una planificación finalizada.
ALTER TABLE periodo ADD COLUMN IF NOT EXISTS origen                VARCHAR(20) NOT NULL DEFAULT 'EXCEL';
ALTER TABLE periodo ADD COLUMN IF NOT EXISTS periodo_origen_id     INT REFERENCES periodo(id);

ALTER TABLE periodo DROP CONSTRAINT IF EXISTS ck_periodo_origen;
ALTER TABLE periodo ADD CONSTRAINT ck_periodo_origen CHECK (origen IN ('EXCEL', 'COPIA'));

ALTER TABLE periodo DROP CONSTRAINT IF EXISTS ck_periodo_fechas_inscripcion;
ALTER TABLE periodo ADD CONSTRAINT ck_periodo_fechas_inscripcion
    CHECK (fecha_congelamiento IS NULL OR fecha_fin_inscripcion IS NULL
           OR fecha_fin_inscripcion > fecha_congelamiento);

-- Log de cambios hechos por el backoffice (y más adelante, las sugerencias de IA aplicadas).
-- Se graba desde la API en la misma transacción que el cambio.
CREATE TABLE IF NOT EXISTS auditoria_cambios (
    id             BIGSERIAL PRIMARY KEY,
    fecha_hora     TIMESTAMPTZ NOT NULL DEFAULT now(),
    usuario        VARCHAR(255) NOT NULL,
    periodo_id     INT REFERENCES periodo(id),
    entidad        VARCHAR(50)  NOT NULL,   -- ej. 'clase', 'asignacion', 'periodo'
    entidad_id     BIGINT,
    accion         VARCHAR(20)  NOT NULL CHECK (accion IN ('ALTA', 'MODIFICACION', 'BAJA')),
    es_emergencia  BOOLEAN      NOT NULL DEFAULT false,  -- cambio hecho con la planificación ya congelada
    motivo         TEXT,
    antes          JSONB,
    despues        JSONB,
    CHECK (NOT es_emergencia OR motivo IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS idx_auditoria_periodo ON auditoria_cambios(periodo_id, fecha_hora DESC);
CREATE INDEX IF NOT EXISTS idx_auditoria_entidad ON auditoria_cambios(entidad, entidad_id);

COMMIT;
