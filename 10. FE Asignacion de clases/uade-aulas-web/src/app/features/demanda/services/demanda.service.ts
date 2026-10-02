import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { DemandaRow } from '../models/demanda.model';

@Injectable({ providedIn: 'root' })
export class DemandaService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/demanda`;

  obtenerDemanda(): Observable<DemandaRow[]> {
    return this.http.get<DemandaRow[]>(this.baseUrl);
  }

  actualizarDemanda(): Observable<{ demandas: DemandaRow[] }> {
    return this.http.post<{ demandas: DemandaRow[] }>(`${this.baseUrl}/actualizar`, {});
  }
}
