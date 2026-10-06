import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';

import { isReviewDue, type RiskEntry, type RiskMatrix } from '../riskRules.ts';

import { RatingBadge, RiskStatusBadge, UnratedBadge } from './RiskBadges.tsx';

interface RiskTableProps {
  caption: string;
  entries: RiskEntry[];
  today: string;
  /** SCR-080 across projects and SCR-081 name each risk's project; a project's register is one project's. */
  showProject: boolean;
  matrix: RiskMatrix | null;
  categoryLabel: (id: string) => string;
  personName: (id: string | null) => string;
}

/** A due review is said in words and with the warning icon, as well as by the row's thick edge. */
function ReviewDueFlag(): ReactElement {
  const { t } = useI18n();
  return (
    <span className="overdue-flag">
      <svg
        className="overdue-flag__icon"
        viewBox="0 0 16 16"
        width="16"
        height="16"
        aria-hidden="true"
        focusable="false"
      >
        <path d="M8 1 15 14H1Z" fill="none" stroke="currentColor" strokeWidth="1.6" />
        <path d="M8 6v4" stroke="currentColor" strokeWidth="1.6" />
        <circle cx="8" cy="12" r="0.9" fill="currentColor" />
      </svg>
      {t('risks.table.reviewDue')}
    </span>
  );
}

/**
 * The risk rows of SCR-080 and SCR-081: title (which opens SCR-082), category, owner, WF-06's status, the rating the
 * server gave with its probability and overall impact, the next review and an ACTIVE acceptance. An unassessed risk
 * says so, never a low rating (WF-06 §8: no false Low values).
 */
export function RiskTable({
  caption,
  entries,
  today,
  showProject,
  matrix,
  categoryLabel,
  personName,
}: RiskTableProps): ReactElement {
  const { t } = useI18n();
  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          <th scope="col">{t('risks.table.risk')}</th>
          {showProject && <th scope="col">{t('risks.table.project')}</th>}
          <th scope="col">{t('risks.table.category')}</th>
          <th scope="col">{t('risks.table.owner')}</th>
          <th scope="col">{t('risks.table.status')}</th>
          <th scope="col">{t('risks.table.rating')}</th>
          <th scope="col">{t('risks.table.nextReview')}</th>
        </tr>
      </thead>
      <tbody>
        {entries.map(({ risk, project }) => {
          const due = isReviewDue(risk, today);
          const assessment = risk.currentAssessment;
          return (
            <tr
              key={risk.id}
              className={due ? 'row--overdue' : risk.status === 'CLOSED' ? 'row--muted' : undefined}
            >
              <td>
                <Link to={`/projects/${project.id}/risks/${risk.id}`} dir="auto">
                  {risk.title.text}
                </Link>
                {risk.materialisedAt !== null && (
                  <span className="cell__aside">{t('risks.table.materialised')}</span>
                )}
              </td>
              {showProject && (
                <td>
                  <Link to={`/projects/${project.id}/risks`} dir="auto">
                    {project.title.text}
                  </Link>
                </td>
              )}
              <td>{categoryLabel(risk.riskCategoryItemId)}</td>
              <td>
                {risk.ownerUserId === null
                  ? t('risks.table.noOwner')
                  : personName(risk.ownerUserId)}
              </td>
              <td>
                <RiskStatusBadge status={risk.status} />
                {risk.acceptedUntil !== null && (
                  <span className="cell__aside">
                    {t('risks.table.acceptedUntil', { date: risk.acceptedUntil })}
                  </span>
                )}
              </td>
              <td>
                {assessment === null ? (
                  <UnratedBadge />
                ) : (
                  <>
                    <RatingBadge
                      code={assessment.rating.code}
                      label={assessment.rating.label}
                      matrix={matrix}
                    />
                    <span className="cell__aside">
                      {t('risks.table.levels', {
                        probability: assessment.probabilityLevel,
                        impact: assessment.overallImpactLevel,
                      })}
                    </span>
                  </>
                )}
              </td>
              <td>
                {risk.nextReviewDate === null ? (
                  <span className="figure figure--none">{t('risks.table.noReview')}</span>
                ) : (
                  <span dir="ltr">{risk.nextReviewDate}</span>
                )}
                {due && <ReviewDueFlag />}
              </td>
            </tr>
          );
        })}
      </tbody>
    </TableContainer>
  );
}
