import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { OfertaService } from '../../../oferta/services/oferta.service';
import { Catalogos, OfertaFiltros, OfertaRow } from '../../../oferta/models/oferta.model';
import { OrdenTabla } from '../../../../shared/utils/ordenar-tabla';
import { CampoAgrupador, agruparFilas } from '../../../../shared/utils/agrupar-tabla';

const NOMBRES_TURNO: Record<string, string> = { MANANA: 'Mañana', TARDE: 'Tarde', NOCHE: 'Noche' };

const CAMPOS_AGRUPADORES: CampoAgrupador<OfertaRow>[] = [
  { etiqueta: 'Facultad', valor: (f) => f.facultad, formatear: (v) => (v ? String(v) : 'Sin facultad') },
  { etiqueta: 'Materia', valor: (f) => f.materiaNombre },
  { etiqueta: 'Sede', valor: (f) => f.sedeNombre },
  { etiqueta: 'Turno', valor: (f) => f.turno, formatear: (v) => NOMBRES_TURNO[v as string] ?? String(v) },
];

@Component({
  selector: 'app-materias-dashboard',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './materias-dashboard.html',
  styleUrl: './materias-dashboard.css',
})
export class MateriasDashboardComponent implements OnInit {
  private readonly ofertaService = inject(OfertaService);

  protected readonly camposAgrupadores = CAMPOS_AGRUPADORES;

  protected readonly filas = signal<OfertaRow[]>([]);
  protected readonly catalogos = signal<Catalogos>({ sedes: [], facultades: [], aulas: [] });
  protected readonly cargando = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly orden = new OrdenTabla<OfertaRow>();
  protected readonly nivelesActivos = signal(1);

  protected sedeSeleccionada = '';
  protected turnoSeleccionado = '';
  protected facultadSeleccionada = '';
  protected busqueda = '';

  protected readonly camposActivos = computed(() => this.camposAgrupadores.slice(0, this.nivelesActivos()));

  protected readonly filasAgrupadas = computed(() =>
    agruparFilas(this.orden.aplicar(this.filas()), this.camposActivos()),
  );

  ngOnInit(): void {
    this.cargarCatalogos();
    this.buscar();
  }

  protected buscar(): void {
    this.cargando.set(true);
    this.error.set(null);

    const filtros: OfertaFiltros = {
      sedeId: this.sedeSeleccionada ? Number(this.sedeSeleccionada) : undefined,
      turno: this.turnoSeleccionado || undefined,
      facultad: this.facultadSeleccionada || undefined,
      q: this.busqueda.trim() || undefined,
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

  protected toggleNivel(indice: number): void {
    this.nivelesActivos.set(indice < this.nivelesActivos() ? indice : indice + 1);
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
    return 'No se pudo cargar la información de materias. Verificá que el backend esté disponible.';
  }
}
