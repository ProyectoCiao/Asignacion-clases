---
name: uade-demanda
description: Consulta la demanda real de una clase en la base uade_aulas (inscriptos e inscriptos_con_reserva). Usar cuando preguntan cuántos inscriptos/alumnos tiene una clase, dan un número de inscriptos ("esta tiene 29"), o piden validar la demanda antes de cuadrar un aula. No asigna ni sugiere aulas.
---

# uade-demanda

Rol: informar y validar la demanda de una clase. No decide aula.

## Qué sabe

| Dato | Columna | Uso |
|---|---|---|
| Inscriptos totales | `clase.inscriptos` | referencia informativa |
| Demanda real (MVP) | `clase.inscriptos_con_reserva` | número que se usa para cuadrar aula |

Regla: si el usuario da un número de "inscriptos" sin aclarar, asumir que se refiere a `inscriptos_con_reserva` salvo que diga lo contrario. Si `inscriptos` e `inscriptos_con_reserva` difieren, mostrar los dos valores y usar siempre `inscriptos_con_reserva` para cualquier cálculo de cupo.

## Cómo identificar la clase

Pedir uno de estos si falta:
- `clase.id` (bigint, = Id del Excel), o
- `clase.nro_clase` + período (o `materia_codigo` + sede)

## Consulta tipo

```sql
SELECT id, nro_clase, materia_codigo, sede_id, turno, dias,
       horario_desde, horario_hasta, inscriptos, inscriptos_con_reserva
FROM clase
WHERE nro_clase = :nro_clase;
```

## Qué preguntar si falta info

- `nro_clase` o `clase.id`
- período (si hay más de un resultado con el mismo nro_clase)
- si el número que da el usuario es `inscriptos` o `inscriptos_con_reserva`; si no coincide con la base, avisar la diferencia y no sobreescribir sin confirmación explícita

## Qué no hacer

- No sugerir ni asignar aula (usar **uade-cuadre**).
- No promediar, proyectar ni estimar demanda futura: solo reporta el dato ya cargado.
- No tocar la tabla `asignacion`.
- No mezclar `inscriptos` con `inscriptos_con_reserva` sin aclarar cuál es cuál.
- No trabajar a nivel alumno individual (solicitudes individuales están fuera de alcance).
