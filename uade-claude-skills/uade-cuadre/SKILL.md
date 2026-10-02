---
name: uade-cuadre
description: Asigna o sugiere aula para una clase de UADE, calcula sillas vacías, detecta choques de horario/aula, arma el mapa de aulas y evalúa cupo. Usar cuando el usuario hable de asignar aula, sugerir aula, sillas vacías, choques, "qué aula uso", mapa de aulas, cupo, o pida el Excel/planilla de clases con aula asignada. Este es el único skill que asigna — orquesta a uade-demanda, uade-aulas y uade-oferta como fuentes de conocimiento.
---

# uade-cuadre

Sos el único skill que asigna aula. `uade-demanda`, `uade-aulas` y `uade-oferta` son tus fuentes de datos — no repitas su lógica, consultalos.

## Pasos

1. **Identificar la clase**: `nro_clase`, o materia + sede + turno + día. Si falta algo y hay ambigüedad, preguntar antes de seguir.
2. **Pedir datos bloqueantes si faltan**: inscriptos/inscriptos_con_reserva (vía `uade-demanda`), sede y turno de la clase (vía `uade-oferta`). Sin esto no se puede filtrar.
3. **Filtrar HARD** (elimina, no puntúa) usando `uade-aulas` como conocimiento:

| Filtro HARD | Motivo de descarte |
|---|---|
| `capacidad_total` < inscriptos_con_reserva | No entran los alumnos |
| `sede_id` distinta a la de la clase | Aula de otra sede |
| `activa = false` | Aula no disponible |
| Choque de horario/día con otra clase ya asignada a esa aula | Conflicto — dos clases, misma aula, mismo día, horario solapado |
| `tipo_grupo` incompatible con lo pedido (ej. se pide LAB y el aula es AULA común) | No cumple el tipo requerido |

4. **Puntuar SOFT** entre las que pasaron el filtro HARD, de mejor a peor:
   - Menor sobrante de sillas vacías (`capacidad_total - inscriptos_con_reserva`) gana — el ajuste más justo sin pasarse.
   - Mismo piso/edificio que otras clases de la misma materia ese día, si aplica.
   - Equipamiento que la clase necesite, si se especificó.
5. **Devolver**: 1 recomendada + hasta 2 alternativas + bloqueadas con motivo (nunca ocultar por qué se descartó una).

## Formato de respuesta

```
Clase {nro} | {materia} | {día} {horario} | {insc} insc.
Recomendada: {codigo} (cap {total}) — {vacias} sillas vacías
Alt 1: {codigo} (cap {total}) — {vacias} sillas vacías
Alt 2: {codigo} (cap {total}) — {vacias} sillas vacías
Bloqueadas: {codigo} — {motivo}; {codigo} — {motivo}
```

Si ninguna aula pasa el filtro HARD, decilo explícitamente — no fuerces una recomendación que no cumple capacidad o sede.

## Qué no hacer

- No asignar sin haber filtrado HARD por capacidad, sede y choques primero.
- No recomendar un aula con menos capacidad que `inscriptos_con_reserva`, nunca.
- No cambiar día, horario o docente de la clase — eso es dato fijo de `uade-oferta`.
- No inventar aulas que no estén en `uade-aulas`.
- Si piden SQL: `SELECT`/`UPDATE` contra `clase`, `aula`, `asignacion` — nunca `DROP`, nunca contra tablas fuera de este dominio.

Ver `references/tablas.md` para las columnas completas.
