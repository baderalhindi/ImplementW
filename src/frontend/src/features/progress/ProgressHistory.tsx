import { type ReactElement, useCallback } from 'react';
import { Link } from 'react-router';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { PAGE_SIZE } from '@/shared/api/paging.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { Pagination, TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/States.tsx';

import { progressApi } from './api/progressApi.ts';
import { type ProgressSubmissionDetail } from './api/types.ts';
import { ActualValue, PercentValue, VarianceValue } from './components/Figures.tsx';
import { HealthComparison } from './components/HealthComparison.tsx';
import { periodLabel } from './components/Period.tsx';
import {
  IntakeMarker,
  SemanticStateBadge,
  SubmissionStatusBadge,
} from './components/ProgressBadges.tsx';
import { isForbidden, progressProblemMessage } from './problems.ts';
import { useHealthView } from './useHealthView.ts';
import { usePeriods } from './usePeriods.ts';

/**
 * SCR-070 Progress Update History: every revision of every period, newest first, one page at a time (R-29), the page
 * kept in the address. The official and the live value head it, each with its badge (M-12); a published revision is
 * badged official in its row, and a returned one says why.
 */
export function ProgressHistory({ project }: { project: ProjectDetail }): ReactElement {
  const { t } = useI18n();
  const { page, goToPage } = useListParams();
  const health = useHealthView(project.id);
  const load = useCallback(
    (signal: AbortSignal) =>
      progressApi.submissions(project.id, { page, pageSize: PAGE_SIZE }, signal),
    [project.id, page],
  );
  const submissions = useApiResource(load);
  const period = usePeriods(project.id);
  const people = usePersonNames(
    (submissions.data?.items ?? []).flatMap((item) => [
      item.submittedByUserId,
      item.reviewedByUserId,
    ]),
  );
  const intakeDate = project.legacyIntakeDate;

  const reload = () => {
    health.reload();
    submissions.reload();
  };

  const heading = (
    <p>
      <Link to={`/projects/${project.id}/progress`}>{t('progress.actions.back')}</Link>
    </p>
  );

  if (health.loading || submissions.loading) {
    return (
      <>
        {heading}
        <LoadingState />
      </>
    );
  }
  const error = health.error ?? submissions.error;
  if (error !== null || health.data === undefined || submissions.data === undefined) {
    return (
      <>
        {heading}
        {isForbidden(error) ? (
          <p className="state">{t('progress.forbidden')}</p>
        ) : (
          <ErrorState message={progressProblemMessage(error, t)} onRetry={reload} />
        )}
      </>
    );
  }

  const result = submissions.data;
  return (
    <div className="progress">
      {heading}
      <h2>{t('progress.history.title')}</h2>
      {intakeDate !== null && (
        <p className="progress__intake">
          <IntakeMarker intakeDate={intakeDate} />
          <span className="details__aside">{t('progress.intake.explanation')}</span>
        </p>
      )}
      <HealthComparison view={health.data} intakeDate={intakeDate} />
      {result.items.length === 0 ? (
        <EmptyState title={t('progress.history.empty')} />
      ) : (
        <>
          <TableContainer caption={t('progress.history.caption')}>
            <thead>
              <tr>
                <th scope="col">{t('progress.history.period')}</th>
                <th scope="col">{t('progress.history.revision')}</th>
                <th scope="col">{t('progress.history.status')}</th>
                <th scope="col">{t('progress.figures.planned')}</th>
                <th scope="col">{t('progress.figures.actual')}</th>
                <th scope="col">{t('progress.figures.variance')}</th>
                <th scope="col">{t('progress.history.submitted')}</th>
                <th scope="col">{t('progress.history.reviewed')}</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((item) => (
                <HistoryRow
                  key={item.id}
                  item={item}
                  period={periodLabel(period(item.reportingCycleId), item.reportingCycleId, t)}
                  intakeDate={intakeDate}
                  personName={people}
                />
              ))}
            </tbody>
          </TableContainer>
          <Pagination
            page={result.page}
            pageSize={result.pageSize}
            totalCount={result.totalCount}
            onPageChange={goToPage}
          />
        </>
      )}
    </div>
  );
}

function HistoryRow({
  item,
  period,
  intakeDate,
  personName,
}: {
  item: ProgressSubmissionDetail;
  period: string;
  intakeDate: string | null;
  personName: (id: string | null) => string;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const openingPosition = item.projectIntakeId !== null;
  const when = (at: string | null, by: string | null) =>
    at === null ? t('common.values.none') : `${formatDateTime(at)} · ${personName(by)}`;
  return (
    <tr>
      <td>
        <span dir="ltr">{period}</span>
        {openingPosition && (
          <span className="details__aside">{t('progress.intake.openingPosition')}</span>
        )}
      </td>
      <td>{item.revisionNo}</td>
      <td>
        <span className="badge-group">
          <SubmissionStatusBadge status={item.status} />
          {item.status === 'PUBLISHED' && <SemanticStateBadge state="official" />}
        </span>
        {item.status === 'RETURNED' && item.returnReason !== null && (
          <p className="cell__aside" dir="auto">
            {t('progress.history.returnReason', { reason: item.returnReason.text })}
          </p>
        )}
      </td>
      <td>
        <PercentValue value={item.plannedPercent} />
      </td>
      <td>
        <ActualValue figures={item} />
      </td>
      <td>
        <VarianceValue
          figures={{ ...item, isOpeningPosition: openingPosition }}
          intakeDate={intakeDate}
        />
      </td>
      <td>{when(item.submittedAt, item.submittedByUserId)}</td>
      <td>{when(item.reviewedAt, item.reviewedByUserId)}</td>
    </tr>
  );
}
