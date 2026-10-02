"""
Uso único (2026-10-02): las asignaciones de prueba apuntaban a aulas inventadas (MON-101, BEL-201...).
Este script reubica cada clase en un aula REAL activa de su misma sede: la más chica que alcance para
los inscriptos con reserva y que no choque (mismo período, días en común y horario superpuesto).
Si ninguna alcanza, usa la más grande libre, para que el sobrecupo se vea en Aulas como "Faltan N".
"""
import csv
import io
import os
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from importar_aulas import PSQL, password_postgres  # noqa: E402

ENTORNO = {**os.environ, "PGPASSWORD": password_postgres(), "PGGSSENCMODE": "disable", "PGCLIENTENCODING": "UTF8"}
BASE = [PSQL, "-h", "localhost", "-U", "postgres", "-d", "uade_aulas", "-v", "ON_ERROR_STOP=1"]


def consultar(sql: str) -> list[dict]:
    salida = subprocess.run(BASE + ["--csv", "-c", sql], env=ENTORNO, capture_output=True, text=True,
                            encoding="utf-8", check=True).stdout
    return list(csv.DictReader(io.StringIO(salida)))


def se_superponen(a: dict, b: dict) -> bool:
    if a["periodo_id"] != b["periodo_id"]:
        return False
    if not (set(a["dias"]) & set(b["dias"])):
        return False
    return a["desde"] < b["hasta"] and b["desde"] < a["hasta"]


aulas = consultar("""SELECT id, sede_id, capacidad_real FROM aula
                     WHERE activa AND tipo_grupo NOT IN ('REMOTA', 'SIN_AULA')""")
clases = consultar("""SELECT x.id AS asignacion_id, x.aula_id, a.activa AS aula_activa, c.id, c.sede_id, c.periodo_id,
                             coalesce(array_to_string(c.dias, ','), '') AS dias, c.horario_desde AS desde,
                             c.horario_hasta AS hasta, c.inscriptos, c.inscriptos_con_reserva
                      FROM asignacion x JOIN clase c ON c.id = x.clase_id
                      LEFT JOIN aula a ON a.id = x.aula_id
                      WHERE x.aula_id IS NOT NULL""")
for c in clases:
    c["dias"] = [d for d in c["dias"].split(",") if d]

ocupacion: dict[str, list[dict]] = {}
for c in clases:
    if c["aula_activa"] == "t":
        ocupacion.setdefault(c["aula_id"], []).append(c)

pendientes = sorted((c for c in clases if c["aula_activa"] != "t"), key=lambda c: -int(c["inscriptos_con_reserva"]))
updates = []
for c in pendientes:
    necesidad = int(c["inscriptos_con_reserva"]) or int(c["inscriptos"])
    candidatas = [a for a in aulas if a["sede_id"] == c["sede_id"]]
    libres = [a for a in candidatas if not any(se_superponen(c, o) for o in ocupacion.get(a["id"], []))]
    alcanzan = sorted((a for a in libres if int(a["capacidad_real"]) >= necesidad), key=lambda a: int(a["capacidad_real"]))
    elegida = (alcanzan or sorted(libres or candidatas, key=lambda a: -int(a["capacidad_real"])) or [None])[0]
    if elegida is None:
        print(f"Clase {c['id']}: sin aulas reales en su sede, queda sin aula.")
        updates.append(f"UPDATE asignacion SET aula_id = NULL, sillas_vacias = 0 WHERE id = {c['asignacion_id']};")
        continue
    ocupacion.setdefault(elegida["id"], []).append(c)
    sillas = int(elegida["capacidad_real"]) - int(c["inscriptos_con_reserva"])
    updates.append(f"UPDATE asignacion SET aula_id = {elegida['id']}, sillas_vacias = {sillas} WHERE id = {c['asignacion_id']};")

print(f"Reasignando {len(updates)} clases...")
subprocess.run(BASE + ["-c", "BEGIN;" + "".join(updates) + "COMMIT;"], env=ENTORNO, check=True)
