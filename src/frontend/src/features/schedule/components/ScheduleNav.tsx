import { type ReactElement } from 'react';
import { NavLink } from 'react-router';

import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';

const VIEWS: { path: string; label: TranslationKey }[] = [
  { path: '', label: 'schedule.nav.manager' },
  { path: '/gantt', label: 'schedule.nav.gantt' },
  { path: '/baselines', label: 'schedule.nav.baselines' },
];

/** SCR-060, SCR-045 and SCR-061 under the workspace's Schedule tab. */
export function ScheduleNav({ projectId }: { projectId: string }): ReactElement {
  const { t } = useI18n();
  return (
    <nav className="schedule-nav" aria-label={t('schedule.nav.label')}>
      <ul>
        {VIEWS.map((view) => (
          <li key={view.path}>
            <NavLink to={`/projects/${projectId}/schedule${view.path}`} end>
              {t(view.label)}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
