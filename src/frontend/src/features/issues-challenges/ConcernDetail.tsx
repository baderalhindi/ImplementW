import { type ReactElement, useState } from 'react';
import { Link, useLocation } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { isForbidden } from '@/features/risks/problems.ts';
import { useRiskMatrix } from '@/features/risks/useRiskData.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import {
  canCloseConcern,
  canEditConcern,
  canEscalateConcern,
  canResolveEscalation,
  canReviewConcern,
  canStartConcern,
  canSubmitResolution,
  canWithdrawEscalation,
} from './access.ts';
import { type ConcernCommand } from './api/concernsApi.ts';
import { ConcernStatusBadge, DueFlag, SeverityBadge } from './components/ConcernBadges.tsx';
import { EscalationTable } from './components/EscalationTable.tsx';
import { isOverdue, isReturned, isReviewDue } from './concernRules.ts';
import { type ConcernDialog, ConcernDialogs } from './dialogs/ConcernDialogs.tsx';
import { concernProblemMessage } from './problems.ts';
import { useConcernLookups, useConcernRecord, useRoleNames } from './useConcernData.ts';

/** The notice a screen hands over when it navigates here (MOD-036 and MOD-038 land on the new concern). */
const ARRIVAL_NOTICES: Record<string, TranslationKey> = {
  concernRaised: 'issuesChallenges.done.raised',
};

interface ConcernDetailProps {
  project: ProjectDetail;
  user: SessionUser;
  concernId: string;
}

/**
 * SCR-084 Issue Detail and SCR-086 Challenge Detail: one screen for both types, as the API serves one aggregate
 * (ISS-GP-01). The header, the details — severity read-only, the server's, beside the priority people choose — the
 * description, the impact assessment, the resolution and its validation, and the escalations. The commands the
 * concern's state and the person allow are offered (access.ts: navigation, not protection).
 */
export function ConcernDetail({ project, user, concernId }: ConcernDetailProps): ReactElement {
  const { t, formatDateTime } = useI18n();
  const location = useLocation();
  const record = useConcernRecord(concernId);
  const matrixState = useRiskMatrix();
  const lookups = useConcernLookups();
  const roles = useRoleNames();
  const arrival = (location.state as { notice?: string } | null)?.notice;
  const arrivalKey = arrival === undefined ? undefined : ARRIVAL_NOTICES[arrival];
  const [notice, setNotice] = useState<Notice | null>(
    arrivalKey === undefined ? null : { tone: 'success', message: t(arrivalKey) },
  );
  const [dialog, setDialog] = useState<ConcernDialog | null>(null);
  const data = record.data;
  const personName = usePersonNames([
    data?.concern.data.assigneeUserId ?? null,
    data?.concern.data.raisedByUserId ?? null,
    ...(data?.escalations ?? []).flatMap((entry) => [
      entry.escalatedByUserId,
      entry.resolvedByUserId,
    ]),
  ]);

  const type = data?.concern.data.concernType;
  const back = (
    <p>
      <Link
        to={`/projects/${project.id}/issues-challenges${type === 'CHALLENGE' ? '/challenges' : ''}`}
      >
        {t(
          type === 'CHALLENGE'
            ? 'issuesChallenges.actions.backToChallenges'
            : 'issuesChallenges.actions.backToIssues',
        )}
      </Link>
    </p>
  );

  // A concern of another project is not this project's: it is not shown under it.
  if (data?.concern.data.projectId !== project.id) {
    return (
      <>
        {back}
        {record.loading ? (
          <LoadingState />
        ) : isForbidden(record.error) ? (
          <p className="state">{t('issuesChallenges.forbidden')}</p>
        ) : data !== undefined ? (
          <p className="state">{t('issuesChallenges.detail.notFound')}</p>
        ) : (
          <ErrorState message={concernProblemMessage(record.error, t)} onRetry={record.reload} />
        )}
      </>
    );
  }

  const concern = data.concern.data;
  const today = todayUtc();
  const matrix = matrixState.data?.kind === 'ready' ? matrixState.data.matrix : null;
  const changed = (message: TranslationKey, tone: Notice['tone'] = 'success') => {
    setNotice({ tone, message: t(message) });
    record.reload();
  };
  const command = (name: ConcernCommand) => ({
    label: t(`issuesChallenges.command.${name}.title`),
    dialog: { kind: 'command', command: name } as const,
  });

  // The commands the concern's state and the person allow, in the order of its lifecycle.
  const commands: { label: string; dialog: ConcernDialog; primary?: boolean }[] = [];
  if (canEditConcern(user, project, concern)) {
    commands.push(
      {
        label: t(
          concern.concernType === 'ISSUE'
            ? 'issuesChallenges.actions.editIssue'
            : 'issuesChallenges.actions.editChallenge',
        ),
        dialog: { kind: 'edit' },
      },
      {
        label: t(
          concern.overallImpactLevel === null
            ? 'issuesChallenges.actions.assess'
            : 'issuesChallenges.actions.reassess',
        ),
        dialog: { kind: 'assess' },
        primary: concern.overallImpactLevel === null,
      },
      {
        label: t(
          concern.assigneeUserId === null
            ? 'issuesChallenges.actions.assign'
            : 'issuesChallenges.actions.reassign',
        ),
        dialog: { kind: 'assign' },
      },
    );
  }
  if (canStartConcern(user, project, concern)) {
    commands.push({ ...command('start'), primary: true });
  }
  if (canSubmitResolution(user, project, concern)) {
    commands.push({
      label: t('issuesChallenges.actions.submitResolution'),
      dialog: { kind: 'submit' },
      primary: true,
    });
  }
  if (canReviewConcern(user, project, concern)) {
    commands.push(command('review'));
  }
  if (canEscalateConcern(user, project, concern)) {
    commands.push({ label: t('issuesChallenges.actions.escalate'), dialog: { kind: 'escalate' } });
  }
  if (canCloseConcern(user, project, concern)) {
    commands.push({ ...command('close'), primary: true });
  }

  // The version the severity was computed under, when it is the one in force; a later one changes no recorded severity.
  const pinnedVersion =
    matrix !== null && concern.severityConfigurationVersionId === matrix.versionId
      ? matrix.versionNo
      : null;

  return (
    <div className="progress risk-detail">
      {back}
      <PageNotice notice={notice} />
      <h2 dir="auto">{concern.title.text}</h2>
      {commands.length > 0 && (
        <div
          className="risk-detail__commands"
          role="group"
          aria-label={t('issuesChallenges.detail.commands')}
        >
          {commands.map((entry) => (
            <button
              key={entry.label}
              type="button"
              className={entry.primary === true ? 'button button--primary' : 'button'}
              onClick={() => {
                setDialog(entry.dialog);
              }}
            >
              {entry.label}
            </button>
          ))}
        </div>
      )}
      {concern.status === 'RESOLVED' && concern.openEscalation !== null && (
        <p className="form__note">{t('issuesChallenges.detail.closeWaitsForEscalation')}</p>
      )}
      <dl className="details">
        <Detail term={t('issuesChallenges.fields.type')}>
          {t(`issuesChallenges.type.${concern.concernType}`)}
        </Detail>
        <Detail term={t('issuesChallenges.table.status')}>
          <ConcernStatusBadge status={concern.status} />
          {isReturned(concern) && (
            <span className="cell__aside">{t('issuesChallenges.table.returned')}</span>
          )}
        </Detail>
        <Detail term={t('issuesChallenges.fields.severity')}>
          <span data-field="severity">
            <SeverityBadge concern={concern} itemLabel={lookups.itemLabel} />
          </span>
          {concern.overallImpactLevel !== null && (
            <span className="cell__aside">
              {t('issuesChallenges.table.overallImpact', { level: concern.overallImpactLevel })}
            </span>
          )}
          <span className="cell__aside">{t('issuesChallenges.detail.severityComputed')}</span>
        </Detail>
        <Detail term={t('issuesChallenges.fields.priority')}>
          <span data-field="priority">{lookups.itemLabel(concern.priorityItemId)}</span>
        </Detail>
        <Detail term={t('issuesChallenges.fields.category')}>
          {lookups.itemLabel(concern.categoryItemId)}
        </Detail>
        <Detail term={t('issuesChallenges.table.assignee')}>
          {concern.assigneeUserId === null
            ? t('issuesChallenges.table.unassigned')
            : personName(concern.assigneeUserId)}
        </Detail>
        <Detail term={t('issuesChallenges.detail.raised')}>
          {formatDateTime(concern.raisedAt)}
          <span className="cell__aside">{personName(concern.raisedByUserId)}</span>
        </Detail>
        <Detail term={t('issuesChallenges.fields.targetResolutionDate')}>
          {concern.targetResolutionDate === null ? (
            t('issuesChallenges.table.noTarget')
          ) : (
            <span dir="ltr">{concern.targetResolutionDate}</span>
          )}
          {isOverdue(concern, today) && <DueFlag label={t('issuesChallenges.table.overdue')} />}
        </Detail>
        <Detail term={t('issuesChallenges.detail.nextReview')}>
          <span dir="ltr">{concern.nextReviewDate}</span>
          {isReviewDue(concern, today) && <DueFlag label={t('issuesChallenges.table.reviewDue')} />}
          {concern.lastReviewedAt !== null && (
            <span className="cell__aside">
              {t('issuesChallenges.detail.lastReviewed', {
                date: formatDateTime(concern.lastReviewedAt),
              })}
            </span>
          )}
        </Detail>
        <Detail term={t('issuesChallenges.detail.revision')}>
          <span dir="ltr">{concern.revisionNo}</span>
        </Detail>
        {concern.originatingRiskId !== null && (
          <Detail term={t('issuesChallenges.detail.originatingRisk')}>
            <Link to={`/projects/${project.id}/risks/${concern.originatingRiskId}`}>
              {t('issuesChallenges.detail.openRisk')}
            </Link>
          </Detail>
        )}
        {concern.resolvedAt !== null && (
          <Detail term={t('issuesChallenges.detail.resolved')}>
            {formatDateTime(concern.resolvedAt)}
          </Detail>
        )}
        {concern.closedAt !== null && (
          <Detail term={t('issuesChallenges.detail.closed')}>
            {formatDateTime(concern.closedAt)}
          </Detail>
        )}
      </dl>

      <section className="section" aria-labelledby="concern-description">
        <h3 id="concern-description">{t('issuesChallenges.fields.description')}</h3>
        <p dir="auto" className="risk-detail__text">
          {concern.description.text}
        </p>
      </section>

      <section className="section" aria-labelledby="concern-impacts">
        <h3 id="concern-impacts">{t('issuesChallenges.detail.impactsTitle')}</h3>
        {concern.impacts.length === 0 ? (
          <p className="state">{t('issuesChallenges.detail.notAssessed')}</p>
        ) : (
          <>
            <p className="form__note">
              {pinnedVersion === null
                ? t('issuesChallenges.detail.pinnedOther')
                : t('issuesChallenges.detail.pinnedCurrent', { version: pinnedVersion })}
            </p>
            <TableContainer caption={t('issuesChallenges.detail.impactsCaption')}>
              <thead>
                <tr>
                  <th scope="col">{t('issuesChallenges.detail.dimension')}</th>
                  <th scope="col">{t('issuesChallenges.detail.level')}</th>
                </tr>
              </thead>
              <tbody>
                {concern.impacts.map((impact) => (
                  <tr key={impact.impactDimensionItemId}>
                    <td>{lookups.itemLabel(impact.impactDimensionItemId)}</td>
                    <td>
                      <span dir="ltr">{impact.impactLevel}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </TableContainer>
          </>
        )}
      </section>

      {concern.resolution !== null && (
        <section className="section" aria-labelledby="concern-resolution">
          <h3 id="concern-resolution">{t('issuesChallenges.fields.resolution')}</h3>
          <p className="form__note">
            {concern.status === 'PENDING_VALIDATION'
              ? t('issuesChallenges.detail.withValidation', { revision: concern.revisionNo })
              : isReturned(concern)
                ? t('issuesChallenges.detail.returned', { revision: concern.revisionNo })
                : t('issuesChallenges.detail.validated')}
          </p>
          <p dir="auto" className="risk-detail__text">
            {concern.resolution.text}
          </p>
        </section>
      )}

      <section className="section" aria-labelledby="concern-escalations">
        <h3 id="concern-escalations">{t('issuesChallenges.detail.escalationsTitle')}</h3>
        {data.escalations.length === 0 ? (
          <EmptyState title={t('issuesChallenges.detail.noEscalations')} />
        ) : (
          <EscalationTable
            caption={t('issuesChallenges.detail.escalationsCaption')}
            entries={data.escalations.map((escalation) => ({ escalation }))}
            roles={roles}
            itemLabel={lookups.itemLabel}
            personName={personName}
            actions={(escalation) => (
              <span className="figure-group">
                {canResolveEscalation(
                  user,
                  escalation,
                  roles.code(escalation.escalatedToRoleId),
                ) && (
                  <button
                    type="button"
                    className="button button--link"
                    aria-label={t('issuesChallenges.resolveEscalation.named', {
                      number: escalation.escalationNo,
                    })}
                    onClick={() => {
                      setDialog({ kind: 'resolveEscalation', escalationId: escalation.id });
                    }}
                  >
                    {t('issuesChallenges.resolveEscalation.confirm')}
                  </button>
                )}
                {canWithdrawEscalation(user, escalation) && (
                  <button
                    type="button"
                    className="button button--link"
                    aria-label={t('issuesChallenges.withdrawEscalation.named', {
                      number: escalation.escalationNo,
                    })}
                    onClick={() => {
                      setDialog({ kind: 'withdrawEscalation', escalationId: escalation.id });
                    }}
                  >
                    {t('issuesChallenges.withdrawEscalation.confirm')}
                  </button>
                )}
              </span>
            )}
          />
        )}
      </section>

      <ConcernDialogs
        dialog={dialog}
        onDialogChange={setDialog}
        record={data}
        project={project}
        user={user}
        lookups={lookups}
        matrixState={matrixState}
        onChanged={changed}
      />
    </div>
  );
}
