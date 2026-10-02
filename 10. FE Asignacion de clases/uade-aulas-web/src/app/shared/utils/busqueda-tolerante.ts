/** Quita acentos/diacríticos y pasa a minúsculas, para comparar texto sin exigir tipeo idéntico. */
export function normalizarTexto(texto: string): string {
  return texto
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .trim();
}

export function coincideBusqueda(haystack: string, busqueda: string): boolean {
  const busquedaNormalizada = normalizarTexto(busqueda);
  if (!busquedaNormalizada) return true;
  return normalizarTexto(haystack).includes(busquedaNormalizada);
}
