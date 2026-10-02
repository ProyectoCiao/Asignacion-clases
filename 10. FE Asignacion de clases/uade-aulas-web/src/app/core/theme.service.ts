import { Injectable, signal } from '@angular/core';

export type Tema = 'day' | 'night';

const CLAVE_STORAGE = 'uade-theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly tema = signal<Tema>(this.leerTemaGuardado());

  constructor() {
    this.aplicarTema(this.tema());
  }

  alternar(): void {
    const nuevoTema: Tema = this.tema() === 'day' ? 'night' : 'day';
    this.tema.set(nuevoTema);
    this.aplicarTema(nuevoTema);
    try {
      localStorage.setItem(CLAVE_STORAGE, nuevoTema);
    } catch {
      // localStorage no disponible (modo privado, etc.): seguimos sin persistir
    }
  }

  private leerTemaGuardado(): Tema {
    try {
      const guardado = localStorage.getItem(CLAVE_STORAGE);
      return guardado === 'night' ? 'night' : 'day';
    } catch {
      return 'day';
    }
  }

  private aplicarTema(tema: Tema): void {
    document.documentElement.setAttribute('data-theme', tema);
  }
}
