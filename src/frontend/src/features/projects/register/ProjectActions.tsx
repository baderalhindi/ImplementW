import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

/** Offered to a holder of PROJECT_REGISTER only (the 24 Oct 2026 review, C3: no "Create project" for a role that cannot). */
export function CreateProjectLink(): ReactElement | null {
  const { t } = useI18n();
  return useCan('PROJECT_REGISTER') ? (
    <Link className="button button--primary" to="/projects/new">
      {t('projects.create.action')}
    </Link>
  ) : null;
}

/** Projects → Import, next to Create (the 2 Nov 2026 review, R3-4): a holder of PROJECT_IMPORT prepares a batch, of PROJECT_IMPORT_APPROVE decides it. */
export function ImportProjectsLink(): ReactElement | null {
  const { t } = useI18n();
  return useCan('PROJECT_IMPORT', 'PROJECT_IMPORT_APPROVE') ? (
    <Link className="button" to="/projects/import">
      {t('projects.import.open')}
    </Link>
  ) : null;
}
