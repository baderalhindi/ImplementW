import { type ReactElement, useCallback } from 'react';
import { Link } from 'react-router';

import { auditActivityApi } from '@/features/audit-activity/api/auditActivityApi.ts';
import { SYSTEM_EVENT_LOGS_PATH } from '@/features/audit-activity/access.ts';
import { integrationAdminApi } from '@/features/integration-admin/api/integrationAdminApi.ts';
import { INTEGRATIONS_OVERVIEW_PATH } from '@/features/integration-admin/catalogue.ts';
import { usersApi } from '@/features/identity-access/api/identityAccessApi.ts';
import { usePermissions } from '@/features/identity-access/permissions/permissions.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';

import { HomeCard } from './HomeCard.tsx';

/**
 * The administrator's Home (the 24 Oct 2026 review, M-1: "show platform health … not business data"): the integrations' availability
 * and open alerts, the audit pipeline's backlog and failures, and the people the platform holds. Each card is offered to a holder of
 * the permission its read needs, and leads to the screen where it is acted on.
 */
export function PlatformHealth(): ReactElement {
  const { t } = useI18n();
  const { can } = usePermissions();
  return (
    <div className="home__grid">
      {can('INTEGRATION_VIEW') && <Integrations />}
      {can('AUDIT_OPERATIONS_MANAGE') && <AuditPipeline />}
      {can('USER_VIEW') && <People />}
      <section className="widget home__card" aria-label={t('home.actions.title')}>
        <h2 className="widget__title">{t('home.actions.title')}</h2>
        <ul className="home__actions">
          {can('USER_MANAGE') && (
            <li>
              <Link to="/admin/users/new">{t('home.health.createUser')}</Link>
            </li>
          )}
          {can('ROLE_ASSIGN') && (
            <li>
              <Link to="/admin/role-assignments">{t('identityAccess.nav.roleAssignments')}</Link>
            </li>
          )}
          {can('ROLE_VIEW') && (
            <li>
              <Link to="/admin/permission-matrix">{t('identityAccess.nav.permissionMatrix')}</Link>
            </li>
          )}
          {can('DATA_IMPORT') && (
            <li>
              <Link to="/admin/data-migration">{t('dataImport.nav')}</Link>
            </li>
          )}
        </ul>
      </section>
    </div>
  );
}

function Integrations(): ReactElement {
  const { t } = useI18n();
  const catalogue = useApiResource(integrationAdminApi.catalogue);
  const instancesOf = (data: NonNullable<typeof catalogue.data>) =>
    data.items.flatMap((item) => item.instances);
  return (
    <HomeCard
      title={t('home.health.integrations.title')}
      resource={catalogue}
      figure={(data) =>
        instancesOf(data).filter(
          (instance) => instance.isEnabled && instance.availability === 'DOWN',
        ).length
      }
      viewAll={{ to: INTEGRATIONS_OVERVIEW_PATH, label: t('home.health.integrations.all') }}
      empty={t('home.health.integrations.allUp')}
    >
      {(data) => {
        const enabled = instancesOf(data).filter((instance) => instance.isEnabled);
        return (
          <p className="home__aside">
            {t('home.health.integrations.counts', {
              down: enabled.filter((instance) => instance.availability === 'DOWN').length,
              stale: enabled.filter((instance) => instance.freshness === 'STALE').length,
              alerts: enabled.reduce((sum, instance) => sum + instance.openAlertCount, 0),
            })}
          </p>
        );
      }}
    </HomeCard>
  );
}

function AuditPipeline(): ReactElement {
  const { t } = useI18n();
  const status = useApiResource(auditActivityApi.ingestionStatus);
  return (
    <HomeCard
      title={t('home.health.audit.title')}
      resource={status}
      figure={(data) => data.failingMessages}
      viewAll={{ to: SYSTEM_EVENT_LOGS_PATH, label: t('home.health.audit.all') }}
      empty={t('home.health.audit.healthy')}
    >
      {(data) => (
        <p className="home__aside">
          {t('home.health.audit.backlog', {
            pending: data.pendingMessages,
            lag: data.projectionLag,
          })}
        </p>
      )}
    </HomeCard>
  );
}

function People(): ReactElement {
  const { t } = useI18n();
  const counts = useApiResource(
    useCallback(async (signal: AbortSignal) => {
      const [internal, external, disabled] = await Promise.all([
        usersApi.list({ status: 'ACTIVE', userType: 'INTERNAL', pageSize: 1 }, signal),
        usersApi.list({ status: 'ACTIVE', userType: 'EXTERNAL', pageSize: 1 }, signal),
        usersApi.list({ status: 'DISABLED', pageSize: 1 }, signal),
      ]);
      return {
        internal: internal.totalCount,
        external: external.totalCount,
        disabled: disabled.totalCount,
      };
    }, []),
  );
  return (
    <HomeCard
      title={t('home.health.people.title')}
      resource={counts}
      figure={(data) => data.internal + data.external}
      viewAll={{ to: '/admin/users', label: t('home.health.people.all') }}
      empty={t('home.health.people.none')}
    >
      {(data) => <p className="home__aside">{t('home.health.people.counts', data)}</p>}
    </HomeCard>
  );
}
