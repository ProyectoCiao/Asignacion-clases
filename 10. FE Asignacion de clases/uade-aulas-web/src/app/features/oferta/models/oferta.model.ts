export interface OfertaRow {
  claseId: number;
  nroClase: number;
  materiaCodigo: string;
  materiaNombre: string;
  facultad?: string;
  departamento?: string;
  sedeId: number;
  sedeCodigo: string;
  sedeNombre: string;
  aulaId?: number;
  aulaCodigo?: string;
  aulaTipo?: string;
  aulaCapacidad?: number;
  aulaPiso?: number;
  turno: string;
  horarioDesde: string;
  horarioHasta: string;
  dias?: string[];
  regimen?: string;
  idioma?: string;
  modalidad?: string;
  inscriptos: number;
  inscriptosConReserva: number;
  docentes: string;
  sillasVacias: number;
  origen: string;
}

export interface Sede {
  id: number;
  codigo: string;
  nombre: string;
}

export interface Aula {
  id: number;
  sedeId: number;
  codigo: string;
  tipoGrupo: string;
  tipoDetalle?: string;
  piso: number;
  /** Piso tal cual figura en el Excel de aulas ("3", "S1", "Lab", "B"...). `piso` es el entero derivado. */
  pisoTexto?: string;
  numeroAula?: string;
  capacidadReal: number;
  capacidadTotal: number;
  equipamiento?: string[];
  activa: boolean;
  edificio?: string;
  sede?: Sede;
}

export interface Catalogos {
  sedes: Sede[];
  facultades: string[];
  aulas: Aula[];
}

export interface OfertaFiltros {
  sedeId?: number;
  turno?: string;
  q?: string;
  facultad?: string;
}

export interface CambioClase {
  id: number;
  aulaId?: number;
  inscriptosConReserva: number;
  turno: string;
}

export interface Choque {
  claseId: number;
  nroClase: number;
  materia: string;
  aulaCodigo: string;
  horario: string;
  motivo: string;
}

export interface Sobrecupo {
  claseId: number;
  nroClase: number;
  materia: string;
  inscriptos: number;
  capacidadAula: number;
  exceso: number;
}

export interface GrabarResponse {
  filas: OfertaRow[];
  choques: Choque[];
  sobrecupo: Sobrecupo[];
}

export interface AulaAlt {
  id: number;
  codigo: string;
  tipoGrupo: string;
  capacidadTotal: number;
  piso: number;
  sillasVacias: number;
  puntaje: number;
}

export interface AulaBloqueada {
  codigo: string;
  motivo: string;
}

export interface SugerenciaAula {
  claseId: number;
  nroClase: number;
  materia: string;
  inscriptos: number;
  aulaActual?: AulaAlt;
  regimen: string;
  recomendada?: AulaAlt;
  alternativas: AulaAlt[];
  bloqueadas: AulaBloqueada[];
  yaOptima: boolean;
}

export interface Periodo {
  id: number;
  ciclo: string;
  nombre: string;
}

export interface CargaPlanificacionResponse {
  periodoId: number;
  periodoNombre: string;
  clasesCargadas: number;
  mensaje: string;
}
