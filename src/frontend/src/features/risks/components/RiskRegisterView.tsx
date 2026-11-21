import { type ReactElement } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { useListParams } from '@/shared/api/useListParams.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar, type FilterBarChip } from '@/shared/ui/FilterBar.tsx';
import { EmptyState } from '@/shared/ui/States.tsx';

import {
  ANY_RATING,
  byExposure,
  matchesRating,
  matchesView,
  REGISTER_VIEWS,
  type RiskEntry,
  type RiskMatrix,
  riskSummary,
} from '../riskRules.ts';
import { type RiskLookups } from '../useRiskData.ts';

import { RiskSummaryCards } from './RiskSummaryCards.tsx';
import { RiskTable } from './RiskTable.tsx';
import { type RowActionsColumn } from '@/features/identity-access/components/RowActions.tsx';

const DEFAULT_VIEW = 'open';

interface RiskRegisterViewProps {
  entries: RiskEntry[];
  matrix: RiskMatrix | null;
  lookups: RiskLookups;
  showProject: boolean;
  caption: string;
  actions?: RowActionsColumn<RiskEntry> | undefined;
  /** Filters the page applies before the risks reach this view, such as a dashboard's carried over (review round 8, point 01). */
  chips?: FilterBarChip[];
  /** The changes that clear those filters too. */
  clearedChips?: Record<string, ''>;
}

/**
 * SCR-080's body, for one project or across them: the summary cards, the view and rating filters, and the risks most
 * severe first. The rating filter offers the ratings of the matrix in force (none are built in); without the matrix it
 * offers the ratings the risks carry. The view and the rating are in the URL, so a dashboard's rating opens the register filtered to it.
 */
export function RiskRegisterView({
  actions,
  entries,
  matrix,
  lookups,
  showProject,
  caption,
  chips = [],
  clearedChips = {},
}: RiskRegisterViewProps): ReactElement {
  const { t, language } = useI18n();
  const { params, setFilter, setFilters } = useListParams();
  const view = REGISTER_VIEWS.find((candidate) => candidate === params.get('view')) ?? DEFAULT_VIEW;
  const rating = params.get('rating') ?? ANY_RATING;
  const today = todayUtc();
  const risks = entries.map((entry) => entry.risk);
  const personName = usePersonNames(risks.map((risk) => risk.ownerUserId));
  const summary = riskSummary(risks, matrix, today);
  const shown = entries
    .filter((entry) => matchesView(entry.risk, view, today) && matchesRating(entry.risk, rating))
    .sort(byExposure(matrix));

  return (
    <>
      <RiskSummaryCards summary={summary} />
      <FilterBar
        fields={[
          {
            name: 'view',
            label: t('risks.filter.view'),
            all: t(`risks.filter.views.${DEFAULT_VIEW}`),
            options: REGISTER_VIEWS.filter((value) => value !== DEFAULT_VIEW).map((value) => ({
              value,
              label: t(`risks.filter.views.${value}`),
            })),
          },
          {
            name: 'rating',
            label: t('risks.filter.rating'),
            all: t('common.filters.any'),
            options: summary.byRating.map((entry) => ({
              value: entry.code,
              label: entry.label[language],
            })),
          },
        ]}
        values={{
          view: view === DEFAULT_VIEW ? undefined : view,
          rating: rating === ANY_RATING ? undefined : rating,
        }}
        chips={chips}
        onChange={(name, value) => {
          setFilter(name, value ?? '');
        }}
        onClearAll={() => {
          setFilters({ view: '', rating: '', ...clearedChips });
        }}
      />
      {shown.length === 0 ? (
        <EmptyState title={t('risks.empty.filtered')} reason="filter" />
      ) : (
        <RiskTable
          actions={actions}
          caption={caption}
          entries={shown}
          today={today}
          showProject={showProject}
          matrix={matrix}
          categoryLabel={lookups.itemLabel}
          personName={personName}
        />
      )}
    </>
  );
}
