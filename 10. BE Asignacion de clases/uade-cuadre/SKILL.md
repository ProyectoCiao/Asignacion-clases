---
name: uade-cuadre
description: Sugiere el aula física de una clase de la planificación cargada aplicando reglas de cupo, tipo de espacio, choques de aula/docente y sillas vacías. Usar cuando piden asignar aula, sugerir aula, "qué aula uso", resolver un choque de aulas, calcular sillas vacías o revisar el mapa de aulas. Es el único skill que asigna — orquesta a uade-demanda, uade-aulas y uade-oferta como fuentes de datos.
---

# uade-cuadre

Rol: **el único skill que sugiere aula.** Orquesta:

- **uade-oferta** → la clase (`Id`, sede, días, horario, docentes, aula actual). INPUT, no se toca.
- **uade-demanda** → inscriptos reales (solo `demanda_excel`).
- **uade-aulas** → inventario y capacidad (`aulas`).

Responde una sola pregunta: ¿en qué aula entra esta clase?

## Modelo de datos

- Clases: filas de `planificacion_excel.filas`; clave = columna **Id**.
- Aula actual de una clase = la edición en `asignacion_aula` si existe; si no, "Aula Principal" del Excel cruzada con `aulas.aula_nombre` (por `UPPER(TRIM())`). El Excel original **nunca se modifica**.
- Cambios: se guardan en `asignacion_aula` (clase Id → `aula_id`, quién y cuándo) y se registran en `log_acciones`.
- Demanda: `demanda_excel` (ver uade-demanda). Sin demanda cargada no se puede validar cupo: decirlo y no recomendar.

API: `GET /clases` (clase con aula efectiva, demanda y sillas vacías), `PUT /clases/{id}/aula` {aulaId} con `X-Usuario`, `DELETE /clases/{id}/aula` (vuelve al Excel). El PUT valida: 409 por CHOQUE_AULA o CUPO (`forzarCupo: true` acepta el sobrecupo) y devuelve `advertencias` (CHOQUE_DOCENTE); este skill igual propone solo aulas que pasan los filtros HARD. La demanda por clase exige que el Excel de demanda traiga "Id Clase".

## Paso 1 — Identificar la clase

Necesita: `Id`, sede, día(s), horario, docentes, demanda. Si falta algo bloqueante, preguntarlo. No inventar valores.

## Paso 2 — Filtro HARD (elimina aulas, no rankea)

| # | Regla | Chequeo |
|---|---|---|
| 1 | Cupo | demanda <= `aula_capacidad_real` |
| 2 | Sede | `aulas.sede_id` = sede de la clase |
| 3 | Aula activa | tipo de aula distinto de INACTIVA/ELIMINADO |
| 4 | Choque de aula | mismo aula, día en común y horarios superpuestos (desde_A < hasta_B Y desde_B < hasta_A), contra el estado final (ediciones aplicadas sobre el Excel) |
| 5 | Choque de docente | mismo docente, mismo criterio de solape, en cualquier aula |
| 6 | Remoto no ocupa mapa | aula `-` / CLASE REMOTA / modalidad remota no pasan por este filtro |
| 7 | No degradar espacio específico | si la clase ya está en LAB/TALLER/ARTE, no bajarla a AULA común salvo pedido explícito |
| 8 | No mover turno | no reasignar entre turnos como optimización |

Cualquier aula que falle va a "Bloqueadas" con el motivo puntual.

## Paso 3 — Ranking SOFT

| Prioridad | Criterio | Objetivo |
|---|---|---|
| 1 | sillas vacías = capacidad − demanda | minimizar |
| 2 | aula del Excel original | conservarla si sigue siendo viable |
| 3 | tipo específico | preferir HÍBRIDA/LAB/TALLER/ARTE si la clase ya usaba ese tipo |
| 4 | tamaño relativo | no mandar una comisión chica a un aula de 70+ si hay una de 30–45 libre |

## Paso 4 — Formato de respuesta

```
Clase {Id} | {materia} | {días} {horario} | {demanda} insc.
Recomendada: {aula} (cap {cap}) — {vacias} sillas vacías
Alt 1: {aula} (cap {cap}) — {vacias} sillas vacías
Alt 2: {aula} (cap {cap}) — {vacias} sillas vacías
Bloqueadas: {aula} ({motivo}), ...
```

Si no hay aula viable, decirlo y listar todas las bloqueadas con motivo.

## Qué no hacer

- No sugerir ni reasignar docente.
- No cambiar día, turno ni horario: son INPUT.
- No tocar el Excel guardado (`planificacion_excel`): las ediciones van en `asignacion_aula`.
- No usar los inscriptos de la planificación como demanda.
- No asignar aula a clases remotas / sin aula.
- No recomendar un aula que no pasó los filtros HARD.
- No ejecutar DROP ni borrar filas; solo INSERT/UPDATE sobre `asignacion_aula`.
