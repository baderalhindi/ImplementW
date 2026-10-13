import { type ReactElement } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { PageHeader } from '@/shared/ui/Layout.tsx';

import {
  AUDIT_TRAIL_PATH,
  auditEventPath,
  canReadAuditTrail,
  canReadIntegrationAudit,
  heldCategories,
} from './access.ts';
import { type AuditAccessView, type AuditCategory } from './api/types.ts';
import { AuditEventDetailView } from './components/AuditEventDetailView.tsx';
import { AuditEventList } from './components/AuditEventList.tsx';
import { AuditFilters } from './components/AuditFilters.tsx';
import { RequireAuditAccess } from './components/RequireAuditAccess.tsx';
import { auditFilterValues, type AuditFilterName, toAuditQuery } from './filters.ts';

const TRAIL_FILTERS: readonly AuditFilterName[] = [
  'category',
  'eventClass',
  'outcome',
  'from',
  'to',
  'eventType',
  'projectId',
  'actorUserId',
  'correlationId',
];

const INTEGRATION_FILTERS: readonly AuditFilterName[] = [
  'outcome',
  'from',
  'to',
  'eventType',
  'actorUserId',
  'correlationId',
];

const INTEGRATION_ONLY: readonly AuditCategory[] = ['INTEGRATION'];

/** The categories the person reads, named, so the page says whose evidence it is showing. */
function HeldCategories({ categories }: { categories: AuditCategory[] }): ReactElement {
  const { t } = useI18n();
  return (
    <p className="audit-scope" data-held-categories={categories.join(',')}>
      {t('auditActivity.trail.held', {
        categories: categories
          .map((category) => t(`auditActivity.category.${category}`))
          .join(', '),
      })}
    </p>
  );
}

function AuditTrail({ access }: { access: AuditAccessView }): ReactElement {
  const { t } = useI18n();
  const [params] = useSearchParams();
  const values = auditFilterValues(params);
  const categories = heldCategories(access);
  const title = t('auditActivity.trail.title');
  return (
    <>
      <PageHeader title={title} description={t('auditActivity.trail.description')} />
      <HeldCategories categories={categories} />
      <AuditFilters
        key={params.toString()}
        fields={TRAIL_FILTERS}
        categories={categories}
        values={values}
      />
      <AuditEventList
        query={toAuditQuery(values, { categories: values.category === '' ? [] : categories })}
        caption={title}
        emptyTitle={t('auditActivity.trail.empty')}
        detailPath={(event) => auditEventPath(event.id)}
      />
    </>
  );
}

/**
 * ADM-050 Audit Trail (FG-06 §8.2): formal audit across the categories the person holds — one permission each, never
 * R01 by itself (R01 ships with administration audit only, TASK-073 D-11) — filtered on the server.
 */
export function AuditTrailPage(): ReactElement {
  return (
    <RequireAuditAccess allows={canReadAuditTrail} deniedReason="auditActivity.denied.trail">
      {(access) => <AuditTrail access={access} />}
    </RequireAuditAccess>
  );
}

/** ADM-051 Audit Event Detail, from ADM-050 or ADM-053. */
export function AuditEventDetailPage(): ReactElement {
  const { t } = useI18n();
  const { auditEventId = '' } = useParams();
  return (
    <RequireAuditAccess allows={canReadAuditTrail} deniedReason="auditActivity.denied.trail">
      {() => (
        <>
          <PageHeader
            title={t('auditActivity.detail.title')}
            actions={
              <Link className="button" to={AUDIT_TRAIL_PATH}>
                {t('auditActivity.detail.backToTrail')}
              </Link>
            }
          />
          <AuditEventDetailView auditEventId={auditEventId} />
        </>
      )}
    </RequireAuditAccess>
  );
}

function IntegrationAudit(): ReactElement {
  const { t } = useI18n();
  const [params] = useSearchParams();
  const values = auditFilterValues(params);
  const title = t('auditActivity.integration.title');
  return (
    <>
      <PageHeader title={title} description={t('auditActivity.integration.description')} />
      <p className="audit-boundary" role="note">
        {t('auditActivity.integration.boundary')}
      </p>
      <AuditFilters
        key={params.toString()}
        fields={INTEGRATION_FILTERS}
        categories={INTEGRATION_ONLY}
        values={values}
      />
      <AuditEventList
        query={toAuditQuery(values, { categories: INTEGRATION_ONLY })}
        caption={title}
        emptyTitle={t('auditActivity.integration.empty')}
        detailPath={(event) => auditEventPath(event.id)}
      />
    </>
  );
}

/**
 * ADM-053 Integration Audit Logs (FG-06 §8.1, BR-AUD-044): the formal audit of integration operator and configuration
 * actions — the INTEGRATION category and nothing else, under AUDIT_INTEGRATION_VIEW. FG-05's runtime integration logs
 * (ADM-048) are not here and not duplicated.
 */
export function IntegrationAuditPage(): ReactElement {
  return (
    <RequireAuditAccess
      allows={canReadIntegrationAudit}
      deniedReason="auditActivity.denied.integration"
    >
      {() => <IntegrationAudit />}
    </RequireAuditAccess>
  );
}
