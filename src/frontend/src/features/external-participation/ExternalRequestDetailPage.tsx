import { type ReactElement, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { useSession } from '@/features/identity-access/session/useSession.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { languageTag } from '@/features/projects/presentation.ts';
import { ConfirmCommandDialog } from '@/features/risks/dialogs/ConfirmCommandDialog.tsx';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { canCancelRequest, canManageDraft, canReassign, isResponder } from './access.ts';
import { externalRequestsApi } from './api/externalParticipationApi.ts';
import { type ExternalContributionDetail, type ExternalUpdateRequestDetail } from './api/types.ts';
import { AttemptsTable } from './components/AttemptsTable.tsx';
import { DueBadge, RequestStatusBadge } from './components/Badges.tsx';
import { ProjectReference } from './components/RequestTable.tsx';
import { ResponseForm } from './components/ResponseForm.tsx';
import { RevisionHistory } from './components/RevisionHistory.tsx';
import { AssignPersonDialog } from './dialogs/AssignPersonDialog.tsx';
import { WordsDialog } from './dialogs/WordsDialog.tsx';
import {
  APPLICATION_MONITOR_PATH,
  contributionReviewPath,
  editExternalRequestPath,
  EXTERNAL_REQUESTS_PATH,
  MY_EXTERNAL_REQUESTS_PATH,
  projectExternalRequestsPath,
} from './paths.ts';
import { usePurpose } from './labels.ts';
import { disclosedRequest, isExternalView } from './projection.ts';
import { isNotFound, isStale, participationProblemMessage } from './problems.ts';
import { awaitsReview, currentRevision, isApplicationCase } from './rules.ts';
import {
  useAttempts,
  useEntityNames,
  useRequestRecord,
  useRevisions,
} from './useExternalParticipationData.ts';

/** The notices SCR-161 hands over when it lands here. */
const ARRIVAL_NOTICES: Record<string, TranslationKey> = {
  saved: 'externalParticipation.done.saved',
  issued: 'externalParticipation.done.issued',
};

type OpenDialog = 'issue' | 'delete' | 'cancel' | 'assignResponder' | 'assignReviewer';

/**
 * SCR-162 External Update Request Detail, one address for both audiences (WF-13 §12.3). The layout follows the record's
 * own projection, which the API decides: AHDA's internal case view (who reviews it, who issued it, the configuration
 * that enabled it, every revision with its source version, reviewer and internal note, the application lineage), or the
 * entity's least-disclosure workspace (the request's safe context, the answer form for its responder, its own
 * submission history). An external view renders only what `disclosedRequest` keeps (acceptance criterion 1) and
 * offers no origination, cancellation, assignment, review or escalation (gate decision).
 */
export function ExternalRequestDetailPage(): ReactElement | null {
  const { t } = useI18n();
  const { session } = useSession();
  const { requestId = '' } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const record = useRequestRecord(requestId);
  const raw = record.data?.data;
  const revisions = useRevisions(raw === undefined || raw.status === 'DRAFT' ? null : raw.id);
  const purpose = usePurpose();
  const arrival = (location.state as { notice?: string } | null)?.notice;
  const arrivalKey = arrival === undefined ? undefined : ARRIVAL_NOTICES[arrival];
  const [notice, setNotice] = useState<Notice | null>(
    arrivalKey === undefined ? null : { tone: 'success', message: t(arrivalKey) },
  );
  const [dialog, setDialog] = useState<OpenDialog | null>(null);

  if (session === null) {
    return null;
  }
  if (record.data === undefined || raw === undefined) {
    return (
      <>
        <PageHeader title={t('externalParticipation.detail.title')} />
        {record.loading ? (
          <LoadingState />
        ) : isNotFound(record.error) ? (
          <p className="state">{t('externalParticipation.detail.notFound')}</p>
        ) : (
          <ErrorState
            message={participationProblemMessage(record.error, t)}
            onRetry={record.reload}
          />
        )}
      </>
    );
  }

  const user = session.user;
  const request = disclosedRequest(raw);
  const external = isExternalView(request);
  const etag = record.data.etag;

  const reload = () => {
    record.reload();
    revisions.reload();
  };
  const changed = (message: TranslationKey, tone: Notice['tone'] = 'success') => {
    setDialog(null);
    setNotice({ tone, message: t(message) });
    reload();
  };
  const stale = () => {
    changed('externalParticipation.problems.stale', 'warning');
  };

  // AHDA's commands on the request; none is ever offered on an external view (gate decision: no origination, no
  // escalation, AHDA issues and decides).
  const commands: { label: string; open: OpenDialog; primary?: boolean; danger?: boolean }[] = [];
  if (!external && canManageDraft(user, request)) {
    commands.push(
      { label: t('externalParticipation.actions.issue'), open: 'issue', primary: true },
      { label: t('externalParticipation.actions.delete'), open: 'delete', danger: true },
    );
  }
  if (!external && canReassign(user, request)) {
    commands.push(
      { label: t('externalParticipation.actions.changeResponder'), open: 'assignResponder' },
      { label: t('externalParticipation.actions.changeReviewer'), open: 'assignReviewer' },
    );
  }
  if (!external && canCancelRequest(user, request)) {
    commands.push({
      label: t('externalParticipation.actions.cancelRequest'),
      open: 'cancel',
      danger: true,
    });
  }
  const editable = !external && canManageDraft(user, request);

  return (
    <div className="external-request" data-projection={request.projection}>
      <PageHeader
        title={purpose(request)}
        description={t(
          external
            ? 'externalParticipation.detail.externalDescription'
            : 'externalParticipation.detail.internalDescription',
        )}
      />
      <p>
        <Link to={external ? MY_EXTERNAL_REQUESTS_PATH : EXTERNAL_REQUESTS_PATH}>
          {t(
            external
              ? 'externalParticipation.actions.backToMine'
              : 'externalParticipation.actions.backToRegister',
          )}
        </Link>
      </p>
      <PageNotice notice={notice} />

      {(commands.length > 0 || editable) && (
        <div
          className="risk-detail__commands"
          role="group"
          aria-label={t('externalParticipation.detail.commands')}
        >
          {editable && (
            <Link className="button" to={editExternalRequestPath(request.id)}>
              {t('externalParticipation.actions.edit')}
            </Link>
          )}
          {commands.map((entry) => (
            <button
              key={entry.open}
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

      <RequestContext request={request} external={external} />

      {external ? (
        <ExternalWorkspace
          request={request}
          user={user}
          revisions={revisions.data}
          loading={revisions.loading}
          onSaved={() => {
            changed('externalParticipation.done.draftSaved');
          }}
          onSubmitted={() => {
            changed('externalParticipation.done.submitted');
          }}
          onStale={stale}
        />
      ) : (
        <InternalCase request={request} revisions={revisions.data} loading={revisions.loading} />
      )}

      {dialog === 'issue' && (
        <ConfirmCommandDialog
          title={t('externalParticipation.command.issue.title')}
          consequence={t('externalParticipation.command.issue.consequence')}
          confirmLabel={t('externalParticipation.actions.issue')}
          run={() => externalRequestsApi.command(request.id, 'issue', etag)}
          describe={participationProblemMessage}
          isStale={isStale}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            changed('externalParticipation.done.issued');
          }}
          onStale={stale}
        />
      )}
      {dialog === 'delete' && (
        <ConfirmCommandDialog
          title={t('externalParticipation.command.delete.title')}
          consequence={t('externalParticipation.command.delete.consequence')}
          confirmLabel={t('externalParticipation.actions.delete')}
          run={() => externalRequestsApi.remove(request.id, etag)}
          describe={participationProblemMessage}
          isStale={isStale}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            void navigate(projectExternalRequestsPath(request.projectId));
          }}
          onStale={stale}
        />
      )}
      {dialog === 'cancel' && (
        <WordsDialog
          title={t('externalParticipation.command.cancel.title')}
          consequence={t('externalParticipation.command.cancel.consequence')}
          confirmLabel={t('externalParticipation.actions.cancelRequest')}
          reason="required"
          internalNote={false}
          danger
          run={({ reason }) =>
            externalRequestsApi.cancel(request.id, reason ?? { text: '', language: 'en' }, etag)
          }
          isStale={isStale}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            changed('externalParticipation.done.cancelled');
          }}
          onStale={stale}
        />
      )}
      {(dialog === 'assignResponder' || dialog === 'assignReviewer') && (
        <AssignPersonDialog
          role={dialog === 'assignResponder' ? 'responder' : 'reviewer'}
          request={request}
          etag={etag}
          user={user}
          onClose={() => {
            setDialog(null);
          }}
          onDone={() => {
            changed(
              dialog === 'assignResponder'
                ? 'externalParticipation.done.responderChanged'
                : 'externalParticipation.done.reviewerChanged',
            );
          }}
          onStale={stale}
        />
      )}
    </div>
  );
}

/**
 * What both audiences read: the project as the entity may see it, the purpose, the source's safe label, the
 * instructions, due date and condition, status and responder. AHDA's view adds the entity, a link to the project's
 * workspace and its own handling — reviewer, issue, configuration, authors — which an external record does not carry.
 */
function RequestContext({
  request,
  external,
}: {
  request: ExternalUpdateRequestDetail;
  external: boolean;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const purpose = usePurpose();
  const entityName = useEntityNames(!external);
  const personName = usePersonNames([
    request.responsibleUserId,
    request.reviewerUserId ?? null,
    request.issuedByUserId ?? null,
    request.createdBy ?? null,
    request.updatedBy ?? null,
  ]);

  return (
    <>
      <section className="section" aria-labelledby="external-request-context">
        <h2 id="external-request-context">{t('externalParticipation.detail.contextTitle')}</h2>
        <dl className="details">
          <Detail term={t('externalParticipation.fields.project')}>
            <ProjectReference formalProjectId={request.formalProjectId} />
            {!external && (
              <span className="cell__aside">
                <Link to={projectExternalRequestsPath(request.projectId)}>
                  {t('externalParticipation.detail.openProject')}
                </Link>
              </span>
            )}
          </Detail>
          {!external && (
            <Detail term={t('externalParticipation.fields.entity')}>
              <span dir="auto">{entityName(request.externalEntityId)}</span>
            </Detail>
          )}
          <Detail term={t('externalParticipation.fields.purpose')}>{purpose(request)}</Detail>
          {request.targetLabel !== null && (
            <Detail term={t('externalParticipation.fields.source')}>
              <span dir="auto" lang={languageTag(request.targetLabel.language)}>
                {request.targetLabel.text}
              </span>
            </Detail>
          )}
          <Detail term={t('externalParticipation.fields.status')}>
            <RequestStatusBadge status={request.status} />
          </Detail>
          <Detail term={t('externalParticipation.fields.dueDate')}>
            <DueBadge dueDate={request.dueDate} condition={request.dueCondition} />
          </Detail>
          <Detail term={t('externalParticipation.fields.responder')}>
            {request.responsibleUserId === null
              ? t('externalParticipation.people.notNamed')
              : personName(request.responsibleUserId)}
          </Detail>
          {request.issuedAt !== null && (
            <Detail term={t('externalParticipation.fields.issuedAt')}>
              {formatDateTime(request.issuedAt)}
            </Detail>
          )}
          {request.closedAt !== null && (
            <Detail term={t('externalParticipation.fields.closedAt')}>
              {formatDateTime(request.closedAt)}
            </Detail>
          )}
          {request.cancelledAt !== null && (
            <Detail term={t('externalParticipation.fields.cancelledAt')}>
              {formatDateTime(request.cancelledAt)}
              {request.cancellationReason !== null && (
                <span className="cell__aside pre-line" dir="auto">
                  {request.cancellationReason.text}
                </span>
              )}
            </Detail>
          )}
        </dl>
        <h3>{t('externalParticipation.fields.instructions')}</h3>
        <p
          dir="auto"
          lang={languageTag(request.instructions.language)}
          className="risk-detail__text"
        >
          {request.instructions.text}
        </p>
      </section>

      {!external && (
        <section className="section" aria-labelledby="external-request-handling">
          <h2 id="external-request-handling">{t('externalParticipation.detail.handlingTitle')}</h2>
          <dl className="details" data-field="internalHandling">
            <Detail term={t('externalParticipation.fields.reviewer')}>
              <span data-field="reviewer">
                {request.reviewerUserId === null || request.reviewerUserId === undefined
                  ? t('externalParticipation.people.notNamed')
                  : personName(request.reviewerUserId)}
              </span>
            </Detail>
            <Detail term={t('externalParticipation.fields.issuedBy')}>
              <span data-field="issuedBy">
                {request.issuedByUserId === null || request.issuedByUserId === undefined
                  ? t('externalParticipation.detail.notIssued')
                  : personName(request.issuedByUserId)}
              </span>
            </Detail>
            <Detail term={t('externalParticipation.fields.configuration')}>
              <span dir="ltr" className="break-all" data-field="configurationVersion">
                {request.participationConfigurationVersionId ??
                  t('externalParticipation.detail.notIssued')}
              </span>
            </Detail>
            <Detail term={t('externalParticipation.fields.schema')}>
              <span dir="ltr">{request.contributionSchemaCode}</span>
              <span className="cell__aside">
                {t(`externalParticipation.applicationMode.${request.applicationMode}`)}
              </span>
            </Detail>
            <Detail term={t('externalParticipation.fields.createdBy')}>
              {personName(request.createdBy ?? null)}
              <span className="cell__aside">{formatDateTime(request.createdAt)}</span>
            </Detail>
            <Detail term={t('externalParticipation.fields.updatedBy')}>
              {personName(request.updatedBy ?? null)}
              <span className="cell__aside">{formatDateTime(request.updatedAt)}</span>
            </Detail>
          </dl>
        </section>
      )}
    </>
  );
}

/**
 * The entity's workspace: the answer form for the named responder while the request is open to them, the reason a
 * returned answer came back, and its own history. Nothing of AHDA's handling is read or shown.
 */
function ExternalWorkspace({
  request,
  user,
  revisions,
  loading,
  onSaved,
  onSubmitted,
  onStale,
}: {
  request: ExternalUpdateRequestDetail;
  user: SessionUser;
  revisions: ExternalContributionDetail[] | undefined;
  loading: boolean;
  onSaved: () => void;
  onSubmitted: () => void;
  onStale: () => void;
}): ReactElement {
  const { t } = useI18n();
  if (revisions === undefined) {
    return loading ? <LoadingState /> : <></>;
  }
  const current = currentRevision(revisions);
  const draft = current?.status === 'DRAFT' ? current : null;
  // The reason the latest return gave, shown above the draft that corrects it.
  const returnedReason =
    draft?.previousRevisionId == null
      ? null
      : (revisions.find((revision) => revision.status === 'RETURNED')?.reviewReason ?? null);
  const answering =
    isResponder(user, request) &&
    ((request.status === 'ISSUED' && revisions.length === 0) ||
      (request.status === 'IN_PROGRESS' && draft !== null));

  return (
    <>
      <section className="section" aria-labelledby="external-response-title">
        <h2 id="external-response-title">{t('externalParticipation.response.title')}</h2>
        {answering ? (
          <>
            {returnedReason !== null && (
              <div className="returned-reason" role="note">
                <p className="returned-reason__title">
                  {t('externalParticipation.response.returnedTitle')}
                </p>
                <p className="returned-reason__text" dir="auto">
                  {returnedReason.text}
                </p>
              </div>
            )}
            <p className="form__note">{t('externalParticipation.response.intro')}</p>
            <ResponseForm
              request={request}
              draftId={draft?.id ?? null}
              onSaved={onSaved}
              onSubmitted={onSubmitted}
              onStale={onStale}
            />
          </>
        ) : loading ? (
          // The request and its revisions are read again apart: until both have, the state is not yet known.
          <LoadingState />
        ) : (
          <p className="form__note">{responseStateText(request, current, user, t)}</p>
        )}
      </section>
      <section className="section" aria-labelledby="external-history-title">
        <h2 id="external-history-title">{t('externalParticipation.response.historyTitle')}</h2>
        {revisions.filter((revision) => revision.status !== 'DRAFT').length === 0 ? (
          <p className="form__note">{t('externalParticipation.response.noHistory')}</p>
        ) : (
          <RevisionHistory
            revisions={revisions.filter((revision) => revision.status !== 'DRAFT')}
            definitions={request.responseFields}
          />
        )}
      </section>
    </>
  );
}

/** Why the entity has no form in front of them, in a sentence. */
function responseStateText(
  request: ExternalUpdateRequestDetail,
  current: ExternalContributionDetail | null,
  user: SessionUser,
  t: (key: TranslationKey) => string,
): string {
  if (request.status === 'CANCELLED') {
    return t('externalParticipation.response.cancelled');
  }
  if (request.status === 'CLOSED') {
    return t('externalParticipation.response.closed');
  }
  // RESPONDED: the answer is with AHDA, under review or accepted and not yet applied (AHDA's to finish).
  if (request.status === 'RESPONDED' || (current !== null && awaitsReview(current.status))) {
    return t('externalParticipation.response.withAhda');
  }
  return request.responsibleUserId === user.id
    ? t('externalParticipation.response.notOpen')
    : t('externalParticipation.response.otherResponder');
}

/**
 * AHDA's case: every revision with its source version, reviewer and internal note, the one awaiting review linked to
 * SCR-165, and — for a typed source — the application lineage of the accepted answer, linked to SCR-166.
 */
function InternalCase({
  request,
  revisions,
  loading,
}: {
  request: ExternalUpdateRequestDetail;
  revisions: ExternalContributionDetail[] | undefined;
  loading: boolean;
}): ReactElement {
  const { t } = useI18n();
  const accepted = revisions?.find((revision) => isApplicationCase(request, revision)) ?? null;
  const attempts = useAttempts(accepted?.id ?? null);
  if (request.status === 'DRAFT') {
    return (
      <section className="section" aria-labelledby="external-revisions-title">
        <h2 id="external-revisions-title">{t('externalParticipation.detail.revisionsTitle')}</h2>
        <p className="form__note">{t('externalParticipation.detail.draftNotVisible')}</p>
      </section>
    );
  }
  const awaiting = revisions?.find((revision) => awaitsReview(revision.status)) ?? null;
  return (
    <>
      <section className="section" aria-labelledby="external-revisions-title">
        <h2 id="external-revisions-title">{t('externalParticipation.detail.revisionsTitle')}</h2>
        {awaiting !== null && (
          <p>
            <Link className="button button--primary" to={contributionReviewPath(awaiting.id)}>
              {t('externalParticipation.actions.reviewRevision', {
                revision: awaiting.revisionNo,
              })}
            </Link>
          </p>
        )}
        {revisions === undefined ? (
          loading ? (
            <LoadingState />
          ) : null
        ) : revisions.length === 0 ? (
          <p className="form__note">{t('externalParticipation.detail.noRevisions')}</p>
        ) : (
          <RevisionHistory revisions={revisions} definitions={request.responseFields} />
        )}
      </section>
      {accepted !== null && (
        <section className="section" aria-labelledby="external-application-title">
          <h2 id="external-application-title">
            {t('externalParticipation.detail.applicationTitle')}
          </h2>
          <p>
            <Link to={APPLICATION_MONITOR_PATH}>
              {t('externalParticipation.detail.openApplications')}
            </Link>
          </p>
          {attempts.data === undefined ? (
            attempts.loading ? (
              <LoadingState />
            ) : (
              <ErrorState
                message={participationProblemMessage(attempts.error, t)}
                onRetry={attempts.reload}
              />
            )
          ) : attempts.data.length === 0 ? (
            <p className="form__note">{t('externalParticipation.detail.noAttempts')}</p>
          ) : (
            <AttemptsTable
              attempts={attempts.data}
              caption={t('externalParticipation.detail.attemptsCaption')}
            />
          )}
        </section>
      )}
    </>
  );
}
