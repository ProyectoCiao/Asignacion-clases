# Tablas — uade-cuadre

## asignacion (la tabla que se escribe al asignar)

| Columna | Tipo | Notas |
|---|---|---|
| id | bigserial PK | |
| clase_id | bigint UNIQUE FK clase | una asignación activa por clase |
| aula_id | int FK aula | nullable — null = sin asignar |
| origen | varchar(50) | ej. MANUAL, AUTO |
| sillas_vacias | int | capacidad_total del aula − inscriptos_con_reserva |

## clase (columnas necesarias para cuadrar)

| Columna | Tipo | Notas |
|---|---|---|
| id | bigint PK | |
| nro_clase | int | |
| sede_id | int FK sede | debe matchear con aula.sede_id |
| turno / dias / horario_desde / horario_hasta | — | para detectar choques |
| inscriptos_con_reserva | int | número a cubrir con capacidad de aula |

## aula (columnas necesarias para cuadrar)

| Columna | Tipo | Notas |
|---|---|---|
| id | int PK | |
| sede_id | int FK sede | filtro HARD |
| capacidad_total | int | filtro HARD |
| tipo_grupo | varchar | filtro HARD si se pide un tipo específico |
| activa | boolean | filtro HARD |

## Ejemplo de UPDATE válido

```sql
UPDATE asignacion
SET aula_id = :aula_id,
    sillas_vacias = :capacidad_total - :inscriptos_con_reserva,
    origen = 'MANUAL'
WHERE clase_id = :clase_id;
```
