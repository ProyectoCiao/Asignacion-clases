---
name: uade-oferta
description: Consulta la oferta académica de un período en la base uade_aulas — materia, sede, turno, día, horario, régimen, idioma, modalidad, packs y el docente ya asignado a la clase. Usar cuando preguntan qué clases hay de una materia, en qué día/horario cursa una comisión, quién es el docente de una clase, o datos de un período/cuatrimestre. No asigna aulas, no cambia día, horario ni docente.
---

# uade-oferta

Rol: leer la oferta de clases del período (día, horario, docente, régimen) tal como está definida. Día, turno, horario y docente son **INPUT** — no se cuestionan ni se optimizan acá.

## Tabla `clase` (datos de oferta)

| Columna | Nota |
|---|---|
| id / nro_clase | identificadores |
| periodo_id | FK periodo |
| sede_id | FK sede |
| materia_codigo | FK materia |
| turno | MANANA/TARDE/NOCHE |
| horario_desde / horario_hasta | no es grilla fija, conviven distintos horarios |
| dias | text[], ej. {Lunes,Miercoles} |
| regimen | SEMANAL / ALEKS / REMOTO |
| idioma, modalidad | |
| packs | text[] |

## Docente asignado (dato, no modelo)

```sql
SELECT d.nombre
FROM clase_docente cd
JOIN docente d ON d.id = cd.docente_id
WHERE cd.clase_id = :clase_id;
```

El docente que devuelve esta consulta es un dato ya definido por el motor de afinidad docente↔clase de otro equipo. Reportarlo tal cual; no proponer cambios ni alternativas.

## Tabla `periodo`

| Columna | Nota |
|---|---|
| ciclo, nombre | |
| fecha_desde, fecha_hasta | **restricción de calendario** del cuatrimestre, no define el día de cursada semanal (eso es `clase.dias`) |

## Tabla `materia`

| Columna | Nota |
|---|---|
| codigo, nombre | |
| facultad | FACE/FACO/FAIN/FADI/FAJU/FASA/FARU |
| departamento, carga_horaria | |

## Consulta tipo

```sql
SELECT c.nro_clase, m.nombre AS materia, s.codigo AS sede, c.turno,
       c.dias, c.horario_desde, c.horario_hasta, c.regimen, c.packs
FROM clase c
JOIN materia m ON m.codigo = c.materia_codigo
JOIN sede s ON s.id = c.sede_id
WHERE m.codigo = :materia_codigo AND c.periodo_id = :periodo_id;
```

## Qué no hacer

- No reasignar, sugerir ni predecir docente (es del motor ML de otro equipo; compatibilidad ≠ probabilidad de éxito).
- No cambiar día, turno ni horario "para optimizar".
- No confundir `periodo` (fechas de calendario) con el día de cursada semanal (`clase.dias`).
- No asignar aula (usar **uade-cuadre**).
- No trabajar a nivel alumno individual.
