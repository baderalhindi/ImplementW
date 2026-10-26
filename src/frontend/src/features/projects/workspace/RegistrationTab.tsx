import { type ReactElement } from 'react';

import { usePersonNames } from '@/features/identity-access/assignments/useUserNames.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { Detail } from '../components/Detail.tsx';
import { formatSar, languageTag } from '../presentation.ts';

import { useWorkspace } from './workspaceContext.ts';

/** SCR-041 Registration: every field of the registration as the API holds it, with who made and last changed it. */
export function RegistrationTab(): ReactElement {
  const { t, formatDateTime } = useI18n();
  const { project, lookups } = useWorkspace();
  const person = usePersonNames([project.createdBy, project.updatedBy]);
  const none = t('common.values.none');

  return (
    <dl className="details">
      <Detail term={t('projects.fields.title')}>
        <span lang={languageTag(project.title.language)} dir="auto">
          {project.title.text}
        </span>
      </Detail>
      <Detail term={t('projects.fields.description')}>
        {project.description === null ? (
          none
        ) : (
          <span className="pre-line" lang={languageTag(project.description.language)} dir="auto">
            {project.description.text}
          </span>
        )}
      </Detail>
      <Detail term={t('projects.fields.formalProjectId')}>
        {project.formalProjectId === null ? (
          t('projects.formalProjectId.notIssued')
        ) : (
          <span dir="ltr">{project.formalProjectId}</span>
        )}
      </Detail>
      <Detail term={t('projects.fields.classification')}>
        {lookups.itemLabel(project.classificationItemId)}
      </Detail>
      <Detail term={t('projects.fields.governanceProfile')}>
        {lookups.itemLabel(project.governanceProfileItemId)}
      </Detail>
      <Detail term={t('projects.fields.participation')}>
        {t(`projects.participationMode.${project.participationMode}`)}
      </Detail>
      <Detail term={t('projects.fields.department')}>
        {lookups.departmentName(project.departmentId)}
      </Detail>
      <Detail term={t('projects.fields.externalEntity')}>
        {lookups.entityName(project.externalEntityId) ?? none}
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
      <Detail term={t('projects.fields.plannedStartDate')}>
        <span dir="ltr">{project.plannedStartDate ?? none}</span>
      </Detail>
      <Detail term={t('projects.fields.plannedEndDate')}>
        <span dir="ltr">{project.plannedEndDate ?? none}</span>
      </Detail>
      <Detail term={t('projects.fields.revision')}>{project.revisionNo}</Detail>
      <Detail term={t('projects.fields.createdAt')}>
        {formatDateTime(project.createdAt)}
        <span className="details__aside">{person(project.createdBy)}</span>
      </Detail>
      <Detail term={t('projects.fields.updatedAt')}>
        {formatDateTime(project.updatedAt)}
        <span className="details__aside">{person(project.updatedBy)}</span>
      </Detail>
    </dl>
  );
}
