# Tablas — uade-oferta

## clase (columnas usadas)
id, nro_clase, periodo_id, sede_id, materia_codigo, turno, horario_desde, horario_hasta, dias, regimen, idioma, modalidad, packs

## clase_docente
clase_id, docente_id

## docente
id, nombre

## periodo
id, ciclo, nombre, fecha_desde, fecha_hasta

## materia
codigo, nombre, facultad, departamento, carga_horaria

No se usan en este skill: `aula`, `asignacion` (salvo lectura indirecta vía uade-cuadre).
