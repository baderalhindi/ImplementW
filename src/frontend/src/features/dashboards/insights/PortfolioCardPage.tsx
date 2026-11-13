import { type ReactElement, useCallback } from 'react';
import { Link, useParams } from 'react-router';

import { GapValue, OnPlanBadge } from '@/features/projects/components/FollowUpBadges.tsx';
import { Detail } from '@/features/projects/components/Detail.tsx';
import { ProjectStatusBadge } from '@/features/projects/components/ProjectStatusBadge.tsx';
import { formatSar, languageTag } from '@/features/projects/presentation.ts';
import { useProjectLookups } from '@/features/projects/useProjectLookups.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader, TableContainer } from '@/shared/ui/Layout.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { ErrorState, LoadingState } from '@/shared/ui/States.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';

import { dashboardsApi } from '../api/dashboardsApi.ts';
import { type PortfolioCard, type PortfolioCardDimension } from '../api/insightTypes.ts';
import { DASHBOARDS_PATH } from '../paths.ts';
import { dashboardProblemMessage } from '../problems.ts';

import { Coverage, Percent } from './figures.tsx';

const DIMENSIONS: Record<string, PortfolioCardDimension> = {
  portfolio: 'PORTFOLIO',
  initiative: 'INITIATIVE',
  sector: 'SECTOR',
};

/**
 * A printable portfolio, initiative or sector card (the 5 Nov 2026 review §3 row 11): the group's summary, then every project. The API scopes
 * it; printing hides the shell (styles.css, @media print).
 */
export function PortfolioCardPage(): ReactElement {
  const { t } = useI18n();
  const { dimension = '', itemId = '' } = useParams();
  const wire = DIMENSIONS[dimension];
  const card = useApiResource(
    useCallback(
      async (signal: AbortSignal) =>
        wire === undefined ? null : dashboardsApi.card(wire, itemId, signal),
      [wire, itemId],
    ),
  );

  if (card.data === undefined || card.data === null) {
    return (
      <>
        <PageHeader title={t('dashboards.insights.card.title')} />
        {card.loading ? (
          <LoadingState />
        ) : card.data === null ? (
          <p className="state">{t('dashboards.insights.card.notFound')}</p>
        ) : (
          <ErrorState
            error={card.error}
            message={dashboardProblemMessage(card.error, t)}
            onRetry={card.reload}
          />
        )}
      </>
    );
  }
  return <CardView card={card.data} />;
}

function CardView({ card }: { card: PortfolioCard }): ReactElement {
  const { t } = useI18n();
  const lookups = useProjectLookups();
  const selection = useRowSelection();
  const { summary, projects } = card;
  const name = lookups.itemLabel(card.itemId) ?? '';
  const showValue = summary.valueSar !== null;
  const notAvailable = t('dashboards.insights.notAvailable');

  return (
    <div className="print-card">
      <PageHeader
        title={t(`dashboards.insights.card.${card.dimension}`, { name })}
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
        <Link to={DASHBOARDS_PATH}>{t('dashboards.insights.card.back')}</Link>
      </p>
      <section aria-label={t('dashboards.insights.card.summary')} data-section="cardSummary">
        <dl className="details">
          <Detail term={t('dashboards.insights.projects')}>
            <span dir="ltr">{summary.projects}</span>
          </Detail>
          {showValue && (
            <Detail term={t('dashboards.insights.value')}>
              {summary.valueEntered === 0 ? (
                notAvailable
              ) : (
                <span dir="ltr">
                  {t('projects.money.sar', { amount: formatSar(summary.valueSar ?? '0.00') })}
                </span>
              )}
              <Coverage kind="value" count={summary.valueEntered} of={summary.projects} />
            </Detail>
          )}
          <Detail term={t('dashboards.insights.averagePlanned')}>
            <Percent value={summary.averagePlannedPercent} />
            <span className="insights__coverage">{t('dashboards.insights.simpleAverage')}</span>
          </Detail>
          <Detail term={t('dashboards.insights.averageActual')}>
            <Percent value={summary.averageActualPercent} />
            <Coverage kind="progress" count={summary.measured} of={summary.projects} />
          </Detail>
          <Detail term={t('dashboards.insights.gap')}>
            <GapValue points={summary.gapPoints} /> <OnPlanBadge status={summary.onPlan} />
          </Detail>
          <Detail term={t('dashboards.insights.needingFollowUp')}>
            <span dir="ltr">{summary.needingFollowUp}</span>
          </Detail>
        </dl>
      </section>
      {projects.length === 0 ? (
        <p className="state">{t('dashboards.noData')}</p>
      ) : (
        <TableContainer
          caption={t('dashboards.insights.card.projects')}
          selection={{ selection, exportName: 'portfolio-card-projects' }}
        >
          <thead>
            <tr>
              <SelectAllHeader
                ids={projects.map((project) => project.projectId)}
                selection={selection}
              />
              <th scope="col">{t('projects.fields.formalProjectId')}</th>
              <th scope="col">{t('projects.fields.title')}</th>
              <th scope="col">{t('projects.fields.status')}</th>
              {showValue && <th scope="col">{t('dashboards.insights.value')}</th>}
              <th scope="col">{t('projects.fields.plannedPeriod')}</th>
              <th scope="col">{t('dashboards.insights.planned')}</th>
              <th scope="col">{t('dashboards.insights.actual')}</th>
              <th scope="col">{t('dashboards.insights.gap')}</th>
              <th scope="col">{t('dashboards.insights.label')}</th>
              <th scope="col">{t('projects.followUpList.obstacle')}</th>
              <th scope="col">{t('projects.followUpList.nextAction')}</th>
            </tr>
          </thead>
          <tbody>
            {projects.map((project) => (
              <tr key={project.projectId}>
                <SelectRowCell
                  id={project.projectId}
                  name={project.title.text}
                  selection={selection}
                />
                <td dir="ltr" className="cell--ltr">
                  {project.formalProjectId ?? project.legacyReference ?? '—'}
                </td>
                <td>
                  <Link
                    to={`/projects/${project.projectId}`}
                    lang={languageTag(project.title.language)}
                    dir="auto"
                  >
                    {project.title.text}
                  </Link>
                </td>
                <td>
                  <ProjectStatusBadge status={project.status} legacyIntakeDate={null} />
                </td>
                {showValue && (
                  <td>
                    {project.valueSar === null ? (
                      notAvailable
                    ) : (
                      <span dir="ltr">{formatSar(project.valueSar)}</span>
                    )}
                  </td>
                )}
                <td dir="ltr" className="cell--ltr">
                  {project.plannedStartDate ?? '—'} – {project.plannedEndDate ?? '—'}
                </td>
                <td>
                  <Percent value={project.plannedPercent} />
                </td>
                <td>
                  <Percent value={project.actualPercent} />
                </td>
                <td>
                  <GapValue points={project.gapPoints} />
                </td>
                <td>
                  <OnPlanBadge status={project.onPlan} />
                </td>
                <td dir="auto">{project.currentObstacle?.text ?? notAvailable}</td>
                <td>
                  <span dir="auto">{project.nextAction?.text ?? notAvailable}</span>
                  {project.nextActionOwner !== null && (
                    <span className="cell__aside" dir="auto">
                      {project.nextActionOwner}
                    </span>
                  )}
                  {project.nextActionDueDate !== null && (
                    <span className="cell__aside" dir="ltr">
                      {project.nextActionDueDate}
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </TableContainer>
      )}
    </div>
  );
}
