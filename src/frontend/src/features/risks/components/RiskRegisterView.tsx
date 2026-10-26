import { type ReactElement, useState } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
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

function isRegisterView(value: string): value is RegisterView {
  return (REGISTER_VIEWS as readonly string[]).includes(value);
}

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
  const [view, setView] = useState<RegisterView>('open');
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
      <div className="filters filters--inline">
        <SelectField
          label={t('risks.filter.view')}
          name="view"
          value={view}
          options={REGISTER_VIEWS.map((value) => ({
            value,
            label: t(`risks.filter.views.${value}`),
          }))}
          onChange={(chosen) => {
            if (isRegisterView(chosen)) {
              setView(chosen);
            }
          }}
        />
        <SelectField
          label={t('risks.filter.rating')}
          name="rating"
          value={rating}
          options={[
            { value: ANY_RATING, label: t('common.filters.any') },
            ...summary.byRating.map((entry) => ({
              value: entry.code,
              label: entry.label[language],
            })),
          ]}
          onChange={setRating}
        />
      </div>
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
