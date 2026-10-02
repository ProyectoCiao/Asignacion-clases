---
name: uade-cuadre
description: Asigna o sugiere el aula física de una clase en la base uade_aulas aplicando reglas de cupo, tipo de espacio, choques de aula/docente y sillas vacías. Usar cuando piden asignar aula, sugerir aula, "qué aula uso", resolver un choque de aulas, calcular sillas vacías, armar o revisar el mapa de aulas, o procesar el Excel de clases. Es el único skill que asigna — orquesta a uade-demanda, uade-aulas y uade-oferta como fuentes de datos. Alcance: Campus Buenos Aires (Monserrat ahora, Belgrano después).
---

# uade-cuadre

Rol: **el único skill que asigna aula.** Orquesta:

- **uade-demanda** → `inscriptos_con_reserva`
- **uade-aulas** → inventario y capacidad
- **uade-oferta** → día, horario, sede, docente (todos INPUT, no se tocan)

Día, turno, horario y docente ya están definidos antes de entrar acá. Este skill solo responde: ¿en qué aula entra esta clase?

## Paso 1 — Identificar la clase

Necesita: clase (id o nro_clase), materia, sede, día(s), horario_desde/hasta, inscriptos_con_reserva.
Si falta algo bloqueante, preguntarlo antes de sugerir aula. No inventar valores.

## Paso 2 — Filtro HARD (elimina aulas, no rankea)

| # | Regla | Chequeo |
|---|---|---|
| 1 | Cupo | inscriptos_con_reserva <= aula.capacidad_total |
| 2 | Sede | aula.sede_id = clase.sede_id (Monserrat no va a Belgrano) |
| 3 | Choque de aula | mismo aula_id, día en común (clase.dias) Y horario_desde_A < horario_hasta_B AND horario_desde_B < horario_hasta_A |
| 4 | Choque de docente | mismo docente (clase_docente), mismo criterio de solape que #3, en cualquier aula |
| 5 | Remoto no ocupa mapa | REMOTO / CLASE REMOTA / SIN_AULA → asignacion.aula_id = NULL, no pasan por este filtro |
| 6 | No degradar espacio específico | si la clase ya está en LAB/TALLER/ARTE, no bajarla a AULA común salvo pedido explícito del usuario |
| 7 | No mover turno | no reasignar una clase de NOCHE a MAÑANA (ni ningún otro turno) como optimización |

Cualquier aula que falle una regla HARD va a "Bloqueadas" con el motivo puntual.

## Paso 3 — Ranking SOFT (desempate entre las que sobrevivieron)

| Prioridad | Criterio | Objetivo |
|---|---|---|
| 1 | sillas_vacias = capacidad_total - inscriptos_con_reserva | minimizar |
| 2 | origen EXCEL | preferir conservar el aula del Excel si sigue siendo viable |
| 3 | tipo_grupo específico | preferir HIBRIDA/LAB/TALLER/ARTE si la clase ya usaba ese tipo |
| 4 | tamaño relativo | evitar mandar una comisión chica a un aula de 70+ si hay una de 30-45 libre |

## Paso 4 — Formato de respuesta (usar siempre este formato)

```
Clase {nro} | {materia} | {día} {horario} | {insc} insc.
Recomendada: {codigo} (cap {total}) — {vacias} sillas vacías
Alt 1: {codigo} (cap {total}) — {vacias} sillas vacías
Alt 2: {codigo} (cap {total}) — {vacias} sillas vacías
Bloqueadas: {codigo} ({motivo}), {codigo} ({motivo}), ...
```

Si no hay ninguna aula viable, decirlo explícitamente y listar todas las bloqueadas con motivo — no inventar una recomendación.

## SQL

Candidatos (SELECT, filtra HARD 1-2):

```sql
SELECT a.id, a.codigo, a.tipo_grupo, a.capacidad_total,
       a.capacidad_total - :inscriptos_con_reserva AS sillas_vacias
FROM aula a
WHERE a.sede_id = :sede_id
  AND a.activa = true
  AND a.capacidad_total >= :inscriptos_con_reserva
ORDER BY sillas_vacias;
```

Choques de aula (HARD 3):

```sql
SELECT c2.nro_clase
FROM asignacion asg
JOIN clase c2 ON c2.id = asg.clase_id
WHERE asg.aula_id = :aula_id_candidata
  AND c2.dias && :dias_clase_nueva
  AND c2.horario_desde < :horario_hasta_nueva
  AND :horario_desde_nueva < c2.horario_hasta;
```

Choques de docente (HARD 4):

```sql
SELECT c2.nro_clase
FROM clase_docente cd2
JOIN clase c2 ON c2.id = cd2.clase_id
WHERE cd2.docente_id = :docente_id
  AND c2.id <> :clase_id
  AND c2.dias && :dias_clase_nueva
  AND c2.horario_desde < :horario_hasta_nueva
  AND :horario_desde_nueva < c2.horario_hasta;
```

Confirmar asignación (UPDATE, nunca DROP):

```sql
UPDATE asignacion
SET aula_id = :aula_id_elegida,
    origen = 'SUGERIDA',
    sillas_vacias = :capacidad_total - :inscriptos_con_reserva
WHERE clase_id = :clase_id;
```

Si la clase todavía no tiene fila en `asignacion`, insertarla en vez de actualizar (`clase_id` es unique).

## Qué no hacer

- No predecir, sugerir ni reasignar docente — eso es del motor ML de otro equipo (afinidad ≠ probabilidad de éxito).
- No cambiar día, turno ni horario: son INPUT.
- No asignar a nivel alumno individual; la unidad es la clase/comisión.
- No incluir aulas de PINAMAR en las sugerencias del MVP.
- No bajar una clase de LAB/TALLER/ARTE a AULA común sin pedido explícito.
- No mover una clase de NOCHE a MAÑANA (ni entre turnos) como "optimización".
- No asignar aula a clases REMOTO/CLASE REMOTA/SIN_AULA (van con aula_id = NULL).
- No ejecutar DROP ni borrar filas de `asignacion`; solo SELECT/UPDATE/INSERT sobre `asignacion`.
- No recomendar un aula que no pasó los 7 filtros HARD.
