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
	fecha_hasta DATE NOT NULL
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
CREATE TABLE IF NOT EXISTS carrera (
	id SERIAL PRIMARY KEY,
	codigo VARCHAR(50) NOT NULL UNIQUE,
	nombre VARCHAR(255) NOT NULL
);

CREATE TABLE IF NOT EXISTS materia_carrera (
	materia_codigo VARCHAR(50) NOT NULL REFERENCES materia(codigo),
	carrera_id INT NOT NULL REFERENCES carrera(id),
	PRIMARY KEY (materia_codigo, carrera_id)
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

-- Índices para performance
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
