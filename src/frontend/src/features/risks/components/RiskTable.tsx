import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useSession } from '@/features/identity-access/session/useSession.ts';
import { languageTag } from '@/features/projects/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';

import { canManageRisk } from '../access.ts';
import { type BilingualLabel, type RiskOpenTreatmentAction } from '../api/types.ts';
import {
  impactLabel,
  isReviewDue,
  probabilityLabel,
  type RiskEntry,
  type RiskMatrix,
} from '../riskRules.ts';
import { ADD_ACTION_PARAM } from '../riskLinks.ts';

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

/** A level as the matrix in force names it, with its number; the number alone when the matrix gives no name. */
function LevelCell({
  level,
  label,
}: {
  level: number;
  label: BilingualLabel | null;
}): ReactElement {
  const { language } = useI18n();
  return (
    <span className="risk-level">
      <span className="risk-level__number" dir="ltr">
        {level}
      </span>
      {label !== null && <span>{label[language]}</span>}
    </span>
  );
}

/** The risk's open corrective actions: the first by title and due date, and how many more. */
function CorrectiveActions({
  actions,
  addTo,
}: {
  actions: RiskOpenTreatmentAction[];
  /** Where the person may add one, or null when they may not. */
  addTo: string | null;
}): ReactElement {
  const { t } = useI18n();
  const first = actions[0];
  return (
    <>
      {first === undefined ? (
        <span className="figure figure--none">{t('risks.table.noAction')}</span>
      ) : (
        <span lang={languageTag(first.title.language)} dir="auto">
          {first.title.text}
        </span>
      )}
      {first?.dueDate != null && (
        <span className="cell__aside">{t('risks.table.actionDue', { date: first.dueDate })}</span>
      )}
      {actions.length > 1 && (
        <span className="cell__aside">
          {t('risks.table.moreActions', { count: actions.length - 1 })}
        </span>
      )}
      {addTo !== null && (
        <Link className="cell__aside" to={addTo}>
          {t('risks.actions.addAction')}
        </Link>
      )}
    </>
  );
}

/**
 * The risk rows of SCR-080 and SCR-081: title (which opens SCR-082), category, owner, WF-06's status, the probability
 * and overall impact as the matrix in force names them, the rating the server gave, the open corrective actions (the
 * 27 Oct 2026 review), and the next review with an ACTIVE acceptance. An unassessed risk says so, never a low rating
 * (WF-06 §8: no false Low values).
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
  const { session } = useSession();
  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          <th scope="col">{t('risks.table.risk')}</th>
          {showProject && <th scope="col">{t('risks.table.project')}</th>}
          <th scope="col">{t('risks.table.category')}</th>
          <th scope="col">{t('risks.table.owner')}</th>
          <th scope="col">{t('risks.table.status')}</th>
          <th scope="col">{t('risks.table.probability')}</th>
          <th scope="col">{t('risks.table.impact')}</th>
          <th scope="col">{t('risks.table.rating')}</th>
          <th scope="col">{t('risks.table.correctiveAction')}</th>
          <th scope="col">{t('risks.table.nextReview')}</th>
        </tr>
      </thead>
      <tbody>
        {entries.map(({ risk, project }) => {
          const due = isReviewDue(risk, today);
          const assessment = risk.currentAssessment;
          const detailPath = `/projects/${project.id}/risks/${risk.id}`;
          const mayAdd = session !== null && canManageRisk(session.user, project, risk);
          return (
            <tr
              key={risk.id}
              className={due ? 'row--overdue' : risk.status === 'CLOSED' ? 'row--muted' : undefined}
            >
              <td>
                <Link to={detailPath} lang={languageTag(risk.title.language)} dir="auto">
                  {risk.title.text}
                </Link>
                {risk.materialisedAt !== null && (
                  <span className="cell__aside">{t('risks.table.materialised')}</span>
                )}
              </td>
              {showProject && (
                <td>
                  <Link
                    to={`/projects/${project.id}/risks`}
                    lang={languageTag(project.title.language)}
                    dir="auto"
                  >
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
                  <span className="figure figure--none">—</span>
                ) : (
                  <LevelCell
                    level={assessment.probabilityLevel}
                    label={probabilityLabel(matrix, assessment.probabilityLevel)}
                  />
                )}
              </td>
              <td>
                {assessment === null ? (
                  <span className="figure figure--none">—</span>
                ) : (
                  <LevelCell
                    level={assessment.overallImpactLevel}
                    label={impactLabel(matrix, assessment.overallImpactLevel)}
                  />
                )}
              </td>
              <td>
                {assessment === null ? (
                  <UnratedBadge />
                ) : (
                  <RatingBadge
                    code={assessment.rating.code}
                    label={assessment.rating.label}
                    matrix={matrix}
                  />
                )}
              </td>
              <td>
                <CorrectiveActions
                  actions={risk.openTreatmentActions}
                  addTo={mayAdd ? `${detailPath}?${ADD_ACTION_PARAM}=1` : null}
                />
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
