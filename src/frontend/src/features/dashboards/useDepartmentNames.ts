import { useOrganizationUnits } from '@/features/identity-access/names/useOrganizationUnits.ts';
import { shortId } from '@/features/projects/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

/**
 * A department's name in the interface language, for the filter the dashboard's own options fill (GET /organization-units, which every
 * signed-in person may read; the 24 Oct 2026 review, H1). One the API does not name is shown by its short id.
 */
export function useDepartmentNames(): (id: string) => string {
  const { t, language } = useI18n();
  const { departments } = useOrganizationUnits();
  return (id) =>
    departments.find((department) => department.id === id)?.name[language] ??
    t('projects.lookups.unknownItem', { id: shortId(id) });
}
