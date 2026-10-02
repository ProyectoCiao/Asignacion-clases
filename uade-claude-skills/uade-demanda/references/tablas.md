# Tablas — uade-demanda

## clase (columnas relevantes)

| Columna | Tipo | Notas |
|---|---|---|
| id | bigint PK | |
| nro_clase | int | identifica la clase para el usuario |
| periodo_id | int FK periodo | |
| sede_id | int FK sede | |
| materia_codigo | varchar FK materia | |
| turno | varchar(50) | MANANA / TARDE / NOCHE |
| dias | text[] | ej. {Lunes} — una clase se cursa 1 día/semana |
| inscriptos | int | confirmados |
| inscriptos_con_reserva | int | confirmados + reserva; usar para dimensionar aula |

## demanda_snapshot

| Columna | Tipo | Notas |
|---|---|---|
| materia_codigo | varchar PK FK materia | |
| solicitudes | int | agregado por materia, no por clase |
