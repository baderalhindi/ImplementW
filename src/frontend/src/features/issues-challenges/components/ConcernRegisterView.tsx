import { type ReactElement, useState } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { EmptyState } from '@/shared/ui/States.tsx';

import {
  ANY,
  bySeverity,
  type ConcernEntry,
  concernSummary,
  matchesPriority,
  matchesSeverity,
  matchesView,
  REGISTER_VIEWS,
  type RegisterView,
  UNASSESSED,
} from '../concernRules.ts';
import { type ConcernLookups } from '../useConcernData.ts';

import { ConcernTable } from './ConcernTable.tsx';

function isRegisterView(value: string): value is RegisterView {
  return (REGISTER_VIEWS as readonly string[]).includes(value);
}

/** The distinct ids, in first-seen order. */
function distinct(ids: (string | null)[]): string[] {
  return [...new Set(ids.filter((id): id is string => id !== null))];
}

interface ConcernRegisterViewProps {
  entries: ConcernEntry[];
  lookups: ConcernLookups;
  userId: string;
  showProject: boolean;
  caption: string;
}

/**
 * The body of SCR-083 and SCR-085, for one project or across them: the summary cards, the view, severity and priority
 * filters, and the concerns most severe first. The severity filter offers the severities the concerns carry, and
 * "Not assessed".
 */
export function ConcernRegisterView({
  entries,
  lookups,
  userId,
  showProject,
  caption,
}: ConcernRegisterViewProps): ReactElement {
  const { t } = useI18n();
  const [view, setView] = useState<RegisterView>('open');
  const [severity, setSeverity] = useState(ANY);
  const [priority, setPriority] = useState(ANY);
  const today = todayUtc();
  const concerns = entries.map((entry) => entry.concern);
  const personName = usePersonNames(concerns.map((concern) => concern.assigneeUserId));
  const summary = concernSummary(concerns, today);
  const shown = entries
    .filter(
      ({ concern }) =>
        matchesView(concern, view, today, userId) &&
        matchesSeverity(concern, severity) &&
        matchesPriority(concern, priority),
    )
    .sort(bySeverity);

  const cards: { key: keyof typeof summary; label: string }[] = [
    { key: 'open', label: t('issuesChallenges.summary.open') },
    { key: 'unassigned', label: t('issuesChallenges.summary.unassigned') },
    { key: 'escalated', label: t('issuesChallenges.summary.escalated') },
    { key: 'overdue', label: t('issuesChallenges.summary.overdue') },
    { key: 'reviewDue', label: t('issuesChallenges.summary.reviewDue') },
    { key: 'pendingValidation', label: t('issuesChallenges.summary.pendingValidation') },
  ];

  return (
    <>
      <dl className="risk-summary">
        {cards.map((card) => (
          <div key={card.key} className="risk-summary__card" data-summary={card.key}>
            <dt>{card.label}</dt>
            <dd className="figure">{summary[card.key]}</dd>
          </div>
        ))}
      </dl>
      <div className="filters filters--inline">
        <SelectField
          label={t('issuesChallenges.filter.view')}
          name="view"
          value={view}
          options={REGISTER_VIEWS.map((value) => ({
            value,
            label: t(`issuesChallenges.filter.views.${value}`),
          }))}
          onChange={(chosen) => {
            if (isRegisterView(chosen)) {
              setView(chosen);
            }
          }}
        />
        <SelectField
          label={t('issuesChallenges.filter.severity')}
          name="severity"
          value={severity}
          options={[
            { value: ANY, label: t('common.filters.any') },
            ...distinct(concerns.map((concern) => concern.severityItemId)).map((id) => ({
              value: id,
              label: lookups.itemLabel(id),
            })),
            { value: UNASSESSED, label: t('issuesChallenges.severity.none') },
          ]}
          onChange={setSeverity}
        />
        <SelectField
          label={t('issuesChallenges.filter.priority')}
          name="priority"
          value={priority}
          options={[
            { value: ANY, label: t('common.filters.any') },
            ...distinct(concerns.map((concern) => concern.priorityItemId)).map((id) => ({
              value: id,
              label: lookups.itemLabel(id),
            })),
          ]}
          onChange={setPriority}
        />
      </div>
      {shown.length === 0 ? (
        <EmptyState title={t('issuesChallenges.empty.filtered')} />
      ) : (
        <ConcernTable
          caption={caption}
          entries={shown}
          today={today}
          showProject={showProject}
          itemLabel={lookups.itemLabel}
          personName={personName}
        />
      )}
    </>
  );
}
