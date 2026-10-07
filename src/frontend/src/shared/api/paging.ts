import { apiRequest, type QueryValue } from './httpClient.ts';

/** Rows per page on the list screens (R-29 allows up to 200). */
export const PAGE_SIZE = 25;

/** R-29's largest page, for a screen that reads a collection whole (a schedule, a project's tasks). */
export const MAX_PAGE_SIZE = 200;

/** The `page` query parameter, or 1 when it is absent or not a positive integer. */
export function pageFrom(params: URLSearchParams): number {
  const page = Number(params.get('page') ?? '1');
  return Number.isInteger(page) && page > 0 ? page : 1;
}

interface Paged<T> {
  items: T[];
  totalCount: number;
}

/** Every item of a collection, reading page after page of MAX_PAGE_SIZE until the total is reached. */
export async function readAllPages<T>(
  path: string,
  query: Record<string, QueryValue>,
  signal?: AbortSignal,
): Promise<T[]> {
  const items: T[] = [];
  for (let page = 1; ; page += 1) {
    const { data } = await apiRequest<Paged<T>>(path, {
      query: { ...query, page, pageSize: MAX_PAGE_SIZE },
      signal,
    });
    items.push(...data.items);
    if (data.items.length === 0 || items.length >= data.totalCount) {
      return items;
    }
  }
}

/** Reads at once across a set of records (a project's list each): enough to be quick, few enough not to flood the API. */
export const PARALLEL_READS = 6;

/** `read` over every item, PARALLEL_READS at a time, the results flattened in the items' order. */
export async function readInBatches<T, R>(
  items: T[],
  read: (item: T) => Promise<R[]>,
): Promise<R[]> {
  const results: R[] = [];
  for (let index = 0; index < items.length; index += PARALLEL_READS) {
    const batch = await Promise.all(items.slice(index, index + PARALLEL_READS).map(read));
    results.push(...batch.flat());
  }
  return results;
}
