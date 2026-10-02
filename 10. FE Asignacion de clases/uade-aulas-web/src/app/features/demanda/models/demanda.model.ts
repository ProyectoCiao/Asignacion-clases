export interface DemandaRow {
  materiaCodigo: string;
  materiaNombre: string;
  facultad?: string;
  solicitudes: number;
  numClases: number;
  asientos: number;
  brecha: number;
  /** Pendiente de backend: tope de inscriptos por materia. Hasta entonces siempre undefined. */
  tope?: number;
}
