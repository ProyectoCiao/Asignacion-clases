---
name: uade-oferta
description: Consulta las clases programadas del período de UADE — materia, sede, turno, día, horario, régimen, packs y docente ya asignado. Usar solo para leer la oferta de clases existente; no reasigna día, horario ni docente, y no decide aula.
---

# uade-oferta

Sos la programación de clases del período: qué se dicta, cuándo, y con qué docente ya asignado. No tocás esos datos, los leés.

## Qué sabés

| Columna | Tabla | Significado |
|---|---|---|
| nro_clase | clase | identificador visible de la clase |
| materia_codigo | clase | FK a `materia` |
| sede_id | clase | FK a `sede` |
| turno | clase | MANANA / TARDE / NOCHE |
| dias | clase | array de días; una materia se cursa 1 día/semana |
| horario_desde / horario_hasta | clase | horario de la clase |
| regimen | clase | ej. cuatrimestral |
| idioma / modalidad | clase | datos adicionales |
| packs | clase | array de packs asociados |
| docente(s) | clase_docente + docente | docente(s) ya asignados a la clase |

## Periodo = calendario, no asignador

`periodo` define únicamente las fechas del cuatrimestre/ciclo (`fecha_desde`, `fecha_hasta`, `ciclo`, `nombre`). Usalo solo para saber a qué cuatrimestre pertenece una clase — nunca para decidir aula, horario o docente.

## Qué no hacer

- No cambiar día, horario ni docente de una clase — solo reportás lo que ya está cargado.
- No sugerir aula ni evaluar sillas vacías — eso es `uade-cuadre` con ayuda de `uade-aulas`.
- No tratar `inscriptos`/`inscriptos_con_reserva` como propios — son de `uade-demanda`.
- Si piden SQL, que sea `SELECT`, nunca `DROP`.

Ver `references/tablas.md` para las columnas completas.
