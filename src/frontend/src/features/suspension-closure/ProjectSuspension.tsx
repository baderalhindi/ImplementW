import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { SuspensionRegisterView } from './components/SuspensionRegisterView.tsx';
import { newSuspensionRequestPath, suspensionRequestPath } from './paths.ts';
import { isForbidden, suspensionClosureProblemMessage } from './problems.ts';
import { canRaiseSuspensionRequest, openPeriodOf } from './suspensionRules.ts';
import { useProjectSuspension } from './useSuspensionClosureData.ts';

/**
 * SCR-108 Suspension Requests for one project, inside its workspace: whether a suspension is in effect, the project's
 * suspension and resumption requests (each opening SCR-110), and its suspension periods across cycles. The Project
 * Manager requests a suspension of an ACTIVE project, or a resumption of a SUSPENDED one (SCR-109).
 */
export function ProjectSuspension({
  project,
  user,
}: {
  project: ProjectDetail;
  user: SessionUser;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const suspension = useProjectSuspension(project.id);

  if (suspension.data === undefined) {
    if (suspension.loading) {
      return <LoadingState />;
    }
    return isForbidden(suspension.error) ? (
      <p className="state">{t('suspensionClosure.suspension.forbidden')}</p>
    ) : (
      <ErrorState
        message={suspensionClosureProblemMessage(suspension.error, t)}
        onRetry={suspension.reload}
      />
    );
  }

  const { requests, periods } = suspension.data;
  const open = openPeriodOf(periods);
  const suspends = canRaiseSuspensionRequest(user, project, requests, 'SUSPEND');
  const resumes = canRaiseSuspensionRequest(user, project, requests, 'RESUME');
  const entries = requests.map((request) => ({ request, project }));

  return (
    <section className="section" aria-labelledby="project-suspension">
      <div className="section__header">
        <h2 id="project-suspension">{t('suspensionClosure.suspension.projectTitle')}</h2>
        {(suspends || resumes) && (
          <span className="tasks__actions">
            {suspends && (
              <Link
                className="button button--primary"
                to={newSuspensionRequestPath(project.id, 'SUSPEND')}
              >
                {t('suspensionClosure.actions.requestSuspension')}
              </Link>
            )}
            {resumes && (
              <Link
                className="button button--primary"
                to={newSuspensionRequestPath(project.id, 'RESUME')}
              >
                {t('suspensionClosure.actions.requestResumption')}
              </Link>
            )}
          </span>
        )}
      </div>
      <p className="form__note" data-field="suspensionState">
        {open === null
          ? t('suspensionClosure.suspension.notSuspended')
          : t('suspensionClosure.suspension.inEffectSince', {
              date: formatDateTime(open.startedAt),
            })}
        {open !== null && (
          <>
            {' '}
            <Link to={suspensionRequestPath(open.suspensionRequestId)}>
              {t('suspensionClosure.suspension.openSuspension')}
            </Link>
          </>
        )}
      </p>

      {entries.length === 0 ? (
        <EmptyState title={t('suspensionClosure.suspension.emptyTitle')}>
          <p>{t('suspensionClosure.suspension.emptyProject')}</p>
        </EmptyState>
      ) : (
        <SuspensionRegisterView
          entries={entries}
          showProject={false}
          caption={t('suspensionClosure.suspension.projectCaption')}
        />
      )}

      <h3>{t('suspensionClosure.suspension.periodsTitle')}</h3>
      {periods.length === 0 ? (
        <p className="form__note">{t('suspensionClosure.suspension.noPeriods')}</p>
      ) : (
        <TableContainer caption={t('suspensionClosure.suspension.periodsCaption')}>
          <thead>
            <tr>
              <th scope="col">{t('suspensionClosure.suspension.period.started')}</th>
              <th scope="col">{t('suspensionClosure.suspension.period.ended')}</th>
              <th scope="col">{t('suspensionClosure.suspension.period.endReason')}</th>
            </tr>
          </thead>
          <tbody>
            {periods.map((period) => (
              <tr key={period.id}>
                <td>
                  <Link to={suspensionRequestPath(period.suspensionRequestId)}>
                    {formatDateTime(period.startedAt)}
                  </Link>
                </td>
                <td>
                  {period.endedAt === null
                    ? t('suspensionClosure.suspension.period.ongoing')
                    : formatDateTime(period.endedAt)}
                </td>
                <td>
                  {period.endReason === null ? (
                    t('suspensionClosure.notStated')
                  ) : period.resumptionRequestId === null ? (
                    t(`suspensionClosure.suspension.endReason.${period.endReason}`)
                  ) : (
                    <Link to={suspensionRequestPath(period.resumptionRequestId)}>
                      {t(`suspensionClosure.suspension.endReason.${period.endReason}`)}
                    </Link>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </TableContainer>
      )}
    </section>
  );
}
