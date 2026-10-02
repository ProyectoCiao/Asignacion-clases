import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { OfertaService } from '../../../oferta/services/oferta.service';
import { PlanificacionService } from '../../../oferta/services/planificacion.service';
import { Aula, Catalogos, Choque, OfertaFiltros, OfertaRow, Sobrecupo } from '../../../oferta/models/oferta.model';
import { OrdenTabla } from '../../../../shared/utils/ordenar-tabla';
import { CampoAgrupador, agruparFilas } from '../../../../shared/utils/agrupar-tabla';
import { coincideBusqueda } from '../../../../shared/utils/busqueda-tolerante';

const NOMBRES_TURNO: Record<string, string> = { MANANA: 'Mañana', TARDE: 'Tarde', NOCHE: 'Noche' };

@Component({
  selector: 'app-planificacion-final',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './planificacion-final.html',
  styleUrl: './planificacion-final.css',
})
export class PlanificacionFinalComponent implements OnInit {
  private readonly ofertaService = inject(OfertaService);
  private readonly planificacionService = inject(PlanificacionService);
  private readonly route = inject(ActivatedRoute);

  protected readonly filas = signal<OfertaRow[]>([]);
  protected readonly catalogos = signal<Catalogos>({ sedes: [], facultades: [], aulas: [] });
  protected readonly cargando = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly modalConfirmacionAbierto = signal(false);
  protected readonly grabando = signal(false);
  protected readonly resultadoGrabado = signal<{ choques: Choque[]; sobrecupo: Sobrecupo[] } | null>(null);
  protected filasOriginales: Map<number, OfertaRow> = new Map();

  /** Selector de aula en cascada (Sede fija de la clase → Edificio → Piso → Aula): evita saltar de edificio sin querer. */
  protected readonly filaSeleccionAula = signal<OfertaRow | null>(null);
  protected edificioSeleccionModal = '';
  protected pisoSeleccionModal = '';
  protected aulaSeleccionModal = '';

  protected readonly modalFinalizarAbierto = signal(false);
  protected readonly finalizando = signal(false);
  protected readonly errorFinalizar = signal<string | null>(null);
  protected readonly resultadoFinalizar = signal<string | null>(null);
  protected cuatrimestreFinal = '1';
  protected anioFinal = new Date().getFullYear();

  protected readonly orden = new OrdenTabla<OfertaRow>();
  protected readonly nivelesActivos = signal(1);

  protected sedeSeleccionada = '';
  protected turnoSeleccionado = '';
  protected facultadSeleccionada = '';
  protected busqueda = '';

  /** Sede → Edificio → Piso → Tipo → Turno: de lo general a lo particular. Edificio se cruza por aulaId contra el catálogo. */
  protected readonly camposAgrupadores = computed<CampoAgrupador<OfertaRow>[]>(() => {
    const aulasPorId = new Map(this.catalogos().aulas.map((a) => [a.id, a]));
    return [
      { etiqueta: 'Sede', valor: (f: OfertaRow) => f.sedeNombre },
      {
        etiqueta: 'Edificio',
        valor: (f: OfertaRow) => (f.aulaId != null ? aulasPorId.get(f.aulaId)?.edificio : undefined),
        formatear: (v) => (v ? String(v) : 'Sin edificio / sin aula asignada'),
      },
      {
        etiqueta: 'Piso',
        valor: (f: OfertaRow) => f.aulaPiso,
        formatear: (v) => (v != null ? `Piso ${v}` : 'Sin aula asignada'),
      },
      {
        etiqueta: 'Tipo de aula',
        valor: (f: OfertaRow) => f.aulaTipo,
        formatear: (v) => (v ? String(v) : 'Sin aula asignada'),
      },
      {
        etiqueta: 'Turno',
        valor: (f: OfertaRow) => f.turno,
        formatear: (v) => NOMBRES_TURNO[v as string] ?? String(v),
      },
    ];
  });

  protected readonly camposActivos = computed(() => this.camposAgrupadores().slice(0, this.nivelesActivos()));

  ngOnInit(): void {
    const materiaQuery = this.route.snapshot.queryParamMap.get('materia');
    if (materiaQuery) {
      this.busqueda = materiaQuery;
    }
    this.cargarCatalogos();
    this.buscar();
  }

  protected buscar(): void {
    this.cargando.set(true);
    this.error.set(null);
    this.resultadoGrabado.set(null);

    const filtros: OfertaFiltros = {
      sedeId: this.sedeSeleccionada ? Number(this.sedeSeleccionada) : undefined,
      turno: this.turnoSeleccionado || undefined,
      facultad: this.facultadSeleccionada || undefined,
    };

    this.ofertaService.obtenerOferta(filtros).subscribe({
      next: (filas) => {
        this.filas.set(filas);
        this.filasOriginales = new Map(filas.map((f) => [f.claseId, { ...f }]));
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

  /** Cascada general→particular: activar un nivel activa también los anteriores; desactivarlo apaga los siguientes. */
  protected toggleNivel(indice: number): void {
    this.nivelesActivos.set(indice < this.nivelesActivos() ? indice : indice + 1);
  }

  /** aulaCodigo/aulaCapacidad en la fila quedan desactualizados tras editar aulaId; estos lookups siempre están al día. */
  protected codigoAulaActual(fila: OfertaRow): string {
    if (fila.aulaId == null) return 'Sin asignar';
    return this.catalogos().aulas.find((a) => a.id === fila.aulaId)?.codigo ?? 'Sin asignar';
  }

  protected capacidadAulaActual(fila: OfertaRow): number | string {
    if (fila.aulaId == null) return '—';
    return this.catalogos().aulas.find((a) => a.id === fila.aulaId)?.capacidadTotal ?? '—';
  }

  protected abrirSelectorAula(fila: OfertaRow): void {
    const aulaActual = fila.aulaId != null ? this.catalogos().aulas.find((a) => a.id === fila.aulaId) : undefined;
    this.edificioSeleccionModal = aulaActual?.edificio ?? '';
    this.pisoSeleccionModal = aulaActual ? String(aulaActual.piso) : '';
    this.aulaSeleccionModal = fila.aulaId != null ? String(fila.aulaId) : '';
    this.filaSeleccionAula.set(fila);
  }

  protected cerrarSelectorAula(): void {
    this.filaSeleccionAula.set(null);
  }

  protected onCambioEdificioModal(): void {
    this.pisoSeleccionModal = '';
    this.aulaSeleccionModal = '';
  }

  protected onCambioPisoModal(): void {
    this.aulaSeleccionModal = '';
  }

  /** Edificios disponibles en la sede de la clase que se está editando — primer nivel, vinculante. */
  protected edificiosSeleccionModal(): string[] {
    const fila = this.filaSeleccionAula();
    if (!fila) return [];
    const edificios = new Set(
      this.catalogos()
        .aulas.filter((a) => a.sedeId === fila.sedeId)
        .map((a) => a.edificio)
        .filter((e): e is string => !!e),
    );
    return [...edificios].sort((a, b) => a.localeCompare(b, 'es'));
  }

  /** Si las aulas de la sede no tienen edificio cargado (el Excel de aulas no lo trae), se salta ese nivel. */
  protected sinEdificiosModal(): boolean {
    return this.edificiosSeleccionModal().length === 0;
  }

  /** Pisos disponibles dentro del edificio elegido — segundo nivel, vinculante al edificio. */
  protected pisosSeleccionModal(): number[] {
    const fila = this.filaSeleccionAula();
    if (!fila || (!this.edificioSeleccionModal && !this.sinEdificiosModal())) return [];
    const pisos = new Set(
      this.catalogos()
        .aulas.filter((a) => a.sedeId === fila.sedeId && (this.sinEdificiosModal() || a.edificio === this.edificioSeleccionModal))
        .map((a) => a.piso),
    );
    return [...pisos].sort((a, b) => a - b);
  }

  /** Aulas disponibles dentro del piso elegido — tercer nivel, vinculante al edificio+piso. */
  protected aulasSeleccionModal(): Aula[] {
    const fila = this.filaSeleccionAula();
    if (!fila || (!this.edificioSeleccionModal && !this.sinEdificiosModal()) || this.pisoSeleccionModal === '') return [];
    return this.catalogos().aulas.filter(
      (a) =>
        a.sedeId === fila.sedeId &&
        (this.sinEdificiosModal() || a.edificio === this.edificioSeleccionModal) &&
        a.piso === Number(this.pisoSeleccionModal),
    );
  }

  protected confirmarSelectorAula(): void {
    const fila = this.filaSeleccionAula();
    if (!fila) return;
    fila.aulaId = this.aulaSeleccionModal ? Number(this.aulaSeleccionModal) : undefined;
    this.cerrarSelectorAula();
  }

  protected quitarAulaSeleccionModal(): void {
    const fila = this.filaSeleccionAula();
    if (!fila) return;
    fila.aulaId = undefined;
    this.cerrarSelectorAula();
  }

  protected abrirModalFinalizar(): void {
    this.errorFinalizar.set(null);
    this.resultadoFinalizar.set(null);
    this.modalFinalizarAbierto.set(true);
  }

  protected cerrarModalFinalizar(): void {
    this.modalFinalizarAbierto.set(false);
  }

  protected finalizarPlanificacion(): void {
    this.finalizando.set(true);
    this.errorFinalizar.set(null);
    this.resultadoFinalizar.set(null);

    this.planificacionService.finalizarPlanificacion(Number(this.cuatrimestreFinal), this.anioFinal).subscribe({
      next: (respuesta) => {
        this.resultadoFinalizar.set(`${respuesta.mensaje} Quedó guardada como ${respuesta.periodoNombre}.`);
        this.finalizando.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.errorFinalizar.set(
          err.status === 404 || err.status === 0
            ? 'Esta función todavía no está disponible: el backend no implementó POST /planificacion/finalizar.'
            : 'Error al finalizar: ' + this.extraerMensajeError(err),
        );
        this.finalizando.set(false);
      },
    });
  }

  /** Filtro de texto tolerante a acentos, agrupación y orden, todo aplicado en el cliente. */
  protected filasAgrupadas() {
    const filtradas = this.filas().filter((f) => this.coincideFila(f));
    return agruparFilas(this.orden.aplicar(filtradas), this.camposActivos());
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

  protected obtenerCambios(): number {
    let cantidad = 0;
    this.filas().forEach((fila) => {
      const original = this.filasOriginales.get(fila.claseId);
      if (original && original.aulaId !== fila.aulaId) {
        cantidad++;
      }
    });
    return cantidad;
  }

  protected abrirConfirmacion(): void {
    if (this.obtenerCambios() === 0) return;
    this.resultadoGrabado.set(null);
    this.modalConfirmacionAbierto.set(true);
  }

  protected cerrarModal(): void {
    this.modalConfirmacionAbierto.set(false);
  }

  protected grabar(): void {
    this.grabando.set(true);
    const cambios = this.filas()
      .filter((fila) => {
        const original = this.filasOriginales.get(fila.claseId);
        return original && original.aulaId !== fila.aulaId;
      })
      .map((fila) => ({
        id: fila.claseId,
        aulaId: fila.aulaId,
        inscriptosConReserva: fila.inscriptosConReserva,
        turno: fila.turno,
      }));

    this.ofertaService.grabarCambios(cambios).subscribe({
      next: (respuesta) => {
        this.filas.set(respuesta.filas);
        this.filasOriginales = new Map(respuesta.filas.map((f) => [f.claseId, { ...f }]));
        this.resultadoGrabado.set({ choques: respuesta.choques, sobrecupo: respuesta.sobrecupo });
        this.grabando.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set('Error al grabar: ' + this.extraerMensajeError(err));
        this.grabando.set(false);
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
    return 'No se pudo cargar la planificación. Verificá que el backend esté disponible.';
  }
}
