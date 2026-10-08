import { type ReactElement, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router';

import { type ApprovalDecision } from '@/features/approvals/api/types.ts';
import { DecisionDialog } from '@/features/approvals/dialogs/DecisionDialog.tsx';
import { WithdrawDialog } from '@/features/approvals/dialogs/WithdrawDialog.tsx';
import { useOptionalProject } from '@/features/change-requests/useChangeRequestData.ts';
import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { ProjectStatusBadge } from '@/features/projects/components/ProjectStatusBadge.tsx';
import { ConfirmCommandDialog } from '@/features/risks/dialogs/ConfirmCommandDialog.tsx';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { type SuspensionCommand, suspensionApi } from './api/suspensionApi.ts';
import {
  type Command,
  CommandBar,
  GovernedStateSection,
  ReviewSection,
} from './components/GovernedState.tsx';
import {
  canActivate,
  canDecide,
  canDeleteRecord,
  canEditRecord,
  canStartReview,
  canWithdrawRecord,
  canWithdrawReview,
} from './governedRequest.ts';
import {
  editSuspensionRequestPath,
  newSuspensionRequestPath,
  projectSuspensionPath,
  suspensionRequestPath,
} from './paths.ts';
import { isForbidden, isStale, suspensionClosureProblemMessage } from './problems.ts';
import {
  canRaiseSuspensionRequest,
  openRequestOf,
  SUSPENSION_WITHDRAWABLE,
} from './suspensionRules.ts';
import { useReview, useSiblingRequests, useSuspensionRecord } from './useSuspensionClosureData.ts';

/** The notices SCR-109 hands over when it lands here. */
const ARRIVAL_NOTICES: Record<string, TranslationKey> = {
  saved: 'suspensionClosure.done.saved',
  submitted: 'suspensionClosure.done.submitted',
};

/** A WF-11 decision is recorded at once; the request follows when WF-11 delivers the outcome to WF-09. */
const DECISION_NOTICES: Record<ApprovalDecision, TranslationKey> = {
  approve: 'suspensionClosure.done.approve',
  return: 'suspensionClosure.done.return',
  reject: 'suspensionClosure.done.reject',
};

type DialogCommand = Exclude<SuspensionCommand, 'submit'>;

const COMMAND_TEXT: Record<
  DialogCommand,
  { title: TranslationKey; consequence: TranslationKey; done: TranslationKey }
> = {
  withdraw: {
    title: 'suspensionClosure.command.withdraw.title',
    consequence: 'suspensionClosure.command.withdraw.consequence',
    done: 'suspensionClosure.done.withdrawn',
  },
  'start-review': {
    title: 'suspensionClosure.command.startReview.title',
    consequence: 'suspensionClosure.command.startReview.consequence',
    done: 'suspensionClosure.done.reviewStarted',
  },
  activate: {
    title: 'suspensionClosure.command.activate.title',
    consequence: 'suspensionClosure.suspension.command.activateConsequence',
    done: 'suspensionClosure.done.activated',
  },
};

type OpenDialog =
  | { kind: 'command'; command: DialogCommand }
  | { kind: 'delete' }
  | { kind: 'decision'; decision: ApprovalDecision }
  | { kind: 'withdrawReview' };

/**
 * SCR-110 Suspension Request Detail (WF-09 §13.1): the request, its review through WF-11 and its effect on the project,
 * shown apart — an approved suspension has not suspended the project until it is activated on its effective date. For
 * a suspension in effect, the resumption is a sub-flow of this page: its request is linked, or requested from here.
 */
export function SuspensionDetailPage(): ReactElement | null {
  const { t, formatDateTime } = useI18n();
  const { session } = useSession();
  const { suspensionRequestId = '' } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const record = useSuspensionRecord(suspensionRequestId);
  const request = record.data?.data;
  const projectState = useOptionalProject(request?.projectId ?? null);
  const siblings = useSiblingRequests(request?.projectId ?? null);
  const review = useReview({ module: 'Suspension', type: 'SuspensionRequest' }, request);
  const arrival = (location.state as { notice?: string } | null)?.notice;
  const arrivalKey = arrival === undefined ? undefined : ARRIVAL_NOTICES[arrival];
  const [notice, setNotice] = useState<Notice | null>(
    arrivalKey === undefined ? null : { tone: 'success', message: t(arrivalKey) },
  );
  const [dialog, setDialog] = useState<OpenDialog | null>(null);
  const personName = usePersonNames([request?.requestedByUserId ?? null]);

  if (session === null) {
    return null;
  }
  if (record.data === undefined || request === undefined) {
    return (
      <>
        <PageHeader title={t('suspensionClosure.suspension.detail.title')} />
        {record.loading ? (
          <LoadingState />
        ) : isForbidden(record.error) ? (
          <p className="state">{t('suspensionClosure.suspension.forbidden')}</p>
        ) : (
          <ErrorState
            message={suspensionClosureProblemMessage(record.error, t)}
            onRetry={record.reload}
          />
        )}
      </>
    );
  }

  const user = session.user;
  const project = projectState.data ?? null;
  const etag = record.data.etag;
  const task = review.data?.task ?? null;
  const pendingRun = (review.data?.runs ?? []).find((run) => run.status === 'PENDING') ?? null;
  const period = request.suspension;
  const inEffect = request.requestType === 'SUSPEND' && period !== null && period.endedAt === null;
  const resumption = inEffect ? openRequestOf(siblings.data ?? [], 'RESUME') : null;
  const resumes =
    inEffect &&
    project !== null &&
    canRaiseSuspensionRequest(user, project, siblings.data ?? [], 'RESUME');

  const reload = () => {
    record.reload();
    review.reload();
    siblings.reload();
  };
  const changed = (message: TranslationKey, tone: Notice['tone'] = 'success') => {
    setDialog(null);
    setNotice({ tone, message: t(message) });
    reload();
  };
  const stale = () => {
    changed('suspensionClosure.problems.stale', 'warning');
  };

  const commands: Command[] = [];
  const command = (name: DialogCommand, primary = false) => {
    commands.push({
      key: name,
      label: t(COMMAND_TEXT[name].title),
      onClick: () => {
        setDialog({ kind: 'command', command: name });
      },
      primary,
    });
  };
  if (canWithdrawRecord(user, request, project, SUSPENSION_WITHDRAWABLE)) {
    command('withdraw');
  }
  if (canDeleteRecord(user, request, project)) {
    commands.push({
      key: 'delete',
      label: t('suspensionClosure.actions.delete'),
      onClick: () => {
        setDialog({ kind: 'delete' });
      },
      danger: true,
    });
  }
  if (canStartReview(user, request, project)) {
    command('start-review', true);
  }
  if (canDecide(user, request, task)) {
    for (const decision of ['approve', 'return', 'reject'] as const) {
      commands.push({
        key: decision,
        label: t(`approvals.decision.${decision}.action`),
        onClick: () => {
          setDialog({ kind: 'decision', decision });
        },
        primary: decision === 'approve',
        danger: decision === 'reject',
      });
    }
  }
  if (canWithdrawReview(user, request, pendingRun)) {
    commands.push({
      key: 'withdrawReview',
      label: t('suspensionClosure.actions.withdrawReview'),
      onClick: () => {
        setDialog({ kind: 'withdrawReview' });
      },
    });
  }
  if (canActivate(user, request, project)) {
    command('activate', true);
  }
  const editable = canEditRecord(user, request, project);

  return (
    <div className="progress governed-detail">
      <PageHeader
        title={t(`suspensionClosure.suspension.detail.heading.${request.requestType}`)}
        {...(projectState.loading
          ? {}
          : {
              description:
                project === null
                  ? t('suspensionClosure.projectUnreadable')
                  : t('suspensionClosure.ofProject', { project: project.title.text }),
            })}
      />
      <p>
        <Link to={projectSuspensionPath(request.projectId)}>
          {t('suspensionClosure.actions.backToList')}
        </Link>
      </p>
      <PageNotice notice={notice} />

      <CommandBar
        label={t('suspensionClosure.commands')}
        links={
          editable && (
            <Link className="button button--primary" to={editSuspensionRequestPath(request.id)}>
              {t(
                request.status === 'RETURNED'
                  ? 'suspensionClosure.actions.correct'
                  : 'suspensionClosure.actions.edit',
              )}
            </Link>
          )
        }
        commands={commands}
      />

      <GovernedStateSection
        status={request.status}
        explanation={t(
          `suspensionClosure.suspension.explain.${request.requestType}.${request.status}`,
        )}
      >
        {project !== null && (
          <Detail term={t('suspensionClosure.state.project')}>
            <ProjectStatusBadge
              status={project.status}
              legacyIntakeDate={project.legacyIntakeDate}
            />
          </Detail>
        )}
      </GovernedStateSection>

      <section className="section" aria-labelledby="suspension-details">
        <h2 id="suspension-details">{t('suspensionClosure.detailsTitle')}</h2>
        <dl className="details">
          <Detail term={t('suspensionClosure.suspension.fields.requestType')}>
            {t(`suspensionClosure.suspension.type.${request.requestType}`)}
          </Detail>
          <Detail term={t('suspensionClosure.revision')}>
            <span dir="ltr">{request.revisionNo}</span>
          </Detail>
          <Detail term={t('suspensionClosure.requestedBy')}>
            {personName(request.requestedByUserId)}
            <span className="cell__aside">{formatDateTime(request.createdAt)}</span>
          </Detail>
          <Detail term={t('suspensionClosure.submitted')}>
            {request.submittedAt === null
              ? t('suspensionClosure.notSubmitted')
              : formatDateTime(request.submittedAt)}
          </Detail>
          <Detail term={t('suspensionClosure.suspension.fields.requestedEffectiveDate')}>
            {request.requestedEffectiveDate === null ? (
              t('suspensionClosure.notStated')
            ) : (
              <span dir="ltr">{request.requestedEffectiveDate}</span>
            )}
          </Detail>
          {request.requestType === 'SUSPEND' && (
            <Detail term={t('suspensionClosure.suspension.fields.plannedResumptionDate')}>
              {request.plannedResumptionDate === null ? (
                t('suspensionClosure.notStated')
              ) : (
                <>
                  <span dir="ltr">{request.plannedResumptionDate}</span>
                  <span className="cell__aside">
                    {t('suspensionClosure.suspension.plannedOnly')}
                  </span>
                </>
              )}
            </Detail>
          )}
          {request.effectedAt !== null && (
            <Detail term={t('suspensionClosure.effectedAt')}>
              {formatDateTime(request.effectedAt)}
            </Detail>
          )}
        </dl>
        <h3>{t('suspensionClosure.suspension.fields.reason')}</h3>
        <p dir="auto" className="risk-detail__text">
          {request.reason.text}
        </p>
      </section>

      {period !== null && (
        <section className="section" aria-labelledby="suspension-period">
          <h2 id="suspension-period">{t('suspensionClosure.suspension.periodTitle')}</h2>
          <dl className="details">
            <Detail term={t('suspensionClosure.suspension.period.started')}>
              {formatDateTime(period.startedAt)}
            </Detail>
            <Detail term={t('suspensionClosure.suspension.period.ended')}>
              {period.endedAt === null
                ? t('suspensionClosure.suspension.period.ongoing')
                : formatDateTime(period.endedAt)}
            </Detail>
            {period.endReason !== null && (
              <Detail term={t('suspensionClosure.suspension.period.endReason')}>
                {t(`suspensionClosure.suspension.endReason.${period.endReason}`)}
              </Detail>
            )}
            {request.requestType === 'SUSPEND' && period.resumptionRequestId !== null && (
              <Detail term={t('suspensionClosure.suspension.resumption.title')}>
                <Link to={suspensionRequestPath(period.resumptionRequestId)}>
                  {t('suspensionClosure.suspension.resumption.open')}
                </Link>
              </Detail>
            )}
            {request.requestType === 'RESUME' && (
              <Detail term={t('suspensionClosure.suspension.resumption.ofSuspension')}>
                <Link to={suspensionRequestPath(period.suspensionRequestId)}>
                  {t('suspensionClosure.suspension.resumption.openSuspension')}
                </Link>
              </Detail>
            )}
          </dl>
          {inEffect && (
            <div className="closeout-stage__action">
              {resumption !== null ? (
                <p className="form__note">
                  {t('suspensionClosure.suspension.resumption.underWay')}{' '}
                  <Link to={suspensionRequestPath(resumption.id)}>
                    {t('suspensionClosure.suspension.resumption.open')}
                  </Link>
                </p>
              ) : resumes ? (
                <Link
                  className="button button--primary"
                  to={newSuspensionRequestPath(request.projectId, 'RESUME')}
                >
                  {t('suspensionClosure.actions.requestResumption')}
                </Link>
              ) : (
                <p className="form__note">{t('suspensionClosure.suspension.resumption.none')}</p>
              )}
            </div>
          )}
        </section>
      )}

      <ReviewSection
        review={review.data}
        loading={review.loading}
        deciding={request.status === 'UNDER_REVIEW' && task !== null}
      />

      {dialog?.kind === 'command' && (
        <ConfirmCommandDialog
          title={t(COMMAND_TEXT[dialog.command].title)}
          consequence={t(COMMAND_TEXT[dialog.command].consequence)}
          confirmLabel={t(COMMAND_TEXT[dialog.command].title)}
          run={() => suspensionApi.command(request.id, dialog.command, etag)}
          describe={suspensionClosureProblemMessage}
          isStale={isStale}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            changed(COMMAND_TEXT[dialog.command].done);
          }}
          onStale={stale}
        />
      )}
      {dialog?.kind === 'delete' && (
        <ConfirmCommandDialog
          title={t('suspensionClosure.command.delete.title')}
          consequence={t('suspensionClosure.command.delete.consequence')}
          confirmLabel={t('suspensionClosure.actions.delete')}
          run={() => suspensionApi.remove(request.id, etag)}
          describe={suspensionClosureProblemMessage}
          isStale={isStale}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            void navigate(projectSuspensionPath(request.projectId));
          }}
          onStale={stale}
        />
      )}
      <DecisionDialog
        target={
          dialog?.kind === 'decision' && task !== null
            ? { taskId: task.taskId, action: dialog.decision }
            : null
        }
        onClose={() => {
          setDialog(null);
        }}
        onDone={(action) => {
          if (action !== 'escalate') {
            changed(DECISION_NOTICES[action]);
          }
        }}
        onStale={(message) => {
          setDialog(null);
          setNotice({ tone: 'warning', message });
          reload();
        }}
      />
      <WithdrawDialog
        instanceId={dialog?.kind === 'withdrawReview' ? (pendingRun?.id ?? null) : null}
        onClose={() => {
          setDialog(null);
        }}
        onDone={() => {
          changed('suspensionClosure.done.reviewWithdrawn');
        }}
      />
    </div>
  );
}
