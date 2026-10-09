import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { ProjectReference } from './components/RequestTable.tsx';
import { registerOfPath } from './paths.ts';
import { participationProblemMessage } from './problems.ts';
import { participationRows } from './rules.ts';
import { useEntityNames, useRequestList } from './useExternalParticipationData.ts';

/**
 * SCR-167 External Participation Monitor (WF-13 §12.3): by project and entity, the requests AHDA holds open and where
 * each stands — not yet started by the entity, being drafted, with AHDA — what is due or overdue, how many responders
 * are named, and what is closed or cancelled; each row opens SCR-160 narrowed to it. The counts are of the requests the
 * API lists to this person, read once. Active external users and access expiry have no API yet (TASK-066 F-1): the
 * screen says so rather than guessing, and it changes no access (§12.3: no automatic access mutation).
 */
export function ParticipationMonitorPage(): ReactElement {
  const { t, formatDateTime } = useI18n();
  const requests = useRequestList({});
  const entityName = useEntityNames();

  return (
    <>
      <PageHeader
        title={t('externalParticipation.monitor.title')}
        description={t('externalParticipation.monitor.description')}
      />
      <p className="notice notice--warning" data-field="accessNotAvailable">
        {t('externalParticipation.monitor.accessNotAvailable')}
      </p>
      {requests.data === undefined ? (
        requests.loading ? (
          <LoadingState />
        ) : (
          <ErrorState
            message={participationProblemMessage(requests.error, t)}
            onRetry={requests.reload}
          />
        )
      ) : requests.data.length === 0 ? (
        <EmptyState title={t('externalParticipation.monitor.emptyTitle')} />
      ) : (
        <TableContainer caption={t('externalParticipation.monitor.caption')}>
          <thead>
            <tr>
              <th scope="col">{t('externalParticipation.table.project')}</th>
              <th scope="col">{t('externalParticipation.table.entity')}</th>
              <th scope="col">{t('externalParticipation.monitor.notStarted')}</th>
              <th scope="col">{t('externalParticipation.monitor.drafting')}</th>
              <th scope="col">{t('externalParticipation.monitor.withAhda')}</th>
              <th scope="col">{t('externalParticipation.monitor.dueOverdue')}</th>
              <th scope="col">{t('externalParticipation.monitor.responders')}</th>
              <th scope="col">{t('externalParticipation.monitor.drafts')}</th>
              <th scope="col">{t('externalParticipation.monitor.closedCancelled')}</th>
              <th scope="col">{t('externalParticipation.table.updated')}</th>
            </tr>
          </thead>
          <tbody>
            {participationRows(requests.data).map((row) => (
              <tr key={`${row.projectId}|${row.externalEntityId}`}>
                <td>
                  <Link
                    className="cell__link"
                    to={registerOfPath(row.projectId, row.externalEntityId)}
                  >
                    <ProjectReference formalProjectId={row.formalProjectId} />
                  </Link>
                </td>
                <td>
                  <span dir="auto">{entityName(row.externalEntityId)}</span>
                </td>
                <td dir="ltr">{row.notStarted}</td>
                <td dir="ltr">{row.drafting}</td>
                <td dir="ltr">{row.withAhda}</td>
                <td>
                  <span className="badge-group">
                    {row.overdue > 0 && (
                      <StatusBadge
                        label={t('externalParticipation.monitor.overdueCount', {
                          count: row.overdue,
                        })}
                        tone="negative"
                      />
                    )}
                    {row.due > 0 && (
                      <StatusBadge
                        label={t('externalParticipation.monitor.dueCount', { count: row.due })}
                        tone="warning"
                      />
                    )}
                    {row.overdue === 0 && row.due === 0 && '—'}
                  </span>
                </td>
                <td dir="ltr">{row.responders}</td>
                <td dir="ltr">{row.drafts}</td>
                <td dir="ltr">{`${String(row.closed)} / ${String(row.cancelled)}`}</td>
                <td>{formatDateTime(row.lastChangedAt)}</td>
              </tr>
            ))}
          </tbody>
        </TableContainer>
      )}
    </>
  );
}
