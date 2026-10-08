import { type ReactElement, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router';

import { type ApprovalDecision } from '@/features/approvals/api/types.ts';
import { DecisionDialog } from '@/features/approvals/dialogs/DecisionDialog.tsx';
import { WithdrawDialog } from '@/features/approvals/dialogs/WithdrawDialog.tsx';
import { useOptionalProject } from '@/features/change-requests/useChangeRequestData.ts';
import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useSaveAction } from '@/features/identity-access/forms.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { ProjectStatusBadge } from '@/features/projects/components/ProjectStatusBadge.tsx';
import { ConfirmCommandDialog } from '@/features/risks/dialogs/ConfirmCommandDialog.tsx';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';

import {
  type CloseoutCommand,
  closeoutApi,
  type CloseoutStage,
  SUBJECT_TYPES,
} from './api/closeoutApi.ts';
import { type ReadinessCheckCode } from './api/types.ts';
import {
  CLOSEOUT_STAGES,
  CLOSEOUT_WITHDRAWABLE,
  closeoutStagesOf,
  isWaivableNow,
  obligationCaseOf,
} from './closeoutRules.ts';
import { CloseoutStages } from './components/CloseoutStages.tsx';
import {
  type Command,
  CommandBar,
  GovernedStateSection,
  ReviewSection,
} from './components/GovernedState.tsx';
import { ObligationsPanel } from './components/ObligationsPanel.tsx';
import { ReadinessPanel } from './components/ReadinessPanel.tsx';
import { WaiveCheckDialog } from './dialogs/WaiveCheckDialog.tsx';
import {
  canActivate,
  canDecide,
  canDeleteRecord,
  canEditRecord,
  canStartReview,
  canWithdrawRecord,
  canWithdrawReview,
  isAhdaGatekeeper,
  isEditable,
} from './governedRequest.ts';
import { editCloseoutCasePath, projectCloseoutPath } from './paths.ts';
import { isForbidden, isStale, suspensionClosureProblemMessage } from './problems.ts';
import { useCloseoutRecord, useProjectCloseout, useReview } from './useSuspensionClosureData.ts';

const ARRIVAL_NOTICES: Record<string, TranslationKey> = {
  saved: 'suspensionClosure.done.saved',
};

const DECISION_NOTICES: Record<ApprovalDecision, TranslationKey> = {
  approve: 'suspensionClosure.done.approve',
  return: 'suspensionClosure.done.return',
  reject: 'suspensionClosure.done.reject',
};

type DialogCommand = Exclude<CloseoutCommand, 'evaluate-readiness'>;

const COMMAND_TEXT: Record<
  DialogCommand,
  { title: TranslationKey; consequence: TranslationKey; done: TranslationKey }
> = {
  submit: {
    title: 'suspensionClosure.command.submit.title',
    consequence: 'suspensionClosure.closeout.command.submitConsequence',
    done: 'suspensionClosure.done.submitted',
  },
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
    consequence: 'suspensionClosure.closeout.command.activateConsequence',
    done: 'suspensionClosure.done.activated',
  },
};

type OpenDialog =
  | { kind: 'command'; command: DialogCommand }
  | { kind: 'delete' }
  | { kind: 'waive'; checkCode: ReadinessCheckCode }
  | { kind: 'decision'; decision: ApprovalDecision }
  | { kind: 'withdrawReview' };

/** SCR-113 at `/closure-requests/:stage/:caseId`. */
export function CloseoutDetailPage(): ReactElement {
  const { stage: stageParam, caseId = '' } = useParams();
  const { t } = useI18n();
  const stage = CLOSEOUT_STAGES.find((candidate) => candidate === stageParam);
  if (stage === undefined) {
    return <p className="state">{t('common.notFound.title')}</p>;
  }
  return <CloseoutDetail key={`${stage}/${caseId}`} stage={stage} caseId={caseId} />;
}

/**
 * SCR-113 Closure Request Detail, reused for both stages (WF-10 §14.2): which of the two sequential stages this case is,
 * its approval and its activation shown apart, the server's readiness with the criteria that block it and where they are
 * settled, the project's post-project obligations, and its review through WF-11.
 */
function CloseoutDetail({
  stage,
  caseId,
}: {
  stage: CloseoutStage;
  caseId: string;
}): ReactElement | null {
  const { t, formatDateTime } = useI18n();
  const { session } = useSession();
  const location = useLocation();
  const navigate = useNavigate();
  const record = useCloseoutRecord(stage, caseId);
  const closeoutCase = record.data?.data;
  const projectState = useOptionalProject(closeoutCase?.projectId ?? null);
  const closeout = useProjectCloseout(closeoutCase?.projectId ?? null);
  const review = useReview({ module: 'Closure', type: SUBJECT_TYPES[stage] }, closeoutCase);
  const evaluate = useSaveAction(suspensionClosureProblemMessage);
  const arrival = (location.state as { notice?: string } | null)?.notice;
  const arrivalKey = arrival === undefined ? undefined : ARRIVAL_NOTICES[arrival];
  const [notice, setNotice] = useState<Notice | null>(
    arrivalKey === undefined ? null : { tone: 'success', message: t(arrivalKey) },
  );
  const [dialog, setDialog] = useState<OpenDialog | null>(null);
  const personName = usePersonNames([closeoutCase?.requestedByUserId ?? null]);

  if (session === null) {
    return null;
  }
  if (record.data === undefined || closeoutCase === undefined) {
    return (
      <>
        <PageHeader title={t(`suspensionClosure.closeout.detail.heading.${stage}`)} />
        {record.loading ? (
          <LoadingState />
        ) : isForbidden(record.error) ? (
          <p className="state">{t('suspensionClosure.closeout.forbidden')}</p>
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
  const stages =
    project === null || closeout.data === undefined
      ? null
      : closeoutStagesOf(project.status, closeout.data.completions, closeout.data.closures);
  const outcome = 'outcome' in closeoutCase ? closeoutCase.outcome : null;

  const reload = () => {
    record.reload();
    review.reload();
    closeout.reload();
  };
  const changed = (message: TranslationKey, tone: Notice['tone'] = 'success') => {
    setDialog(null);
    setNotice({ tone, message: t(message) });
    reload();
  };
  const stale = () => {
    changed('suspensionClosure.problems.stale', 'warning');
  };

  const editable = canEditRecord(user, closeoutCase, project);
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
  if (editable) {
    command('submit', true);
  }
  if (canWithdrawRecord(user, closeoutCase, project, CLOSEOUT_WITHDRAWABLE)) {
    command('withdraw');
  }
  if (canDeleteRecord(user, closeoutCase, project)) {
    commands.push({
      key: 'delete',
      label: t('suspensionClosure.actions.delete'),
      onClick: () => {
        setDialog({ kind: 'delete' });
      },
      danger: true,
    });
  }
  if (canStartReview(user, closeoutCase, project)) {
    command('start-review', true);
  }
  if (canDecide(user, closeoutCase, task)) {
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
  if (canWithdrawReview(user, closeoutCase, pendingRun)) {
    commands.push({
      key: 'withdrawReview',
      label: t('suspensionClosure.actions.withdrawReview'),
      onClick: () => {
        setDialog({ kind: 'withdrawReview' });
      },
    });
  }
  if (canActivate(user, closeoutCase, project)) {
    command('activate', true);
  }

  const runEvaluation = async () => {
    const result = await evaluate.run(() =>
      closeoutApi.command(stage, closeoutCase.id, 'evaluate-readiness', etag),
    );
    if (result.ok) {
      changed('suspensionClosure.done.evaluated');
    } else if (isStale(result.error)) {
      stale();
    }
  };
  const waives = isEditable(closeoutCase.status) && isAhdaGatekeeper(user, closeoutCase, project);

  return (
    <div className="progress governed-detail">
      <PageHeader
        title={t(`suspensionClosure.closeout.detail.heading.${stage}`)}
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
        <Link to={projectCloseoutPath(closeoutCase.projectId)}>
          {t('suspensionClosure.actions.backToCloseout')}
        </Link>
      </p>
      <PageNotice notice={notice} />

      <CommandBar
        label={t('suspensionClosure.commands')}
        links={
          editable && (
            <Link className="button" to={editCloseoutCasePath(stage, closeoutCase.id)}>
              {t(
                closeoutCase.status === 'RETURNED'
                  ? 'suspensionClosure.actions.correct'
                  : 'suspensionClosure.actions.edit',
              )}
            </Link>
          )
        }
        commands={commands}
      />

      {stages === null ? (
        <p className="closeout-stage-label" data-stage={stage}>
          {t('suspensionClosure.stages.numbered', {
            number: stage === 'completion' ? 1 : 2,
            name: t(`suspensionClosure.stages.name.${stage}`),
          })}
        </p>
      ) : (
        <CloseoutStages stages={stages} highlight={stage} />
      )}

      <GovernedStateSection
        status={closeoutCase.status}
        explanation={t(`suspensionClosure.closeout.explain.${stage}.${closeoutCase.status}`)}
      >
        {project !== null && (
          <Detail term={t('suspensionClosure.state.project')}>
            <ProjectStatusBadge
              status={project.status}
              legacyIntakeDate={project.legacyIntakeDate}
            />
          </Detail>
        )}
        {outcome !== null && (
          <Detail term={t('suspensionClosure.closeout.fields.outcome')}>
            {t(`suspensionClosure.closeout.outcome.${outcome}`)}
          </Detail>
        )}
      </GovernedStateSection>

      <section className="section" aria-labelledby="closeout-details">
        <h2 id="closeout-details">{t('suspensionClosure.detailsTitle')}</h2>
        <dl className="details">
          <Detail term={t('suspensionClosure.revision')}>
            <span dir="ltr">{closeoutCase.revisionNo}</span>
          </Detail>
          <Detail term={t('suspensionClosure.requestedBy')}>
            {personName(closeoutCase.requestedByUserId)}
            <span className="cell__aside">{formatDateTime(closeoutCase.createdAt)}</span>
          </Detail>
          <Detail term={t('suspensionClosure.submitted')}>
            {closeoutCase.submittedAt === null
              ? t('suspensionClosure.notSubmitted')
              : formatDateTime(closeoutCase.submittedAt)}
          </Detail>
          {'actualProjectCompletionDate' in closeoutCase && (
            <Detail term={t('suspensionClosure.closeout.fields.actualProjectCompletionDate')}>
              {closeoutCase.actualProjectCompletionDate === null ? (
                t('suspensionClosure.notStated')
              ) : (
                <span dir="ltr">{closeoutCase.actualProjectCompletionDate}</span>
              )}
            </Detail>
          )}
          {closeoutCase.effectedAt !== null && (
            <Detail term={t('suspensionClosure.effectedAt')}>
              {formatDateTime(closeoutCase.effectedAt)}
            </Detail>
          )}
        </dl>
        <h3>{t(`suspensionClosure.closeout.fields.narrative.${stage}`)}</h3>
        {(() => {
          const narrative =
            'completionNarrative' in closeoutCase
              ? closeoutCase.completionNarrative
              : closeoutCase.closureNarrative;
          return narrative === null ? (
            <p className="form__note">{t('suspensionClosure.notStated')}</p>
          ) : (
            <p dir="auto" className="risk-detail__text">
              {narrative.text}
            </p>
          );
        })()}
      </section>

      <FormAlert message={evaluate.formError} />
      <ReadinessPanel
        readiness={closeoutCase.readiness}
        projectId={closeoutCase.projectId}
        onEvaluate={editable ? () => void runEvaluation() : undefined}
        evaluating={evaluate.saving}
        canWaive={(check) => waives && isWaivableNow(check)}
        onWaive={(check) => {
          setDialog({ kind: 'waive', checkCode: check.checkCode });
        }}
      />

      {closeout.data !== undefined && (
        <ObligationsPanel
          obligations={closeout.data.obligations}
          user={user}
          project={project}
          target={stages === null ? null : obligationCaseOf(stages)}
          onChanged={(message, tone) => {
            changed(message, tone);
          }}
        />
      )}

      <ReviewSection
        review={review.data}
        loading={review.loading}
        deciding={closeoutCase.status === 'UNDER_REVIEW' && task !== null}
      />

      {dialog?.kind === 'command' && (
        <ConfirmCommandDialog
          title={t(COMMAND_TEXT[dialog.command].title)}
          consequence={t(COMMAND_TEXT[dialog.command].consequence)}
          confirmLabel={t(COMMAND_TEXT[dialog.command].title)}
          run={() => closeoutApi.command(stage, closeoutCase.id, dialog.command, etag)}
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
          consequence={t('suspensionClosure.closeout.command.deleteConsequence')}
          confirmLabel={t('suspensionClosure.actions.delete')}
          run={() => closeoutApi.remove(stage, closeoutCase.id, etag)}
          describe={suspensionClosureProblemMessage}
          isStale={isStale}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            void navigate(projectCloseoutPath(closeoutCase.projectId));
          }}
          onStale={stale}
        />
      )}
      {dialog?.kind === 'waive' && (
        <WaiveCheckDialog
          stage={stage}
          caseId={closeoutCase.id}
          checkCode={dialog.checkCode}
          etag={etag}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            changed('suspensionClosure.done.waived');
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
