-- Ediciones de aula sobre la planificación guardada. Idempotente; solo crea (no borra nada).
-- El Excel original (planificacion_excel) no se modifica: acá queda solo el aula elegida por clase.
-- El historial (antes/después, motivo, lote) iría en log_acciones, que por ahora no se llena.

CREATE TABLE IF NOT EXISTS asignacion_aula (
    clase_id            varchar(50)  PRIMARY KEY,                               -- columna "Id" del Excel (única por fila)
    aula_id             integer      REFERENCES aulas(id) ON DELETE RESTRICT,   -- NULL = se dejó sin aula
    modificado_por      varchar(150) NOT NULL,                                  -- usuario efectivo
    modificado_por_real varchar(150) NOT NULL,                                  -- usuario real (distinto si suplanta)
    modificado_en       timestamptz  NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_asignacion_aula_aula ON asignacion_aula (aula_id);
