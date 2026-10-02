---
name: uade-aulas
description: Consulta el inventario físico de aulas de la base uade_aulas — capacidad, tipo de espacio, piso, equipamiento y sede — y explica cómo leer un código de aula del Excel (ej. 333-AULA, 635-HÍBRIDA). Usar cuando preguntan qué aulas hay en un piso, la capacidad o el tipo de un aula puntual, o qué significa un código de aula. No asigna clases a aulas ni resuelve el cupo de una clase concreta.
---

# uade-aulas

Rol: inventario y lectura de códigos de aula. No decide qué clase va en cada una.

## Tabla `aula` (uso principal)

| Columna | Nota |
|---|---|
| sede_id | FK sede |
| codigo | código Excel, ver parseo abajo |
| tipo_grupo | AULA / HIBRIDA / LAB / TALLER / ARTE / REMOTA / SIN_AULA |
| tipo_detalle | detalle textual (ej. "LAB MULTIPLATAFORMA-MAC") |
| piso | |
| capacidad_real | sillas físicas — usar solo para dibujar el mapa |
| capacidad_total | **tope para asignar clases** |
| equipamiento | text[] |
| activa | boolean |

## Cómo parsear `codigo`

| Patrón | Ejemplo | Lectura |
|---|---|---|
| NNN-TIPO | 333-AULA, 635-HÍBRIDA | número de aula + tipo |
| sNNN-TIPO | s234-AULA | aula en subsuelo |
| NNN-LAB ... | 165-LAB MULTIPLATAFORMA-MAC | laboratorio, detalle en tipo_detalle |
| CLASE REMOTA | — | sin ubicación física |
| - | — | SIN_AULA, sin aula asignable |

## Mapa físico vs no-mapa

- Van al mapa: `tipo_grupo` en AULA, HIBRIDA, LAB, TALLER, ARTE (con `capacidad_real > 0` y `activa = true`).
- No van al mapa: `tipo_grupo` = REMOTA o SIN_AULA.

## Alcance (sede)

- MVP activo: sede = MONSERRAT.
- Próximamente: BELGRANO.
- PINAMAR puede existir en los datos pero no forma parte del mapa/alcance de producto — si aparece, aclararlo y no incluirla en listados ni sugerencias salvo pedido explícito.

## Consulta tipo

```sql
SELECT codigo, tipo_grupo, tipo_detalle, piso, capacidad_real, capacidad_total, equipamiento
FROM aula
WHERE sede_id = (SELECT id FROM sede WHERE codigo = 'MONSERRAT')
  AND activa = true
  AND piso = 3
ORDER BY capacidad_total;
```

## Qué no hacer

- No asignar ni sugerir qué clase va en qué aula (usar **uade-cuadre**).
- No evaluar choques de horario ni cupo de una clase específica (requiere `clase`/`asignacion`, dominio de uade-cuadre).
- No incluir aulas de PINAMAR en el mapa MVP salvo pedido explícito.
- No incluir REMOTA/SIN_AULA en un listado de "mapa de aulas".
