# Tablas — uade-demanda

## clase (columnas usadas)

| Columna | Tipo | Nota |
|---|---|---|
| id | bigint PK | = Id del Excel |
| nro_clase | | identificador visible para el usuario |
| periodo_id | FK periodo | |
| sede_id | FK sede | |
| materia_codigo | FK materia | |
| turno | MANANA / TARDE / NOCHE | |
| dias | text[] | ej. {Lunes} |
| horario_desde / horario_hasta | | |
| inscriptos | int | dato bruto |
| inscriptos_con_reserva | int | **demanda real del MVP** |

No se usan en este skill: `aula`, `docente`, `asignacion`, `periodo` (salvo como filtro), `materia` (salvo código).
