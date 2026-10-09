import { type ReactElement, useState } from 'react';
import { Link } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { ConfirmCommandDialog } from '@/features/risks/dialogs/ConfirmCommandDialog.tsx';
import { useListParams } from '@/shared/api/useListParams.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { canApply } from './access.ts';
import { sourceApplicationsApi } from './api/externalParticipationApi.ts';
import { type SourceApplicationDetail, type SourceApplicationStatus } from './api/types.ts';
import { AttemptsTable } from './components/AttemptsTable.tsx';
import { ApplicationStateBadge } from './components/Badges.tsx';
import { ProjectReference } from './components/RequestTable.tsx';
import { ApplyDialog } from './dialogs/ApplyDialog.tsx';
import { useFailureText, usePurpose } from './labels.ts';
import { contributionReviewPath, externalRequestPath } from './paths.ts';
import { applicationStyle } from './presentation.ts';
import { isStale, participationProblemMessage } from './problems.ts';
import {
  type ApplicationAction,
  applicationActionOf,
  type ApplicationState,
  APPLICATION_STATES,
} from './rules.ts';
import { type ApplicationCase, useApplicationCases } from './useExternalParticipationData.ts';

/** What a new attempt's outcome says: every outcome is a result, and a conflict or a refusal is told as such. */
const ATTEMPT_NOTICES: Record<
  SourceApplicationStatus,
  { message: TranslationKey; tone: Notice['tone'] }
> = {
  APPLIED: { message: 'externalParticipation.done.applied', tone: 'success' },
  CONFLICT: { message: 'externalParticipation.done.conflictRecorded', tone: 'warning' },
  FAILED: { message: 'externalParticipation.done.failureRecorded', tone: 'warning' },
};

const ACTION_LABELS: Record<ApplicationAction, TranslationKey> = {
  apply: 'externalParticipation.actions.apply',
  revalidate: 'externalParticipation.actions.revalidate',
  retry: 'externalParticipation.actions.applyAgain',
};

interface OpenDialog {
  action: ApplicationAction;
  entry: ApplicationCase;
}

function isApplicationState(value: string): value is ApplicationState {
  return (APPLICATION_STATES as readonly string[]).includes(value);
}

/**
 * SCR-166 Source Application Monitoring (acceptance criterion 2): every accepted answer of a typed source, where its
 * application stands, and the one action that state takes. A conflict (the source changed since the answer) and a
 * retry (a revalidated conflict, or a refusal the source may lift) are states with their own words, mark, colour and
 * explanation — never a generic error — and a final failure is told apart from both. The lineage of every attempt is
 * kept with each answer. Nothing is applied by this screen on its own: AHDA applies, revalidates and applies again.
 */
export function SourceApplicationMonitorPage(): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  const cases = useApplicationCases();
  const { params, setFilter } = useListParams();
  const stateFilter = params.get('state') ?? '';
  const [notice, setNotice] = useState<Notice | null>(null);
  const [dialog, setDialog] = useState<OpenDialog | null>(null);

  if (session === null) {
    return null;
  }
  const applier = canApply(session.user);
  const done = (message: TranslationKey, tone: Notice['tone'], params?: Record<string, string>) => {
    setDialog(null);
    setNotice({ tone, message: t(message, params) });
    cases.reload();
  };
  const stale = () => {
    done('externalParticipation.problems.stale', 'warning');
  };

  return (
    <>
      <PageHeader
        title={t('externalParticipation.applications.title')}
        description={t('externalParticipation.applications.description')}
      />
      <PageNotice notice={notice} />
      <StateLegend />
      {cases.data === undefined ? (
        cases.loading ? (
          <LoadingState />
        ) : (
          <ErrorState
            message={participationProblemMessage(cases.error, t)}
            onRetry={cases.reload}
          />
        )
      ) : cases.data.length === 0 ? (
        <EmptyState title={t('externalParticipation.applications.emptyTitle')}>
          <p>{t('externalParticipation.applications.emptyHint')}</p>
        </EmptyState>
      ) : (
        <>
          <div className="filters">
            <SelectField
              label={t('externalParticipation.filter.applicationState')}
              name="state"
              value={isApplicationState(stateFilter) ? stateFilter : ''}
              placeholder={t('externalParticipation.filter.allStates')}
              options={APPLICATION_STATES.map((state) => ({
                value: state,
                label: t(`externalParticipation.applicationState.${state}`),
              }))}
              onChange={(value) => {
                setFilter('state', value);
              }}
            />
          </div>
          <p className="list-order" role="status">
            {APPLICATION_STATES.map((state) => ({
              state,
              count: cases.data?.filter((entry) => entry.state === state).length ?? 0,
            }))
              .filter(({ count }) => count > 0)
              .map(({ state, count }) =>
                t('externalParticipation.applications.count', {
                  count,
                  state: t(`externalParticipation.applicationState.${state}`),
                }),
              )
              .join(' · ')}
          </p>
          <ul className="application-cases">
            {cases.data
              .filter((entry) => stateFilter === '' || entry.state === stateFilter)
              .map((entry) => (
                <ApplicationCaseCard
                  key={entry.revision.id}
                  entry={entry}
                  applier={applier}
                  onAction={(action) => {
                    setDialog({ action, entry });
                  }}
                />
              ))}
          </ul>
        </>
      )}

      {dialog !== null && dialog.action !== 'revalidate' && (
        <ApplyDialog
          contributionId={dialog.entry.revision.id}
          retry={dialog.action === 'retry'}
          onClose={() => {
            setDialog(null);
          }}
          onDone={(attempt) => {
            const outcome = ATTEMPT_NOTICES[attempt.status];
            done(outcome.message, outcome.tone);
          }}
          onStale={stale}
        />
      )}
      {dialog?.action === 'revalidate' && dialog.entry.attempts[0] !== undefined && (
        <ConfirmCommandDialog
          title={t('externalParticipation.command.revalidate.title')}
          consequence={t('externalParticipation.command.revalidate.consequence', {
            found: dialog.entry.attempts[0].actualTargetRevisionNo ?? '—',
          })}
          confirmLabel={t('externalParticipation.actions.revalidate')}
          run={() => sourceApplicationsApi.revalidate(dialog.entry.attempts[0]?.id ?? '')}
          describe={participationProblemMessage}
          isStale={isStale}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            done('externalParticipation.done.revalidated', 'success');
          }}
          onStale={stale}
        />
      )}
    </>
  );
}

/** Each state in words with its mark, so a reader learns them once (WCAG 1.4.1: never colour alone). */
function StateLegend(): ReactElement {
  const { t } = useI18n();
  return (
    <details className="application-legend">
      <summary>{t('externalParticipation.applications.legendTitle')}</summary>
      <dl className="details">
        {APPLICATION_STATES.map((state) => (
          <Detail key={state} term={t(`externalParticipation.applicationState.${state}`)}>
            <ApplicationStateBadge state={state} />{' '}
            {t(`externalParticipation.applicationExplain.${state}`)}
          </Detail>
        ))}
      </dl>
    </details>
  );
}

/** One accepted answer: where its application stands, why, the action it takes and its lineage. */
function ApplicationCaseCard({
  entry,
  applier,
  onAction,
}: {
  entry: ApplicationCase;
  applier: boolean;
  onAction: (action: ApplicationAction) => void;
}): ReactElement {
  const { t } = useI18n();
  const purpose = usePurpose();
  const { request, revision, attempts, state } = entry;
  const action = applicationActionOf(state);
  const headingId = `application-${revision.id}`;
  return (
    <li
      className={`application-case application-case--${applicationStyle(state)}`}
      data-application-case={state}
      aria-labelledby={headingId}
    >
      <div className="application-case__header">
        <h2 id={headingId} className="application-case__title">
          <Link to={externalRequestPath(request.id)}>{purpose(request)}</Link>
          {request.targetLabel !== null && (
            <span className="cell__aside" dir="auto">
              {request.targetLabel.text}
            </span>
          )}
        </h2>
        <ApplicationStateBadge state={state} />
      </div>
      <p className="application-case__explain" data-field="stateExplanation">
        <StateSentence entry={entry} />
      </p>
      <dl className="details">
        <Detail term={t('externalParticipation.fields.project')}>
          <ProjectReference formalProjectId={request.formalProjectId} />
        </Detail>
        <Detail term={t('externalParticipation.table.revision')}>
          <Link to={contributionReviewPath(revision.id)}>
            {t('externalParticipation.revision.number', { revision: revision.revisionNo })}
          </Link>
        </Detail>
        <Detail term={t('externalParticipation.applications.attempts')}>
          <span dir="ltr">{attempts.length}</span>
        </Detail>
      </dl>
      {applier && action !== null && (
        <p>
          <button
            type="button"
            className={action === 'revalidate' ? 'button' : 'button button--primary'}
            onClick={() => {
              onAction(action);
            }}
          >
            {t(ACTION_LABELS[action])}
          </button>
        </p>
      )}
      {attempts.length > 0 && (
        <details>
          <summary>{t('externalParticipation.applications.lineage')}</summary>
          <AttemptsTable
            attempts={attempts}
            caption={t('externalParticipation.applications.lineageCaption', {
              revision: revision.revisionNo,
            })}
          />
        </details>
      )}
    </li>
  );
}

/** Why the answer stands where it does, in a sentence naming the versions and the safe failure. */
function StateSentence({ entry }: { entry: ApplicationCase }): ReactElement {
  const { t, formatDateTime } = useI18n();
  const failureText = useFailureText();
  const latest: SourceApplicationDetail | undefined = entry.attempts[0];
  const personName = usePersonNames([
    latest?.attemptedByUserId ?? null,
    latest?.revalidatedByUserId ?? null,
  ]);
  switch (entry.state) {
    case 'PENDING':
      return (
        <>
          {t('externalParticipation.applicationExplain.PENDING_AT', {
            date:
              entry.revision.reviewedAt === null ? '—' : formatDateTime(entry.revision.reviewedAt),
          })}
        </>
      );
    case 'CONFLICT':
      return (
        <>
          {t('externalParticipation.applicationExplain.CONFLICT_VERSIONS', {
            expected: latest?.expectedTargetRevisionNo ?? '—',
            found: latest?.actualTargetRevisionNo ?? '—',
          })}
        </>
      );
    case 'REVALIDATED':
      return (
        <>
          {t('externalParticipation.applicationExplain.REVALIDATED_BY', {
            person: personName(latest?.revalidatedByUserId ?? null),
            version: latest?.revalidatedTargetRevisionNo ?? '—',
          })}
        </>
      );
    case 'RETRYABLE':
      return (
        <>
          {t('externalParticipation.applicationExplain.RETRYABLE_BECAUSE', {
            failure:
              latest?.failureCode === null || latest === undefined
                ? '—'
                : failureText(latest.failureCode),
          })}
        </>
      );
    case 'APPLIED':
      return (
        <>
          {t('externalParticipation.applicationExplain.APPLIED_AT', {
            attempt: latest?.attemptNo ?? '—',
            date: latest === undefined ? '—' : formatDateTime(latest.completedAt),
            person: personName(latest?.attemptedByUserId ?? null),
          })}
        </>
      );
    case 'FAILED':
      return (
        <>
          {t('externalParticipation.applicationExplain.FAILED_BECAUSE', {
            failure:
              latest?.failureCode === null || latest === undefined
                ? '—'
                : failureText(latest.failureCode),
          })}
        </>
      );
  }
}
