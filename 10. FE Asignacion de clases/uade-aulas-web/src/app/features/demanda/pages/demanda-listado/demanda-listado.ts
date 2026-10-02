import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { DemandaService } from '../../services/demanda.service';
import { DemandaRow } from '../../models/demanda.model';
import { OrdenTabla } from '../../../../shared/utils/ordenar-tabla';

@Component({
  selector: 'app-demanda-listado',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './demanda-listado.html',
  styleUrl: './demanda-listado.css',
})
export class DemandaListadoComponent implements OnInit {
  private readonly demandaService = inject(DemandaService);

  protected readonly filas = signal<DemandaRow[]>([]);
  protected readonly cargando = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly actualizando = signal(false);

  protected readonly orden = new OrdenTabla<DemandaRow>();
  protected readonly filasOrdenadas = computed(() => this.orden.aplicar(this.filas()));

  ngOnInit(): void {
    this.cargar();
  }

  protected cargar(): void {
    this.cargando.set(true);
    this.error.set(null);

    this.demandaService.obtenerDemanda().subscribe({
      next: (filas) => {
        this.filas.set(filas);
        this.cargando.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(this.extraerMensajeError(err));
        this.cargando.set(false);
      },
    });
  }

  protected actualizar(): void {
    this.actualizando.set(true);
    this.error.set(null);

    this.demandaService.actualizarDemanda().subscribe({
      next: (respuesta) => {
        this.filas.set(respuesta.demandas);
        this.actualizando.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set('Error al actualizar demanda: ' + this.extraerMensajeError(err));
        this.actualizando.set(false);
      },
    });
  }

  /** brecha = solicitudes - asientos: positivo significa que la demanda supera los asientos (faltan sillas). */
  protected obtenerClaseBrecha(brecha: number): string {
    if (brecha > 0) return 'demanda__brecha--deficit';
    if (brecha < 0) return 'demanda__brecha--exceso';
    return '';
  }

  protected obtenerTextoBrecha(brecha: number): string {
    if (brecha > 0) return `Faltan ${brecha} sillas`;
    if (brecha < 0) return `Sobran ${Math.abs(brecha)} sillas`;
    return 'Justo a tiempo';
  }

  private extraerMensajeError(err: HttpErrorResponse): string {
    const cuerpo = err.error as { error?: string } | null;
    if (cuerpo?.error) {
      return cuerpo.error;
    }
    return 'No se pudo cargar la demanda. Verificá que el backend esté disponible.';
  }
}
