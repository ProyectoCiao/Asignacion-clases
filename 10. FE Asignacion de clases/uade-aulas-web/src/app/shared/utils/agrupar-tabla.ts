export interface CampoAgrupador<T> {
  etiqueta: string;
  valor: (fila: T) => unknown;
  formatear?: (valor: unknown) => string;
}

export type FilaAgrupada<T> =
  | { tipo: 'grupo'; nivel: number; etiqueta: string; cantidad: number }
  | { tipo: 'fila'; fila: T };

/** Agrupa filas de forma jerárquica (general a particular) según los campos activos, en ese orden. */
export function agruparFilas<T>(filas: T[], campos: CampoAgrupador<T>[]): FilaAgrupada<T>[] {
  if (campos.length === 0) {
    return filas.map((fila) => ({ tipo: 'fila', fila }));
  }
  const resultado: FilaAgrupada<T>[] = [];
  agruparNivel(filas, campos, 0, resultado);
  return resultado;
}

function agruparNivel<T>(filas: T[], campos: CampoAgrupador<T>[], nivel: number, resultado: FilaAgrupada<T>[]): void {
  const campoActual = campos[nivel];
  const grupos = new Map<string, T[]>();

  for (const fila of filas) {
    const valor = campoActual.valor(fila);
    const clave = String(valor ?? '—');
    if (!grupos.has(clave)) {
      grupos.set(clave, []);
    }
    grupos.get(clave)!.push(fila);
  }

  const clavesOrdenadas = [...grupos.keys()].sort((a, b) => a.localeCompare(b, 'es', { numeric: true }));

  for (const clave of clavesOrdenadas) {
    const filasGrupo = grupos.get(clave)!;
    const valorOriginal = campoActual.valor(filasGrupo[0]);
    const etiquetaValor = campoActual.formatear ? campoActual.formatear(valorOriginal) : clave;

    resultado.push({
      tipo: 'grupo',
      nivel,
      etiqueta: `${campoActual.etiqueta}: ${etiquetaValor}`,
      cantidad: filasGrupo.length,
    });

    if (nivel + 1 < campos.length) {
      agruparNivel(filasGrupo, campos, nivel + 1, resultado);
    } else {
      for (const fila of filasGrupo) {
        resultado.push({ tipo: 'fila', fila });
      }
    }
  }
}
