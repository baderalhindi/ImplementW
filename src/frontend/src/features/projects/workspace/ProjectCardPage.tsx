import { type ReactElement, useCallback } from 'react';
import { Link, useParams } from 'react-router';

import { Percent } from '@/features/dashboards/insights/figures.tsx';
import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { projectsApi } from '../api/projectsApi.ts';
import { type ProjectDetail } from '../api/types.ts';
import { Detail } from '../components/Detail.tsx';
import { GapValue, OnPlanBadge } from '../components/FollowUpBadges.tsx';
import { ProjectStatusBadge } from '../components/ProjectStatusBadge.tsx';
import { formatSar, languageTag } from '../presentation.ts';
import { projectProblemMessage } from '../problems.ts';
import { useProjectLookups } from '../useProjectLookups.ts';

/** One printable page of a project (the 5 Nov 2026 review §3 row 11), for whoever can read it; amounts only with FINANCIAL_VIEW_SENSITIVE. */
export function ProjectCardPage(): ReactElement {
  const { t } = useI18n();
  const { projectId = '' } = useParams();
  const project = useApiResource(
    useCallback((signal: AbortSignal) => projectsApi.get(projectId, signal), [projectId]),
  );
  if (project.data === undefined) {
    return (
      <>
        <PageHeader title={t('projects.card.title')} />
        {project.loading ? (
          <LoadingState />
        ) : (
          <ErrorState
            error={project.error}
            message={projectProblemMessage(project.error, t)}
            onRetry={project.reload}
          />
        )}
      </>
    );
  }
  return <CardView project={project.data.data} />;
}

function CardView({ project }: { project: ProjectDetail }): ReactElement {
  const { t } = useI18n();
  const lookups = useProjectLookups();
  const seesAmounts = useCan('FINANCIAL_VIEW_SENSITIVE');
  const managerName = usePersonNames([project.projectManagerUserId]);
  const notAvailable = t('projects.followUp.notAvailable');
  const followUp = project.followUp;
  const indicators = project.indicators;
  const named = (value: string | null | undefined) => value ?? notAvailable;

  return (
    <div className="print-card">
      <PageHeader
        title={project.title.text}
        titleLanguage={languageTag(project.title.language)}
        description={project.formalProjectId ?? project.legacyReference ?? undefined}
        actions={
          <button
            type="button"
            className="button print-hide"
            onClick={() => {
              window.print();
            }}
          >
            {t('dashboards.insights.card.print')}
          </button>
        }
      />
      <p className="print-hide">
        <Link to={`/projects/${project.id}`}>{t('projects.card.back')}</Link>
      </p>
      <dl className="details">
        <Detail term={t('projects.fields.status')}>
          <ProjectStatusBadge status={project.status} legacyIntakeDate={project.legacyIntakeDate} />
        </Detail>
        <Detail term={t('projects.fields.department')}>
          {lookups.departmentName(project.departmentId)}
        </Detail>
        {project.externalEntityId !== null && (
          <Detail term={t('dashboards.filter.externalEntityId')}>
            {lookups.entityName(project.externalEntityId)}
          </Detail>
        )}
        <Detail term={t('projects.fields.projectManager')}>
          {project.projectManagerUserId === null
            ? t('projects.projectManager.notNamed')
            : managerName(project.projectManagerUserId)}
        </Detail>
        <Detail term={t('dashboards.filter.classificationItemId')}>
          {lookups.itemLabel(project.classificationItemId)}
        </Detail>
        <Detail term={t('dashboards.filter.strategicPortfolioItemId')}>
          {named(lookups.itemLabel(project.strategicPortfolioItemId ?? null))}
        </Detail>
        <Detail term={t('dashboards.filter.strategicInitiativeItemId')}>
          {named(lookups.itemLabel(project.strategicInitiativeItemId ?? null))}
        </Detail>
        <Detail term={t('projects.fields.plannedPeriod')}>
          <span dir="ltr">
            {project.plannedStartDate ?? '—'} – {project.plannedEndDate ?? '—'}
          </span>
        </Detail>
        {seesAmounts && (
          <Detail term={t('projects.fields.registrationBudget')}>
            {project.registrationBudgetSar === null ? (
              notAvailable
            ) : (
              <span dir="ltr">
                {t('projects.money.sar', { amount: formatSar(project.registrationBudgetSar) })}
              </span>
            )}
          </Detail>
        )}
        <Detail term={t('dashboards.insights.planned')}>
          <Percent value={followUp?.declaredPlannedPercent} />
        </Detail>
        <Detail term={t('dashboards.insights.actual')}>
          <Percent value={followUp?.declaredActualPercent} />
        </Detail>
        <Detail term={t('projects.followUp.gap')}>
          <GapValue points={indicators?.gapPoints} />
        </Detail>
        <Detail term={t('projects.followUp.onPlan')}>
          <OnPlanBadge status={indicators?.onPlan} />
        </Detail>
        <Detail term={t('projects.followUp.daysRemaining')}>
          {indicators?.daysRemaining === null || indicators?.daysRemaining === undefined
            ? notAvailable
            : t('projects.followUp.daysValue', { days: indicators.daysRemaining })}
        </Detail>
        <Detail term={t('projects.followUp.obstacle')}>
          <span dir="auto">{named(followUp?.currentObstacle?.text)}</span>
        </Detail>
        <Detail term={t('projects.followUp.actionDue')}>
          <span dir="auto">{named(followUp?.nextAction?.text)}</span>
          {followUp?.nextActionOwner != null && (
            <span className="details__aside" dir="auto">
              {followUp.nextActionOwner}
            </span>
          )}
          {followUp?.nextActionDueDate != null && (
            <span className="details__aside" dir="ltr">
              {followUp.nextActionDueDate}
            </span>
          )}
        </Detail>
      </dl>
    </div>
  );
}
