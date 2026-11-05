import { type ReactElement, useState } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar } from '@/shared/ui/FilterBar.tsx';
import { EmptyState } from '@/shared/ui/States.tsx';

import {
  ANY_RATING,
  byExposure,
  matchesRating,
  matchesView,
  REGISTER_VIEWS,
  type RegisterView,
  type RiskEntry,
  type RiskMatrix,
  riskSummary,
} from '../riskRules.ts';
import { type RiskLookups } from '../useRiskData.ts';

import { RiskSummaryCards } from './RiskSummaryCards.tsx';
import { RiskTable } from './RiskTable.tsx';

const DEFAULT_VIEW: RegisterView = 'open';

interface RiskRegisterViewProps {
  entries: RiskEntry[];
  matrix: RiskMatrix | null;
  lookups: RiskLookups;
  showProject: boolean;
  caption: string;
}

/**
 * SCR-080's body, for one project or across them: the summary cards, the view and rating filters, and the risks most
 * severe first. The rating filter offers the ratings of the matrix in force (none are built in); without the matrix it
 * offers the ratings the risks carry.
 */
export function RiskRegisterView({
  entries,
  matrix,
  lookups,
  showProject,
  caption,
}: RiskRegisterViewProps): ReactElement {
  const { t, language } = useI18n();
  const [view, setView] = useState<RegisterView>(DEFAULT_VIEW);
  const [rating, setRating] = useState(ANY_RATING);
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
        onChange={(name, value) => {
          if (name === 'view') {
            setView(REGISTER_VIEWS.find((candidate) => candidate === value) ?? DEFAULT_VIEW);
          } else {
            setRating(value ?? ANY_RATING);
          }
        }}
        onClearAll={() => {
          setView(DEFAULT_VIEW);
          setRating(ANY_RATING);
        }}
      />
      {shown.length === 0 ? (
        <EmptyState title={t('risks.empty.filtered')} />
      ) : (
        <RiskTable
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
