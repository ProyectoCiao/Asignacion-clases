import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Evita que el navegador sirva respuestas GET cacheadas (ej. después de grabar un cambio en otra
 * pantalla y volver a esta) — sin esto, a veces hacía falta un F5 para ver datos actualizados.
 */
export const noCacheInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.method !== 'GET') {
    return next(req);
  }
  return next(
    req.clone({
      setHeaders: {
        'Cache-Control': 'no-cache, no-store, must-revalidate',
        Pragma: 'no-cache',
      },
    }),
  );
};
