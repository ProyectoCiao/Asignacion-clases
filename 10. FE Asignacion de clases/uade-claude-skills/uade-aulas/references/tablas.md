# Tablas — uade-aulas

## aula

| Columna | Tipo | Notas |
|---|---|---|
| id | serial PK | |
| sede_id | int FK sede | |
| codigo | varchar(50) | |
| tipo_grupo | varchar(50) | AULA / HIBRIDA / LAB / TALLER / ARTE / REMOTA / SIN_AULA |
| tipo_detalle | varchar(255) | nullable |
| piso | int | |
| capacidad_real | int | |
| capacidad_total | int | |
| equipamiento | text[] | nullable |
| activa | boolean | default true |
| edificio | varchar | nullable |

## sede

| Columna | Tipo | Notas |
|---|---|---|
| id | serial PK | |
| codigo | varchar(50) unique | |
| nombre | varchar(255) | |
