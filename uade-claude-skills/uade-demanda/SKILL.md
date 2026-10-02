---
name: uade-demanda
description: Consulta inscriptos e inscriptos_con_reserva por clase en el sistema de aulas UADE, y qué preguntar cuando un pedido da un número suelto (ej. "esta tiene 29"). Usar solo para leer o interpretar demanda de inscripción por clase; no resuelve ni sugiere aula.
---

# uade-demanda

Interpretá pedidos de demanda de inscripción por clase. No asignás aula ni tomás decisiones de cuadre — eso es trabajo de `uade-cuadre`.

## Qué sabés

| Columna | Tabla | Significado |
|---|---|---|
| `inscriptos` | `clase` | Inscriptos confirmados a la clase |
| `inscriptos_con_reserva` | `clase` | Confirmados + lista de espera/reserva; es el número que se usa para dimensionar aula |
| `solicitudes` | `demanda_snapshot` | Total de solicitudes por materia (agregado, no por clase) |

## Cómo leer un pedido

Un pedido como "esta tiene 29" es ambiguo. Antes de responder algo con ese número, identificá:

| Falta preguntar | Por qué importa |
|---|---|
| ¿Es `inscriptos` o `inscriptos_con_reserva`? | Determinan capacidades de aula distintas |
| ¿A qué `nro_clase` corresponde? | `inscriptos` vive en `clase`, no en la materia |
| ¿Es de la clase completa o de un turno/día puntual? | Una materia puede tener varias clases (`clase.turno`, `clase.dias`) |

Si el usuario no da `nro_clase` ni materia+sede+turno+día para identificar la clase, pedilo antes de asumir cuál es.

## Qué no hacer

- No sugerir ni descartar aulas — no conocés `aula` en detalle (delegá a `uade-aulas`).
- No decidir si conviene una clase u otra — eso es `uade-cuadre`.
- No mezclar `solicitudes` (por materia) con `inscriptos_con_reserva` (por clase) como si fueran lo mismo.
- No escribir en la base sin que te lo pidan explícitamente; si piden SQL, que sea `SELECT`, nunca `DROP`.

Ver `references/tablas.md` para las columnas completas.
