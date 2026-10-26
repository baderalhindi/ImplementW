import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState } from '@/shared/ui/States.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';
import { ValueStateFlag } from '@/shared/ui/ValueState.tsx';

import { type IntegrationInstanceView, type IntegrationSummary } from '../api/types.ts';
import { integrationDetailPath } from '../catalogue.ts';
import { activationTone, labelIn } from '../presentation.ts';

import { Code, Moment } from './Display.tsx';
import { AvailabilityIndicator, FreshnessIndicator } from './HealthIndicators.tsx';

interface IntegrationTableProps {
  items: readonly IntegrationSummary[];
  caption: string;
  emptyTitle: string;
}

/**
 * Integrations with each instance's runtime state: activation, then availability and freshness in two columns of their
 * own, last success and failure, when the runtime last evaluated it, and its live alerts. One row per instance (an
 * environment has one per definition today, TASK-075 D-15); a definition without an instance says so.
 */
export function IntegrationTable({
  items,
  caption,
  emptyTitle,
}: IntegrationTableProps): ReactElement {
  const { t } = useI18n();
  if (items.length === 0) {
    return <EmptyState title={emptyTitle} />;
  }
  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          <th scope="col">{t('integrationAdmin.fields.integration')}</th>
          <th scope="col">{t('integrationAdmin.fields.activation')}</th>
          <th scope="col">{t('integrationAdmin.indicators.availability')}</th>
          <th scope="col">{t('integrationAdmin.indicators.freshness')}</th>
          <th scope="col">{t('integrationAdmin.fields.lastSuccess')}</th>
          <th scope="col">{t('integrationAdmin.fields.lastFailure')}</th>
          <th scope="col">{t('integrationAdmin.fields.evaluatedAt')}</th>
          <th scope="col">{t('integrationAdmin.fields.liveAlerts')}</th>
          <th scope="col">{t('common.table.actions')}</th>
        </tr>
      </thead>
      <tbody>
        {items.flatMap((item) =>
          item.instances.length === 0 ? (
            <IntegrationRow key={item.id} item={item} instance={null} />
          ) : (
            item.instances.map((instance) => (
              <IntegrationRow key={instance.id} item={item} instance={instance} />
            ))
          ),
        )}
      </tbody>
    </TableContainer>
  );
}

function IntegrationRow({
  item,
  instance,
}: {
  item: IntegrationSummary;
  instance: IntegrationInstanceView | null;
}): ReactElement {
  const { t, language } = useI18n();
  const name = labelIn(item.name, language);
  return (
    <tr data-integration={item.code}>
      <th scope="row">
        {name}
        <span className="cell__aside">
          <Code>{item.code}</Code>
          {instance !== null && item.instances.length > 1 && <> · {instance.code}</>}
        </span>
      </th>
      <td>
        <StatusBadge
          label={t(`integrationAdmin.activationStatus.${item.status}`)}
          tone={activationTone(item.status)}
        />
      </td>
      {instance === null ? (
        <td colSpan={6}>
          <ValueStateFlag
            icon="missing"
            state="no-instance"
            label={t('integrationAdmin.values.noInstance')}
          />
        </td>
      ) : (
        <>
          <td>
            <AvailabilityIndicator availability={instance.availability} />
          </td>
          <td>
            <FreshnessIndicator freshness={instance.freshness} />
          </td>
          <td>
            <Moment value={instance.lastSuccessAt} />
          </td>
          <td>
            <Moment value={instance.lastFailureAt} />
          </td>
          <td>
            <Moment
              value={instance.evaluatedAt}
              absent={t('integrationAdmin.values.notEvaluated')}
            />
          </td>
          <td>{instance.openAlertCount}</td>
        </>
      )}
      <td className="cell--actions">
        <Link to={integrationDetailPath(item.id)}>
          {t('integrationAdmin.list.open')} <span className="visually-hidden">{name}</span>
        </Link>
      </td>
    </tr>
  );
}
