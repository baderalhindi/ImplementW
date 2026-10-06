import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';

import { type ConcernEntry, isOverdue, isReturned, isReviewDue } from '../concernRules.ts';
import { concernPath } from '../paths.ts';

import { ConcernStatusBadge, DueFlag, EscalatedFlag, SeverityBadge } from './ConcernBadges.tsx';

interface ConcernTableProps {
  caption: string;
  entries: ConcernEntry[];
  today: string;
  /** Across projects each concern names its project; a project's register is one project's. */
  showProject: boolean;
  itemLabel: (id: string) => string;
  personName: (id: string | null) => string;
}

/**
 * The rows of SCR-083 and SCR-085: title (which opens SCR-084 or SCR-086) with its category, assignee, status, the
 * severity the server computed with the overall impact it came from, priority, target date and an open escalation.
 * Overdue, review due and escalated are said in words with an icon, never by colour alone.
 */
export function ConcernTable({
  caption,
  entries,
  today,
  showProject,
  itemLabel,
  personName,
}: ConcernTableProps): ReactElement {
  const { t } = useI18n();
  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          <th scope="col">{t('issuesChallenges.table.title')}</th>
          {showProject && <th scope="col">{t('issuesChallenges.table.project')}</th>}
          <th scope="col">{t('issuesChallenges.table.assignee')}</th>
          <th scope="col">{t('issuesChallenges.table.status')}</th>
          <th scope="col">{t('issuesChallenges.table.severity')}</th>
          <th scope="col">{t('issuesChallenges.table.priority')}</th>
          <th scope="col">{t('issuesChallenges.table.target')}</th>
          <th scope="col">{t('issuesChallenges.table.escalation')}</th>
        </tr>
      </thead>
      <tbody>
        {entries.map(({ concern, project }) => {
          const overdue = isOverdue(concern, today);
          return (
            <tr
              key={concern.id}
              data-concern={concern.id}
              className={
                overdue ? 'row--overdue' : concern.status === 'CLOSED' ? 'row--muted' : undefined
              }
            >
              <td>
                <Link to={concernPath(project.id, concern.id)} dir="auto">
                  {concern.title.text}
                </Link>
                <span className="cell__aside">{itemLabel(concern.categoryItemId)}</span>
                {concern.originatingRiskId !== null && (
                  <span className="cell__aside">{t('issuesChallenges.table.fromRisk')}</span>
                )}
              </td>
              {showProject && (
                <td>
                  <Link to={`/projects/${project.id}/issues-challenges`} dir="auto">
                    {project.title.text}
                  </Link>
                </td>
              )}
              <td>
                {concern.assigneeUserId === null
                  ? t('issuesChallenges.table.unassigned')
                  : personName(concern.assigneeUserId)}
              </td>
              <td>
                <ConcernStatusBadge status={concern.status} />
                {isReturned(concern) && (
                  <span className="cell__aside">{t('issuesChallenges.table.returned')}</span>
                )}
                {isReviewDue(concern, today) && (
                  <DueFlag label={t('issuesChallenges.table.reviewDue')} />
                )}
              </td>
              <td>
                <SeverityBadge concern={concern} itemLabel={itemLabel} />
                {concern.overallImpactLevel !== null && (
                  <span className="cell__aside">
                    {t('issuesChallenges.table.overallImpact', {
                      level: concern.overallImpactLevel,
                    })}
                  </span>
                )}
              </td>
              <td>{itemLabel(concern.priorityItemId)}</td>
              <td>
                {concern.targetResolutionDate === null ? (
                  <span className="figure figure--none">
                    {t('issuesChallenges.table.noTarget')}
                  </span>
                ) : (
                  <span dir="ltr">{concern.targetResolutionDate}</span>
                )}
                {overdue && <DueFlag label={t('issuesChallenges.table.overdue')} />}
              </td>
              <td>
                {concern.openEscalation === null ? (
                  <span className="figure figure--none">{t('issuesChallenges.table.none')}</span>
                ) : (
                  <EscalatedFlag />
                )}
              </td>
            </tr>
          );
        })}
      </tbody>
    </TableContainer>
  );
}
