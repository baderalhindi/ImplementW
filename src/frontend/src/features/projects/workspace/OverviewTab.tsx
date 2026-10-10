import { type ReactElement } from 'react';

import { ProjectDashboard } from '@/features/dashboards/ProjectDashboard.tsx';
import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';

import { type ProjectStatus } from '../api/types.ts';
import { Detail } from '../components/Detail.tsx';
import { profileMandatoryFields } from '../governance.ts';
import { formatSar } from '../presentation.ts';
import {
  FIELD_LABELS,
  missingFields,
  SUBMISSION_REQUIRED_FIELDS,
  valuesOf,
} from '../registration.ts';

import { useWorkspace } from './workspaceContext.ts';

/** What comes next from each state, and whose move it is (TASK-041 §3). */
const NEXT_STEPS: Record<ProjectStatus, TranslationKey> = {
  DRAFT: 'projects.overview.next.DRAFT',
  SUBMITTED: 'projects.overview.next.SUBMITTED',
  UNDER_REVIEW: 'projects.overview.next.UNDER_REVIEW',
  RETURNED: 'projects.overview.next.RETURNED',
  APPROVED_PLANNED: 'projects.overview.next.APPROVED_PLANNED',
  ACTIVE: 'projects.overview.next.ACTIVE',
  SUSPENDED: 'projects.overview.next.SUSPENDED',
  COMPLETED: 'projects.overview.next.COMPLETED',
  CLOSED: 'projects.overview.next.CLOSED',
};

/**
 * SCR-040 Overview: where the project stands, what comes next, and — while the registrant holds it — what is still
 * missing before it can be submitted. Below them, the Project Dashboard (DSH-009; DSH-008 for an entity's person) is
 * composed into this tab, its only place (TASK-070).
 */
export function OverviewTab(): ReactElement {
  const { t, formatDateTime } = useI18n();
  const { project, lookups, access, user } = useWorkspace();
  const managerName = usePersonNames([project.projectManagerUserId]);
  const missing =
    access.reached && (project.status === 'DRAFT' || project.status === 'RETURNED')
      ? missingFields(valuesOf(project), [
          ...SUBMISSION_REQUIRED_FIELDS,
          ...profileMandatoryFields(lookups.profiles, project.governanceProfileItemId),
        ])
      : [];
  const none = t('common.values.none');

  return (
    <>
      <p className="workspace__next">{t(NEXT_STEPS[project.status])}</p>
      {missing.length > 0 && (
        <div className="notice notice--warning" role="status">
          <p>{t('projects.overview.missingForSubmission')}</p>
          <ul>
            {missing.map((field) => (
              <li key={field}>{t(FIELD_LABELS[field])}</li>
            ))}
          </ul>
        </div>
      )}
      <dl className="details">
        <Detail term={t('projects.fields.participation')}>
          {t(`projects.participationMode.${project.participationMode}`)}
          {project.externalEntityId !== null && (
            <span className="details__aside">{lookups.entityName(project.externalEntityId)}</span>
          )}
        </Detail>
        <Detail term={t('projects.fields.department')}>
          {lookups.departmentName(project.departmentId)}
        </Detail>
        <Detail term={t('projects.fields.projectManager')}>
          {project.projectManagerUserId === null
            ? t('projects.projectManager.notNamed')
            : managerName(project.projectManagerUserId)}
        </Detail>
        <Detail term={t('projects.fields.governanceProfile')}>
          {lookups.itemLabel(project.governanceProfileItemId)}
        </Detail>
        <Detail term={t('projects.fields.registrationBudget')}>
          {project.registrationBudgetSar === null ? (
            none
          ) : (
            <span dir="ltr">
              {t('projects.money.sar', { amount: formatSar(project.registrationBudgetSar) })}
            </span>
          )}
        </Detail>
        <Detail term={t('projects.fields.plannedPeriod')}>
          {project.plannedStartDate === null && project.plannedEndDate === null ? (
            none
          ) : (
            <span dir="ltr">
              {project.plannedStartDate ?? none} – {project.plannedEndDate ?? none}
            </span>
          )}
        </Detail>
        {project.activatedAt !== null && (
          <Detail term={t('projects.fields.activatedAt')}>
            {formatDateTime(project.activatedAt)}
          </Detail>
        )}
        {project.legacyIntakeDate !== null && (
          <Detail term={t('projects.fields.legacyIntakeDate')}>
            <span dir="ltr">{project.legacyIntakeDate}</span>
            <span className="details__aside">{t('projects.overview.declaredBaseline')}</span>
          </Detail>
        )}
      </dl>
      <ProjectDashboard key={project.id} projectId={project.id} user={user} />
    </>
  );
}
