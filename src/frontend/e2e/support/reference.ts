import { type PlatformApi } from './api.ts';

// The e2e data set's reference records, found by their codes through the API rather than by id, so the same journey runs on
// the e2e stack and on SIT (docs/testing/e2e-suite.md §5: the data set is loaded by code, its ids are the environment's).

export const DATA_SET = {
  department: 'DEPT-E2E',
  otherDepartment: 'DEPT-E2E-OTHER',
  entityA: 'ENT-E2E-A',
  entityB: 'ENT-E2E-B',
  classification: 'E2E_INFRASTRUCTURE',
  region: 'E2E_NORTH',
  city: 'E2E_NORTH_CITY',
  governanceProfile: 'STANDARD',
} as const;

interface Page<T> {
  items: T[];
}

interface Coded {
  id: string;
  code: string;
}

const cache = new Map<string, string>();

async function cached(key: string, find: () => Promise<string | undefined>): Promise<string> {
  const known = cache.get(key);
  if (known !== undefined) return known;
  const found = await find();
  if (found === undefined)
    throw new Error(`The e2e data set has no ${key}; is the environment provisioned?`);
  cache.set(key, found);
  return found;
}

/** A published master data item's id, by catalogue code and item code. */
export function itemId(api: PlatformApi, catalogue: string, code: string): Promise<string> {
  return cached(`${catalogue}/${code}`, async () => {
    // The catalogue list is a plain array (FG-04 has a few dozen catalogues); the item list is paged.
    const catalogues = await api.get<Coded[]>('/master-data-catalogues');
    const catalogueId = catalogues.find((c) => c.code === catalogue)?.id;
    if (catalogueId === undefined) return undefined;
    const items = await api.get<Page<Coded>>(
      `/master-data-items?catalogueId=${catalogueId}&lifecycleState=PUBLISHED&pageSize=100`,
    );
    return items.items.find((i) => i.code === code)?.id;
  });
}

export function departmentId(api: PlatformApi, code: string): Promise<string> {
  return cached(
    `department/${code}`,
    async () =>
      (await api.get<Page<Coded>>(`/departments?q=${encodeURIComponent(code)}`)).items.find(
        (d) => d.code === code,
      )?.id,
  );
}

export function entityId(api: PlatformApi, code: string): Promise<string> {
  return cached(
    `entity/${code}`,
    async () =>
      (await api.get<Page<Coded>>(`/external-entities?q=${encodeURIComponent(code)}`)).items.find(
        (e) => e.code === code,
      )?.id,
  );
}
