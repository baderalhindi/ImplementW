import { Link } from 'react-router';
import { type ReactElement, useState } from 'react';

import { ProjectDashboard } from '@/features/dashboards/ProjectDashboard.tsx';
import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useCan } from '@/features/identity-access/permissions/permissions.ts';
import { type TranslationKey, useI18n } from '@/shared/i18n/i18n.ts';

import { type ProjectStatus } from '../api/types.ts';
import { Detail } from '../components/Detail.tsx';
import { ChangeManagerDialog } from '../dialogs/ChangeManagerDialog.tsx';
import { profileMandatoryFields } from '../governance.ts';
import {
  FIELD_LABELS,
  missingFields,
  SUBMISSION_REQUIRED_FIELDS,
  valuesOf,
} from '../registration.ts';

import { OverallProgressPanel } from '../stagePlan/OverallProgressPanel.tsx';

import { FollowUpPanel } from './FollowUpPanel.tsx';
import { OverviewKpis } from './OverviewKpis.tsx';
import { useWorkspace } from './workspaceContext.ts';
import { SarAmount } from '@/shared/format/SarAmount.tsx';

/**
 * What comes next from each state, and whose move it is (TASK-041 §3). The status badge above names the state, so the line gives only the next
 * move; an active project has none to state.
 */
const NEXT_STEPS: Partial<Record<ProjectStatus, TranslationKey>> = {
  DRAFT: 'projects.overview.next.DRAFT',
  SUBMITTED: 'projects.overview.next.SUBMITTED',
  UNDER_REVIEW: 'projects.overview.next.UNDER_REVIEW',
  RETURNED: 'projects.overview.next.RETURNED',
  APPROVED_PLANNED: 'projects.overview.next.APPROVED_PLANNED',
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
  const { project, lookups, access, user, reload, etag, notify } = useWorkspace();
  const [changingManager, setChangingManager] = useState(false);
  // Review round 7, item 2: R02 over every project, a department's manager over its own; never a closed project.
  const canChangeManager = useCan('PROJECT_MANAGER_CHANGE') && project.status !== 'CLOSED';
  const managerName = usePersonNames([project.projectManagerUserId]);
  const missing =
    access.reached && (project.status === 'DRAFT' || project.status === 'RETURNED')
      ? missingFields(valuesOf(project), [
          ...SUBMISSION_REQUIRED_FIELDS,
          ...profileMandatoryFields(lookups.profiles, project.governanceProfileItemId),
        ])
      : [];
  const nextStep = NEXT_STEPS[project.status];
  const progress = project.indicators?.progress;
  const hasStageBars =
    progress?.method === 'STAGES' && progress.stages.length > 0 && !progress.needsSetup;
  const none = t('common.values.none');
  const notAvailable = t('projects.followUp.notAvailable');

  return (
    <>
      {nextStep !== undefined && <p className="workspace__next">{t(nextStep)}</p>}
      <p>
        <Link to={`/projects/${project.id}/card`}>{t('projects.card.open')}</Link>
      </p>
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
      {/* The 15 Nov 2026 request: the Overview reads as a dashboard. Headline tiles first, then cards. */}
      <OverviewKpis project={project} />
      {/* With stage bars the progress card is tall and sits beside the details; without them it is a line, so each card takes the full width. */}
      <div className={hasStageBars ? 'overview-board' : 'overview-board overview-board--stacked'}>
        <div className="overview-card">
          <OverallProgressPanel project={project} etag={etag} onChanged={reload} />
        </div>
        <section className="overview-card" aria-labelledby="project-facts-title">
          <h2 id="project-facts-title">{t('projects.overview.facts')}</h2>
          <dl className="details details--tiles">
            <Detail term={t('projects.fields.titleEn')}>
              {project.titleEn == null ? (
                notAvailable
              ) : (
                <span lang="en" dir="ltr">
                  {project.titleEn}
                </span>
              )}
            </Detail>
            <Detail term={t('projects.fields.isStrategic')}>
              {project.isStrategic == null
                ? notAvailable
                : t(project.isStrategic ? 'common.values.yes' : 'common.values.no')}
            </Detail>
            <Detail term={t('projects.fields.participation')}>
              {t(`projects.participationMode.${project.participationMode}`)}
              {project.externalEntityId !== null && (
                <span className="details__aside">
                  {lookups.entityName(project.externalEntityId)}
                </span>
              )}
            </Detail>
            <Detail term={t('projects.fields.department')}>
              {lookups.departmentName(project.departmentId)}
            </Detail>
            <Detail term={t('projects.fields.projectManager')}>
              {project.projectManagerUserId === null
                ? t('projects.projectManager.notNamed')
                : managerName(project.projectManagerUserId)}
              {canChangeManager && (
                <button
                  type="button"
                  className="button button--quiet"
                  onClick={() => {
                    setChangingManager(true);
                  }}
                >
                  {t('projects.changeManager.action')}
                </button>
              )}
            </Detail>
            <Detail term={t('projects.fields.governanceProfile')}>
              {lookups.itemLabel(project.governanceProfileItemId)}
            </Detail>
            <Detail term={t('projects.fields.registrationBudget')}>
              {project.registrationBudgetSar === null ? (
                none
              ) : (
                <SarAmount amount={project.registrationBudgetSar} />
              )}
            </Detail>
            <Detail term={t('projects.fields.plannedPeriod')}>
              {project.plannedStartDate === null && project.plannedEndDate === null ? (
                none
              ) : (
                <span dir="ltr">
                  <span className="value--nowrap">{project.plannedStartDate ?? none}</span> –{' '}
                  <span className="value--nowrap">{project.plannedEndDate ?? none}</span>
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
        </section>
      </div>
      <FollowUpPanel />
      <ProjectDashboard key={project.id} projectId={project.id} user={user} />
      <ChangeManagerDialog
        open={changingManager}
        project={project}
        etag={etag}
        onClose={() => {
          setChangingManager(false);
        }}
        onChanged={() => {
          setChangingManager(false);
          notify({ tone: 'success', message: t('projects.changeManager.done') });
          reload();
        }}
      />
    </>
  );
}
