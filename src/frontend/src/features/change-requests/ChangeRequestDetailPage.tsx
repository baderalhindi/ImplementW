import { type ReactElement, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router';

import { type ApprovalDecision } from '@/features/approvals/api/types.ts';
import { DecisionDialog } from '@/features/approvals/dialogs/DecisionDialog.tsx';
import { WithdrawDialog } from '@/features/approvals/dialogs/WithdrawDialog.tsx';
import { instanceTone } from '@/features/approvals/presentation.ts';
import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useSaveAction } from '@/features/identity-access/forms.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { formatSar } from '@/features/projects/presentation.ts';
import { useProjectLookups } from '@/features/projects/useProjectLookups.ts';
import { ConfirmCommandDialog } from '@/features/risks/dialogs/ConfirmCommandDialog.tsx';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, FormAlert, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import {
  canCloseChangeRequest,
  canDecideChangeRequest,
  canDeleteChangeRequest,
  canEditChangeRequest,
  canMarkImplemented,
  canStartImplementation,
  canStartReview,
  canWithdrawChangeRequest,
  canWithdrawReview,
} from './access.ts';
import { type ChangeRequestCommand, changeRequestsApi } from './api/changeRequestsApi.ts';
import {
  type ChangeAuthorizationDetail,
  type ChangeRequestDetail,
  type MaterialityAssessment,
} from './api/types.ts';
import {
  appliedCount,
  approvalStateOf,
  implementationStateOf,
  isEditable,
} from './changeRequestRules.ts';
import {
  ApprovalStateBadge,
  ChangeRequestStatusBadge,
  ImplementationStateBadge,
} from './components/ChangeRequestBadges.tsx';
import { MaterialityPanel } from './components/MaterialityPanel.tsx';
import { editChangeRequestPath, projectChangeRequestsPath } from './paths.ts';
import { changeRequestProblemMessage, isForbidden, isStale } from './problems.ts';
import { authorizationTone } from './presentation.ts';
import {
  useChangeRequestRecord,
  useChangeRequestReview,
  useOptionalProject,
} from './useChangeRequestData.ts';

/** The notices SCR-106 hands over when it lands here. */
const ARRIVAL_NOTICES: Record<string, TranslationKey> = {
  saved: 'changeRequests.done.saved',
  submitted: 'changeRequests.done.submitted',
};

/** A WF-11 decision is recorded at once; the request follows when WF-11 delivers the outcome to WF-08. */
const DECISION_NOTICES: Record<ApprovalDecision, TranslationKey> = {
  approve: 'changeRequests.done.approve',
  return: 'changeRequests.done.return',
  reject: 'changeRequests.done.reject',
};

const COMMAND_TEXT: Record<
  Exclude<ChangeRequestCommand, 'submit'>,
  { title: TranslationKey; consequence: TranslationKey; done: TranslationKey }
> = {
  withdraw: {
    title: 'changeRequests.command.withdraw.title',
    consequence: 'changeRequests.command.withdraw.consequence',
    done: 'changeRequests.done.withdrawn',
  },
  'start-review': {
    title: 'changeRequests.command.startReview.title',
    consequence: 'changeRequests.command.startReview.consequence',
    done: 'changeRequests.done.reviewStarted',
  },
  'start-implementation': {
    title: 'changeRequests.command.startImplementation.title',
    consequence: 'changeRequests.command.startImplementation.consequence',
    done: 'changeRequests.done.implementationStarted',
  },
  'mark-implemented': {
    title: 'changeRequests.command.markImplemented.title',
    consequence: 'changeRequests.command.markImplemented.consequence',
    done: 'changeRequests.done.implemented',
  },
  close: {
    title: 'changeRequests.command.close.title',
    consequence: 'changeRequests.command.close.consequence',
    done: 'changeRequests.done.closed',
  },
};

type OpenDialog =
  | { kind: 'command'; command: Exclude<ChangeRequestCommand, 'submit'> }
  | { kind: 'delete' }
  | { kind: 'decision'; decision: ApprovalDecision }
  | { kind: 'withdrawReview' };

/** Where a target module applies an authorisation of each scope, under the request's project. */
const TARGET_SCREENS: Partial<Record<ChangeAuthorizationDetail['authorizationScope'], string>> = {
  REBASELINE: 'schedule/baselines',
  COMMITMENT_CHANGE: 'financials',
};

/**
 * SCR-107 Change Request Detail (acceptance criterion 2). Approval and implementation are two dimensions of the request's
 * state (WF-08 §4.1), shown as two labelled badges that never share a style: an approved request has issued
 * authorisations its modules have not applied, and it is Implemented only once they have and AHDA marks it so. The
 * reviewer decides here through WF-11's own dialogs (MOD-040–042), the originator withdraws a review in progress through
 * MOD-044, and the materiality is the server's: recorded at review, or previewed before.
 */
export function ChangeRequestDetailPage(): ReactElement | null {
  const { t, formatDateTime } = useI18n();
  const { session } = useSession();
  const { changeRequestId = '' } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const record = useChangeRequestRecord(changeRequestId);
  const request = record.data?.data;
  const projectState = useOptionalProject(request?.projectId ?? null);
  const review = useChangeRequestReview(request);
  const lookups = useProjectLookups();
  const preview = useSaveAction(changeRequestProblemMessage);
  const [previewed, setPreviewed] = useState<MaterialityAssessment | null>(null);
  const arrival = (location.state as { notice?: string } | null)?.notice;
  const arrivalKey = arrival === undefined ? undefined : ARRIVAL_NOTICES[arrival];
  const [notice, setNotice] = useState<Notice | null>(
    arrivalKey === undefined ? null : { tone: 'success', message: t(arrivalKey) },
  );
  const [dialog, setDialog] = useState<OpenDialog | null>(null);
  const personName = usePersonNames([
    request?.requestedByUserId ?? null,
    ...(request?.authorizations ?? []).map((authorization) => authorization.appliedByUserId),
  ]);

  if (session === null) {
    return null;
  }
  if (record.data === undefined || request === undefined) {
    return (
      <>
        <PageHeader title={t('changeRequests.detail.title')} />
        {record.loading ? (
          <LoadingState />
        ) : isForbidden(record.error) ? (
          <p className="state">{t('changeRequests.forbidden')}</p>
        ) : (
          <ErrorState
            message={changeRequestProblemMessage(record.error, t)}
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
  const runs = review.data?.runs ?? [];
  const pendingRun = runs.find((run) => run.status === 'PENDING') ?? null;
  const approval = approvalStateOf(request.status);
  const implementation = implementationStateOf(request.status);
  const applied = appliedCount(request);

  const reload = () => {
    record.reload();
    review.reload();
    setPreviewed(null);
  };
  const changed = (message: TranslationKey, tone: Notice['tone'] = 'success') => {
    setDialog(null);
    setNotice({ tone, message: t(message) });
    reload();
  };
  const stale = () => {
    changed('changeRequests.problems.stale', 'warning');
  };

  // The commands the request's state and the person allow, in the order of its lifecycle.
  const commands: { label: string; open: OpenDialog; primary?: boolean; danger?: boolean }[] = [];
  const command = (name: Exclude<ChangeRequestCommand, 'submit'>, primary = false) => {
    commands.push({
      label: t(COMMAND_TEXT[name].title),
      open: { kind: 'command', command: name },
      primary,
    });
  };
  if (canWithdrawChangeRequest(user, request, project)) {
    command('withdraw');
  }
  if (canDeleteChangeRequest(user, request, project)) {
    commands.push({
      label: t('changeRequests.actions.delete'),
      open: { kind: 'delete' },
      danger: true,
    });
  }
  if (canStartReview(user, request, project)) {
    command('start-review', true);
  }
  if (canDecideChangeRequest(user, request, task)) {
    commands.push(
      {
        label: t('approvals.decision.approve.action'),
        open: { kind: 'decision', decision: 'approve' },
        primary: true,
      },
      {
        label: t('approvals.decision.return.action'),
        open: { kind: 'decision', decision: 'return' },
      },
      {
        label: t('approvals.decision.reject.action'),
        open: { kind: 'decision', decision: 'reject' },
        danger: true,
      },
    );
  }
  if (canWithdrawReview(user, request, pendingRun)) {
    commands.push({
      label: t('changeRequests.actions.withdrawReview'),
      open: { kind: 'withdrawReview' },
    });
  }
  if (canStartImplementation(user, request, project)) {
    command('start-implementation', true);
  }
  if (canMarkImplemented(user, request, project)) {
    command('mark-implemented', true);
  }
  if (canCloseChangeRequest(user, request, project)) {
    command('close', true);
  }
  const editable = canEditChangeRequest(user, request, project);
  // The classification is previewed until AHDA starts the review, which records it (TASK-060 D-4).
  const previewable = isEditable(request.status) || request.status === 'SUBMITTED';

  const runPreview = async () => {
    const result = await preview.run(() => changeRequestsApi.previewMateriality(request.id));
    if (result.ok) {
      setPreviewed(result.value);
    } else if (isStale(result.error)) {
      stale();
    }
  };

  return (
    <div className="progress change-request">
      <PageHeader
        title={request.title.text}
        {...(projectState.loading
          ? {}
          : {
              description:
                project === null
                  ? t('changeRequests.detail.projectUnreadable')
                  : t('changeRequests.detail.ofProject', { project: project.title.text }),
            })}
      />
      <p>
        <Link to={projectChangeRequestsPath(request.projectId)}>
          {t('changeRequests.actions.backToList')}
        </Link>
      </p>
      <PageNotice notice={notice} />

      {(commands.length > 0 || editable) && (
        <div
          className="risk-detail__commands"
          role="group"
          aria-label={t('changeRequests.detail.commands')}
        >
          {editable && (
            <Link className="button button--primary" to={editChangeRequestPath(request.id)}>
              {t(
                request.status === 'RETURNED'
                  ? 'changeRequests.actions.correct'
                  : 'changeRequests.actions.edit',
              )}
            </Link>
          )}
          {commands.map((entry) => (
            <button
              key={entry.label}
              type="button"
              className={
                entry.danger === true
                  ? 'button button--danger'
                  : entry.primary === true
                    ? 'button button--primary'
                    : 'button'
              }
              onClick={() => {
                setDialog(entry.open);
              }}
            >
              {entry.label}
            </button>
          ))}
        </div>
      )}

      <section className="section" aria-labelledby="change-request-state">
        <h2 id="change-request-state">{t('changeRequests.detail.stateTitle')}</h2>
        <dl className="details state-dimensions">
          <Detail term={t('changeRequests.detail.approval')}>
            <ApprovalStateBadge state={approval} />
          </Detail>
          <Detail term={t('changeRequests.detail.implementation')}>
            <ImplementationStateBadge state={implementation} />
            {request.authorizations.length > 0 && (
              <span className="cell__aside">
                {t('changeRequests.detail.appliedCount', {
                  applied,
                  total: request.authorizations.length,
                })}
              </span>
            )}
          </Detail>
          <Detail term={t('changeRequests.detail.status')}>
            <ChangeRequestStatusBadge status={request.status} />
          </Detail>
        </dl>
        <p className="form__note" data-field="stateExplanation">
          {stateExplanation(request, applied, t, formatDateTime)}
        </p>
        {request.status === 'IMPLEMENTATION' && applied < request.authorizations.length && (
          <p className="form__note">{t('changeRequests.detail.waitingForApplication')}</p>
        )}
      </section>

      <section className="section" aria-labelledby="change-request-details">
        <h2 id="change-request-details">{t('changeRequests.detail.detailsTitle')}</h2>
        <dl className="details">
          <Detail term={t('changeRequests.fields.changeType')}>
            {t(`changeRequests.changeType.${request.changeType}`)}
          </Detail>
          <Detail term={t('changeRequests.detail.revision')}>
            <span dir="ltr">{request.revisionNo}</span>
          </Detail>
          <Detail term={t('changeRequests.detail.requestedBy')}>
            {personName(request.requestedByUserId)}
            <span className="cell__aside">{formatDateTime(request.createdAt)}</span>
          </Detail>
          <Detail term={t('changeRequests.detail.submitted')}>
            {request.submittedAt === null
              ? t('changeRequests.detail.notSubmitted')
              : formatDateTime(request.submittedAt)}
          </Detail>
          {request.implementedAt !== null && (
            <Detail term={t('changeRequests.detail.implementedAt')}>
              {formatDateTime(request.implementedAt)}
            </Detail>
          )}
          {request.closedAt !== null && (
            <Detail term={t('changeRequests.detail.closedAt')}>
              {formatDateTime(request.closedAt)}
            </Detail>
          )}
        </dl>
        <h3>{t('changeRequests.fields.justification')}</h3>
        <p dir="auto" className="risk-detail__text">
          {request.justification.text}
        </p>
      </section>

      <section className="section" aria-labelledby="change-request-impacts">
        <h2 id="change-request-impacts">{t('changeRequests.detail.impactsTitle')}</h2>
        <dl className="details">
          <Detail term={t('changeRequests.fields.costImpactSar')}>
            {request.costImpactSar === null ? (
              t('changeRequests.impacts.none')
            ) : (
              <span dir="ltr">
                {t('changeRequests.impacts.sar', { amount: formatSar(request.costImpactSar) })}
              </span>
            )}
          </Detail>
          <Detail term={t('changeRequests.fields.scheduleImpactDays')}>
            {request.scheduleImpactDays === null
              ? t('changeRequests.impacts.none')
              : t('changeRequests.impacts.days', { days: request.scheduleImpactDays })}
          </Detail>
          <Detail term={t('changeRequests.fields.scopeImpact')}>
            {request.scopeImpact === null ? (
              t('changeRequests.impacts.none')
            ) : (
              <span dir="auto" className="pre-line">
                {request.scopeImpact.text}
              </span>
            )}
          </Detail>
          <Detail term={t('changeRequests.fields.isContractualObligation')}>
            {t(request.isContractualObligation ? 'common.values.yes' : 'common.values.no')}
          </Detail>
          {request.requestedGovernanceProfileItemId !== null && (
            <Detail term={t('changeRequests.fields.requestedGovernanceProfile')}>
              {lookups.itemLabel(request.requestedGovernanceProfileItemId)}
            </Detail>
          )}
        </dl>
      </section>

      {request.materiality !== null ? (
        <MaterialityPanel assessment={request.materiality} impacts={request} headingLevel={2} />
      ) : (
        <section className="section" aria-labelledby="change-request-materiality">
          <h2 id="change-request-materiality">{t('changeRequests.materiality.title')}</h2>
          <p className="form__note">{t('changeRequests.detail.materialityNotRecorded')}</p>
          {previewable && <FormAlert message={preview.formError} />}
          {previewable && previewed === null && (
            <button
              type="button"
              className="button"
              disabled={preview.saving}
              onClick={() => void runPreview()}
            >
              {preview.saving
                ? t('common.states.loading')
                : t('changeRequests.actions.previewClassification')}
            </button>
          )}
          {previewed !== null && (
            <MaterialityPanel assessment={previewed} impacts={request} headingLevel={3} />
          )}
        </section>
      )}

      <section className="section" aria-labelledby="change-request-review">
        <h2 id="change-request-review">{t('changeRequests.detail.reviewTitle')}</h2>
        {review.loading ? (
          <LoadingState />
        ) : review.data?.runs === null ? (
          <p className="form__note">{t('changeRequests.detail.reviewUnreadable')}</p>
        ) : runs.length === 0 ? (
          <p className="form__note">{t('changeRequests.detail.noReview')}</p>
        ) : (
          <ul className="link-list">
            {runs.map((run) => (
              <li key={run.id}>
                <Link to={`/approvals/instances/${run.id}`}>
                  {t('changeRequests.detail.reviewOfRevision', {
                    revision: run.subject.revisionNo,
                  })}
                </Link>{' '}
                <StatusBadge
                  label={t(`approvals.instanceStatus.${run.status}`)}
                  tone={instanceTone(run.status)}
                />
              </li>
            ))}
          </ul>
        )}
        {request.status === 'UNDER_REVIEW' && task !== null && (
          <p className="form__note">{t('changeRequests.detail.youDecide')}</p>
        )}
      </section>

      <section className="section" aria-labelledby="change-request-authorizations">
        <h2 id="change-request-authorizations">{t('changeRequests.detail.authorizationsTitle')}</h2>
        {request.authorizations.length === 0 ? (
          <p className="form__note">
            {t(
              approval === 'APPROVED'
                ? 'changeRequests.detail.noAuthorizationsApproved'
                : 'changeRequests.detail.noAuthorizations',
            )}
          </p>
        ) : (
          <>
            <p className="form__note">{t('changeRequests.detail.authorizationsNote')}</p>
            <TableContainer caption={t('changeRequests.detail.authorizationsCaption')}>
              <thead>
                <tr>
                  <th scope="col">{t('changeRequests.authorization.scope')}</th>
                  <th scope="col">{t('changeRequests.authorization.target')}</th>
                  <th scope="col">{t('changeRequests.authorization.status')}</th>
                  <th scope="col">{t('changeRequests.authorization.issued')}</th>
                  <th scope="col">{t('changeRequests.authorization.applied')}</th>
                </tr>
              </thead>
              <tbody>
                {request.authorizations.map((authorization) => {
                  const screen = TARGET_SCREENS[authorization.authorizationScope];
                  return (
                    <tr key={authorization.id}>
                      <td>
                        {t(
                          `changeRequests.authorization.scopes.${authorization.authorizationScope}`,
                        )}
                      </td>
                      <td>
                        {t('changeRequests.authorization.targetVersion', {
                          version: authorization.targetRevisionNo,
                        })}
                        {screen !== undefined && (
                          <span className="cell__aside">
                            <Link to={`/projects/${request.projectId}/${screen}`}>
                              {t(
                                `changeRequests.authorization.open.${authorization.authorizationScope}`,
                              )}
                            </Link>
                          </span>
                        )}
                      </td>
                      <td>
                        <StatusBadge
                          label={t(`changeRequests.authorization.statuses.${authorization.status}`)}
                          tone={authorizationTone(authorization.status)}
                        />
                      </td>
                      <td>{formatDateTime(authorization.issuedAt)}</td>
                      <td>
                        {authorization.appliedAt === null ? (
                          t('changeRequests.authorization.notApplied')
                        ) : (
                          <>
                            {formatDateTime(authorization.appliedAt)}
                            <span className="cell__aside">
                              {personName(authorization.appliedByUserId)}
                            </span>
                            {authorization.appliedReference !== null && (
                              <span className="cell__aside" dir="ltr">
                                {authorization.appliedReference}
                              </span>
                            )}
                          </>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </TableContainer>
          </>
        )}
      </section>

      {dialog?.kind === 'command' && (
        <ConfirmCommandDialog
          title={t(COMMAND_TEXT[dialog.command].title)}
          consequence={t(COMMAND_TEXT[dialog.command].consequence)}
          confirmLabel={t(COMMAND_TEXT[dialog.command].title)}
          run={() => changeRequestsApi.command(request.id, dialog.command, etag)}
          describe={changeRequestProblemMessage}
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
          title={t('changeRequests.command.delete.title')}
          consequence={t('changeRequests.command.delete.consequence')}
          confirmLabel={t('changeRequests.actions.delete')}
          run={() => changeRequestsApi.remove(request.id, etag)}
          describe={changeRequestProblemMessage}
          isStale={isStale}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            void navigate(projectChangeRequestsPath(request.projectId), {
              state: { notice: 'changeRequestDeleted' },
            });
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
          changed('changeRequests.done.reviewWithdrawn');
        }}
      />
    </div>
  );
}

/** What the two badges mean together for this request, in a sentence. */
function stateExplanation(
  request: ChangeRequestDetail,
  applied: number,
  t: (key: TranslationKey, params?: Record<string, string | number>) => string,
  formatDateTime: (value: string) => string,
): string {
  switch (request.status) {
    case 'APPROVED':
      return t('changeRequests.detail.explain.APPROVED', {
        total: request.authorizations.length,
      });
    case 'IMPLEMENTATION':
      return t('changeRequests.detail.explain.IMPLEMENTATION', {
        applied,
        total: request.authorizations.length,
      });
    case 'IMPLEMENTED':
      return t('changeRequests.detail.explain.IMPLEMENTED', {
        date: request.implementedAt === null ? '' : formatDateTime(request.implementedAt),
      });
    case 'CLOSED':
      return t('changeRequests.detail.explain.CLOSED', {
        date: request.implementedAt === null ? '' : formatDateTime(request.implementedAt),
      });
    default:
      return t(`changeRequests.detail.explain.${request.status}`);
  }
}
