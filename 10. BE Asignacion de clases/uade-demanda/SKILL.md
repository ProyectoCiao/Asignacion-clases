---
name: uade-demanda
description: Consulta la demanda real de una clase a partir del archivo de alumnos inscriptos cargado en el tab Demanda (tabla demanda_excel). Usar cuando preguntan cuántos inscriptos/alumnos tiene una clase o una materia, dan un número de inscriptos ("esta tiene 29"), o piden validar la demanda antes de cuadrar un aula. No asigna ni sugiere aulas.
---

# uade-demanda

Rol: informar la demanda (ocupación) de una clase. No decide aula.

## Fuente única de demanda

La demanda sale **solo** de la tabla `demanda_excel` (Excel de alumnos inscriptos subido en el tab Demanda; cada subida reemplaza a la anterior).

Las columnas de inscriptos de la planificación (`Inscriptos Por Clase…` en `planificacion_excel`) **no cuentan como demanda**: la planificación solo sirve para replicar materia, aula, turno, sede, etc. Si difieren, manda `demanda_excel`; no mezclar ni promediar.

## Modelo

`demanda_excel` guarda una sola fila: `encabezados jsonb` y `filas jsonb` (texto tal cual el Excel; una fila = una inscripción de un alumno a una materia).

- Inscripciones = cantidad de filas que cumplen el filtro.
- Alumnos = legajos distintos (columna cuyo encabezado contiene "legajo").
- Se consulta por la API: `POST /demanda-excel/totales` {filtros, agruparPor, ordenarPor, pagina, tamanio} y `POST /demanda-excel/valores` {columna, filtros}. Filtros por índice de columna, sin acentos; columnas distintas = Y, misma columna = O. Sin filtros devuelve 400.

## Alumnos distribuidos (`asignacion_alumno`)

El botón "Asignar alumnos a aulas" del Mapa copia la demanda a `asignacion_alumno` (legajo, `clase_id`, `opcion`): opción 1 = clase de "Id Clase", opción 2 = clase de "Id Clase 2da Opcion". `GET /clases` devuelve `asignados1/asignados2` por clase y `GET /asignacion-alumnos` el resumen. La segunda opción es **tentativa**: no es una inscripción firme, ocupa silla "en rojo tramado". Si se pide la demanda de una clase, informar las dos cifras por separado.

## Cómo asociar demanda a una clase

El archivo de alumnos no trae el `Id` de la clase: hay que filtrar por los datos de la clase (código de materia, sede, días y horario, y docente si hace falta) tomados de la planificación. **El criterio exacto de cruce está pendiente de definir con el usuario**: no inventarlo; si hay ambigüedad (más de una clase que encaja), decirlo.

## Qué preguntar si falta info

- Clase (`Id` del Excel de planificación) o materia + sede + día + horario.
- Si no hay Excel de demanda cargado (`GET /demanda-excel`), decirlo: no hay demanda para informar.
- Si el usuario da un número que no coincide con el calculado, mostrar la diferencia y no sobreescribir nada.

## Qué no hacer

- No sugerir ni asignar aula (usar **uade-cuadre**).
- No promediar, proyectar ni estimar demanda futura.
- No usar los inscriptos de la planificación como demanda.
- No trabajar a nivel alumno individual (solicitudes individuales fuera de alcance); el legajo solo sirve para contar alumnos distintos.
