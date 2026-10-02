import { signal } from '@angular/core';

export type Direccion = 'asc' | 'desc';

function comparar<T>(a: T, b: T): number {
  if (a == null && b == null) return 0;
  if (a == null) return 1;
  if (b == null) return -1;
  if (typeof a === 'number' && typeof b === 'number') {
    return a - b;
  }
  return String(a).localeCompare(String(b), 'es', { sensitivity: 'base', numeric: true });
}

/** Estado y lógica de ordenamiento asc/desc para una tabla, reutilizable entre listados. */
export class OrdenTabla<T> {
  private readonly campo = signal<keyof T | null>(null);
  private readonly direccion = signal<Direccion>('asc');

  ordenarPor(campo: keyof T): void {
    if (this.campo() === campo) {
      this.direccion.update((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      this.campo.set(campo);
      this.direccion.set('asc');
    }
  }

  aplicar(filas: T[]): T[] {
    const campo = this.campo();
    if (!campo) return filas;
    const direccion = this.direccion();
    const copia = [...filas];
    copia.sort((a, b) => {
      // Los vacíos van siempre al final, en cualquier dirección.
      if (a[campo] == null || b[campo] == null) return comparar(a[campo], b[campo]);
      const resultado = comparar(a[campo], b[campo]);
      return direccion === 'asc' ? resultado : -resultado;
    });
    return copia;
  }

  indicador(campo: keyof T): string {
    if (this.campo() !== campo) return '';
    return this.direccion() === 'asc' ? '▲' : '▼';
  }

  esActivo(campo: keyof T): boolean {
    return this.campo() === campo;
  }
}
