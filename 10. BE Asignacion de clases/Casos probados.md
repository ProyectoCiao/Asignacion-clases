# Casos probados

Casos verificados de punta a punta contra la API (curl) y la base local `uade_aulas`, con el resultado esperado. Lo que figura acá está **firme**: no hace falta volver a probarlo salvo que se toque el código involucrado.

- Datos de prueba: períodos 2098-x / 2099-x y clases con nro_clase ≥ 98000, en la sede BELGRANO. Se borraron siempre al terminar.
- Usuario de prueba: header `X-Usuario: prueba.pasoN`.
- Si se modifica un endpoint, volver a correr solo sus casos (cómo armar el entorno: `Estado de avance.MD` → "Cómo trabajar en este entorno").

> **Vigencia:** el paso 5 (2026-10-06) cambió el ciclo de estados. Ya no existen INSCRIPCION_CERRADA ni la finalización manual (`finalizada_en`). Los casos de pasos anteriores que dependían de eso están marcados como **reemplazados** y su versión vigente está en el paso 5.

> **Vigencia 2026-10-08:** se borraron las tablas del modelo anterior (queda solo `carreras`). Todos los casos de abajo de esta sección quedan **sin vigencia** hasta que existan las tablas nuevas; sus endpoints están sin mapear.

## Planificación inicial cruda y Excel libre (2026-10-08)

| # | Caso | Resultado | Vigente |
|---|---|---|---|
| P1 | `GET /planificacion-inicial` sin nada cargado | 200 `{cargada:false}` | ✅ |
| P2 | `POST /planificacion-inicial/analizar` con Excel sin Día ni Horario | 200, faltantes obligatorias [Día, Horario desde/hasta] y opcionales; no guarda | ✅ |
| P3 | `POST /planificacion-inicial` con columnas faltantes (450 filas, cuatri 1, año 2026) | 200, se guarda igual | ✅ |
| P4 | Volver a subir sin `reemplazar=true` | 409 "Ya hay una planificación inicial cargada" | ✅ |
| P5 | `tamanio=500`, página 3 de 450 filas | se limita a 200; página 3 de 3 con 50 filas; opciones de sede/facultad salen del Excel | ✅ |
| P6 | Filtro `sede=belgrano&turno=MANANA&q=numero 10` | coincide con "Belgrano"/"Mañana"/"número" (sin acentos ni mayúsculas) | ✅ |
| P7 | `POST /excel/leer` (230 filas) | 200 con encabezados y las 230 filas; no guarda | ❌ endpoint quitado (Demanda ahora guarda, ver D1–D10) |
| P8 | Archivo que no es .xlsx | 400 "El archivo debe tener extensión .xlsx." | ✅ |
| P9 | Front: paginado, filtro de turno, modal con el botón de subir deshabilitado hasta analizar; las otras pestañas sin llamadas al back y con sus desplegables | OK | ✅ |

## Demanda provisoria (`/demanda-excel`, 2026-10-08)

Excel de prueba: 300 alumnos (legajo, Materia, Facultad, Sede, Turno, Carrera), 1 a 3 materias por alumno = 600 filas. Borrado al terminar.

| # | Caso | Resultado | Vigente |
|---|---|---|---|
| D1 | `GET` sin Excel | `{cargada:false}` | ✅ |
| D2 | Subir | 200, 600 filas; info detecta legajo en la columna 0 | ✅ |
| D3 | Totales sin filtros | 400 "Aplicá al menos un filtro para ver los totales." | ✅ |
| D4 | Sede=monserrat (minúscula) × Facultad | 300 inscripciones / 150 alumnos; FACE 150/100, FAJU 100/100, FAIN 50/50 | ✅ |
| D5 | Sede=Monserrat y Turno=manana (sin tilde) × Materia | 50/50 | ✅ |
| D6 | Turno=Tarde o Noche (misma columna) × Turno | Noche 300/100, Tarde 200/100 | ✅ |
| D7 | Valores de Materia con Facultad=FACE | solo Contabilidad I y Matemática I | ✅ |
| D8 | Columna inexistente | 400 "La columna 99 no existe en el Excel de demanda." | ✅ |
| D9 | `tamanio=500` | se limita a 200 (225 grupos → 2 páginas) | ✅ |
| D10 | Front: estados sin filtro / sin "Totalizar por", opciones encadenadas, orden por columna, los datos siguen tras recargar | OK | ✅ |

## Desplegables desde tablas maestras (`/maestros/*`, 2026-10-09)

| # | Caso | Resultado | Vigente |
|---|---|---|---|
| M1 | Sede en Docentes, Materias, Mapa y Aulas | Monserrat, Recoleta, Belgrano, Pinamar, Online (ids 1–5, tabla `sedes`) | ✅ |
| M2 | Turno en Docentes, Materias y Mapa | Mañana, Tarde, Noche (ids 1–3, tabla `turno`) | ✅ |
| M3 | Pestañas de sede en Aulas | Todas + las 5 sedes de la tabla | ✅ |
| M4 | Edificio en Aulas | los 8 de la tabla `edificios` | ✅ |
| M5 | Facultad en Materias | FACE, FADI, FAEN, FAIN, FAJU, FARU, FASA, UBS (apodos de `materia_facultad`) | ✅ |
| M6 | Nombre de la materia sin facultad / con FARU / con FASA | 2457 / 54 / 135 (igual que la base) | ✅ |
| M7 | Elegir una materia de FARU y cambiar a FASA | la materia elegida se limpia | ✅ |
| M8 | Nombre de la materia por sede (`materia_sede`) | Monserrat 2335, Recoleta 361, Belgrano 309, Pinamar 761, Online 0 | ✅ |
| M9 | Sede + facultad combinadas | Pinamar+FACE 171 (igual que la base), Recoleta+FACE 85 | ✅ |
| M10 | Elegir materia y cambiar a una sede que no la tiene | la materia elegida se limpia | ✅ |

---

## Estado de la planificación (`GET /periodos`, `GET /periodos/{id}`)

| # | Caso | Resultado | Vigente |
|---|---|---|---|
| E1 | Listar períodos existentes | 200, cada uno con `estado` y `origen` | ✅ |
| E2 | Período inexistente | 404 "No existe el período" | ✅ |
| E3 | Sin fecha de congelamiento | `EN_EDICION` | ✅ |
| E4 | Congelamiento pasado, fin futuro | `INSCRIPCION_ABIERTA` | ✅ |
| E5 | Fin de inscripción pasado | `FINALIZADA` automática | ✅ (paso 5) |
| E6 | Finalizada + reabierta | `REABIERTA` | ✅ (paso 5) |
| E7 | La base rechaza fin ≤ congelamiento (CHECK `ck_periodo_fechas_inscripcion`) | error de la base | ✅ |
| — | Estados `INSCRIPCION_CERRADA` y `FINALIZADA` por `finalizada_en` | — | ❌ reemplazado por E5/E6 |

## Carga inicial del Excel (`POST /planificacion/cargar`)

| # | Caso | Resultado | Vigente |
|---|---|---|---|
| X1 | Período que ya tiene clases (2025-1) | 409, "ya tiene planificación cargada"; no lee el archivo ni modifica nada | ✅ |
| X2 | Período nuevo, Excel válido de 1 fila | 200: crea período, clase y docente nuevo, y registra ALTA en la auditoría | ✅ |
| X3 | Volver a cargar el mismo período | 409 | ✅ |
| X4 | Bug corregido: cast `(int)(long)` de `MAX(nro_clase)+1` | La carga ya guarda; antes fallaba siempre | ✅ |

## Fechas de congelamiento e inscripción (`PUT /periodos/{id}/fechas`)

| # | Caso | Resultado | Vigente |
|---|---|---|---|
| F1 | Sin `X-Usuario` | 400 | ✅ |
| F2 | Fin ≤ congelamiento | 400 | ✅ |
| F3 | Fin sin congelamiento | 400 | ✅ |
| F4 | Fechas futuras en EN_EDICION | 200, sigue EN_EDICION; auditoría con antes/después | ✅ |
| F5 | Congelamiento pasado + fin futuro | 200, pasa a INSCRIPCION_ABIERTA | ✅ |
| F6 | Ya congelada, sin motivo | 400 "requiere un motivo" | ✅ |
| F7 | Ya congelada, con motivo | 200, auditoría con `es_emergencia` y motivo | ✅ |
| F8 | Período inexistente | 404 | ✅ |
| F9 | Fin de inscripción en el pasado | 400 "tiene que ser futura" | ✅ (paso 5) |
| F10 | Planificación FINALIZADA | 409 | ✅ (paso 5) |
| — | Finalizada por `finalizada_en` | — | ❌ reemplazado por F10 |

## Edición por lote (`POST /periodos/{id}/cambios`)

Escenario: X = 40 alumnos en B102 (capacidad 20), Y = 10 alumnos en B101 (capacidad 50), Z = 25 alumnos en B006 (capacidad 30), los tres el lunes de 8 a 10; W en B101 el martes. X y W con el docente 1.

| # | Caso | Resultado | Vigente |
|---|---|---|---|
| L1 | Mover solo X a B101 (ocupada por Y) | 400: choque, con la sugerencia "Y tiene que moverse en el mismo lote (por ejemplo, al aula B102, que deja X)"; no aplica nada | ✅ |
| L2 | Datos inválidos: día "Lunex", hora "25:00", aula de otra sede, clase inexistente | 400 con los 4 errores juntos | ✅ |
| L3 | Sin `X-Usuario` | 400 | ✅ |
| L4 | **Intercambio X↔Y** en el mismo lote | 200: 2 clases modificadas, 2 registros de auditoría con el mismo `lote` | ✅ |
| L5 | **Rueda** de 3 aulas (X→B006, Z→B102, Y→B101) | 200: 3 modificadas, con advertencias de sobrecupo (no bloquean) | ✅ |
| L6 | W pasa al lunes de 8 a 10 | 400: choque de aula con Y y choque del docente 1 con X | ✅ |
| L7 | W pasa al lunes de 10 a 12 con otro docente | 200; auditoría con días, horario y docentes | ✅ |
| L8 | Planificación congelada, sin motivo | 400 | ✅ |
| L9 | Planificación congelada, con motivo | 200, `esEmergencia: true` | ✅ |
| L10 | Planificación FINALIZADA | 409 "hay que reabrirlo" | ✅ (paso 5) |
| L11 | Planificación REABIERTA, sin motivo | 400 | ✅ (paso 5) |
| L12 | Planificación REABIERTA, con motivo (cambio de aula) | 200, emergencia | ✅ (paso 5) |
| — | Bloqueo por `finalizada_en` | — | ❌ reemplazado por L10 |

Verificado en la base después de L4–L7: aulas, horarios y docentes finales correctos; `asignacion.origen = MANUAL` y `sillas_vacias` recalculadas.

## Opciones de aula (`GET /clases/{id}/opciones-aula`)

| # | Caso | Resultado | Vigente |
|---|---|---|---|
| O1 | Opciones de X (escenario anterior) | B101 → `INTERCAMBIO_DIRECTO` con los 2 cambios armados; aulas vacías → `LIBRE`; B006 (Z, que no entra en B102) → `REQUIERE_RUEDA` sin cambios sugeridos; orden: primero las que entran, con menos sillas vacías | ✅ |

## Copia de planificación (`POST /periodos/{id}/copiar`)

Escenario: origen con 3 clases (una con 2 docentes, una en un aula de prueba inactiva).

| # | Caso | Resultado | Vigente |
|---|---|---|---|
| C1 | Origen en EN_EDICION | 409 "solo se puede copiar una planificación finalizada" | ✅ |
| C2 | Destino = mismo ciclo que el origen | 400 | ✅ |
| C3 | Cuatrimestre 3 | 400 | ✅ |
| C4 | Sin `X-Usuario` | 400 | ✅ |
| C5 | Copia a un período nuevo | 200: 3 clases, 3 asignaciones y 4 docentes copiados; inscriptos en 0; `sillas_vacias` = capacidad; origen COPIA con `periodo_origen_id`; destino EN_EDICION; advertencia por el aula inactiva; auditoría ALTA | ✅ |
| C6 | Copiar de nuevo al mismo destino | 409 | ✅ |
| C7 | Destino real con clases (2025-1) | 409 | ✅ |
| C8 | Origen inexistente | 404 | ✅ |
| C9 | Origen REABIERTA | 409 | ✅ (paso 5) |
| C10 | Origen FINALIZADA por fecha | 200 | ✅ (paso 5) |

## Reapertura (`POST /periodos/{id}/reabrir` y `/cerrar-reapertura`)

| # | Caso | Resultado | Vigente |
|---|---|---|---|
| R1 | Cerrar reapertura sin estar reabierta | 409 | ✅ |
| R2 | Reabrir sin motivo | 400 | ✅ |
| R3 | Reabrir con motivo | 200, REABIERTA con `reabiertaEn`/`Por`/`Motivo`; auditoría de emergencia | ✅ |
| R4 | Reabrir una ya reabierta | 409 | ✅ |
| R5 | Cerrar reapertura | 200, FINALIZADA, campos de reapertura en null; la auditoría conserva quién reabrió y por qué | ✅ |
| R6 | Sin `X-Usuario` | 400 | ✅ |

## Sesión, roles y exportable (front, 2026-10-13)

Probado en el navegador (front en :4200, back apagado) y el .xlsx abierto con Excel por COM.

| # | Caso | Resultado | Vigente |
|---|---|---|---|
| S1 | Entrar a cualquier pantalla sin sesión | redirige a `/login?volver=...`; al ingresar vuelve a la pantalla pedida | ✅ |
| S2 | Ingresar con un mail que no es @uade.edu.ar | "Ingresá un mail @uade.edu.ar.", no entra | ✅ |
| S3 | Ingresar con otro mail @uade.edu.ar (sin contraseña) | entra como Backoffice: sin "Cargar planificación (Excel)" ni "Actuar como…" | ✅ |
| S4 | Ingresar como emurbaneja@uade.edu.ar | rol Admin: ve la carga del Excel y "Actuar como…" | ✅ |
| S5 | Admin actúa como bot (Impersonate) | franja "Estás actuando como…"; desaparecen la carga y "Actuar como…"; "Volver a mi usuario" lo restituye | ✅ |
| S6 | Headers mientras suplanta | `X-Usuario` = persona suplantada, `X-Usuario-Real` = Admin | ✅ |
| S7 | `/login` con sesión abierta / recargar la página | vuelve a `/` / la sesión se mantiene (sessionStorage) | ✅ |
| S8 | Planificación final sin filas | "Exportar Excel" y "Exportar CSV" deshabilitados | ❌ reemplazado por S10 |
| S9 | Utilidad de exportación con acentos, ñ, ; " & < > y salto de línea | .xlsx abre en Excel (hoja "Planificación final", números como números, encabezado en negrita, fijo y con autofiltro); CSV con ; y BOM bien escapado | ✅ |
| S10 | Botón "Exportar" | deshabilitado sin filas; al abrirlo despliega Excel (.xlsx) y CSV (.csv); Escape y clic afuera lo cierran | ✅ |

## Mapa: pisos y aulas desde la tabla `aulas` (2026-10-14)

| # | Caso | Resultado | Vigente |
|---|---|---|---|
| MP1 | `GET /maestros/pisos?sede=1` (Monserrat) | 0…9, A, M, P, s, S (numéricos primero) | ✅ |
| MP2 | `GET /maestros/pisos?sede=3` (Belgrano) / `sede=5` (Online) | [B, SUM] / [] | ✅ |
| MP3 | `GET /maestros/aulas?sede=1&piso=9` | 19 aulas del piso 9, capacidad como número | ✅ |
| MP4 | `piso=s` / `piso=S` / `piso=A` | 21 / 15 / 2 (distingue mayúsculas, como está en la base) | ✅ |
| MP5 | `GET /maestros/pisos` sin sede | 400 | ✅ |
| MP6 | Front Mapa: Monserrat → Piso 9 → Belgrano | el desplegable se recarga con los pisos de la sede; la grilla muestra solo las aulas del piso elegido (19 en Monserrat 9; 9 en Belgrano B), sillas = capacidad, 0 ocupadas; sin errores en consola | ✅ |

## Mapa: Edificio y Tipo, `aulas` con `sede_id`/`edificio_id`/`aula_tipo` (2026-10-15)

`aulas` pasó a `sede_id`, `edificio_id` y `aula_tipo` (sin `aula_sede` ni `aula_edificio`); `edificios.edificio_nombre`. Probado con la API nueva en :5091 y un front en :4201.

| # | Caso | Resultado | Vigente |
|---|---|---|---|
| MP7 | `GET /maestros/edificios?sede=3` / `sede=1` | Belgrano / Chile 1, Chile 2, Lima 1–3, Independencia 1 y 2, UADE Labs (solo edificios con aulas en la sede) | ✅ |
| MP8 | `GET /maestros/tipos-aula?sede=3` | AULA, HÍBRIDA (texto) | ❌ reemplazado por MP13: ahora devuelve [{id, nombre}] |
| MP9 | `GET /maestros/aulas?sede=3&piso=B&edificio=9&tipo=AULA` | (el filtro `tipo` ahora es el id, ver MP14) aulas con `edificioId`, `edificio` y `tipo`; conteos de piso, tipo y edificio coinciden con la base (Monserrat piso 3: 41; + AULA: 29; + Chile 1: 0) | ✅ |
| MP10 | Front Mapa: orden de desplegables | Sede, Edificio, Piso, Tipo, Turno, Día; Edificio y Tipo con "Todos"; piso se llena (Monserrat 0…S) | ✅ |
| MP11 | Front Mapa: cambiar filtros y de sede | las tarjetas cambian (41 → 29 → 0 → 41); Belgrano recarga edificios (Belgrano), pisos (B, SUM) y 9 tarjetas; sin errores en consola | ✅ |
| MP12 | Edificio y Piso no son en cascada | se puede elegir una combinación sin aulas (Chile 1 + piso 3 = 0 tarjetas); es la respuesta correcta, no un error | ✅ |

## Desplegables unificados en todos los tabs (2026-10-15)

`aulas.aula_tipo_id` → `aula_tipo`. Probado con la API nueva en :5091 y un front en :4201 contra la base real. Reemplaza a MP8 y MP9 (el tipo ahora va por id, no por texto).

| # | Caso | Resultado | Vigente |
|---|---|---|---|
| MP13 | `GET /maestros/tipos-aula?sede=3` / sin sede | [{8 AULA}, {22 HÍBRIDA}] / los tipos de `aula_tipo` que tienen aulas, con nombre sin espacios sobrantes | ✅ |
| MP14 | `GET /maestros/aulas` sin filtros; `?tipo=22&edificio=9` | 369 aulas (incluye la que no tiene sede); con filtros trae `sedeId`, `sede`, `edificioId`, `edificio`, `tipoId` y `tipo` | ✅ |
| MP15 | Mapa: Tipo por id | Monserrat piso 3: 41 → AULA 29 → HÍBRIDA 6; Belgrano: 9 tarjetas, tipos AULA/HÍBRIDA; coincide con la base | ✅ |
| MP16 | Aulas (antes vacía) | pestañas Todas 369 / Monserrat 321 / Recoleta 8 / Belgrano 10 / Pinamar 29 / Online 0; Lima 2 = 81 y + AULA = 53 (coincide con la base); Edificio, Piso y Tipo se acotan por sede | ✅ |
| MP17 | Última planificación: Sede, Turno, Facultad | opciones de `sedes`, `turno`, `materia_facultad`; filtran el Excel: Online 741 (el Excel dice "UADE ONLINE"), Monserrat 8.072, Intensivo 293, FAIN 1.715 | ✅ |
| MP18 | Docentes: Sede, Turno, Facultad | desde `sedes`, `turno` y `materia_facultad` | ✅ |
| MP19 | Planificación final: Sede, Turno, Facultad | desde las tablas (Turno antes era fijo Mañana/Tarde/Noche) | ✅ |
| MP20 | Materias: Sede, Turno, Facultad, Nombre | sin cambios, sigue funcionando | ✅ |
| MP21 | `GET /maestros/pisos?sede=1&edificio=1` / `edificio=5` / sin edificio | [1, 3] / [1…10, s, S] / todos los de la sede; coincide con la base | ✅ |
| MP22 | Mapa: Piso acotado por Edificio | Monserrat: Todos → 17 pisos y 1 tarjeta (piso 0); Chile 1 → pisos 1 y 3, piso 1 con 2 tarjetas; Lima 2 → piso 1 con 6; volver a Todos restaura los pisos; Belgrano → B, SUM y 9 tarjetas; sin errores en consola | ✅ |

---

## No probado todavía (para no asumirlo como firme)

- El selector de aula (Edificio → Piso → Aula) de Planificación final: no hay clases cargadas para abrirlo. Su lógica ya usa pisos de texto.
- Concurrencia real: dos lotes o dos cargas simultáneos (está el `FOR UPDATE`, pero no se ejerció).
- Carga de un Excel real completo (con códigos de aula, advertencias de aula inexistente y el formato de horario combinado).
- `opciones-aula` con varias ocupantes en la misma aula, y con clases cuya aula actual es REMOTA/SIN_AULA.
- Copia con volumen real (cientos o miles de clases).
- Rendimiento con el volumen de inscripción (45.000 alumnos).
- El front: ninguno de los endpoints nuevos se probó desde la interfaz, que todavía no los usa.
- Exportar la Planificación final con datos reales (hoy viene vacía: está desvinculada).
- El "Grabar" viejo (`/oferta/grabar`) no se volvió a probar y sigue salteando las reglas.
