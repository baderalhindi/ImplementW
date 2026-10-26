import {
  isPublished,
  type MasterDataItemSummary,
  readCatalogueItems,
} from '@/shared/api/masterData.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { type SelectOption } from '@/shared/ui/FormFields.tsx';

import { type DocumentCatalogueCode } from './api/documentsApi.ts';
import { shortId } from './presentation.ts';

type Catalogues = Record<DocumentCatalogueCode, MasterDataItemSummary[]>;

const CATALOGUE_CODES: DocumentCatalogueCode[] = [
  'DOCUMENT_TYPE',
  'DATA_CLASSIFICATION',
  'EVIDENCE_TYPE',
];

/** Module-level, so useApiResource reads the three catalogues once per mount. */
function readCatalogues(signal: AbortSignal): Promise<Catalogues> {
  return readCatalogueItems(CATALOGUE_CODES, signal);
}

export interface DocumentLookups {
  /** The item's label in the interface language, or "Item 1a2b3c4d" when the catalogues cannot be read. */
  label: (id: string) => string;
  /** PUBLISHED items only, in display order: nothing else can be chosen for a new value (TASK-037, 422). */
  options: (code: DocumentCatalogueCode) => SelectOption[];
  loading: boolean;
  /** Set when the catalogues could not be read, e.g. 403 without MASTER_DATA_VIEW (TASK-038 F-1). */
  error: unknown;
}

/**
 * Names for the document types, classifications and evidence types a WF-12 screen shows. The document representations
 * carry item ids only, and the catalogues are read through the FG-04 API.
 */
export function useDocumentLookups(): DocumentLookups {
  const { t, language } = useI18n();
  const { data, error, loading } = useApiResource(readCatalogues);
  const all = data === undefined ? [] : Object.values(data).flat();

  return {
    label: (id) => {
      const item = all.find((candidate) => candidate.id === id);
      return item === undefined
        ? t('documents.lookups.unknownItem', { id: shortId(id) })
        : item.label[language];
    },
    options: (code) =>
      (data?.[code] ?? [])
        .filter(isPublished)
        .map((item) => ({ value: item.id, label: item.label[language] })),
    loading,
    error,
  };
}
