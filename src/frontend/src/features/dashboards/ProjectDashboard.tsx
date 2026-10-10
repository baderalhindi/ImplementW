import { type ReactElement, useCallback, useId } from 'react';

import { isInternal } from '@/features/projects/access.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { dashboardsApi } from './api/dashboardsApi.ts';
import { DashboardCanvas } from './components/DashboardCanvas.tsx';
import { dashboardProblemMessage, isNotFound } from './problems.ts';

/**
 * DSH-009 Project Dashboard, composed into SCR-040 Project Overview and rendered nowhere else (Blueprint §20.3,
 * §21.1: "no duplicate page"). It has no route: the workspace's Overview tab places it under the project's identity.
 * For an external entity's person it is DSH-008, the same composition in their entity's scope (ADR-006, ADR-013):
 * read only, never personalised, with no report composition or export (ADR-019) — the API decides every widget.
 */
export function ProjectDashboard({
  projectId,
  user,
}: {
  projectId: string;
  user: SessionUser;
}): ReactElement {
  const { t, language, formatDateTime } = useI18n();
  const headingId = useId();
  const load = useCallback(
    (signal: AbortSignal) => dashboardsApi.get('PROJECT', { projectId }, signal),
    [projectId],
  );
  const view = useApiResource(load, { keepWhileReloading: true });

  return (
    <section className="dashboard dashboard--project" aria-labelledby={headingId}>
      <div className="section__header">
        <h2 id={headingId}>{view.data?.name[language] ?? t('dashboards.project.title')}</h2>
        {view.data !== undefined && (
          <button type="button" className="button button--quiet" onClick={view.reload}>
            {t('dashboards.actions.refresh')}
          </button>
        )}
      </div>
      {view.data === undefined ? (
        view.loading ? (
          <LoadingState />
        ) : isNotFound(view.error) ? (
          // R-47: not in the PROJECT audience, or no widget reaches this project. Nothing about it is disclosed.
          <p className="form__note">{t('dashboards.project.unavailable')}</p>
        ) : (
          <ErrorState message={dashboardProblemMessage(view.error, t)} onRetry={view.reload} />
        )
      ) : (
        <>
          <p className="dashboard__meta">
            {[
              ...(isInternal(user) ? [] : [t('dashboards.project.entityRendering')]),
              t('dashboards.refreshedAt', { date: formatDateTime(view.data.refreshedAt) }),
            ].join(' · ')}
          </p>
          <DashboardCanvas view={view.data} headingLevel={3} />
        </>
      )}
    </section>
  );
}
