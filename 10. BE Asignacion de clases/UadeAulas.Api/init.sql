-- Script de creación de tablas para uade_aulas
-- PostgreSQL 12+

-- Tabla: sede
CREATE TABLE IF NOT EXISTS sede (
	id SERIAL PRIMARY KEY,
	codigo VARCHAR(50) NOT NULL UNIQUE,
	nombre VARCHAR(255) NOT NULL
);

-- Tabla: materia
CREATE TABLE IF NOT EXISTS materia (
	codigo VARCHAR(50) PRIMARY KEY,
	nombre VARCHAR(255) NOT NULL,
	facultad VARCHAR(255),
	departamento VARCHAR(255),
	carga_horaria INT DEFAULT 0
);

-- Tabla: docente
CREATE TABLE IF NOT EXISTS docente (
	id SERIAL PRIMARY KEY,
	nombre VARCHAR(255) NOT NULL
);

-- Tabla: periodo
CREATE TABLE IF NOT EXISTS periodo (
	id SERIAL PRIMARY KEY,
	ciclo VARCHAR(50) NOT NULL,
	nombre VARCHAR(255) NOT NULL,
	fecha_desde DATE NOT NULL,
	fecha_hasta DATE NOT NULL,
	-- Ciclo de vida de la planificación (el estado se calcula, ver scripts/migracion_2026-10-06_reapertura.sql)
	fecha_congelamiento TIMESTAMPTZ,
	fecha_fin_inscripcion TIMESTAMPTZ,
	reabierta_en TIMESTAMPTZ,
	reabierta_por VARCHAR(255),
	reabierta_motivo TEXT,
	origen VARCHAR(20) NOT NULL DEFAULT 'EXCEL' CONSTRAINT ck_periodo_origen CHECK (origen IN ('EXCEL', 'COPIA')),
	periodo_origen_id INT REFERENCES periodo(id),
	CONSTRAINT ck_periodo_fechas_inscripcion CHECK (fecha_congelamiento IS NULL OR fecha_fin_inscripcion IS NULL
		OR fecha_fin_inscripcion > fecha_congelamiento),
	CONSTRAINT ck_periodo_reapertura CHECK (reabierta_en IS NULL
		OR (fecha_fin_inscripcion IS NOT NULL AND reabierta_por IS NOT NULL AND reabierta_motivo IS NOT NULL))
);

-- Tabla: aula
CREATE TABLE IF NOT EXISTS aula (
	id SERIAL PRIMARY KEY,
	sede_id INT NOT NULL REFERENCES sede(id),
	codigo VARCHAR(50) NOT NULL,
	tipo_grupo VARCHAR(50) NOT NULL,
	tipo_detalle VARCHAR(255),
	piso INT NOT NULL,
	capacidad_real INT NOT NULL,
	capacidad_total INT NOT NULL,
	equipamiento TEXT[],
	activa BOOLEAN DEFAULT true,
	edificio VARCHAR(255),
	piso_texto VARCHAR(50),   -- Piso tal cual el Excel uade_aulas.xlsx ("S1", "Lab", "B"...)
	numero_aula VARCHAR(50),
	UNIQUE (sede_id, codigo)
);

-- Tabla: clase
CREATE TABLE IF NOT EXISTS clase (
	id BIGSERIAL PRIMARY KEY,
	nro_clase INT NOT NULL,
	periodo_id INT NOT NULL REFERENCES periodo(id),
	sede_id INT NOT NULL REFERENCES sede(id),
	materia_codigo VARCHAR(50) NOT NULL REFERENCES materia(codigo),
	turno VARCHAR(50) NOT NULL,
	horario_desde TIME NOT NULL,
	horario_hasta TIME NOT NULL,
	dias TEXT[],
	regimen VARCHAR(50),
	idioma VARCHAR(50),
	modalidad VARCHAR(50),
	inscriptos INT DEFAULT 0,
	inscriptos_con_reserva INT DEFAULT 0,
	packs TEXT[]
);

-- Tabla: clase_docente
CREATE TABLE IF NOT EXISTS clase_docente (
	clase_id BIGINT NOT NULL REFERENCES clase(id),
	docente_id INT NOT NULL REFERENCES docente(id),
	PRIMARY KEY (clase_id, docente_id)
);

-- Tabla: asignacion
CREATE TABLE IF NOT EXISTS asignacion (
	id BIGSERIAL PRIMARY KEY,
	clase_id BIGINT NOT NULL UNIQUE REFERENCES clase(id),
	aula_id INT REFERENCES aula(id),
	origen VARCHAR(50) NOT NULL,
	sillas_vacias INT DEFAULT 0
);

-- Tabla: demanda_snapshot
CREATE TABLE IF NOT EXISTS demanda_snapshot (
	materia_codigo VARCHAR(50) PRIMARY KEY REFERENCES materia(codigo),
	solicitudes INT DEFAULT 0
);

-- Tablas opcionales (no usadas en MVP pero mencionadas en el esquema)
-- Maestro de carreras (cargado desde el Excel; reemplaza a la vieja tabla carrera, 2026-10-08)
CREATE TABLE IF NOT EXISTS carreras (
	codigo_carrera VARCHAR(20) PRIMARY KEY,
	nombre_carrera VARCHAR(150) NOT NULL,
	facultad VARCHAR(100)
);

CREATE TABLE IF NOT EXISTS materia_carrera (
	materia_codigo VARCHAR(50) NOT NULL REFERENCES materia(codigo),
	codigo_carrera VARCHAR(20) NOT NULL REFERENCES carreras(codigo_carrera),
	PRIMARY KEY (materia_codigo, codigo_carrera)
);

CREATE TABLE IF NOT EXISTS alumno (
	id SERIAL PRIMARY KEY,
	legajo INT NOT NULL UNIQUE,
	nombre VARCHAR(255) NOT NULL,
	email VARCHAR(255)
);

CREATE TABLE IF NOT EXISTS inscripcion (
	id SERIAL PRIMARY KEY,
	alumno_id INT NOT NULL REFERENCES alumno(id),
	clase_id BIGINT NOT NULL REFERENCES clase(id),
	fecha_inscripcion TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS docente_materia (
	docente_id INT NOT NULL REFERENCES docente(id),
	materia_codigo VARCHAR(50) NOT NULL REFERENCES materia(codigo),
	PRIMARY KEY (docente_id, materia_codigo)
);

CREATE TABLE IF NOT EXISTS oferta_meta (
	id SERIAL PRIMARY KEY,
	materia_codigo VARCHAR(50) NOT NULL REFERENCES materia(codigo),
	periodo_id INT NOT NULL REFERENCES periodo(id),
	meta_clases INT NOT NULL,
	PRIMARY KEY (materia_codigo, periodo_id)
);

-- Tabla: auditoria_cambios (log de cambios del backoffice, se graba desde la API)
CREATE TABLE IF NOT EXISTS auditoria_cambios (
	id BIGSERIAL PRIMARY KEY,
	fecha_hora TIMESTAMPTZ NOT NULL DEFAULT now(),
	usuario VARCHAR(255) NOT NULL,
	periodo_id INT REFERENCES periodo(id),
	entidad VARCHAR(50) NOT NULL,
	entidad_id BIGINT,
	accion VARCHAR(20) NOT NULL CHECK (accion IN ('ALTA', 'MODIFICACION', 'BAJA')),
	es_emergencia BOOLEAN NOT NULL DEFAULT false,
	motivo TEXT,
	antes JSONB,
	despues JSONB,
	lote UUID,   -- agrupa los cambios aplicados juntos (ej. un intercambio de aulas)
	CHECK (NOT es_emergencia OR motivo IS NOT NULL)
);
CREATE INDEX IF NOT EXISTS idx_auditoria_lote ON auditoria_cambios(lote);

-- Índices para performance
CREATE INDEX IF NOT EXISTS idx_auditoria_periodo ON auditoria_cambios(periodo_id, fecha_hora DESC);
CREATE INDEX IF NOT EXISTS idx_auditoria_entidad ON auditoria_cambios(entidad, entidad_id);
CREATE INDEX IF NOT EXISTS idx_clase_periodo ON clase(periodo_id);
CREATE INDEX IF NOT EXISTS idx_clase_sede ON clase(sede_id);
CREATE INDEX IF NOT EXISTS idx_clase_materia ON clase(materia_codigo);
CREATE INDEX IF NOT EXISTS idx_aula_sede ON aula(sede_id);
CREATE INDEX IF NOT EXISTS idx_asignacion_clase ON asignacion(clase_id);
CREATE INDEX IF NOT EXISTS idx_asignacion_aula ON asignacion(aula_id);
CREATE INDEX IF NOT EXISTS idx_clase_docente_clase ON clase_docente(clase_id);
CREATE INDEX IF NOT EXISTS idx_clase_docente_docente ON clase_docente(docente_id);

-- Insertar datos de prueba
INSERT INTO sede (codigo, nombre) VALUES 
	('MONSERRAT', 'UADE Monserrat'),
	('BELGRANO', 'UADE Belgrano'),
	('RECOLETA', 'UADE Recoleta'),
	('PINAMAR', 'UADE Pinamar')
ON CONFLICT DO NOTHING;

INSERT INTO periodo (ciclo, nombre, fecha_desde, fecha_hasta) VALUES
	('2024-1', 'Primer Cuatrimestre 2024', '2024-02-26', '2024-06-28'),
	('2024-2', 'Segundo Cuatrimestre 2024', '2024-08-05', '2024-11-29')
ON CONFLICT DO NOTHING;

INSERT INTO materia (codigo, nombre, facultad, departamento, carga_horaria) VALUES
	('MAT001', 'Análisis Matemático I', 'Ingeniería', 'Matemática', 120),
	('MAT002', 'Álgebra Lineal', 'Ingeniería', 'Matemática', 120),
	('FIS001', 'Física I', 'Ingeniería', 'Física', 120),
	('PROG001', 'Programación I', 'Ingeniería', 'Computación', 120)
ON CONFLICT DO NOTHING;

INSERT INTO docente (nombre) VALUES
	('Dr. Juan Pérez'),
	('Dra. María García'),
	('Ing. Carlos López')
ON CONFLICT DO NOTHING;

-- Aulas: se cargan desde uade_aulas.xlsx con scripts/importar_aulas.py

-- 2026-10-08: el modelo de arriba (salvo carreras) se borró de la base a la espera de las tablas nuevas.
-- Ver scripts/migracion_2026-10-08_planificacion_excel.sql.
CREATE TABLE IF NOT EXISTS planificacion_excel (
	id SERIAL PRIMARY KEY,
	nombre_archivo VARCHAR(255) NOT NULL,
	subido_en TIMESTAMPTZ NOT NULL DEFAULT now(),
	subido_por VARCHAR(255) NOT NULL,
	encabezados JSONB NOT NULL,
	filas JSONB NOT NULL,
	anio INT,
	cuatrimestre INT CHECK (cuatrimestre IN (1, 2))
);

-- Demanda provisoria (2026-10-08): ver scripts/migracion_2026-10-08_demanda_excel.sql.
CREATE TABLE IF NOT EXISTS demanda_excel (
	id SERIAL PRIMARY KEY,
	nombre_archivo VARCHAR(255) NOT NULL,
	subido_en TIMESTAMPTZ NOT NULL DEFAULT now(),
	subido_por VARCHAR(255) NOT NULL,
	encabezados JSONB NOT NULL,
	filas JSONB NOT NULL
);
