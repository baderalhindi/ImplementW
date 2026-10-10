import { departmentsApi } from '@/features/identity-access/api/identityAccessApi.ts';
import { type DepartmentSummary } from '@/features/identity-access/api/types.ts';
import { shortId } from '@/features/projects/presentation.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

/** Reading the organisation needs its own permission; without it a department is named by its short id. */
async function readDepartments(signal: AbortSignal): Promise<DepartmentSummary[]> {
  try {
    return await departmentsApi.listAll(signal);
  } catch (error) {
    if (signal.aborted) {
      throw error;
    }
    return [];
  }
}

/** A department's name in the interface language, for the filter the dashboard's own options fill. */
export function useDepartmentNames(): (id: string) => string {
  const { t, language } = useI18n();
  const departments = useApiResource(readDepartments);
  return (id) =>
    departments.data?.find((department) => department.id === id)?.name[language] ??
    t('projects.lookups.unknownItem', { id: shortId(id) });
}
