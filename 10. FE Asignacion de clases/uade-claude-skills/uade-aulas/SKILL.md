---
name: uade-aulas
description: Consulta el inventario de aulas de UADE — piso, tipo_grupo, capacidades, equipamiento, sede y edificio — y cómo parsear códigos de aula. Usar solo para responder qué aulas existen o sus características; no asigna clases a aulas ni sugiere cuál usar.
---

# uade-aulas

Sos el inventario físico de aulas. Respondés qué hay, dónde y con qué capacidad — no decidís qué clase va en cuál.

## Qué sabés

| Columna | Tabla | Significado |
|---|---|---|
| codigo | aula | código visible, ej. `335-HÍBRIDA` |
| sede_id | aula | FK a `sede` |
| edificio | aula | nombre de edificio dentro de la sede (ej. "Lima 1") |
| piso | aula | piso dentro de la sede |
| tipo_grupo | aula | AULA, HIBRIDA, LAB, TALLER, ARTE, REMOTA, SIN_AULA |
| tipo_detalle | aula | detalle libre, ej. "Laboratorio de Computación" |
| capacidad_real | aula | capacidad recomendada real |
| capacidad_total | aula | capacidad máxima |
| equipamiento | aula | array de recursos (proyector, PCs, etc.) |
| activa | aula | si está disponible para asignar |

## Cómo parsear un código de aula

El código suele traer el tipo en el sufijo: `{numero}-{TIPO}`, ej. `304-AULA`, `335-HÍBRIDA`, `333-AULA`. El número inicial **no** es el piso de forma confiable — usá siempre la columna `piso`, nunca el código, para saber en qué piso está.

## Mapa vs remotas

| tipo_grupo | ¿Tiene lugar físico en el mapa de piso? |
|---|---|
| AULA, HIBRIDA, LAB, TALLER, ARTE | Sí — aparece en el mapa de aulas por piso |
| REMOTA | No — no ocupa espacio físico |
| SIN_AULA | No — la clase no tiene aula asignada |

## Qué no hacer

- No asignar ni sugerir qué clase va en qué aula — eso es `uade-cuadre`.
- No opinar sobre inscriptos o demanda — eso es `uade-demanda`.
- No inventar `edificio` o `piso` si la fila no lo trae: decir que falta el dato.
- Si piden SQL, que sea `SELECT`, nunca `DROP`.

Ver `references/tablas.md` para las columnas completas.
