import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { Aula, CambioClase, Catalogos, GrabarResponse, OfertaFiltros, OfertaRow, SugerenciaAula } from '../models/oferta.model';

@Injectable({ providedIn: 'root' })
export class OfertaService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/oferta`;

  obtenerOferta(filtros: OfertaFiltros = {}): Observable<OfertaRow[]> {
    let params = new HttpParams();
    if (filtros.sedeId != null) {
      params = params.set('sede', filtros.sedeId);
    }
    if (filtros.turno) {
      params = params.set('turno', filtros.turno);
    }
    if (filtros.q) {
      params = params.set('q', filtros.q);
    }
    if (filtros.facultad) {
      params = params.set('facultad', filtros.facultad);
    }
    return this.http.get<OfertaRow[]>(this.baseUrl, { params });
  }

  /** Inventario completo de aulas (incluye CLASE REMOTA), tal como se cargó desde el Excel. */
  obtenerAulas(): Observable<Aula[]> {
    return this.http.get<Aula[]>(`${environment.apiBaseUrl}/aulas`);
  }

  obtenerCatalogos(): Observable<Catalogos> {
    return this.http.get<Catalogos>(`${this.baseUrl}/catalogos`);
  }

  grabarCambios(cambios: CambioClase[]): Observable<GrabarResponse> {
    return this.http.post<GrabarResponse>(`${this.baseUrl}/grabar`, {
      confirmacion: 'GRABAR',
      cambios,
    });
  }

  sugerirAula(claseId: number): Observable<SugerenciaAula> {
    return this.http.post<SugerenciaAula>(`${environment.apiBaseUrl}/cuadre/sugerir`, {
      claseId,
    });
  }
}
