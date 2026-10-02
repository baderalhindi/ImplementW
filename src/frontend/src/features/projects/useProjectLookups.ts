import {
  departmentsApi,
  externalEntitiesApi,
} from '@/features/identity-access/api/identityAccessApi.ts';
import {
  isPublished,
  type MasterDataItemSummary,
  readCatalogueItems,
} from '@/shared/api/masterData.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { type SelectOption } from '@/shared/ui/FormFields.tsx';

import { governanceProfileApi } from './api/projectsApi.ts';
import { type GovernanceProfileSettings } from './api/types.ts';
import { shortId } from './presentation.ts';

/** The catalogues a project names (db/seed codes; TASK-041 D-8). */
export type ProjectCatalogueCode =
  'PROJECT_CLASSIFICATION' | 'GOVERNANCE_PROFILE' | 'REGION' | 'CITY';

const CATALOGUE_CODES: ProjectCatalogueCode[] = [
  'PROJECT_CLASSIFICATION',
  'GOVERNANCE_PROFILE',
  'REGION',
  'CITY',
];

// Module-level loaders, so each is read once per mount. They are separate resources: a caller without one permission
// still gets the others.

function readCatalogues(signal: AbortSignal) {
  return readCatalogueItems(CATALOGUE_CODES, signal);
}

async function readOrganization(signal: AbortSignal) {
  const [departments, entities] = await Promise.all([
    departmentsApi.listAll(signal),
    externalEntitiesApi.listAll(signal),
  ]);
  return { departments, entities };
}

async function readProfiles(signal: AbortSignal): Promise<GovernanceProfileSettings[]> {
  return (await governanceProfileApi.resolve(signal)).content.governanceProfiles;
}

export interface ProjectLookups {
  /** A master data item's label, or "Item 1a2b3c4d" when it cannot be read. */
  itemLabel: (id: string | null) => string | null;
  /** PUBLISHED items of a catalogue, in display order; CITY narrowed to a region's cities when one is given. */
  options: (code: ProjectCatalogueCode, parentItemId?: string) => SelectOption[];
  departmentName: (id: string | null) => string | null;
  entityName: (id: string | null) => string | null;
  /** Active departments and ACTIVE entities: nothing else may be named (TASK-041 D-8). */
  departmentOptions: SelectOption[];
  entityOptions: SelectOption[];
  /** ADR-015's settings per profile; undefined while loading or when they cannot be read. */
  profiles: GovernanceProfileSettings[] | undefined;
  loading: boolean;
  /** Each set's refusal, e.g. 403 without MASTER_DATA_VIEW, ORGANIZATION_VIEW or CONFIGURATION_VIEW (F-1). */
  catalogueError: unknown;
  organizationError: unknown;
  profileError: unknown;
}

/**
 * Names and choices for what a project names. A project carries ids only, and each set is read through its own
 * module's API, which may refuse the caller (F-1): ids are then shown shortened and the form says what it cannot offer.
 */
export function useProjectLookups(): ProjectLookups {
  const { t, language } = useI18n();
  const catalogues = useApiResource(readCatalogues);
  const organization = useApiResource(readOrganization);
  const profiles = useApiResource(readProfiles);

  const items: MasterDataItemSummary[] =
    catalogues.data === undefined ? [] : Object.values(catalogues.data).flat();
  const unknown = (id: string) => t('projects.lookups.unknownItem', { id: shortId(id) });

  return {
    itemLabel: (id) => {
      if (id === null) {
        return null;
      }
      const item = items.find((candidate) => candidate.id === id);
      return item === undefined ? unknown(id) : item.label[language];
    },
    options: (code, parentItemId) =>
      (catalogues.data?.[code] ?? [])
        .filter(isPublished)
        .filter(
          (item) =>
            parentItemId === undefined ||
            parentItemId === '' ||
            item.parentItemId === null ||
            item.parentItemId === parentItemId,
        )
        .map((item) => ({ value: item.id, label: item.label[language] })),
    departmentName: (id) => {
      if (id === null) {
        return null;
      }
      const department = organization.data?.departments.find((candidate) => candidate.id === id);
      return department === undefined ? unknown(id) : department.name[language];
    },
    entityName: (id) => {
      if (id === null) {
        return null;
      }
      const entity = organization.data?.entities.find((candidate) => candidate.id === id);
      return entity === undefined ? unknown(id) : entity.name[language];
    },
    departmentOptions: (organization.data?.departments ?? [])
      .filter((department) => department.isActive)
      .map((department) => ({ value: department.id, label: department.name[language] })),
    entityOptions: (organization.data?.entities ?? [])
      .filter((entity) => entity.status === 'ACTIVE')
      .map((entity) => ({ value: entity.id, label: entity.name[language] })),
    profiles: profiles.data,
    loading: catalogues.loading || organization.loading,
    catalogueError: catalogues.error,
    organizationError: organization.error,
    profileError: profiles.error,
  };
}
