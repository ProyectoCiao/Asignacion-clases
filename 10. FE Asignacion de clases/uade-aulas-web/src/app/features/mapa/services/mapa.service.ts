import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { MapaAula, MapaFiltros } from '../models/mapa.model';

@Injectable({ providedIn: 'root' })
export class MapaService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/mapa`;

  obtenerMapa(filtros: MapaFiltros): Observable<MapaAula[]> {
    let params = new HttpParams().set('sedeId', filtros.sedeId).set('piso', filtros.piso);
    if (filtros.turno) {
      params = params.set('turno', filtros.turno);
    }
    if (filtros.dia) {
      params = params.set('dia', filtros.dia);
    }
    return this.http.get<MapaAula[]>(this.baseUrl, { params });
  }
}
