import { type ReactElement, useState } from 'react';
import { Link, useLocation } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { type Notice, PageNotice } from '@/shared/ui/PageNotice.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import {
  canAcceptRisk,
  canAssessRisk,
  canManageRisk,
  canMonitorRisk,
  canReopenRisk,
  canRevokeAcceptance,
  canStartTreatment,
} from './access.ts';
import { type RiskCommand } from './api/risksApi.ts';
import {
  type RiskDetail as RiskDetailRecord,
  type RiskTreatmentActionDetail,
} from './api/types.ts';
import {
  AcceptanceStatusBadge,
  ActionStatusBadge,
  RatingBadge,
  RiskStatusBadge,
  UnratedBadge,
} from './components/RiskBadges.tsx';
import { MatrixUnavailable, RiskHeatMap } from './components/RiskHeatMap.tsx';
import { type RiskDialog } from './dialogs/riskDialog.ts';
import { RiskDialogs } from './dialogs/RiskDialogs.tsx';
import { isForbidden, riskProblemMessage } from './problems.ts';
import { isLiveAction, isReviewDue } from './riskRules.ts';
import { useRiskLookups, useRiskMatrix, useRiskRecord } from './useRiskData.ts';

/** The notice a screen hands over when it navigates here (MOD-030 lands on the new risk). */
const ARRIVAL_NOTICES: Record<string, TranslationKey> = {
  riskCreated: 'risks.done.created',
};

interface RiskDetailProps {
  project: ProjectDetail;
  user: SessionUser;
  riskId: string;
}

/**
 * SCR-082 Risk Detail: the risk's header and current exposure on the RISK_MATRIX version in force, its description,
 * assessment history, treatment plan, acceptances and closure. The commands its state and the person allow are offered
 * (access.ts: navigation, not protection); a closed risk is read-only until it is reopened.
 */
export function RiskDetail({ project, user, riskId }: RiskDetailProps): ReactElement {
  const { t, formatDateTime } = useI18n();
  const location = useLocation();
  const record = useRiskRecord(riskId);
  const matrixState = useRiskMatrix();
  const lookups = useRiskLookups();
  const arrival = (location.state as { notice?: string } | null)?.notice;
  const arrivalKey = arrival === undefined ? undefined : ARRIVAL_NOTICES[arrival];
  const [notice, setNotice] = useState<Notice | null>(
    arrivalKey === undefined ? null : { tone: 'success', message: t(arrivalKey) },
  );
  const [dialog, setDialog] = useState<RiskDialog | null>(null);
  const data = record.data;
  const personName = usePersonNames([
    data?.risk.data.ownerUserId ?? null,
    data?.risk.data.closedByUserId ?? null,
    ...(data?.assessments ?? []).map((assessment) => assessment.assessedByUserId),
    ...(data?.acceptances ?? []).map((acceptance) => acceptance.acceptedByUserId),
    ...(data?.actions ?? []).map((action) => action.ownerUserId),
  ]);

  const back = (
    <p>
      <Link to={`/projects/${project.id}/risks`}>{t('risks.actions.backToRegister')}</Link>
    </p>
  );

  // A risk of another project is not this project's: it is not shown under it.
  if (data?.risk.data.projectId !== project.id) {
    return (
      <>
        {back}
        {record.loading ? (
          <LoadingState />
        ) : isForbidden(record.error) ? (
          <p className="state">{t('risks.forbidden')}</p>
        ) : data !== undefined ? (
          <p className="state">{t('risks.detail.notFound')}</p>
        ) : (
          <ErrorState message={riskProblemMessage(record.error, t)} onRetry={record.reload} />
        )}
      </>
    );
  }

  const risk = data.risk.data;
  const matrix = matrixState.data?.kind === 'ready' ? matrixState.data.matrix : null;
  const assessment = risk.currentAssessment;
  const liveActions = data.actions.filter(isLiveAction);
  const manager = canManageRisk(user, project, risk);
  const command = (name: RiskCommand) => ({
    label: t(`risks.command.${name}.title`),
    dialog: { kind: 'command', command: name } as const,
  });
  const changed = (message: TranslationKey, tone: Notice['tone'] = 'success') => {
    setNotice({ tone, message: t(message) });
    record.reload();
  };

  // The commands the risk's state and the person allow, in the order of its lifecycle.
  const commands: { label: string; dialog: RiskDialog; primary?: boolean }[] = [];
  if (manager) {
    commands.push({ label: t('risks.actions.edit'), dialog: { kind: 'edit' } });
  }
  if (canAssessRisk(user, project, risk)) {
    commands.push({
      label: t(assessment === null ? 'risks.actions.assess' : 'risks.actions.reassess'),
      dialog: { kind: 'assess' },
      primary: true,
    });
  }
  if (manager) {
    commands.push(
      { label: t('risks.actions.assignOwner'), dialog: { kind: 'owner' } },
      { label: t('risks.actions.addAction'), dialog: { kind: 'action', actionId: null } },
    );
  }
  if (canStartTreatment(user, project, risk, liveActions.length > 0)) {
    commands.push(command('start-treatment'));
  }
  if (canMonitorRisk(user, project, risk)) {
    commands.push(command('monitor'));
  }
  if (canAcceptRisk(user, project, risk)) {
    commands.push({ label: t('risks.actions.accept'), dialog: { kind: 'accept' } });
  }
  if (canRevokeAcceptance(user, project, risk)) {
    commands.push(command('revoke-acceptance'));
  }
  if (manager) {
    commands.push({ label: t('risks.actions.close'), dialog: { kind: 'close' } });
  }
  if (canReopenRisk(user, project, risk)) {
    commands.push(command('reopen'));
  }

  return (
    <div className="progress risk-detail">
      {back}
      <PageNotice notice={notice} />
      <h2 dir="auto">{risk.title.text}</h2>
      {commands.length > 0 && (
        <div className="risk-detail__commands" role="group" aria-label={t('risks.detail.commands')}>
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
      <dl className="details">
        <Detail term={t('risks.table.status')}>
          <RiskStatusBadge status={risk.status} />
        </Detail>
        <Detail term={t('risks.table.rating')}>
          {assessment === null ? (
            <UnratedBadge />
          ) : (
            <RatingBadge
              code={assessment.rating.code}
              label={assessment.rating.label}
              matrix={matrix}
            />
          )}
        </Detail>
        <Detail term={t('risks.fields.category')}>
          {lookups.itemLabel(risk.riskCategoryItemId)}
        </Detail>
        <Detail term={t('risks.table.owner')}>
          {risk.ownerUserId === null ? t('risks.table.noOwner') : personName(risk.ownerUserId)}
        </Detail>
        <Detail term={t('risks.fields.identifiedDate')}>
          <span dir="ltr">{risk.identifiedDate}</span>
        </Detail>
        <Detail term={t('risks.fields.nextReviewDate')}>
          {risk.nextReviewDate === null ? (
            t('risks.table.noReview')
          ) : (
            <>
              <span dir="ltr">{risk.nextReviewDate}</span>
              {isReviewDue(risk, todayUtc()) && (
                <span className="cell__aside">{t('risks.table.reviewDue')}</span>
              )}
            </>
          )}
        </Detail>
        {risk.acceptedUntil !== null && (
          <Detail term={t('risks.detail.acceptedUntil')}>
            <span dir="ltr">{risk.acceptedUntil}</span>
          </Detail>
        )}
        {risk.materialisedAt !== null && (
          <Detail term={t('risks.detail.materialised')}>
            {t('risks.detail.materialisedIssues', { count: risk.materialisedIssueIds.length })}
          </Detail>
        )}
        {risk.reopenedCount > 0 && (
          <Detail term={t('risks.detail.reopened')}>
            {t('risks.detail.reopenedCount', { count: risk.reopenedCount })}
          </Detail>
        )}
      </dl>

      {risk.status === 'CLOSED' && risk.closureRationale !== null && (
        <section className="section" aria-labelledby="risk-closure">
          <h3 id="risk-closure">{t('risks.detail.closureTitle')}</h3>
          <p className="form__note">
            {t('risks.detail.closedBy', {
              name: personName(risk.closedByUserId),
              date: risk.closedAt === null ? '' : formatDateTime(risk.closedAt),
            })}
          </p>
          <p dir="auto">{risk.closureRationale.text}</p>
        </section>
      )}

      <section className="section" aria-labelledby="risk-description">
        <h3 id="risk-description">{t('risks.fields.description')}</h3>
        <p dir="auto" className="risk-detail__text">
          {risk.description.text}
        </p>
      </section>

      <section className="section" aria-labelledby="risk-exposure">
        <h3 id="risk-exposure">{t('risks.detail.exposureTitle')}</h3>
        <ExposureSection risk={risk} matrixState={matrixState} />
      </section>

      <section className="section" aria-labelledby="risk-assessments">
        <h3 id="risk-assessments">{t('risks.detail.assessmentsTitle')}</h3>
        {data.assessments.length === 0 ? (
          <EmptyState title={t('risks.detail.noAssessments')} />
        ) : (
          <TableContainer caption={t('risks.detail.assessmentsCaption')}>
            <thead>
              <tr>
                <th scope="col">{t('risks.detail.version')}</th>
                <th scope="col">{t('risks.detail.assessed')}</th>
                <th scope="col">{t('risks.table.rating')}</th>
                <th scope="col">{t('risks.detail.levels')}</th>
                <th scope="col">{t('risks.detail.rationale')}</th>
              </tr>
            </thead>
            <tbody>
              {data.assessments.map((entry) => (
                <tr key={entry.id} data-assessment={entry.versionNo}>
                  <td>
                    <span dir="ltr">{entry.versionNo}</span>
                  </td>
                  <td>
                    {formatDateTime(entry.assessedAt)}
                    <span className="cell__aside">{personName(entry.assessedByUserId)}</span>
                  </td>
                  <td>
                    <RatingBadge
                      code={entry.rating.code}
                      label={entry.rating.label}
                      matrix={matrix}
                    />
                  </td>
                  <td>
                    {t('risks.table.levels', {
                      probability: entry.probabilityLevel,
                      impact: entry.overallImpactLevel,
                    })}
                    <ul className="cell__aside risk-detail__impacts">
                      {entry.impacts.map((impact) => (
                        <li key={impact.impactDimensionItemId}>
                          {t('risks.detail.impact', {
                            dimension: lookups.itemLabel(impact.impactDimensionItemId),
                            level: impact.impactLevel,
                          })}
                        </li>
                      ))}
                    </ul>
                  </td>
                  <td dir="auto">{entry.rationale?.text ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </TableContainer>
        )}
      </section>

      <section className="section" aria-labelledby="risk-actions">
        <h3 id="risk-actions">{t('risks.detail.actionsTitle')}</h3>
        {data.actions.length === 0 ? (
          <EmptyState title={t('risks.detail.noActions')} />
        ) : (
          <ActionTable
            actions={data.actions}
            manager={manager}
            personName={personName}
            onOpen={setDialog}
          />
        )}
      </section>

      {data.acceptances.length > 0 && (
        <section className="section" aria-labelledby="risk-acceptances">
          <h3 id="risk-acceptances">{t('risks.detail.acceptancesTitle')}</h3>
          <TableContainer caption={t('risks.detail.acceptancesCaption')}>
            <thead>
              <tr>
                <th scope="col">{t('risks.detail.accepted')}</th>
                <th scope="col">{t('risks.fields.expiresOn')}</th>
                <th scope="col">{t('risks.table.status')}</th>
                <th scope="col">{t('risks.detail.rationale')}</th>
              </tr>
            </thead>
            <tbody>
              {data.acceptances.map((acceptance) => (
                <tr key={acceptance.id}>
                  <td>
                    {formatDateTime(acceptance.acceptedAt)}
                    <span className="cell__aside">{personName(acceptance.acceptedByUserId)}</span>
                  </td>
                  <td>
                    <span dir="ltr">{acceptance.expiresOn}</span>
                  </td>
                  <td>
                    <AcceptanceStatusBadge status={acceptance.status} />
                  </td>
                  <td dir="auto">{acceptance.rationale.text}</td>
                </tr>
              ))}
            </tbody>
          </TableContainer>
        </section>
      )}

      <RiskDialogs
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

/**
 * Where the risk sits on the matrix in force: its probability and overall impact marked. The rating is the one its
 * assessment pinned (D-5); when that was another version, the map is today's and says so.
 */
function ExposureSection({
  risk,
  matrixState,
}: {
  risk: RiskDetailRecord;
  matrixState: ReturnType<typeof useRiskMatrix>;
}): ReactElement {
  const { t } = useI18n();
  const assessment = risk.currentAssessment;
  if (assessment === null) {
    return <p className="state">{t('risks.detail.notAssessed')}</p>;
  }
  if (matrixState.data?.kind !== 'ready') {
    return <MatrixUnavailable state={matrixState} />;
  }
  const { matrix } = matrixState.data;
  return (
    <>
      {assessment.matrixConfigurationVersionId !== matrix.versionId && (
        <p className="form__note">{t('risks.detail.otherVersion')}</p>
      )}
      <RiskHeatMap
        matrix={matrix}
        caption={t('risks.detail.matrixCaption')}
        marked={{
          probabilityLevel: assessment.probabilityLevel,
          impactLevel: assessment.overallImpactLevel,
        }}
        markedLabel={t('risks.detail.marked')}
      />
    </>
  );
}

/** The treatment plan, oldest first; a live action is edited, started, completed or cancelled by the manager. */
function ActionTable({
  actions,
  manager,
  personName,
  onOpen,
}: {
  actions: RiskTreatmentActionDetail[];
  manager: boolean;
  personName: (id: string | null) => string;
  onOpen: (dialog: RiskDialog) => void;
}): ReactElement {
  const { t } = useI18n();
  return (
    <TableContainer caption={t('risks.detail.actionsCaption')}>
      <thead>
        <tr>
          <th scope="col">{t('risks.fields.actionTitle')}</th>
          <th scope="col">{t('risks.fields.actionType')}</th>
          <th scope="col">{t('risks.table.owner')}</th>
          <th scope="col">{t('risks.fields.dueDate')}</th>
          <th scope="col">{t('risks.table.status')}</th>
          {manager && <th scope="col">{t('risks.detail.actionCommands')}</th>}
        </tr>
      </thead>
      <tbody>
        {actions.map((action) => (
          <tr key={action.id} className={isLiveAction(action) ? undefined : 'row--muted'}>
            <td dir="auto">
              {action.title.text}
              {action.description !== null && (
                <span className="cell__aside">{action.description.text}</span>
              )}
            </td>
            <td>{t(`risks.actionType.${action.actionType}`)}</td>
            <td>
              {action.ownerUserId === null
                ? t('risks.table.noOwner')
                : personName(action.ownerUserId)}
            </td>
            <td>{action.dueDate === null ? '—' : <span dir="ltr">{action.dueDate}</span>}</td>
            <td>
              <ActionStatusBadge status={action.status} />
            </td>
            {manager && (
              <td>
                {isLiveAction(action) && (
                  <span className="figure-group">
                    <button
                      type="button"
                      className="button button--link"
                      aria-label={t('risks.actions.editAction', { action: action.title.text })}
                      onClick={() => {
                        onOpen({ kind: 'action', actionId: action.id });
                      }}
                    >
                      {t('risks.actions.edit')}
                    </button>
                    {(action.status === 'PLANNED'
                      ? (['start', 'cancel'] as const)
                      : (['complete', 'cancel'] as const)
                    ).map((command) => (
                      <button
                        key={command}
                        type="button"
                        className="button button--link"
                        aria-label={t(`risks.actionCommand.${command}.named`, {
                          action: action.title.text,
                        })}
                        onClick={() => {
                          onOpen({ kind: 'actionCommand', actionId: action.id, command });
                        }}
                      >
                        {t(`risks.actionCommand.${command}.confirm`)}
                      </button>
                    ))}
                  </span>
                )}
              </td>
            )}
          </tr>
        ))}
      </tbody>
    </TableContainer>
  );
}
