# Overview: el norte del proyecto (leer primero)

Este documento junta lo que el usuario fue definiendo y lo que se decidió construyendo, para no perder el rumbo entre sesiones. Si algo de acá contradice a `Caso de uso.md` (2026-10-05, modelo viejo), **manda este**. El detalle técnico y el último paso están en `Estado de avance.MD`; lo ya verificado en `Casos probados.md`.

## 1. Para qué existe

Reemplazar al Excel como herramienta de **planificación y asignación de aulas** de UADE. Se parte de la planificación del cuatrimestre anterior (un Excel, cargado una sola vez), se le suma la **demanda de los alumnos** y, con esa información, el backoffice (y más adelante una IA) deja cada clase en el aula que mejor le calza: sin sobrecupo y con las menos sillas vacías posibles.

## 2. Cómo funciona el proceso (palabras del usuario)

1. El backoffice define qué materias (clases) se dictan en qué aulas y lo **disponibiliza al alumno** por Web Campus.
2. Se abre la inscripción: los alumnos se anotan. **El alumno no sabe en qué aula va a cursar; solo conoce el cupo.** Si el cupo se llena, Web Campus avisa "sin cupo" (eso es problema de Web Campus: por orden de llegada). Desde acá solo se publica un cupo (ej. 20 sillas) sin saber en qué aula caerá.
3. Las aulas vacías se van llenando. Al cierre, el backoffice recibe las aulas completadas según la materia asignada a cada una.
4. **Segunda instancia (la IA):** el aula inicial puede no ser la definitiva (ej. un aula de 20 sillas con 2 inscriptos y otra de 10 con 20: 10 del cupo + 10 que la marcaron como tentativa o 2ª opción). La IA **propone** reasignar; el backoffice **tilda** cuáles aplica (una o todas). La IA no mueve nada sola.
5. Una nueva materia sin precedentes también hay que asignarla a un aula a mano: la base estimativa es la última planificación + las modificaciones del backoffice.

## 3. Conceptos que no se discuten

- **La clase es el "paquete"**: su número de clase (columna **Id** de la planificación) agrupa alumnos, docente, materia, asistente docente, etc. **Las clases se mueven enteras** (con los alumnos que ya tienen), nunca alumno por alumno.
- **Lo edilicio es fijo**: sede, edificio, piso, aula y capacidad no se editan. El cupo de una clase es la capacidad de su aula.
- **Regla de intercambio**: mover una clase nunca puede dejar a otra sin aula (A↔B, o ruedas A→B→C→A). La IA tiene que mirar el conjunto completo de movimientos dentro de la misma sede y turno, no pares sueltos. (Cuando se construya la sugerencia, el conjunto se aplica todo junto o no se aplica.)
- **Demanda**: la única fuente es el **Excel de demanda** (alumnos inscriptos, tab Demanda). Los inscriptos que traiga la planificación NO cuentan. La planificación solo sirve para replicar qué materia va a qué aula, turno, sede, etc.
- **Primera y segunda opción**: cada alumno elige una clase y una alternativa (se usa si se agota la primera). La **2ª opción es tentativa**: ocupa silla, pero se ve distinta.
- **El Excel original nunca se modifica**: las ediciones de aula van en `asignacion_aula` (quién y cuándo). Subir una planificación nueva las borra.
- **El sistema no valida lo que se sube**: lee la planificación inicial tal cual y la muestra. Docentes y clases cambian cada cuatrimestre y vienen del Excel, no de tablas propias.
- **Estados** (calculados por fechas, ver `Caso de uso.md`): EN_EDICION → INSCRIPCION_ABIERTA → FINALIZADA (REABIERTA excepcional). El código existe pero está sin mapear desde el reinicio del modelo (2026-10-08).

## 4. Modelo de datos actual (PostgreSQL 17, db `uade_aulas`)

| Tabla | Para qué |
|---|---|
| `planificacion_excel` | Planificación subida (1 fila: `encabezados` + `filas` jsonb, texto tal cual). Subir una nueva reemplaza y vacía `asignacion_aula`. |
| `demanda_excel` | Excel de demanda (1 fila, misma forma). Cada subida reemplaza; las inscripciones nuevas se agregan a `filas`. |
| `asignacion_aula` | Aula elegida por clase (clave = Id del Excel): `aula_id`, `modificado_por(_real)`, `modificado_en`. |
| `asignacion_alumno` | Resultado de "Asignar alumnos a aulas": legajo, `clase_id`, `opcion` 1\|2. Cada corrida reemplaza todo. |
| `aulas`, `sedes`, `edificios`, `aula_tipo`, `turno`, `periodo`, `anio` | Maestras edilicias y de desplegables. Hay aulas sin sede, edificio o tipo cargado. |
| `materia`, `materia_sede`, `materia_facultad`, `carreras` | Maestras de materias. |
| `roles`, `log_acciones` | Roles (Admin = 1) y log de acciones. **`log_acciones` todavía no se llena** (decisión del usuario). |

Columnas clave de la planificación: **Id** (único), Clase (no único), Facultad, Sede, Periodo, Ciclo, Turno, Horario, Dias De Cursada, CódigoMateria, Materia, Docentes, Aula Principal ("-" = sin aula). El **Excel de demanda** trae "Id Clase" (el usuario confirmó que el real también) e "Id Clase 2da Opcion". El cruce Excel ↔ `aulas` es por `UPPER(TRIM(nombre))`.

## 5. Lo que ya hace el sistema (API .NET 9 + front Angular)

- **Última planificación**: sube y muestra el Excel (paginado, filtros). **Demanda**: sube el Excel, filtros dinámicos + "Totalizar por" + botón **Aplicar filtro**.
- **Mapa**: aulas con sillas (verde libre, **rojo = 1ª opción, rojo tramado = 2ª opción**), filtros Sede/Edificio/Piso/Tipo/Período/Turno/Día con **Aplicar filtro**, detalle de las clases de un aula, **Cambiar aula** y botón **Asignar alumnos a aulas** (distribuye la demanda entre las clases/aulas existentes; no mueve clases ni usa IA).
- **Aulas**: estado, inscriptos y diferencia por aula (solo lectura), filtro Período con **Aplicar filtro**.
- `PUT /clases/{id}/aula` valida: **bloquea** choque de aula y sobrecupo (el cupo se puede forzar con `forzarCupo`); el **choque de docente solo avisa** (decisión del usuario).
- Sesión y roles en el front (control de interfaz, no seguridad): Admin (solo emurbaneja@uade.edu.ar), Backoffice (cualquier otro @uade.edu.ar), Impersonate (bots/personas sintéticas); el login todavía no valida usuario ni contraseña (vendrá del AD).
- Sin conectar a `/clases`: **Docentes**, **Planificación final** y **Materias**. Falta la IA.

## 6. Personas sintéticas (skills)

La idea: personas que corren el modelo y hacen las pruebas. Los skills `uade-*` son herramientas de datos/reglas (reescritos para el modelo nuevo): `uade-oferta`, `uade-demanda`, `uade-aulas`, `uade-cuadre` (el único que sugiere aula).

| Persona | Qué hace | Estado |
|---|---|---|
| `persona-backoffice` | Reasigna aulas para eliminar sobrecupos (mismo tipo de aula, mismo edificio primero) | ✅ hecha y probada |
| `persona-alumno` | Se inscribe modificando la demanda (`POST /demanda-excel/inscripcion`, 1ª y 2ª opción) | pendiente |
| `persona-rector` | Da el OK final antes del cierre, verificando que todo cuadre | pendiente |
| Ideas | docente (reclama horario/aula), decano/director de carrera, auditor (revisa `log_acciones`), **saboteador** (intenta cosas inválidas; el más útil para testear) | sin crear |

## 7. Próximos pasos (el norte)

1. **IA de reordenamiento** (la etapa grande): sugiere mover clases enteras entre aulas de la misma sede y turno, como conjunto de intercambios, para eliminar sobrecupo y sillas vacías; la UI muestra cada sugerencia con un tilde y "aplicar todas". Usa la demanda de 1ª **y** 2ª opción.
2. `persona-alumno` y `persona-rector`; luego saboteador y el resto.
3. Conectar Docentes y Planificación final a `/clases`; archivo final visible en Planificación final.
4. Llenar `log_acciones`; auth real (AD), permisos por endpoint y suplantación validada; permisos por rol definidos por el usuario.
5. Cruce de demanda real cuando llegue el archivo/API de inscripción; vista para el sistema externo.

## 8. Decisiones abiertas

- ¿Quién puede forzar un sobrecupo? (hoy cualquiera con `aulas.editar`).
- ¿Horario coherente con el turno? No se valida.
- Detalle exacto de la IA y de la demanda con alternativa (el usuario lo ampliará).
- Tabla de permisos y tabla de Oracle con las personas para suplantar.

## 9. Cómo trabajamos (reglas del usuario)

- Leer `Estado de avance.MD` al empezar; actualizarlo (formato sintético) y `Casos probados.md` al terminar cada paso. No volver a probar lo firme.
- **Commit solo cuando el usuario lo pide, nunca push.** El back (`10. BE Asignacion de clases`) y el front (`uade-aulas-web/`, repo propio) se comitean por separado; `bin/` y `obj/` no se incluyen.
- Las tablas de la base las crea el usuario salvo que diga lo contrario; las migraciones se escriben en `scripts/` (idempotentes, con backup previo si borran algo).
- Probar de verdad (API con curl/PowerShell y front en el navegador) y borrar los datos de prueba; no matar la API del usuario en :5080 (usar :5091 con un build en `%TEMP%`).
- Los filtros de pantalla se aplican **con un botón "Aplicar filtro"**, no al elegir cada valor.
- Español rioplatense en la interfaz y los documentos; no inventar reglas: si falta una decisión, preguntar con opciones y una recomendación.
- Cuando algo no se pueda reproducir, decirlo y pedir el dato concreto en vez de adivinar.

## 10. Mapa de documentos

| Archivo | Qué contiene |
|---|---|
| `Overview.md` | Este documento: el norte |
| `Estado de avance.MD` | Estado técnico, endpoints, cómo levantar todo, último paso |
| `Casos probados.md` | Lo verificado (no repetir) |
| `Caso de uso.md` | Negocio original (2026-10-05): estados, reapertura, regla de intercambio |
| `uade-*/SKILL.md`, `persona-*/SKILL.md` | Skills y personas |
| `scripts/` | Migraciones SQL |
