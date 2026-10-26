import { useCallback } from 'react';

import { type Language } from '@/shared/i18n/i18n.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';

import { departmentsApi, externalEntitiesApi } from './api/identityAccessApi.ts';
import {
  type BilingualLabel,
  type DepartmentSummary,
  type ExternalEntitySummary,
} from './api/types.ts';

export function labelOf(label: BilingualLabel, language: Language): string {
  return label[language];
}

export interface OrganizationLookups {
  departments: DepartmentSummary[];
  entities: ExternalEntitySummary[];
  departmentName: (id: string | null) => string | null;
  entityName: (id: string | null) => string | null;
}

/**
 * Departments and external entities by id. The user and assignment representations carry ids only, so every screen
 * that names them reads both collections once (a few hundred rows at most, R-29 pages of 200).
 */
export function useOrganizationLookups(language: Language): {
  lookups: OrganizationLookups | undefined;
  error: unknown;
  loading: boolean;
} {
  const load = useCallback(async (signal: AbortSignal) => {
    const [departments, entities] = await Promise.all([
      departmentsApi.listAll(signal),
      externalEntitiesApi.listAll(signal),
    ]);
    return { departments, entities };
  }, []);
  const { data, error, loading } = useApiResource(load);

  if (data === undefined) {
    return { lookups: undefined, error, loading };
  }
  const departmentById = new Map(data.departments.map((d) => [d.id, d]));
  const entityById = new Map(data.entities.map((e) => [e.id, e]));
  return {
    lookups: {
      departments: data.departments,
      entities: data.entities,
      departmentName: (id) => {
        const department = id === null ? undefined : departmentById.get(id);
        return department === undefined ? null : labelOf(department.name, language);
      },
      entityName: (id) => {
        const entity = id === null ? undefined : entityById.get(id);
        return entity === undefined ? null : labelOf(entity.name, language);
      },
    },
    error,
    loading,
  };
}
