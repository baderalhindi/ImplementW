import { type ReactElement, useState } from 'react';
import { Link, useParams } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { ConfirmCommandDialog } from '@/features/risks/dialogs/ConfirmCommandDialog.tsx';
import { type ProjectTaskDetail } from '@/features/tasks/api/types.ts';
import { type ApiResponse } from '@/shared/api/httpClient.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { canDecide, canStartReview } from './access.ts';
import { contributionsApi, type ReviewDecision } from './api/externalParticipationApi.ts';
import {
  type ContributionFieldValue,
  type ExternalContributionDetail,
  type ExternalUpdateRequestDetail,
} from './api/types.ts';
import { ContributionStatusBadge } from './components/Badges.tsx';
import { FieldValue } from './components/FieldValues.tsx';
import { ProjectReference } from './components/RequestTable.tsx';
import { WordsDialog } from './dialogs/WordsDialog.tsx';
import { useFieldLabel, usePurpose } from './labels.ts';
import { APPLICATION_MONITOR_PATH, externalRequestPath } from './paths.ts';
import { disclosedContribution, disclosedRequest } from './projection.ts';
import { isNotFound, isStale, participationProblemMessage } from './problems.ts';
import { versionOfEtag } from './rules.ts';
import {
  useContributionRecord,
  useRequestRecord,
  useRevisions,
  useSourceTask,
} from './useExternalParticipationData.ts';

const DECISION_TEXT: Record<
  ReviewDecision,
  {
    title: TranslationKey;
    consequence: TranslationKey;
    action: TranslationKey;
    done: TranslationKey;
  }
> = {
  accept: {
    title: 'externalParticipation.command.accept.title',
    consequence: 'externalParticipation.command.accept.consequence',
    action: 'externalParticipation.actions.accept',
    done: 'externalParticipation.done.accepted',
  },
  return: {
    title: 'externalParticipation.command.return.title',
    consequence: 'externalParticipation.command.return.consequence',
    action: 'externalParticipation.actions.return',
    done: 'externalParticipation.done.returned',
  },
  reject: {
    title: 'externalParticipation.command.reject.title',
    consequence: 'externalParticipation.command.reject.consequence',
    action: 'externalParticipation.actions.reject',
    done: 'externalParticipation.done.rejected',
  },
};

type OpenDialog = 'startReview' | ReviewDecision;

/**
 * SCR-165 Contribution Review (WF-13 §12.3): the submitted revision's values, immutable, beside the source as it
 * stands now and the revision it corrects; the source version it was answered against, and whether the source has
 * changed since. The request's assigned reviewer starts the review, then accepts, returns or rejects — with the reason
 * the entity reads and an internal note — and nothing else: no value is edited here (TASK-066 D-5). Accepting a typed
 * answer is not applying it (EXT-P-07): SCR-166 applies it.
 */
export function ContributionReviewPage(): ReactElement | null {
  const { t } = useI18n();
  const { contributionId = '' } = useParams();
  const record = useContributionRecord(contributionId);
  if (record.data === undefined) {
    return (
      <>
        <PageHeader title={t('externalParticipation.review.title')} />
        {record.loading ? (
          <LoadingState />
        ) : isNotFound(record.error) ? (
          <p className="state">{t('externalParticipation.review.notFound')}</p>
        ) : (
          <ErrorState
            message={participationProblemMessage(record.error, t)}
            onRetry={record.reload}
          />
        )}
      </>
    );
  }
  return <RevisionReview record={record.data} reloadRevision={record.reload} />;
}

/** SCR-165 once the revision is read: its request, its siblings and its source are read by the request's id. */
function RevisionReview({
  record,
  reloadRevision,
}: {
  record: ApiResponse<ExternalContributionDetail>;
  reloadRevision: () => void;
}): ReactElement | null {
  const { t, formatDateTime } = useI18n();
  const { session } = useSession();
  const rawRevision = record.data;
  const requestRecord = useRequestRecord(rawRevision.externalUpdateRequestId);
  const revisions = useRevisions(rawRevision.externalUpdateRequestId);
  const rawRequest = requestRecord.data?.data;
  const sourceTask = useSourceTask(rawRequest);
  const purpose = usePurpose();
  const personName = usePersonNames([
    rawRevision.contributorUserId,
    rawRevision.reviewedByUserId ?? null,
    rawRequest?.reviewerUserId ?? null,
  ]);
  const [notice, setNotice] = useState<Notice | null>(null);
  const [dialog, setDialog] = useState<OpenDialog | null>(null);

  if (session === null) {
    return null;
  }
  if (rawRequest === undefined) {
    return (
      <>
        <PageHeader title={t('externalParticipation.review.title')} />
        {requestRecord.loading ? (
          <LoadingState />
        ) : (
          <ErrorState
            message={participationProblemMessage(requestRecord.error, t)}
            onRetry={requestRecord.reload}
          />
        )}
      </>
    );
  }

  const user = session.user;
  const revision = disclosedContribution(rawRevision);
  const request = disclosedRequest(rawRequest);
  const etag = record.etag;
  const previous =
    revision.previousRevisionId === null
      ? null
      : (revisions.data?.find((candidate) => candidate.id === revision.previousRevisionId) ?? null);
  const task = sourceTask.data?.data ?? null;
  const currentVersion = versionOfEtag(sourceTask.data?.etag ?? null);

  const changed = (message: TranslationKey, tone: Notice['tone'] = 'success') => {
    setDialog(null);
    setNotice({ tone, message: t(message) });
    reloadRevision();
    requestRecord.reload();
    revisions.reload();
  };
  const stale = () => {
    changed('externalParticipation.problems.stale', 'warning');
  };
  const reviewer = request.reviewerUserId ?? null;
  const startable = canStartReview(user, request, revision);
  const decidable = canDecide(user, request, revision);

  return (
    <div className="contribution-review">
      <PageHeader
        title={t('externalParticipation.review.heading', {
          purpose: purpose(request),
          revision: revision.revisionNo,
        })}
        description={request.targetLabel?.text ?? t('externalParticipation.review.noSource')}
      />
      <p>
        <Link to={externalRequestPath(request.id)}>
          {t('externalParticipation.actions.openDetail')}
        </Link>
      </p>
      <PageNotice notice={notice} />
      {revision.status === 'ACCEPTED_PENDING_APPLICATION' && (
        <p className="notice">
          {t('externalParticipation.review.pendingApplication')}{' '}
          <Link to={APPLICATION_MONITOR_PATH}>{t('externalParticipation.nav.applications')}</Link>
        </p>
      )}

      {(startable || decidable) && (
        <div
          className="risk-detail__commands"
          role="group"
          aria-label={t('externalParticipation.review.commands')}
        >
          {startable && (
            <button
              type="button"
              className="button button--primary"
              onClick={() => {
                setDialog('startReview');
              }}
            >
              {t('externalParticipation.actions.startReview')}
            </button>
          )}
          {decidable &&
            (['accept', 'return', 'reject'] as const).map((decision) => (
              <button
                key={decision}
                type="button"
                className={
                  decision === 'accept'
                    ? 'button button--primary'
                    : decision === 'reject'
                      ? 'button button--danger'
                      : 'button'
                }
                onClick={() => {
                  setDialog(decision);
                }}
              >
                {t(DECISION_TEXT[decision].action)}
              </button>
            ))}
        </div>
      )}
      {!startable &&
        !decidable &&
        (revision.status === 'SUBMITTED' || revision.status === 'UNDER_REVIEW') && (
          <p className="form__note">
            {t('externalParticipation.review.notReviewer', { name: personName(reviewer) })}
          </p>
        )}

      <section className="section" aria-labelledby="review-state">
        <h2 id="review-state">{t('externalParticipation.review.stateTitle')}</h2>
        <dl className="details">
          <Detail term={t('externalParticipation.fields.status')}>
            <ContributionStatusBadge status={revision.status} />
          </Detail>
          <Detail term={t('externalParticipation.fields.project')}>
            <ProjectReference formalProjectId={request.formalProjectId} />
          </Detail>
          <Detail term={t('externalParticipation.revision.contributor')}>
            {personName(revision.contributorUserId)}
          </Detail>
          <Detail term={t('externalParticipation.revision.submitted')}>
            {revision.submittedAt === null
              ? t('externalParticipation.revision.notSubmitted')
              : formatDateTime(revision.submittedAt)}
          </Detail>
          <Detail term={t('externalParticipation.fields.reviewer')}>{personName(reviewer)}</Detail>
          {revision.reviewStartedAt !== undefined && revision.reviewStartedAt !== null && (
            <Detail term={t('externalParticipation.revision.reviewStartedTerm')}>
              {formatDateTime(revision.reviewStartedAt)}
            </Detail>
          )}
        </dl>
      </section>

      <section className="section" aria-labelledby="review-source">
        <h2 id="review-source">{t('externalParticipation.review.sourceTitle')}</h2>
        <SourceIndicator
          request={request}
          revision={revision}
          task={task}
          currentVersion={currentVersion}
          loading={sourceTask.loading}
        />
      </section>

      <section className="section" aria-labelledby="review-values">
        <h2 id="review-values">{t('externalParticipation.review.valuesTitle')}</h2>
        <p className="form__note">{t('externalParticipation.review.valuesNote')}</p>
        <ComparisonTable
          request={request}
          submitted={revision.fields}
          task={task}
          previous={previous}
        />
      </section>

      {dialog === 'startReview' && (
        <ConfirmCommandDialog
          title={t('externalParticipation.command.startReview.title')}
          consequence={t('externalParticipation.command.startReview.consequence')}
          confirmLabel={t('externalParticipation.actions.startReview')}
          run={() => contributionsApi.startReview(revision.id, etag)}
          describe={participationProblemMessage}
          isStale={isStale}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            changed('externalParticipation.done.reviewStarted');
          }}
          onStale={stale}
        />
      )}
      {(dialog === 'accept' || dialog === 'return' || dialog === 'reject') && (
        <WordsDialog
          title={t(DECISION_TEXT[dialog].title)}
          consequence={t(DECISION_TEXT[dialog].consequence)}
          confirmLabel={t(DECISION_TEXT[dialog].action)}
          reason={dialog === 'accept' ? 'none' : 'required'}
          internalNote
          danger={dialog === 'reject'}
          run={(words) => contributionsApi.decide(revision.id, dialog, words, etag)}
          isStale={isStale}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            changed(DECISION_TEXT[dialog].done);
          }}
          onStale={stale}
        />
      )}
    </div>
  );
}

/**
 * Whether the source changed since the answer was given: the task's row version now (its ETag) against the one the
 * revision was submitted against. A change is not a refusal — the review goes on — but applying the answer will record
 * a conflict to revalidate (TASK-066 D-10).
 */
function SourceIndicator({
  request,
  revision,
  task,
  currentVersion,
  loading,
}: {
  request: ExternalUpdateRequestDetail;
  revision: ExternalContributionDetail;
  task: ProjectTaskDetail | null;
  currentVersion: number | null;
  loading: boolean;
}): ReactElement {
  const { t } = useI18n();
  if (request.applicationMode === 'REFERENCE_ONLY') {
    return <p className="form__note">{t('externalParticipation.review.referenceOnly')}</p>;
  }
  if (loading) {
    return <LoadingState />;
  }
  const answered = revision.targetVersion ?? null;
  const facts = (
    <p className="form__note" data-field="answeredAgainst">
      {answered === null
        ? t('externalParticipation.review.notYetSubmitted')
        : t('externalParticipation.review.answeredAgainst', {
            version: answered,
            state: revision.targetState ?? '—',
          })}
    </p>
  );
  if (task === null || currentVersion === null) {
    return (
      <>
        {facts}
        <p className="form__note">{t('externalParticipation.review.sourceUnreadable')}</p>
      </>
    );
  }
  const changedSince = answered !== null && answered !== currentVersion;
  return (
    <>
      {facts}
      <p
        className={changedSince ? 'notice notice--warning' : 'form__note'}
        data-field="sourceChanged"
        data-changed={changedSince}
      >
        {changedSince
          ? t('externalParticipation.review.sourceChanged', {
              version: currentVersion,
              state: task.status,
            })
          : t('externalParticipation.review.sourceUnchanged', { state: task.status })}
      </p>
    </>
  );
}

/** The value a schema field is applied to, as the source holds it now; only WF-04's task percentage is mapped (F-4). */
function currentSourceValue(
  request: ExternalUpdateRequestDetail,
  fieldCode: string,
  task: ProjectTaskDetail | null,
): ContributionFieldValue | null {
  if (
    task === null ||
    request.contributionSchemaCode !== 'TASK_PROGRESS' ||
    fieldCode !== 'actualPercentComplete'
  ) {
    return null;
  }
  return task.actualPercentComplete === null
    ? null
    : { fieldCode, value: String(task.actualPercentComplete), language: null };
}

/** Side by side: each field as submitted, as the source holds it now, and as the revision this one corrects gave it. */
function ComparisonTable({
  request,
  submitted,
  task,
  previous,
}: {
  request: ExternalUpdateRequestDetail;
  submitted: readonly ContributionFieldValue[];
  task: ProjectTaskDetail | null;
  previous: ExternalContributionDetail | null;
}): ReactElement {
  const { t } = useI18n();
  const label = useFieldLabel();
  const showSource = request.applicationMode === 'UPDATE_ALLOWED_SOURCE_FIELDS';
  return (
    <TableContainer caption={t('externalParticipation.review.valuesCaption')}>
      <thead>
        <tr>
          <th scope="col">{t('externalParticipation.review.field')}</th>
          <th scope="col">{t('externalParticipation.review.submittedValue')}</th>
          {showSource && <th scope="col">{t('externalParticipation.review.currentValue')}</th>}
          {previous !== null && (
            <th scope="col">
              {t('externalParticipation.review.previousValue', { revision: previous.revisionNo })}
            </th>
          )}
        </tr>
      </thead>
      <tbody>
        {request.responseFields.map((definition) => {
          const current = currentSourceValue(request, definition.fieldCode, task);
          return (
            <tr key={definition.fieldCode} data-field-code={definition.fieldCode}>
              <th scope="row">{label(definition.fieldCode)}</th>
              <td>
                <FieldValue
                  value={submitted.find((value) => value.fieldCode === definition.fieldCode)}
                />
              </td>
              {showSource && (
                <td>
                  {current === null ? (
                    <span className="cell__aside">
                      {t('externalParticipation.review.notMapped')}
                    </span>
                  ) : (
                    <FieldValue value={current} />
                  )}
                </td>
              )}
              {previous !== null && (
                <td>
                  <FieldValue
                    value={previous.fields.find(
                      (value) => value.fieldCode === definition.fieldCode,
                    )}
                  />
                </td>
              )}
            </tr>
          );
        })}
      </tbody>
    </TableContainer>
  );
}
