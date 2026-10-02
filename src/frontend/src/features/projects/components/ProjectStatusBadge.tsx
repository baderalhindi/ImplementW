import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type ProjectStatus } from '../api/types.ts';
import { statusTone } from '../presentation.ts';

/**
 * A project's state, and, for one that entered by the legacy intake path, a second badge: its baseline is a Declared
 * Baseline, never an Approved one, and every display tells the two apart (ADR-014, TASK-041 D-14).
 */
export function ProjectStatusBadge({
  status,
  legacyIntakeDate,
}: {
  status: ProjectStatus;
  legacyIntakeDate: string | null;
}): ReactElement {
  const { t } = useI18n();
  return (
    <span className="badge-group">
      <StatusBadge label={t(`projects.status.${status}`)} tone={statusTone(status)} />
      {legacyIntakeDate !== null && (
        <StatusBadge label={t('projects.legacyIntake')} tone="warning" />
      )}
    </span>
  );
}
