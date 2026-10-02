# Tablas — uade-cuadre (orquestador)

## clase
id, nro_clase, periodo_id, sede_id, materia_codigo, turno, horario_desde, horario_hasta, dias, regimen, inscriptos_con_reserva

## clase_docente
clase_id, docente_id

## aula
id, sede_id, codigo, tipo_grupo, capacidad_total, capacidad_real, activa, piso

## asignacion
id, clase_id (unique), aula_id (nullable), origen (EXCEL|SUGERIDA|MANUAL), sillas_vacias

## sede
id, codigo, nombre

Este skill lee de todas las tablas del dominio pero solo escribe en `asignacion`.
