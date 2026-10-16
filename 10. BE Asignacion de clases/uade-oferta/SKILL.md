---
name: uade-oferta
description: Consulta la oferta académica de la planificación cargada (tabla planificacion_excel) — materia, sede, turno, días, horario, régimen, idioma, modalidad, packs y docentes de cada clase. Usar cuando preguntan qué clases hay de una materia, en qué día/horario cursa una comisión, quién es el docente de una clase, o datos del cuatrimestre cargado. No asigna aulas, no cambia día, horario ni docente.
---

# uade-oferta

Rol: leer la oferta de clases tal como está en la planificación. Día, turno, horario y docente son **INPUT**: no se cuestionan ni se optimizan.

## Fuente

`planificacion_excel` tiene **una sola fila** (cada carga la reemplaza): `nombre_archivo`, `subido_en`, `subido_por`, `anio`, `cuatrimestre`, `encabezados jsonb`, `filas jsonb` (texto tal cual el Excel). Cada fila del Excel es una clase.

No hay tablas `clase`, `docente`, `periodo` con datos de la oferta: los docentes y las clases cambian cada cuatrimestre y vienen del Excel.

## Columnas del Excel (por encabezado)

| Encabezado | Dato |
|---|---|
| **Id** | identificador único de la clase (único en todo el archivo) |
| Clase | número de clase; **no** es único (se repite entre sedes) |
| Facultad, Departamento, Sede | |
| Ciclo, Periodo | año y período (VERANO, 1er CUATR…) |
| Desde, Hasta | fechas del período (calendario, no el día de cursada) |
| Turno, Horario | `14:00 - 18:00`; conviven distintos horarios |
| Dias De Cursada | `Lunes - Martes - …` |
| CódigoMateria, Materia, CargaHorariaMateria | |
| Modalidad, Idioma, Nivel Idioma, Régimen, Tipo, Packs Asociados | |
| Aula Principal, Capacidad Real, Capacidad Total | aula del Excel; `-` = sin aula |
| Docentes | texto `Apellido, Nombre` (varios separados) |
| Inscriptos Por Clase… | **no es demanda** (ver uade-demanda) |

Leer por la API: `GET /planificacion-inicial?pagina&tamanio(≤200)&sede&turno&facultad&q` (filtros sin acentos, repetir el parámetro para varios valores). Los encabezados se reconocen sin importar mayúsculas ni acentos.

## Qué no hacer

- No reasignar, sugerir ni predecir docente.
- No cambiar día, turno ni horario "para optimizar".
- No confundir `Desde/Hasta` (calendario del período) con el día de cursada semanal.
- No identificar una clase por `Clase`: usar `Id`.
- No usar los inscriptos de la planificación como demanda.
- No asignar aula (usar **uade-cuadre**).
- No trabajar a nivel alumno individual.
