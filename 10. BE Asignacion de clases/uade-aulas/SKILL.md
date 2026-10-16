---
name: uade-aulas
description: Consulta el inventario físico de aulas de la base uade_aulas (tabla aulas) — capacidad, tipo, piso, edificio y sede — y explica cómo leer un nombre de aula del Excel (ej. 333-AULA, 635-HÍBRIDA). Usar cuando preguntan qué aulas hay en un piso, la capacidad o el tipo de un aula puntual, o qué significa un nombre de aula. No asigna clases a aulas ni resuelve el cupo de una clase concreta.
---

# uade-aulas

Rol: inventario y lectura de nombres de aula. No decide qué clase va en cada una.

## Tablas

`aulas`

| Columna | Nota |
|---|---|
| id | |
| aula_nombre | igual al "Aula Principal" del Excel de planificación (ej. `107-AULA`); es la clave de cruce |
| aula_capacidad_real | sillas físicas; tope para ubicar una clase |
| aula_piso | texto: numéricos y letras (A, B, M, P, s, S, SUM…); `s` y `S` son pisos distintos |
| sede_id | → `sedes(id, nombre_sede)` |
| edificio_id | → `edificios(id, edificio_nombre, sede_id)` |
| aula_tipo_id | → `aula_tipo(id, aula_tipo)`; el nombre trae espacios, usar TRIM. Activa = tipo distinto de INACTIVA/ELIMINADO |

Datos incompletos: algunas aulas no tienen sede, edificio o tipo.

## Cómo leer `aula_nombre`

| Patrón | Ejemplo | Lectura |
|---|---|---|
| NNN-TIPO | 333-AULA, 635-HÍBRIDA | número de aula + tipo |
| sNNN-TIPO | s234-AULA | subsuelo |
| NNN-LAB … | 165-LAB MULTIPLATAFORMA-MAC | laboratorio |
| CLASE REMOTA | — | sin ubicación física (sede MS TEAMS) |
| `-` | — | la clase no tiene aula en el Excel; no cruza con `aulas` |

El cruce Excel ↔ `aulas` es por `UPPER(TRIM(aula_nombre))`. En la planificación actual todas las aulas con nombre cruzan; las filas con `-` quedan sin aula.

## Consulta

Por API: `GET /maestros/aulas?sede&piso&edificio&tipo` (repetir el parámetro para varios valores), `/maestros/pisos`, `/maestros/edificios`, `/maestros/tipos-aula`, `/maestros/sedes`.

```sql
SELECT a.aula_nombre, a.aula_capacidad_real, a.aula_piso, e.edificio_nombre, TRIM(t.aula_tipo) AS tipo
FROM aulas a
LEFT JOIN edificios e ON e.id = a.edificio_id
LEFT JOIN aula_tipo t ON t.id = a.aula_tipo_id
WHERE a.sede_id = :sede_id AND a.aula_piso = :piso
ORDER BY a.aula_capacidad_real;
```

## Qué no hacer

- No asignar ni sugerir qué clase va en qué aula (usar **uade-cuadre**).
- No evaluar choques ni cupo de una clase (requiere la planificación y la demanda).
- No incluir "CLASE REMOTA" ni aulas inactivas en un listado de mapa.
- No asumir que Online tiene aulas: no tiene.
