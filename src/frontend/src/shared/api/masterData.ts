import { apiRequest } from './httpClient.ts';

// FG-04 master data (TASK-034) as the feature screens read it: the items of a few catalogues, to offer as choices and
// to name the ids a representation carries. Needs MASTER_DATA_VIEW, which only R01 holds today (TASK-038 F-1).

/** A master data item (FG-04) as `GET /master-data-items` lists it. */
export interface MasterDataItemSummary {
  id: string;
  catalogueId: string;
  code: string;
  label: { ar: string; en: string };
  parentItemId: string | null;
  sortOrder: number;
  lifecycleState: string;
  isSystem: boolean;
}

export interface MasterDataCatalogue {
  id: string;
  code: string;
}

interface ItemPage {
  items: MasterDataItemSummary[];
}

/** R-29's largest page: a catalogue holds tens of items, so one page is all of it. */
const CATALOGUE_PAGE_SIZE = 200;

export const masterDataApi = {
  /** Unpaged: the catalogues are a small closed set. */
  listCatalogues: async (signal?: AbortSignal) =>
    (await apiRequest<MasterDataCatalogue[]>('/master-data-catalogues', { signal })).data,
  /** Every item of a catalogue, in display order, retired ones included so an existing record still reads. */
  listItems: async (catalogueId: string, signal?: AbortSignal) =>
    (
      await apiRequest<ItemPage>('/master-data-items', {
        query: { catalogueId, pageSize: CATALOGUE_PAGE_SIZE },
        signal,
      })
    ).data,
};

/** The items of each catalogue named, by catalogue code; a catalogue that does not exist has none. */
export async function readCatalogueItems<Code extends string>(
  codes: readonly Code[],
  signal: AbortSignal,
): Promise<Record<Code, MasterDataItemSummary[]>> {
  const catalogues = await masterDataApi.listCatalogues(signal);
  const entries = await Promise.all(
    codes.map(async (code) => {
      const catalogue = catalogues.find((candidate) => candidate.code === code);
      const items =
        catalogue === undefined ? [] : (await masterDataApi.listItems(catalogue.id, signal)).items;
      return [code, items] as const;
    }),
  );
  return Object.fromEntries(entries) as Record<Code, MasterDataItemSummary[]>;
}

/** Only a PUBLISHED item can be chosen for a new value; any other is refused (422 `*_REFERENCE_INVALID`). */
export function isPublished(item: MasterDataItemSummary): boolean {
  return item.lifecycleState === 'PUBLISHED';
}
