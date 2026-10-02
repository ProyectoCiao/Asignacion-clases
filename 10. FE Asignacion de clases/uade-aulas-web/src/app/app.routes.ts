import { Routes } from '@angular/router';
import { OfertaListadoComponent } from './features/oferta/pages/oferta-listado/oferta-listado';
import { DemandaListadoComponent } from './features/demanda/pages/demanda-listado/demanda-listado';
import { PlanificacionFinalComponent } from './features/planificacion-final/pages/planificacion-final/planificacion-final';
import { MapaVistaComponent } from './features/mapa/pages/mapa-vista/mapa-vista';
import { AulasDashboardComponent } from './features/aulas/pages/aulas-dashboard/aulas-dashboard';
import { MateriasDashboardComponent } from './features/materias/pages/materias-dashboard/materias-dashboard';
import { DocentesDashboardComponent } from './features/docentes/pages/docentes-dashboard/docentes-dashboard';

export const routes: Routes = [
  {
    path: '',
    component: OfertaListadoComponent,
  },
  {
    path: 'demanda',
    component: DemandaListadoComponent,
  },
  {
    path: 'planificacion-final',
    component: PlanificacionFinalComponent,
  },
  {
    path: 'mapa',
    component: MapaVistaComponent,
  },
  {
    path: 'aulas',
    component: AulasDashboardComponent,
  },
  {
    path: 'materias',
    component: MateriasDashboardComponent,
  },
  {
    path: 'docentes',
    component: DocentesDashboardComponent,
  },
  { path: '**', redirectTo: '' },
];
