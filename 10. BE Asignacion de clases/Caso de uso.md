# Caso de uso: Planificación y asignación de aulas UADE

> Contexto definido con el usuario el 2026-10-05. Este documento describe **qué debe hacer el sistema**, no cómo está implementado.

## Objetivo

Reemplazar al Excel como herramienta de planificación de aulas. El Excel se usa **una sola vez**, para cargar los datos iniciales. Desde ahí, toda la planificación vive en el sistema (PostgreSQL), y cada cuatrimestre arranca de la planificación anterior.

## Actores

| Actor | Rol |
|---|---|
| **Backoffice** | Carga la planificación inicial, la edita, define las fechas de congelamiento e inscripción y hace correcciones de emergencia. |
| **Alumnos** | Se inscriben en las clases publicadas, **en un sistema externo** (no en este). |
| **Sistema externo de inscripción** | Lee la planificación que publiquemos y graba las inscripciones en una tabla propia (pendiente de obtener acceso). |
| **IA** | En una etapa posterior, sugiere reordenar las aulas para que queden menos sillas vacías. |

## Flujo del proceso

### 1. Carga de la planificación modelo

- Hay **una única planificación por cuatrimestre**, en un solo Excel que incluye **todas las sedes y turnos**. El Excel está bien separado por sede y turno, sin ambigüedades.
- El backoffice sube el Excel **una sola vez**. Volver a subirlo no tiene sentido, porque la información ya está en el sistema y lo pisaría. El sistema no debe permitirlo una vez que existe la planificación.
- Al cerrar la inscripción, la planificación queda **finalizada** en Postgres. En la próxima ventana de planificación se **copia completa, docentes incluidos**, como punto de partida del cuatrimestre siguiente. Funciona como el modelo de disponibilidad de aulas.

### 2. Edición por el backoffice

- **Lo edilicio es fijo y no se edita:** sede, piso, aula y capacidad de cada aula. La cantidad de aulas por piso y por sede también es fija.
- **El cupo de cada clase es la capacidad de su aula.**
- **El backoffice puede cambiar todo lo que no es edilicio:** la materia que se dicta en un aula, el docente a cargo, el día, el horario y el aula asignada a cada clase (es decir, a qué aula van sus alumnos).
- Sobre día y horario, la intención es que el backoffice siga las sugerencias de la IA.
- **Edita un solo usuario del backoffice a la vez:** no hay edición concurrente.
- **Permisos:** quién puede ver y quién puede editar se define en una tabla que el usuario entregará más adelante.
- **Auditoría obligatoria:** cada cambio queda registrado con el usuario que lo hizo, la fecha y hora, y los valores de antes y después.

### 3. Congelamiento y publicación

- El backoffice carga en el sistema dos fechas: la de **congelamiento** y la de **fin de inscripción**.
- Al llegar la fecha de congelamiento, la planificación se congela **automáticamente** y pasa a estar disponible para la inscripción.
- La planificación congelada tiene "casilleros vacíos" (los cupos), que se van llenando con la demanda de los alumnos.
- Mientras la inscripción está abierta **se pueden editar aulas, materias, docentes, etc.**, pero cada cambio exige un motivo y queda registrado.
- El sistema externo de inscripción **lee lo que le publiquemos desde este sistema**, probablemente mediante una API hacia una tabla. El formato se define cuando se conozcan los campos de su tabla.

### 4. Inscripción (sistema externo)

- Los alumnos se inscriben en otro sistema (app de celular o web), que graba una tabla propia. Se va a pedir acceso a esa tabla una vez que se entienda cómo funciona.
- Esa tabla de aulas y cátedras disponibles tiene restricciones propias del sistema externo: por ejemplo, que el alumno solo se anote a materias de su carrera o que tenga la cuota paga.
- Los alumnos solo pueden llenar los cupos que el backoffice publicó en la planificación.
- **Volumen:** unos 45.000 alumnos, con unas 5 materias cada uno, en todos los días hábiles. Entran de a miles en simultáneo por miedo a quedarse sin cupo, y al terminar el primer día ya se inscribió más del 95%.
- **Lectura de la demanda:** durante la inscripción, este sistema lee la demanda mediante una API y la guarda en una tabla propia, que se actualiza cada vez que se refresca la página.
- **Demanda en dos niveles:** el alumno elige una **primera opción** (materia, sede, turno y docente) y una **alternativa** provisoria, que se usa si el cupo de la primera se agota. Es una política de la universidad para no tener que volver a preguntarle al alumno. *Falta información sobre cómo funciona exactamente.*
- Cuando una clase se llena, hay una **lista de espera solo informativa**.
- La inscripción termina en la fecha de fin, y en ese momento la planificación se **finaliza automáticamente**. Algunas aulas pueden llenarse al 100% el primer día y otras quedar con lugares libres al cierre.
- Un alumno que no encontró cupo puede quedarse sin cursar lo que quería, o bien el reacomodamiento sugerido por la IA puede abrir lugares nuevos. En ese caso se avisa a los alumnos que pueden inscribirse, aunque sea en una materia que no era su primera elección.
- El backoffice no necesita ver el llenado en tiempo real: alcanza con que se actualice al refrescar la página.

### 5. Reordenamiento sugerido por IA (etapa posterior)

- La IA sugiere aulas cuya capacidad se ajuste mejor a la demanda, para dejar **menos sillas vacías**.
- Debe **respetar el turno y la sede**.
- **Actúa durante la inscripción abierta**, con la demanda leída hasta ese momento. El backoffice decide si aplica sus sugerencias.
- Esta etapa se aborda **una vez terminados los puntos 1 a 4**. El usuario va a ampliar el detalle más adelante.

### 6. Reapertura excepcional

- Una planificación finalizada **se puede reabrir**, por ejemplo si un aula entra en refacciones o le falta el proyector u otro material y hay que cambiarla por otra.
- Es una **excepción**, no algo masivo ni habitual: se reabre con un motivo, cada cambio exige motivo y se vuelve a cerrar a mano. Todo queda en el log.

## Regla clave: un cambio de aula siempre es un intercambio

Mover los alumnos de una clase a otra aula **nunca puede dejar a otra clase sin aula**.

**Ejemplo:**
- El aula **A** tiene capacidad para 20 y se anotan 40.
- El aula **B** tiene capacidad para 40 y se anotan 10.
- Si los 40 alumnos de A pasan a B para que entren todos, **los 10 alumnos de B tienen que pasar a A en el mismo movimiento**. No puede quedar ninguna clase sin asignar.

Con más aulas puede ser una rueda: A→B, B→C, C→A.

- **Backoffice:** la interfaz tiene que impedir que una clase quede sin aula. Al mover una clase, obliga a resolver a dónde va la que ocupaba el aula de destino, y el conjunto de movimientos se aplica todo junto o no se aplica.
- **IA:** no tiene que mirar solo pares de aulas, sino **todas las combinaciones posibles** dentro de la misma sede y turno, y sugerir el conjunto completo de movimientos **en simultáneo**.

## Estados de la planificación

El estado no se guarda: se calcula a partir de las fechas.

| Estado | Cuándo | Qué se puede hacer |
|---|---|---|
| `EN_EDICION` | Antes de la fecha de congelamiento, o si todavía no está cargada | Editar libremente |
| `INSCRIPCION_ABIERTA` | Desde la fecha de congelamiento hasta la de fin de inscripción | Editar con motivo; la IA sugiere reacomodamientos |
| `FINALIZADA` | Automáticamente, al llegar la fecha de fin de inscripción | Solo lectura; sirve de base para el cuatrimestre siguiente |
| `REABIERTA` | Cuando el backoffice reabre una finalizada (con motivo) | Editar con motivo, hasta que se cierre la reapertura a mano |

## Pendientes de definir

- [ ] Tabla de permisos de usuarios (quién ve y quién edita).
- [ ] Estructura de la tabla de inscripciones del sistema externo y forma de publicarle la planificación.
- [ ] Detalle de la demanda con primera opción y alternativa.
- [ ] Detalle del reordenamiento por IA (lo explica el usuario en una próxima sesión).

## Plan de implementación

1. ✅ Migración (columnas de fechas y estado en `periodo`, tabla `auditoria_cambios`) y cálculo del estado.
2. ✅ Bloquear que se vuelva a subir el Excel; el backoffice carga las fechas de congelamiento y de fin de inscripción.
3. ✅ Endpoint para editar una clase: validación de choques de aula y de docente, cambios de aula como intercambios aplicados en una sola operación, y registro en el log.
4. ✅ Copia de una planificación finalizada al cuatrimestre siguiente.
5. ✅ Finalización automática al cerrar la inscripción, y reapertura excepcional.
