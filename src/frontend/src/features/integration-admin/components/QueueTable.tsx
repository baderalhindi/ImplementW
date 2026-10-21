import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState } from '@/shared/ui/States.tsx';

import { type IntegrationSummary } from '../api/types.ts';
import { integrationDetailPath } from '../catalogue.ts';
import { labelIn } from '../presentation.ts';

import { Code, Moment } from './Display.tsx';

interface QueueTableProps {
  items: readonly IntegrationSummary[];
  caption: string;
  emptyTitle: string;
}

/**
 * Every registered integration's queued work and dead letters (TASK-092, FG-05 §6.6): the depth — attempts in flight
 * plus operations awaiting a retry — each part, when the oldest queued operation was first attempted, and the dead
 * letters, which are counted apart because they leave the queue and wait for an operator. One row per instance, in
 * the catalogue's order, so an integration with nothing queued is shown with zeros, not left out.
 */
export function QueueTable({ items, caption, emptyTitle }: QueueTableProps): ReactElement {
  const { t, language } = useI18n();
  const rows = items.flatMap((item) => item.instances.map((instance) => ({ item, instance })));
  if (rows.length === 0) {
    return <EmptyState title={emptyTitle} />;
  }
  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          <th scope="col">{t('integrationAdmin.fields.integration')}</th>
          <th scope="col">{t('integrationAdmin.queue.depth')}</th>
          <th scope="col">{t('integrationAdmin.queue.inFlight')}</th>
          <th scope="col">{t('integrationAdmin.queue.awaitingRetry')}</th>
          <th scope="col">{t('integrationAdmin.queue.oldestQueuedAt')}</th>
          <th scope="col">{t('integrationAdmin.queue.deadLetters')}</th>
          <th scope="col">{t('common.table.actions')}</th>
        </tr>
      </thead>
      <tbody>
        {rows.map(({ item, instance }) => {
          const name = labelIn(item.name, language);
          return (
            <tr key={instance.id} data-queue={item.code}>
              <th scope="row">
                {name}
                <span className="cell__aside">
                  <Code>{item.code}</Code>
                  {item.instances.length > 1 && <> · {instance.code}</>}
                </span>
              </th>
              <td data-field="depth">{instance.queue.depth}</td>
              <td data-field="inFlight">{instance.queue.inFlight}</td>
              <td data-field="awaitingRetry">{instance.queue.awaitingRetry}</td>
              <td data-field="oldestQueuedAt">
                <Moment
                  value={instance.queue.oldestQueuedAt}
                  absent={t('integrationAdmin.queue.nothingQueued')}
                />
              </td>
              <td data-field="deadLetters">{instance.queue.deadLetters}</td>
              <td className="cell--actions">
                <Link to={integrationDetailPath(item.id)}>
                  {t('integrationAdmin.list.open')} <span className="visually-hidden">{name}</span>
                </Link>
              </td>
            </tr>
          );
        })}
      </tbody>
    </TableContainer>
  );
}
