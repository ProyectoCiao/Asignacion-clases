import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MapaService } from '../../services/mapa.service';
import { MapaAula } from '../../models/mapa.model';
import { OfertaService } from '../../../oferta/services/oferta.service';
import { Aula, OfertaRow, Sede } from '../../../oferta/models/oferta.model';

const DIAS = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];

@Component({
  selector: 'app-mapa-vista',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './mapa-vista.html',
  styleUrl: './mapa-vista.css',
})
export class MapaVistaComponent implements OnInit {
  private readonly mapaService = inject(MapaService);
  private readonly ofertaService = inject(OfertaService);

  protected readonly dias = DIAS;

  protected readonly sedes = signal<Sede[]>([]);
  protected readonly aulasCatalogo = signal<Aula[]>([]);
  protected readonly aulasMapa = signal<MapaAula[]>([]);
  protected readonly ofertaFilas = signal<OfertaRow[]>([]);
  protected readonly cargando = signal(false);
  protected readonly error = signal<string | null>(null);

  protected sedeSeleccionada = '';
  protected pisoSeleccionado = '';
  protected turnoSeleccionado = '';
  protected diaSeleccionado = '';

  ngOnInit(): void {
    this.cargarCatalogos();
  }

  /** Reintenta lo que haya fallado: si no hay sedes cargadas, reintenta catálogos; si no, reintenta la búsqueda del mapa. */
  protected reintentar(): void {
    if (this.sedes().length === 0) {
      this.cargarCatalogos();
    } else {
      this.buscar();
    }
  }

  /** No usar computed(): depende de sedeSeleccionada, que es un campo ngModel, no una signal. */
  protected pisosDisponibles(): number[] {
    const sedeId = Number(this.sedeSeleccionada);
    if (!sedeId) return [];
    const pisos = new Set(
      this.aulasCatalogo()
        .filter((a) => a.sedeId === sedeId && a.activa)
        .map((a) => a.piso),
    );
    return [...pisos].sort((a, b) => a - b);
  }

  protected onCambioSede(): void {
    const pisos = this.pisosDisponibles();
    this.pisoSeleccionado = pisos.length > 0 ? String(pisos[0]) : '';
    this.cargarOfertaSede();
    this.buscar();
  }

  protected buscar(): void {
    if (!this.sedeSeleccionada || this.pisoSeleccionado === '') {
      this.aulasMapa.set([]);
      return;
    }

    this.cargando.set(true);
    this.error.set(null);

    this.mapaService
      .obtenerMapa({
        sedeId: Number(this.sedeSeleccionada),
        piso: Number(this.pisoSeleccionado),
        turno: this.turnoSeleccionado || undefined,
        dia: this.diaSeleccionado || undefined,
      })
      .subscribe({
        next: (aulas) => {
          this.aulasMapa.set(aulas);
          this.cargando.set(false);
        },
        error: (err: HttpErrorResponse) => {
          this.error.set(this.extraerMensajeError(err));
          this.aulasMapa.set([]);
          this.cargando.set(false);
        },
      });
  }

  /**
   * Capacidad real del aula, cruzada contra el catálogo de Aulas (por aulaId) — misma fuente de
   * verdad que usan las pantallas Aulas y Planificación final, en vez de confiar ciegamente en lo
   * que devuelva /mapa.
   */
  protected capacidadReal(aula: MapaAula): number {
    return this.aulasCatalogo().find((a) => a.id === aula.aulaId)?.capacidadTotal ?? aula.capacidadTotal;
  }

  protected sillas(aula: MapaAula): boolean[] {
    const capacidad = this.capacidadReal(aula);
    return Array.from({ length: capacidad }, (_, i) => i < aula.ocupados);
  }

  /** Columnas de la grilla de sillas: una forma rectangular razonable, más ancha que profunda, como un aula real. */
  protected columnas(capacidad: number): number {
    if (capacidad <= 0) return 1;
    return Math.min(14, Math.max(4, Math.ceil(Math.sqrt(capacidad * 1.8))));
  }

  protected sillasVacias(aula: MapaAula): number {
    return this.capacidadReal(aula) - aula.ocupados;
  }

  /** MapaDto no trae turno; se cruza con /oferta (por nroClase). */
  protected turnoDeAula(aula: MapaAula): string | null {
    if (aula.claseNro == null) return null;
    const fila = this.ofertaFilas().find((f) => f.nroClase === aula.claseNro);
    return fila?.turno ?? null;
  }

  private cargarOfertaSede(): void {
    const sedeId = Number(this.sedeSeleccionada);
    if (!sedeId) {
      this.ofertaFilas.set([]);
      return;
    }
    this.ofertaService.obtenerOferta({ sedeId }).subscribe({
      next: (filas) => this.ofertaFilas.set(filas),
      error: () => {
        // El mapa sigue siendo consultable aunque falle la carga de oferta; solo se pierde el badge de turno.
      },
    });
  }

  private cargarCatalogos(): void {
    this.error.set(null);
    this.ofertaService.obtenerCatalogos().subscribe({
      next: (catalogos) => {
        this.sedes.set(catalogos.sedes);
        this.aulasCatalogo.set(catalogos.aulas);
        if (catalogos.sedes.length > 0) {
          this.sedeSeleccionada = String(catalogos.sedes[0].id);
          this.onCambioSede();
        }
      },
      error: () => {
        this.error.set('No se pudieron cargar las sedes y aulas.');
      },
    });
  }

  private extraerMensajeError(err: HttpErrorResponse): string {
    const cuerpo = err.error as { error?: string } | null;
    if (cuerpo?.error) {
      return cuerpo.error;
    }
    return 'No se pudo cargar el mapa. Verificá que el backend esté disponible.';
  }
}
