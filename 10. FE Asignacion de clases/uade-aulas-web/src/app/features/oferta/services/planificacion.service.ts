import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { CargaPlanificacionResponse, Periodo } from '../models/oferta.model';

@Injectable({ providedIn: 'root' })
export class PlanificacionService {
  private readonly http = inject(HttpClient);

  /** Período al que corresponden la oferta, la demanda y el mapa (404 si no hay períodos cargados). */
  obtenerPeriodoActual(): Observable<Periodo> {
    return this.http.get<Periodo>(`${environment.apiBaseUrl}/periodos/actual`);
  }

  /** Reemplaza la planificación del cuatrimestre/año indicado con el contenido del Excel. */
  cargarExcel(cuatrimestre: number, anio: number, archivo: File): Observable<CargaPlanificacionResponse> {
    const formData = new FormData();
    formData.append('cuatrimestre', String(cuatrimestre));
    formData.append('anio', String(anio));
    formData.append('archivo', archivo);
    return this.http.post<CargaPlanificacionResponse>(`${environment.apiBaseUrl}/planificacion/cargar`, formData);
  }

  /** Pendiente de backend: hoy no existe /planificacion/finalizar (404 hasta que se implemente). */
  finalizarPlanificacion(cuatrimestre: number, anio: number): Observable<CargaPlanificacionResponse> {
    return this.http.post<CargaPlanificacionResponse>(`${environment.apiBaseUrl}/planificacion/finalizar`, {
      cuatrimestre,
      anio,
    });
  }
}
