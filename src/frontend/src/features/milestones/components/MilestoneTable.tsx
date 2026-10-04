import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { varianceLabel } from '@/features/schedule/presentation.ts';
import { OverdueFlag } from '@/features/tasks/components/TaskBadges.tsx';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';

import { achievementState, type MilestoneEntry, overdueDays } from '../milestoneRules.ts';

import { AchievementBadge, MilestoneStatusBadge } from './MilestoneBadges.tsx';
import { ReturnedReasonLine } from './ReturnedReason.tsx';

interface MilestoneTableProps {
  caption: string;
  entries: MilestoneEntry[];
  today: string;
  /** SCR-062 names each milestone's project; SCR-046 is one project's. */
  showProject: boolean;
  categoryLabel: (id: string) => string;
  onOpen: (entry: MilestoneEntry) => void;
}

/**
 * The milestone rows of SCR-046 and SCR-062: title (which opens MOD-019), category, the forecast with the baseline's date
 * and the variance, WF-03's status, and where the claim stands. A returned claim's reason is on the row itself
 * (acceptance criterion 2), and an overdue milestone carries the warning icon and words as well as a thick edge.
 */
export function MilestoneTable({
  caption,
  entries,
  today,
  showProject,
  categoryLabel,
  onOpen,
}: MilestoneTableProps): ReactElement {
  const { t } = useI18n();
  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          <th scope="col">{t('milestones.table.milestone')}</th>
          {showProject && <th scope="col">{t('milestones.table.project')}</th>}
          <th scope="col">{t('milestones.table.category')}</th>
          <th scope="col">{t('milestones.table.forecast')}</th>
          <th scope="col">{t('milestones.table.status')}</th>
          <th scope="col">{t('milestones.table.achievement')}</th>
        </tr>
      </thead>
      <tbody>
        {entries.map((entry) => {
          const { milestone, project, revisions } = entry;
          const state = revisions === null ? null : achievementState(revisions);
          const overdue = overdueDays(milestone, today);
          const rowClass =
            state?.kind === 'returned'
              ? 'row--returned'
              : overdue !== null
                ? 'row--overdue'
                : milestone.status === 'CANCELLED'
                  ? 'row--muted'
                  : undefined;
          return (
            <tr key={milestone.id} className={rowClass}>
              <td>
                <button
                  type="button"
                  className="button button--link task__title"
                  dir="auto"
                  onClick={() => {
                    onOpen(entry);
                  }}
                >
                  {milestone.title.text}
                </button>
                {overdue !== null && <OverdueFlag days={overdue} />}
                {state?.kind === 'returned' && <ReturnedReasonLine revision={state.revision} />}
              </td>
              {showProject && (
                <td>
                  <Link to={`/projects/${project.id}/milestones`} dir="auto">
                    {project.title.text}
                  </Link>
                </td>
              )}
              <td>{categoryLabel(milestone.milestoneCategoryItemId)}</td>
              <td>
                <span dir="ltr">{milestone.forecastDate}</span>
                <span className="cell__aside">
                  {milestone.baselinePlannedDate === null
                    ? t('milestones.table.notBaselined')
                    : t('milestones.table.baseline', {
                        date: milestone.baselinePlannedDate,
                        variance: varianceLabel(milestone.forecastVarianceDays, t),
                      })}
                </span>
              </td>
              <td>
                <MilestoneStatusBadge status={milestone.status} />
              </td>
              <td>
                {state === null ? (
                  <span className="figure figure--none">{t('milestones.table.claimsHidden')}</span>
                ) : (
                  <>
                    <AchievementBadge state={state} />
                    {state.kind === 'accepted' && (
                      <span className="cell__aside">
                        {t('milestones.table.achievedOn', {
                          date: state.revision.acceptedActualAchievementDate ?? '',
                        })}
                      </span>
                    )}
                  </>
                )}
              </td>
            </tr>
          );
        })}
      </tbody>
    </TableContainer>
  );
}
