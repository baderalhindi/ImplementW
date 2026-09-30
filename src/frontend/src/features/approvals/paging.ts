/** Rows per page on the WF-11 lists (R-29 allows up to 200). */
export const PAGE_SIZE = 25;

/** The `page` query parameter, or 1 when it is absent or not a positive integer. */
export function pageFrom(params: URLSearchParams): number {
  const page = Number(params.get('page') ?? '1');
  return Number.isInteger(page) && page > 0 ? page : 1;
}
