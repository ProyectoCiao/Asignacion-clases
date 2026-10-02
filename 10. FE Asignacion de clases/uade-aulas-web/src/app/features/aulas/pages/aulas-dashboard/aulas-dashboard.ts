import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { OfertaService } from '../../../oferta/services/oferta.service';
import { Aula, OfertaRow } from '../../../oferta/models/oferta.model';
import { OrdenTabla } from '../../../../shared/utils/ordenar-tabla';
import { CampoAgrupador, agruparFilas } from '../../../../shared/utils/agrupar-tabla';

/** Aula + datos derivados del cruce con la demanda, para poder mostrar y ordenar por cualquier columna. */
interface AulaFila extends Aula {
  sedeNombre: string;
  pisoEtiqueta: string;
  estado: string;
  inscriptosTexto: string;
  /** Mayor cantidad de inscriptos entre las clases que ocupan el aula (para ordenar). */
  inscriptosOrden: number | null;
  diferenciaTexto: string;
  /** Peor diferencia (la más negativa) entre las clases que ocupan el aula (para ordenar). */
  diferenciaOrden: number | null;
  sobrecupo: boolean;
}

interface PestanaSede {
  codigo: string;
  etiqueta: string;
}

@Component({
  selector: 'app-aulas-dashboard',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './aulas-dashboard.html',
  styleUrl: './aulas-dashboard.css',
})
export class AulasDashboardComponent implements OnInit {
  private readonly ofertaService = inject(OfertaService);

  protected readonly aulas = signal<Aula[]>([]);
  protected readonly ofertaFilas = signal<OfertaRow[]>([]);
  protected readonly cargando = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly orden = new OrdenTabla<AulaFila>();
  protected readonly nivelesActivos = signal(1);

  /** '' = todas las sedes; si no, el código de la sede de la pestaña elegida. */
  protected readonly pestanaActiva = signal('');
  protected readonly pestanasSede: PestanaSede[] = [
    { codigo: 'MONSERRAT', etiqueta: 'Monserrat' },
    { codigo: 'RECOLETA', etiqueta: 'Recoleta' },
    { codigo: 'BELGRANO', etiqueta: 'Belgrano' },
    { codigo: 'PINAMAR', etiqueta: 'Pinamar' },
  ];

  protected sedeSeleccionada = '';
  protected edificioSeleccionado = '';
  protected pisoSeleccionado = '';
  protected tipoSeleccionado = '';

  private readonly todosLosAgrupadores: CampoAgrupador<AulaFila>[] = [
    { etiqueta: 'Sede', valor: (a) => a.sedeNombre },
    { etiqueta: 'Edificio', valor: (a) => a.edificio, formatear: (v) => (v ? String(v) : 'Sin edificio') },
    { etiqueta: 'Piso', valor: (a) => a.pisoEtiqueta, formatear: (v) => this.etiquetaPiso(String(v)) },
    { etiqueta: 'Tipo', valor: (a) => a.tipoGrupo },
  ];

  /** Dentro de la pestaña de una sede, agrupar por sede no aporta nada. */
  protected readonly camposAgrupadores = computed(() =>
    this.pestanaActiva() ? this.todosLosAgrupadores.filter((c) => c.etiqueta !== 'Sede') : this.todosLosAgrupadores,
  );

  protected readonly camposActivos = computed(() => this.camposAgrupadores().slice(0, this.nivelesActivos()));

  /** Ocupación derivada de la planificación final: se refresca cada vez que se entra a esta pantalla. */
  private readonly ocupacionPorAula = computed(() => {
    const mapa = new Map<number, OfertaRow[]>();
    for (const fila of this.ofertaFilas()) {
      if (fila.aulaId == null) continue;
      const lista = mapa.get(fila.aulaId) ?? [];
      lista.push(fila);
      mapa.set(fila.aulaId, lista);
    }
    return mapa;
  });

  private readonly filas = computed<AulaFila[]>(() => {
    const ocupacion = this.ocupacionPorAula();
    return this.aulas().map((aula) => this.armarFila(aula, ocupacion.get(aula.id) ?? []));
  });

  /** Aulas de la pestaña activa (todas o una sede): base de los filtros en cascada. */
  private readonly filasPestana = computed(() => {
    const sede = this.pestanaActiva();
    return sede ? this.filas().filter((a) => a.sede?.codigo === sede) : this.filas();
  });

  ngOnInit(): void {
    this.cargar();
  }

  protected cargar(): void {
    this.cargando.set(true);
    this.error.set(null);

    this.ofertaService.obtenerAulas().subscribe({
      next: (aulas) => {
        this.aulas.set(aulas);
        this.cargando.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(this.extraerMensajeError(err));
        this.aulas.set([]);
        this.cargando.set(false);
      },
    });

    // Estado actual (ocupada/libre): viene de la planificación final, no bloquea la carga del catálogo si falla.
    this.ofertaService.obtenerOferta({}).subscribe({
      next: (filas) => this.ofertaFilas.set(filas),
      error: () => {
        // El catálogo de aulas se sigue mostrando aunque no se pueda calcular el estado actual.
      },
    });
  }

  protected seleccionarPestana(codigo: string): void {
    if (this.pestanaActiva() === codigo) return;
    this.pestanaActiva.set(codigo);
    // El filtro de sede no aplica dentro de la pestaña de una sede: se limpia junto con la cascada.
    this.sedeSeleccionada = '';
    this.edificioSeleccionado = '';
    this.pisoSeleccionado = '';
    this.nivelesActivos.set(codigo ? 0 : 1);
  }

  protected cantidadPorSede(codigo: string): number {
    return codigo ? this.filas().filter((a) => a.sede?.codigo === codigo).length : this.filas().length;
  }

  private aulasParaCascada(): AulaFila[] {
    return this.sedeSeleccionada
      ? this.filasPestana().filter((a) => a.sedeId === Number(this.sedeSeleccionada))
      : this.filasPestana();
  }

  /** Edificios disponibles para la sede elegida (o todos si no eligió sede) — cascada. */
  protected edificiosDisponibles(): string[] {
    const edificios = new Set(this.aulasParaCascada().map((a) => a.edificio).filter((e): e is string => !!e));
    return [...edificios].sort((a, b) => a.localeCompare(b, 'es'));
  }

  /** Pisos disponibles para la sede/edificio elegidos — cascada. Se usa el piso tal cual el Excel. */
  protected pisosDisponibles(): string[] {
    let aulas = this.aulasParaCascada();
    if (this.edificioSeleccionado) {
      aulas = aulas.filter((a) => a.edificio === this.edificioSeleccionado);
    }
    const pisos = new Map<string, number>();
    for (const a of aulas) pisos.set(a.pisoEtiqueta, a.piso);
    return [...pisos.entries()]
      .sort(([ta, na], [tb, nb]) => na - nb || ta.localeCompare(tb, 'es', { numeric: true }))
      .map(([texto]) => texto);
  }

  protected onCambioSede(): void {
    this.edificioSeleccionado = '';
    this.pisoSeleccionado = '';
  }

  protected onCambioEdificio(): void {
    this.pisoSeleccionado = '';
  }

  protected toggleNivel(indice: number): void {
    this.nivelesActivos.set(indice < this.nivelesActivos() ? indice : indice + 1);
  }

  protected aulasFiltradas() {
    let filtradas = this.aulasParaCascada();
    if (this.edificioSeleccionado) {
      filtradas = filtradas.filter((a) => a.edificio === this.edificioSeleccionado);
    }
    if (this.pisoSeleccionado) {
      filtradas = filtradas.filter((a) => a.pisoEtiqueta === this.pisoSeleccionado);
    }
    if (this.tipoSeleccionado) {
      filtradas = filtradas.filter((a) => a.tipoGrupo === this.tipoSeleccionado);
    }
    return agruparFilas(this.orden.aplicar(filtradas), this.camposActivos());
  }

  protected tiposDisponibles(): string[] {
    const tipos = new Set(this.filasPestana().map((a) => a.tipoGrupo));
    return [...tipos].sort((a, b) => a.localeCompare(b, 'es'));
  }

  protected sedesDisponibles(): { id: number; nombre: string }[] {
    const sedes = new Map<number, string>();
    for (const a of this.aulas()) {
      if (a.sede) sedes.set(a.sede.id, a.sede.nombre);
    }
    return [...sedes.entries()].map(([id, nombre]) => ({ id, nombre })).sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
  }

  protected etiquetaPiso(piso: string): string {
    return /^\d+$/.test(piso) ? `Piso ${piso}` : piso;
  }

  private armarFila(aula: Aula, ocupaciones: OfertaRow[]): AulaFila {
    const base = {
      ...aula,
      sedeNombre: aula.sede?.nombre ?? '—',
      pisoEtiqueta: aula.pisoTexto ?? String(aula.piso),
    };
    if (ocupaciones.length === 0) {
      return {
        ...base,
        estado: 'Libre',
        inscriptosTexto: '—',
        inscriptosOrden: null,
        diferenciaTexto: '—',
        diferenciaOrden: null,
        sobrecupo: false,
      };
    }

    const estado =
      ocupaciones.length === 1
        ? `${ocupaciones[0].materiaNombre} — ${ocupaciones[0].turno}`
        : ocupaciones.map((o) => `${o.materiaNombre} (${o.turno})`).join(' / ');

    // Diferencia contra la capacidad REAL, calculada por CLASE (no sumando turnos distintos, que daría un
    // falso sobrecupo). Si cualquier clase individual se pasa de capacidad, se marca sobrecupo.
    const diferencias = ocupaciones.map((o) => aula.capacidadReal - o.inscriptos);
    return {
      ...base,
      estado,
      inscriptosTexto: ocupaciones.map((o) => o.inscriptos).join(' / '),
      inscriptosOrden: Math.max(...ocupaciones.map((o) => o.inscriptos)),
      diferenciaTexto: diferencias.map((d) => (d < 0 ? `⚠️ Faltan ${Math.abs(d)}` : `Sobran ${d}`)).join(' / '),
      diferenciaOrden: Math.min(...diferencias),
      sobrecupo: diferencias.some((d) => d < 0),
    };
  }

  private extraerMensajeError(err: HttpErrorResponse): string {
    const cuerpo = err.error as { error?: string } | null;
    if (cuerpo?.error) {
      return cuerpo.error;
    }
    return 'No se pudo cargar el catálogo de aulas. Verificá que el backend esté disponible.';
  }
}
