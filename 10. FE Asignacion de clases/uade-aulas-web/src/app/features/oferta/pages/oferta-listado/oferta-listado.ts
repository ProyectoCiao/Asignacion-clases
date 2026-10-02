import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { OfertaService } from '../../services/oferta.service';
import { PlanificacionService } from '../../services/planificacion.service';
import { Catalogos, OfertaFiltros, OfertaRow, Periodo } from '../../models/oferta.model';
import { OrdenTabla } from '../../../../shared/utils/ordenar-tabla';
import { coincideBusqueda } from '../../../../shared/utils/busqueda-tolerante';

@Component({
  selector: 'app-oferta-listado',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './oferta-listado.html',
  styleUrl: './oferta-listado.css',
})
export class OfertaListadoComponent implements OnInit {
  private readonly ofertaService = inject(OfertaService);
  private readonly planificacionService = inject(PlanificacionService);

  protected readonly filas = signal<OfertaRow[]>([]);
  protected readonly catalogos = signal<Catalogos>({ sedes: [], facultades: [], aulas: [] });
  protected readonly cargando = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly periodoActual = signal<Periodo | null>(null);
  protected readonly periodoDesconocido = signal(false);

  protected readonly panelCargaAbierto = signal(false);
  protected readonly cargandoExcel = signal(false);
  protected readonly errorCarga = signal<string | null>(null);
  protected readonly resultadoCarga = signal<string | null>(null);
  protected cuatrimestreSeleccionado = '1';
  protected anioSeleccionado = new Date().getFullYear();
  protected archivoSeleccionado: File | null = null;

  protected readonly orden = new OrdenTabla<OfertaRow>();

  protected sedeSeleccionada = '';
  protected turnoSeleccionado = '';
  protected facultadSeleccionada = '';
  protected busqueda = '';

  ngOnInit(): void {
    this.cargarCatalogos();
    this.cargarPeriodoActual();
    this.buscar();
  }

  protected buscar(): void {
    this.cargando.set(true);
    this.error.set(null);

    const filtros: OfertaFiltros = {
      sedeId: this.sedeSeleccionada ? Number(this.sedeSeleccionada) : undefined,
      turno: this.turnoSeleccionado || undefined,
      facultad: this.facultadSeleccionada || undefined,
    };

    this.ofertaService.obtenerOferta(filtros).subscribe({
      next: (filas) => {
        this.filas.set(filas);
        this.cargando.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(this.extraerMensajeError(err));
        this.filas.set([]);
        this.cargando.set(false);
      },
    });
  }

  protected limpiarFiltros(): void {
    this.sedeSeleccionada = '';
    this.turnoSeleccionado = '';
    this.facultadSeleccionada = '';
    this.busqueda = '';
    this.buscar();
  }

  /** Filtro de texto tolerante a acentos, aplicado en el cliente (el `q` del backend es sensible a acentos). */
  protected filasVisibles(): OfertaRow[] {
    const filtradas = this.filas().filter((f) => this.coincideFila(f));
    return this.orden.aplicar(filtradas);
  }

  /** Sugerencias para el autocompletado del buscador, a partir de los datos ya cargados. */
  protected sugerenciasBusqueda(): string[] {
    const valores = new Set<string>();
    for (const f of this.filas()) {
      valores.add(f.materiaNombre);
      valores.add(f.materiaCodigo);
      if (f.docentes) {
        for (const docente of f.docentes.split(',')) {
          valores.add(docente.trim());
        }
      }
      if (f.aulaCodigo) {
        valores.add(f.aulaCodigo);
      }
    }
    return [...valores].filter(Boolean).sort((a, b) => a.localeCompare(b, 'es'));
  }

  protected abrirPanelCarga(): void {
    this.errorCarga.set(null);
    this.resultadoCarga.set(null);
    this.archivoSeleccionado = null;
    this.panelCargaAbierto.set(true);
  }

  protected cerrarPanelCarga(): void {
    this.panelCargaAbierto.set(false);
  }

  protected onArchivoSeleccionado(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.archivoSeleccionado = input.files?.[0] ?? null;
  }

  protected subirExcel(): void {
    if (!this.archivoSeleccionado) return;

    this.cargandoExcel.set(true);
    this.errorCarga.set(null);
    this.resultadoCarga.set(null);

    this.planificacionService
      .cargarExcel(Number(this.cuatrimestreSeleccionado), this.anioSeleccionado, this.archivoSeleccionado)
      .subscribe({
        next: (respuesta) => {
          this.resultadoCarga.set(
            `${respuesta.mensaje} (${respuesta.clasesCargadas} clases cargadas para ${respuesta.periodoNombre}).`,
          );
          this.cargandoExcel.set(false);
          this.cargarPeriodoActual();
          this.buscar();
        },
        error: (err: HttpErrorResponse) => {
          // status 0 también cuenta como "no existe": si la ruta no está mapeada, el backend puede
          // no agregar headers de CORS a la respuesta 404, y el navegador la reporta como error de red (status 0).
          this.errorCarga.set(
            err.status === 404 || err.status === 0
              ? 'Esta función todavía no está disponible: el backend no implementó POST /planificacion/cargar.'
              : 'Error al subir el archivo: ' + this.extraerMensajeError(err),
          );
          this.cargandoExcel.set(false);
        },
      });
  }

  private cargarPeriodoActual(): void {
    this.planificacionService.obtenerPeriodoActual().subscribe({
      next: (periodo) => {
        this.periodoActual.set(periodo);
        this.periodoDesconocido.set(false);
      },
      error: () => {
        this.periodoActual.set(null);
        this.periodoDesconocido.set(true);
      },
    });
  }

  private coincideFila(f: OfertaRow): boolean {
    if (!this.busqueda.trim()) return true;
    const texto = [f.materiaCodigo, f.materiaNombre, f.docentes, f.aulaCodigo, f.nroClase].filter(Boolean).join(' ');
    return coincideBusqueda(texto, this.busqueda);
  }

  private cargarCatalogos(): void {
    this.ofertaService.obtenerCatalogos().subscribe({
      next: (catalogos) => this.catalogos.set(catalogos),
      error: () => {
        // Los selectores de sede/facultad quedan vacíos; el error de la búsqueda ya se muestra aparte.
      },
    });
  }

  private extraerMensajeError(err: HttpErrorResponse): string {
    const cuerpo = err.error as { error?: string } | null;
    if (cuerpo?.error) {
      return cuerpo.error;
    }
    return 'No se pudo cargar la última planificación. Verificá que el backend esté disponible en el puerto 5080.';
  }
}
