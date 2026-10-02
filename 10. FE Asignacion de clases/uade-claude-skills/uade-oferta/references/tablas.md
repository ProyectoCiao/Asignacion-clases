# Tablas — uade-oferta

## clase

| Columna | Tipo | Notas |
|---|---|---|
| id | bigserial PK | |
| nro_clase | int | |
| periodo_id | int FK periodo | |
| sede_id | int FK sede | |
| materia_codigo | varchar FK materia | |
| turno | varchar(50) | |
| horario_desde / horario_hasta | time | |
| dias | text[] | nullable |
| regimen / idioma / modalidad | varchar(50) | nullable |
| packs | text[] | nullable |

## clase_docente

| Columna | Tipo | Notas |
|---|---|---|
| clase_id | bigint FK clase | PK compuesta |
| docente_id | int FK docente | PK compuesta |

## docente

| Columna | Tipo | Notas |
|---|---|---|
| id | serial PK | |
| nombre | varchar(255) | |

## periodo

| Columna | Tipo | Notas |
|---|---|---|
| id | serial PK | |
| ciclo | varchar(50) | |
| nombre | varchar(255) | |
| fecha_desde / fecha_hasta | date | |
