import { type ReactElement, useId } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';

import { useWorkspace } from '@/features/projects/workspace/workspaceContext.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import {
  AUDIT_TRAIL_PATH,
  canReadActivity,
  projectAuditCategories,
  projectAuditEventPath,
} from '../access.ts';
import { AuditEventDetailView } from '../components/AuditEventDetailView.tsx';
import { AuditEventList } from '../components/AuditEventList.tsx';
import { AuditFilters } from '../components/AuditFilters.tsx';
import { auditFilterValues, type AuditFilterName, toAuditQuery } from '../filters.ts';

const PROJECT_AUDIT_FILTERS: readonly AuditFilterName[] = [
  'category',
  'eventClass',
  'outcome',
  'from',
  'to',
  'eventType',
];

/**
 * SCR-058 Project Audit History (FG-06 §7.2): the project's formal audit, for a holder of an explicit project audit
 * permission (business, financial or external-participation audit). Shown apart from the Activity tab and as evidence:
 * codes, classes, outcomes and actors, each event opening its immutable detail inside the project. Only the categories
 * the reader holds are asked for, so the API refuses (403) anyone who reaches the address without one.
 */
export function ProjectAuditTab(): ReactElement {
  const { t } = useI18n();
  const { project, auditAccess } = useWorkspace();
  const [params] = useSearchParams();
  const values = auditFilterValues(params);
  const categories = projectAuditCategories(auditAccess);
  const title = t('auditActivity.projectAudit.title');
  const titleId = useId();
  return (
    <section className="audit-history" data-history="audit" aria-labelledby={titleId}>
      <div className="audit-history__header">
        <h2 id={titleId}>{title}</h2>
        <Link
          to={`${AUDIT_TRAIL_PATH}?${new URLSearchParams({ projectId: project.id }).toString()}`}
        >
          {t('auditActivity.projectAudit.searchTrail')}
        </Link>
      </div>
      <p className="audit-history__intro">{t('auditActivity.projectAudit.intro')}</p>
      <AuditFilters
        key={params.toString()}
        fields={PROJECT_AUDIT_FILTERS}
        categories={categories}
        values={values}
      />
      <AuditEventList
        query={toAuditQuery(values, { categories, projectId: project.id })}
        caption={t('auditActivity.projectAudit.caption')}
        emptyTitle={t('auditActivity.projectAudit.empty')}
        detailPath={(event) => projectAuditEventPath(project.id, event.id)}
      />
    </section>
  );
}

/** ADM-051's detail inside the project, reached from SCR-058. */
export function ProjectAuditEventTab(): ReactElement {
  const { t } = useI18n();
  const { project } = useWorkspace();
  const { auditEventId = '' } = useParams();
  return (
    <section className="audit-history" data-history="audit">
      <p>
        <Link to={`/projects/${project.id}/audit-history`}>
          {t('auditActivity.projectAudit.back')}
        </Link>
      </p>
      <AuditEventDetailView auditEventId={auditEventId} />
    </section>
  );
}

/**
 * What SCR-058 says to someone without a project audit permission who reaches its address — R01 among them: the
 * reason, and the way back to the project's Activity when they may read it. Nothing is read for them.
 */
export function ProjectAuditDenied(): ReactElement {
  const { t } = useI18n();
  const { project, auditAccess } = useWorkspace();
  return (
    <div className="state" role="status" data-audit-denied="true">
      <p className="state__title">{t('auditActivity.projectAudit.deniedTitle')}</p>
      <p>{t('auditActivity.denied.projectAudit')}</p>
      {canReadActivity(auditAccess) && (
        <Link to={`/projects/${project.id}/activity-history`}>
          {t('auditActivity.projectAudit.toActivity')}
        </Link>
      )}
    </div>
  );
}
