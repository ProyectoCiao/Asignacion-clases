-- Alumnos distribuidos en las clases (y por lo tanto en las aulas): resultado del botón "Asignar alumnos a aulas" del Mapa.
-- Idempotente; solo crea. Cada corrida del botón reemplaza todo el contenido. El Excel de demanda no se modifica.
-- opcion 1 = la clase que el alumno eligió primero (silla ocupada); opcion 2 = su segunda opción (tentativa).

CREATE TABLE IF NOT EXISTS asignacion_alumno (
    id            bigserial    PRIMARY KEY,
    legajo        varchar(30)  NOT NULL,
    alumno        varchar(200),
    clase_id      varchar(50)  NOT NULL,                       -- columna "Id" de la planificación
    opcion        smallint     NOT NULL CHECK (opcion IN (1, 2)),
    asignado_por  varchar(150) NOT NULL,
    asignado_en   timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT uq_asignacion_alumno UNIQUE (legajo, clase_id, opcion)
);

CREATE INDEX IF NOT EXISTS ix_asignacion_alumno_clase ON asignacion_alumno (clase_id, opcion);
