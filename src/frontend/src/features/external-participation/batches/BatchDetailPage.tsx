import { type ReactElement, useCallback, useState } from 'react';
import { Link, useLocation, useParams } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { languageTag } from '@/features/projects/presentation.ts';
import { ConfirmCommandDialog } from '@/features/risks/dialogs/ConfirmCommandDialog.tsx';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { updateRequestBatchesApi } from '../api/monthlyUpdatesApi.ts';
import { BATCH_ROW_STATES, type UpdateRequestBatchDetail } from '../api/monthlyUpdateTypes.ts';
import { WordsDialog } from '../dialogs/WordsDialog.tsx';
import { useCounted } from '../monthly/counts.ts';
import { externalRequestPath, UPDATE_BATCHES_PATH } from '../paths.ts';
import { isNotFound, isStale, participationProblemMessage } from '../problems.ts';
import { useEntityNames } from '../useExternalParticipationData.ts';

import { BATCH_STATE_TONES } from './batchRules.ts';

/**
 * One batch (the 5 Nov 2026 review §2.2): where each project stands, counted on top, and the sender's two commands. A row opens the request's
 * own page, where its answer is reviewed as any other.
 */
export function BatchDetailPage(): ReactElement {
  const { t } = useI18n();
  const { batchId = '' } = useParams();
  const batch = useApiResource(
    useCallback((signal: AbortSignal) => updateRequestBatchesApi.get(batchId, signal), [batchId]),
    { keepWhileReloading: true },
  );

  if (batch.data === undefined) {
    return (
      <>
        <PageHeader title={t('externalParticipation.batch.detailTitle')} />
        {batch.loading ? (
          <LoadingState />
        ) : isNotFound(batch.error) ? (
          <p className="state">{t('externalParticipation.batch.notFound')}</p>
        ) : (
          <ErrorState
            error={batch.error}
            message={participationProblemMessage(batch.error, t)}
            onRetry={batch.reload}
          />
        )}
      </>
    );
  }
  return <BatchDetail detail={batch.data} reload={batch.reload} />;
}

function BatchDetail({
  detail,
  reload,
}: {
  detail: UpdateRequestBatchDetail;
  reload: () => void;
}): ReactElement {
  const { t } = useI18n();
  const location = useLocation();
  const maySend = useCan('UPDATE_REQUEST_SEND');
  const entityName = useEntityNames();
  const counted = useCounted();
  const personName = usePersonNames([
    detail.batch.reviewerUserId,
    ...detail.rows.map((row) => row.responsibleUserId),
  ]);
  const justSent = (location.state as { notice?: string } | null)?.notice === 'sent';
  const [notice, setNotice] = useState<Notice | null>(
    justSent ? { tone: 'success', message: t('externalParticipation.batch.sent') } : null,
  );
  const [dialog, setDialog] = useState<'remind' | 'withdraw' | null>(null);
  const { batch, rows, instructions } = detail;
  const late = rows.filter((row) => row.overdue).length;
  const withdrawn = batch.withdrawnAt !== null;

  const done = (message: string) => {
    setDialog(null);
    setNotice({ tone: 'success', message });
    reload();
  };
  const stale = () => {
    setDialog(null);
    setNotice({ tone: 'warning', message: t('externalParticipation.problems.stale') });
    reload();
  };

  return (
    <>
      <PageHeader
        title={t('externalParticipation.batch.detailTitle')}
        description={t('externalParticipation.batch.detailDescription', {
          projects: counted('projects', batch.projectCount),
          entities: counted('entities', batch.entityCount),
        })}
        actions={
          maySend && !withdrawn ? (
            <>
              <button
                type="button"
                className="button"
                disabled={late === 0}
                onClick={() => {
                  setDialog('remind');
                }}
              >
                {t('externalParticipation.batch.remind', { count: late })}
              </button>
              <button
                type="button"
                className="button button--danger"
                onClick={() => {
                  setDialog('withdraw');
                }}
              >
                {t('externalParticipation.batch.withdraw')}
              </button>
            </>
          ) : undefined
        }
      />
      <p>
        <Link to={UPDATE_BATCHES_PATH}>{t('externalParticipation.batch.backToList')}</Link>
      </p>
      <PageNotice notice={notice} />
      <ul className="batch-counts" aria-label={t('externalParticipation.batch.countsLabel')}>
        {BATCH_ROW_STATES.map((state) => {
          const count = rows.filter((row) => row.state === state).length;
          return count === 0 ? null : (
            <li key={state} data-state={state}>
              <strong>{count}</strong>{' '}
              <StatusBadge
                label={t(`externalParticipation.batch.state.${state}`)}
                tone={BATCH_STATE_TONES[state]}
              />
            </li>
          );
        })}
        {late > 0 && (
          <li data-state="OVERDUE">
            <strong>{late}</strong>{' '}
            <StatusBadge label={t('externalParticipation.monthly.overdue')} tone="negative" />
          </li>
        )}
      </ul>
      <dl className="details">
        <Detail term={t('externalParticipation.fields.dueDate')}>
          <span dir="ltr">{batch.dueDate}</span>
        </Detail>
        <Detail term={t('externalParticipation.batch.repeat')}>
          {batch.repeatDay === null
            ? t('externalParticipation.batch.noRepeat')
            : t('externalParticipation.batch.repeatsOn', { day: batch.repeatDay })}
        </Detail>
        {batch.nextRunOn !== null && (
          <Detail term={t('externalParticipation.batch.nextRun')}>
            <span dir="ltr">{batch.nextRunOn}</span>
          </Detail>
        )}
        <Detail term={t('externalParticipation.fields.reviewer')}>
          {personName(batch.reviewerUserId)}
        </Detail>
        <Detail term={t('externalParticipation.fields.instructions')}>
          <span className="pre-line" lang={languageTag(instructions.language)} dir="auto">
            {instructions.text}
          </span>
        </Detail>
        {withdrawn && (
          <Detail term={t('externalParticipation.batch.withdrawn')}>
            <StatusBadge label={t('externalParticipation.batch.withdrawn')} tone="neutral" />
          </Detail>
        )}
      </dl>
      <TableContainer caption={t('externalParticipation.batch.rowsCaption')}>
        <thead>
          <tr>
            <th scope="col">{t('externalParticipation.fields.project')}</th>
            <th scope="col">{t('externalParticipation.fields.entity')}</th>
            <th scope="col">{t('externalParticipation.batch.step2.responder')}</th>
            <th scope="col">{t('externalParticipation.fields.status')}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.requestId} data-state={row.state}>
              <td>
                <Link
                  to={externalRequestPath(row.requestId)}
                  lang={languageTag(row.projectTitle.language)}
                  dir="auto"
                >
                  {row.projectTitle.text}
                </Link>
                <span className="cell__aside" dir="ltr">
                  {row.formalProjectId ?? row.legacyReference}
                </span>
              </td>
              <td>{entityName(row.externalEntityId)}</td>
              <td>{personName(row.responsibleUserId)}</td>
              <td>
                <StatusBadge
                  label={t(`externalParticipation.batch.state.${row.state}`)}
                  tone={BATCH_STATE_TONES[row.state]}
                />
                {row.overdue && (
                  <>
                    {' '}
                    <StatusBadge
                      label={t('externalParticipation.monthly.overdue')}
                      tone="negative"
                    />
                  </>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </TableContainer>
      {dialog === 'remind' && (
        <ConfirmCommandDialog
          title={t('externalParticipation.batch.remindTitle')}
          consequence={t('externalParticipation.batch.remindConsequence', { count: late })}
          confirmLabel={t('externalParticipation.batch.remind', { count: late })}
          run={() => updateRequestBatchesApi.remindLate(batch.id)}
          describe={participationProblemMessage}
          isStale={isStale}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            done(t('externalParticipation.batch.reminded'));
          }}
          onStale={stale}
        />
      )}
      {dialog === 'withdraw' && (
        <WordsDialog
          title={t('externalParticipation.batch.withdraw')}
          consequence={t('externalParticipation.batch.withdrawConsequence')}
          confirmLabel={t('externalParticipation.batch.withdraw')}
          reason="required"
          internalNote={false}
          danger
          run={({ reason }) =>
            updateRequestBatchesApi.withdraw(batch.id, reason?.text ?? '', reason?.language ?? 'ar')
          }
          isStale={isStale}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            done(t('externalParticipation.batch.withdrawnDone'));
          }}
          onStale={stale}
        />
      )}
    </>
  );
}
