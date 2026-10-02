"""
Importa uade_aulas.xlsx a la tabla `aula` de PostgreSQL (base uade_aulas).

Columnas esperadas del Excel (Hoja1):
    Sede | Aula Principal | Piso | Numero de aula | Tipo de Aula | Capacidad Real

Mapeo a la tabla `aula`:
    Sede            -> sede_id (por sede.codigo)
    Aula Principal  -> codigo
    Piso            -> piso_texto (tal cual) + piso (entero derivado, lo usa el Mapa)
    Numero de aula  -> numero_aula
    Tipo de Aula    -> tipo_detalle (tal cual) + tipo_grupo (categoría normalizada)
    Capacidad Real  -> capacidad_real (y capacidad_total, que queda igual a la real)

Es idempotente: hace upsert por (sede_id, codigo). Las aulas que ya no están en el Excel se borran
si nadie las usa, o quedan inactivas si tienen clases asignadas.

Uso:
    .venv/Scripts/python.exe scripts/importar_aulas.py [ruta_excel]

La contraseña de Postgres se toma de PGPASSWORD o, si no está, de los user-secrets del BE.
"""
import os
import re
import subprocess
import sys
import tempfile
import unicodedata
from pathlib import Path

import openpyxl

RAIZ = Path(__file__).resolve().parent.parent
PSQL = r"C:\Program Files\PostgreSQL\17\bin\psql.exe"
SEDES_VALIDAS = {"MONSERRAT", "RECOLETA", "BELGRANO", "PINAMAR"}
ENCABEZADOS = ["Sede", "Aula Principal", "Piso", "Numero de aula", "Tipo de Aula", "Capacidad Real"]


def sin_acentos(texto: str) -> str:
    return "".join(c for c in unicodedata.normalize("NFD", texto) if unicodedata.category(c) != "Mn")


def tipo_grupo(tipo: str) -> str:
    t = sin_acentos(tipo).upper().strip()
    if t == "CLASE REMOTA":
        return "REMOTA"
    if t in ("-", ""):
        return "SIN_AULA"
    if "HIBRIDA" in t:
        return "HIBRIDA"
    if re.search(r"\bLAB\b", t) or "INFORMATICA" in t:
        return "LAB"
    if "TALLER" in t:
        return "TALLER"
    if t == "ARTE":
        return "ARTE"
    if "AUDITORIO" in t or "HEMICICLO" in t:
        return "AUDITORIO"
    return "AULA"


def piso_numerico(piso: str, numero: str) -> int:
    """Entero usado por el Mapa: subsuelos negativos, planta baja/especiales en 0."""
    p = piso.strip()
    if p.isdigit():
        return int(p)
    m = re.fullmatch(r"[sS](\d+)", p)
    if m:
        return -int(m.group(1))
    m = re.fullmatch(r"[uU](\d+)", p)
    if m:
        return int(m.group(1))
    if p.upper() in ("B", "P"):
        digitos = re.sub(r"\D", "", numero)
        return int(digitos) // 100 if digitos else 0
    return 0


def sql_texto(valor) -> str:
    return "'" + str(valor).replace("'", "''") + "'"


def leer_excel(ruta: Path) -> list[dict]:
    hoja = openpyxl.load_workbook(ruta, data_only=True).active
    filas = list(hoja.iter_rows(values_only=True))
    encabezado = [str(c).strip() if c is not None else "" for c in filas[0][: len(ENCABEZADOS)]]
    if encabezado != ENCABEZADOS:
        sys.exit(f"Encabezados inesperados: {encabezado}. Se esperaba: {ENCABEZADOS}")

    aulas, vistos, descartadas = [], set(), 0
    for nro, fila in enumerate(filas[1:], start=2):
        sede = str(fila[0] or "").strip().upper()
        # Debajo de la tabla pueden quedar restos de versiones viejas del archivo con otro formato: se ignoran.
        if sede not in SEDES_VALIDAS:
            descartadas += 1
            continue
        codigo = str(fila[1]).strip()
        clave = (sede, codigo.upper())
        if clave in vistos:
            print(f"  Fila {nro}: aula duplicada {sede}/{codigo}, se ignora.")
            continue
        vistos.add(clave)
        piso = str(fila[2]).strip()
        if re.fullmatch(r"[sS]\d+", piso):
            piso = piso.upper()  # el Excel mezcla "s1" y "S1" para el mismo subsuelo
        numero = str(fila[3]).strip()
        tipo = str(fila[4]).strip()
        aulas.append(
            {
                "sede": sede,
                "codigo": codigo,
                "piso_texto": piso,
                "piso": piso_numerico(piso, numero),
                "numero_aula": numero,
                "tipo_detalle": tipo,
                "tipo_grupo": tipo_grupo(tipo),
                "capacidad_real": int(fila[5] or 0),
            }
        )
    print(f"Excel: {len(aulas)} aulas válidas, {descartadas} filas ignoradas (sede vacía o formato viejo).")
    return aulas


def generar_sql(aulas: list[dict]) -> str:
    valores = ",\n".join(
        f"({sql_texto(a['sede'])}, {sql_texto(a['codigo'])}, {sql_texto(a['tipo_grupo'])}, "
        f"{sql_texto(a['tipo_detalle'])}, {a['piso']}, {sql_texto(a['piso_texto'])}, "
        f"{sql_texto(a['numero_aula'])}, {a['capacidad_real']})"
        for a in aulas
    )
    return f"""
BEGIN;

CREATE TEMP TABLE aula_excel (
    sede VARCHAR(50), codigo VARCHAR(50), tipo_grupo VARCHAR(50), tipo_detalle VARCHAR(255),
    piso INT, piso_texto VARCHAR(50), numero_aula VARCHAR(50), capacidad_real INT
) ON COMMIT DROP;

INSERT INTO aula_excel VALUES
{valores};

INSERT INTO aula (sede_id, codigo, tipo_grupo, tipo_detalle, piso, piso_texto, numero_aula,
                  capacidad_real, capacidad_total, activa)
SELECT s.id, e.codigo, e.tipo_grupo, e.tipo_detalle, e.piso, e.piso_texto, e.numero_aula,
       e.capacidad_real, e.capacidad_real, true
FROM aula_excel e
JOIN sede s ON s.codigo = e.sede
ON CONFLICT (sede_id, codigo) DO UPDATE SET
    tipo_grupo = EXCLUDED.tipo_grupo,
    tipo_detalle = EXCLUDED.tipo_detalle,
    piso = EXCLUDED.piso,
    piso_texto = EXCLUDED.piso_texto,
    numero_aula = EXCLUDED.numero_aula,
    capacidad_real = EXCLUDED.capacidad_real,
    capacidad_total = EXCLUDED.capacidad_real,
    activa = true;

-- Aulas que ya no están en el Excel: se borran si nadie las usa, si no quedan inactivas.
CREATE TEMP TABLE aula_fuera AS
SELECT a.id FROM aula a JOIN sede s ON s.id = a.sede_id
WHERE NOT EXISTS (SELECT 1 FROM aula_excel e WHERE e.sede = s.codigo AND e.codigo = a.codigo);

DELETE FROM aula WHERE id IN (SELECT id FROM aula_fuera)
  AND id NOT IN (SELECT aula_id FROM asignacion WHERE aula_id IS NOT NULL);
UPDATE aula SET activa = false WHERE id IN (SELECT id FROM aula_fuera);

COMMIT;

SELECT s.codigo AS sede, count(*) FILTER (WHERE a.activa) AS activas, count(*) FILTER (WHERE NOT a.activa) AS inactivas
FROM aula a JOIN sede s ON s.id = a.sede_id GROUP BY s.codigo ORDER BY s.codigo;
"""


def password_postgres() -> str:
    if os.environ.get("PGPASSWORD"):
        return os.environ["PGPASSWORD"]
    salida = subprocess.run(
        ["dotnet", "user-secrets", "list", "--project", str(RAIZ / "UadeAulas.Api")],
        capture_output=True, text=True, check=True,
    ).stdout
    m = re.search(r"Password=([^;]*)", salida)
    if not m:
        sys.exit("No encontré la contraseña de Postgres (PGPASSWORD o user-secrets).")
    return m.group(1)


def main() -> None:
    ruta = Path(sys.argv[1]) if len(sys.argv) > 1 else RAIZ / "uade_aulas.xlsx"
    aulas = leer_excel(ruta)
    with tempfile.NamedTemporaryFile("w", suffix=".sql", delete=False, encoding="utf-8") as f:
        f.write(generar_sql(aulas))
        archivo_sql = f.name
    entorno = {**os.environ, "PGPASSWORD": password_postgres(), "PGGSSENCMODE": "disable", "PGCLIENTENCODING": "UTF8"}
    try:
        subprocess.run(
            [PSQL, "-h", "localhost", "-U", "postgres", "-d", "uade_aulas", "-v", "ON_ERROR_STOP=1", "-f", archivo_sql],
            env=entorno, check=True,
        )
    finally:
        os.unlink(archivo_sql)


if __name__ == "__main__":
    main()
