import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { approvalTasksApi } from '@/features/approvals/api/approvalsApi.ts';
import { SubjectSummary } from '@/features/approvals/components/SubjectSummary.tsx';
import { isOverdue } from '@/features/approvals/presentation.ts';
import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { GapValue } from '@/features/projects/components/FollowUpBadges.tsx';
import { languageTag } from '@/features/projects/presentation.ts';
import { RatingBadge } from '@/features/risks/components/RiskBadges.tsx';
import { byExposure, isCritical, isOpen } from '@/features/risks/riskRules.ts';
import { matrixOf, useRiskMatrix, useRiskPortfolio } from '@/features/risks/useRiskData.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { SarAmount } from '@/shared/format/SarAmount.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { NoteList } from '@/shared/ui/NoteList.tsx';
import { EmptyState, LoadingState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type PortfolioFilters } from '../api/dashboardsApi.ts';
import { type PortfolioHeadline, type PortfolioOnPlan } from '../api/insightTypes.ts';
import { registerPath } from '../drillLinks.ts';
import { Card } from './Card.tsx';
import { ON_PLAN_TONES } from './onPlanTones.ts';
import { Coverage, Percent } from './figures.tsx';

/** Review round 8, point 14: how many top risks and decisions the Executive's dashboard lists before its charts. */
const ATTENTION_SIZE = 5;

const RISKS_PATH = '/risks';
const CRITICAL_RISKS_PATH = '/risks/critical';
const INBOX_PATH = '/approvals/inbox';

function readInbox(signal: AbortSignal) {
  return approvalTasksApi.listInbox(1, ATTENTION_SIZE, signal);
}

function readNoInbox(): Promise<null> {
  return Promise.resolve(null);
}

/**
 * What the Executive's headline and attention sections read, once for both: the risks the Critical Risks page lists, read the same way so the
 * card's count and that page agree (point 01), and the first decisions in the approvals inbox — for someone who decides.
 */
function useExecutiveData() {
  const decides = useCan('APPROVAL_DECIDE');
  const portfolio = useRiskPortfolio();
  const matrix = matrixOf(useRiskMatrix());
  const inbox = useApiResource(decides ? readInbox : readNoInbox);
  const open = (portfolio.data?.entries ?? []).filter((entry) => isOpen(entry.risk));
  return {
    decides,
    inbox,
    risks: {
      loading: portfolio.data === undefined && portfolio.loading,
      readable: portfolio.data !== undefined,
      matrix,
      critical:
        matrix === null ? null : open.filter((entry) => isCritical(entry.risk, matrix)).length,
      top: [...open].sort(byExposure(matrix)).slice(0, ATTENTION_SIZE),
    },
  };
}

type ExecutiveData = ReturnType<typeof useExecutiveData>;

/**
 * The Executive's headline (review round 8, point 14): five figures in compact numbers (point 11) — the total value, the average actual against
 * the average planned, the stalled projects, the critical risks and the decisions waiting — each opening its list (point 01).
 */
function ExecutiveHeadline({
  headline,
  onPlan,
  filters,
  data,
}: {
  headline: PortfolioHeadline;
  onPlan: PortfolioOnPlan | null;
  filters: PortfolioFilters;
  data: ExecutiveData;
}): ReactElement {
  const { t } = useI18n();
  const { decides, risks, inbox } = data;
  const total = headline.registeredProjects;
  const projectsHint = (count: number) => t('dashboards.drill.projects', { count });
  const stalled = onPlan?.stalled ?? null;
  const waiting = inbox.data?.totalCount ?? null;

  return (
    <ul
      className="home__tiles insights__headline executive__headline"
      data-section="executive-headline"
    >
      {headline.totalValueSar !== null && (
        <Card
          label={t('dashboards.insights.totalValue')}
          to={registerPath(filters, { sort: 'value:desc' })}
          hint={projectsHint(total)}
        >
          {headline.valueEntered === 0 ? (
            <Percent value={null} />
          ) : (
            <SarAmount className="home__tile-value" amount={headline.totalValueSar} />
          )}
          <Coverage kind="value" count={headline.valueEntered} of={total} />
        </Card>
      )}
      <Card
        label={t('dashboards.executive.actualVsPlanned')}
        tone={headline.onPlan === null ? undefined : ON_PLAN_TONES[headline.onPlan]}
        to={registerPath(filters, { measured: 'true' })}
        hint={projectsHint(headline.measured)}
      >
        <span className="home__tile-value executive__pair">
          <Percent value={headline.averageActualPercent} />
          <span className="executive__versus" aria-hidden="true">
            /
          </span>
          <Percent value={headline.averagePlannedPercent} />
        </span>
        <NoteList className="insights__notes">
          <span className="insights__coverage">{t('dashboards.executive.actualThenPlanned')}</span>
          <span className="insights__coverage">
            <GapValue points={headline.gapPoints} />
          </span>
          <Coverage kind="progress" count={headline.measured} of={total} />
        </NoteList>
      </Card>
      <Card
        label={t('dashboards.executive.stalled')}
        tone={stalled !== null && stalled > 0 ? 'negative' : undefined}
        to={registerPath(filters, { onPlan: 'STALLED' })}
        hint={stalled === null ? undefined : projectsHint(stalled)}
      >
        <span className="home__tile-value" dir="ltr">
          {stalled ?? '—'}
        </span>
        <Coverage kind="progress" count={headline.measured} of={total} />
      </Card>
      <Card
        label={t('dashboards.executive.criticalRisks')}
        tone={risks.critical !== null && risks.critical > 0 ? 'negative' : undefined}
        to={CRITICAL_RISKS_PATH}
      >
        <span className="home__tile-value" dir="ltr">
          {risks.loading ? '…' : (risks.critical ?? '—')}
        </span>
        <span className="insights__coverage">
          {risks.readable && risks.critical === null
            ? t('dashboards.executive.noMatrix')
            : t('dashboards.executive.wholePortfolio')}
        </span>
      </Card>
      {decides && (
        <Card label={t('dashboards.executive.waitingDecision')} to={INBOX_PATH}>
          <span className="home__tile-value" dir="ltr">
            {waiting ?? (inbox.loading ? '…' : '—')}
          </span>
          <span className="insights__coverage">
            {t('dashboards.executive.waitingDecisionNote')}
          </span>
        </Card>
      )}
    </ul>
  );
}

/**
 * Before any chart or table (review round 8, point 14): the top risks across the portfolio, most exposed first, and what waits for the
 * Executive's decision, each opening its record and its list.
 */
function ExecutiveAttention({ data }: { data: ExecutiveData }): ReactElement {
  const { t, formatDateTime, language } = useI18n();
  const { decides, risks, inbox } = data;

  return (
    <div className="executive__attention" data-section="executive-attention">
      <section className="insights__section" aria-labelledby="executive-risks">
        <div className="section__header">
          <h2 id="executive-risks">{t('dashboards.executive.topRisks')}</h2>
          <Link to={CRITICAL_RISKS_PATH}>{t('dashboards.executive.allCritical')}</Link>
        </div>
        {risks.loading ? (
          <LoadingState />
        ) : !risks.readable ? (
          <p className="state">{t('dashboards.executive.risksUnavailable')}</p>
        ) : risks.top.length === 0 ? (
          <EmptyState title={t('dashboards.executive.noOpenRisks')} reason="notYet" />
        ) : (
          <ol className="executive__list">
            {risks.top.map(({ risk, project }) => (
              <li key={risk.id}>
                <Link
                  to={`/projects/${project.id}/risks/${risk.id}`}
                  lang={languageTag(risk.title.language)}
                  dir="auto"
                >
                  {risk.title.text}
                </Link>
                <span className="home__aside" lang={languageTag(project.title.language)} dir="auto">
                  {project.title.text}
                </span>
                {risk.currentAssessment === null ? (
                  <span className="value--unavailable">
                    {t('dashboards.executive.notAssessed')}
                  </span>
                ) : (
                  <RatingBadge
                    code={risk.currentAssessment.rating.code}
                    label={risk.currentAssessment.rating.label}
                    matrix={risks.matrix}
                  />
                )}
              </li>
            ))}
          </ol>
        )}
        <p className="home__more">
          <Link to={RISKS_PATH}>{t('dashboards.executive.riskRegister')}</Link>
        </p>
      </section>
      {decides && (
        <section className="insights__section" aria-labelledby="executive-decisions">
          <div className="section__header">
            <h2 id="executive-decisions">{t('dashboards.executive.decisions')}</h2>
            <Link to={INBOX_PATH}>{t('dashboards.executive.allDecisions')}</Link>
          </div>
          {inbox.data == null ? (
            inbox.loading ? (
              <LoadingState />
            ) : (
              <p className="state">{t('dashboards.executive.decisionsUnavailable')}</p>
            )
          ) : inbox.data.items.length === 0 ? (
            <EmptyState title={t('dashboards.executive.noDecisions')}>
              <p>{t('dashboards.executive.noDecisionsHint')}</p>
            </EmptyState>
          ) : (
            <ul className="executive__list" lang={language}>
              {inbox.data.items.map((item) => (
                <li key={item.taskId}>
                  <SubjectSummary
                    subject={item.instance.subject}
                    routingKey={item.instance.routingKey}
                  />
                  {item.dueAt !== null &&
                    (isOverdue(item.dueAt) ? (
                      <StatusBadge label={t('approvals.overdue')} tone="negative" />
                    ) : (
                      <span className="home__aside">
                        {t('home.dueAt', { date: formatDateTime(item.dueAt) })}
                      </span>
                    ))}
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </div>
  );
}

/** The Executive's top of the dashboard: the headline, then the top risks and the decisions waiting, before the charts and the tables. */
export function ExecutiveTop({
  headline,
  onPlan,
  filters,
}: {
  headline: PortfolioHeadline | null;
  onPlan: PortfolioOnPlan | null;
  filters: PortfolioFilters;
}): ReactElement {
  const data = useExecutiveData();
  return (
    <>
      {headline !== null && (
        <ExecutiveHeadline headline={headline} onPlan={onPlan} filters={filters} data={data} />
      )}
      <ExecutiveAttention data={data} />
    </>
  );
}
