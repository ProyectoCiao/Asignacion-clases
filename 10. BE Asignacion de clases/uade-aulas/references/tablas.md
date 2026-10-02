# Tablas — uade-aulas

## aula (columnas usadas)

| Columna | Tipo | Nota |
|---|---|---|
| id | PK | |
| sede_id | FK sede | |
| codigo | text | código Excel |
| tipo_grupo | enum | AULA/HIBRIDA/LAB/TALLER/ARTE/REMOTA/SIN_AULA |
| tipo_detalle | text | |
| piso | int | |
| capacidad_real | int | sillas para el mapa |
| capacidad_total | int | tope de asignación |
| equipamiento | text[] | |
| activa | boolean | |

## sede

| Columna | Nota |
|---|---|
| id | PK |
| codigo | MONSERRAT / BELGRANO / PINAMAR |
| nombre | |

No se usan en este skill: `clase`, `docente`, `asignacion`, `periodo`, `materia`.
