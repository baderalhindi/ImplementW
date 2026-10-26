import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { projectCloseoutPath, projectSuspensionPath } from '../paths.ts';

/**
 * The project's lifecycle banner across its workspace (WF-09 §13 SCR-040 "prominent Suspended banner"; WF-10 §14
 * CapabilityBanner): suspended, completed, or closed and read-only (acceptance criterion 2). It says what the state
 * means for the person's work and where the governing record is. Nothing for earlier states.
 */
export function LifecycleBanner({
  project,
}: {
  project: Pick<ProjectDetail, 'id' | 'status' | 'closedAt'>;
}): ReactElement | null {
  const { t, formatDateTime } = useI18n();
  switch (project.status) {
    case 'SUSPENDED':
      return (
        <div className="lifecycle-banner lifecycle-banner--suspended" role="note">
          <p className="lifecycle-banner__title">{t('suspensionClosure.banner.SUSPENDED.title')}</p>
          <p>
            {t('suspensionClosure.banner.SUSPENDED.body')}{' '}
            <Link to={projectSuspensionPath(project.id)}>
              {t('suspensionClosure.banner.SUSPENDED.link')}
            </Link>
          </p>
        </div>
      );
    case 'COMPLETED':
      return (
        <div className="lifecycle-banner lifecycle-banner--completed" role="note">
          <p className="lifecycle-banner__title">{t('suspensionClosure.banner.COMPLETED.title')}</p>
          <p>
            {t('suspensionClosure.banner.COMPLETED.body')}{' '}
            <Link to={projectCloseoutPath(project.id)}>
              {t('suspensionClosure.banner.COMPLETED.link')}
            </Link>
          </p>
        </div>
      );
    case 'CLOSED':
      return (
        <div
          className="lifecycle-banner lifecycle-banner--closed"
          role="note"
          data-read-only="true"
        >
          <p className="lifecycle-banner__title">
            <svg
              className="lifecycle-banner__icon"
              viewBox="0 0 16 16"
              width="16"
              height="16"
              aria-hidden="true"
              focusable="false"
            >
              <path
                d="M4.5 7V5a3.5 3.5 0 0 1 7 0v2M3 7h10v7H3z"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.5"
              />
            </svg>
            {t('suspensionClosure.banner.CLOSED.title')}
          </p>
          <p>
            {project.closedAt === null
              ? t('suspensionClosure.banner.CLOSED.body')
              : t('suspensionClosure.banner.CLOSED.bodyDated', {
                  date: formatDateTime(project.closedAt),
                })}{' '}
            <Link to={projectCloseoutPath(project.id)}>
              {t('suspensionClosure.banner.CLOSED.link')}
            </Link>
          </p>
        </div>
      );
    default:
      return null;
  }
}
