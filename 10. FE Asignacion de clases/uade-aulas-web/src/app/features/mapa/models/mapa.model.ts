export interface MapaAula {
  aulaId: number;
  codigo: string;
  tipoGrupo: string;
  capacidadTotal: number;
  ocupados: number;
  claseNro?: number;
  materia?: string;
  horario?: string;
  docentes?: string;
}

export interface MapaFiltros {
  sedeId: number;
  piso: number;
  turno?: string;
  dia?: string;
}
