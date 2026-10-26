import { type ReactElement } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import { type InvocationSummary } from '../api/types.ts';
import { invocationPath } from '../catalogue.ts';
import { invocationTone } from '../presentation.ts';

import { Code, Moment } from './Display.tsx';

interface InvocationTableProps {
  items: readonly InvocationSummary[];
  caption: string;
  /** The attempt being viewed, marked in a lineage and not linked to itself. */
  current?: string;
}

/**
 * Attempts as structured, redacted diagnostics (FG-05 §8.2 "default visible"): when, which integration and operation,
 * the attempt's number in its lineage, its state and failure category, and its response code and error summary — plain
 * codes, or `[REDACTED]` where the API removed a provider's text (TASK-075 D-11). No payload exists to show.
 */
export function InvocationTable({ items, caption, current }: InvocationTableProps): ReactElement {
  const { t } = useI18n();
  return (
    <TableContainer caption={caption}>
      <thead>
        <tr>
          <th scope="col">{t('integrationAdmin.fields.startedAt')}</th>
          <th scope="col">{t('integrationAdmin.fields.integration')}</th>
          <th scope="col">{t('integrationAdmin.fields.operation')}</th>
          <th scope="col">{t('integrationAdmin.fields.attempt')}</th>
          <th scope="col">{t('integrationAdmin.fields.invocationStatus')}</th>
          <th scope="col">{t('integrationAdmin.fields.failure')}</th>
          <th scope="col">{t('common.table.actions')}</th>
        </tr>
      </thead>
      <tbody>
        {items.map((invocation) => (
          <tr
            key={invocation.id}
            data-invocation={invocation.id}
            aria-current={invocation.id === current ? 'true' : undefined}
          >
            <td>
              <Moment value={invocation.startedAt} />
            </td>
            <td>
              <Code>{invocation.integrationCode}</Code>
            </td>
            <td>
              <Code>{invocation.operationCode}</Code>
              {invocation.sourceModule !== null && (
                <span className="cell__aside">{invocation.sourceModule}</span>
              )}
            </td>
            <td>{invocation.attemptNo}</td>
            <td>
              <StatusBadge
                label={t(`integrationAdmin.invocationStatus.${invocation.status}`)}
                tone={invocationTone(invocation.status)}
              />
              {invocation.nextAttemptAt !== null && (
                <span className="cell__aside">
                  {t('integrationAdmin.invocations.nextAttempt')}{' '}
                  <Moment value={invocation.nextAttemptAt} />
                </span>
              )}
            </td>
            <td>
              {invocation.failureCategory === null ? (
                t('common.values.none')
              ) : (
                <>
                  {t(`integrationAdmin.failureCategory.${invocation.failureCategory}`)}
                  {invocation.responseCode !== null && (
                    <span className="cell__aside">
                      <Code>{invocation.responseCode}</Code>
                    </span>
                  )}
                  {invocation.errorSummary !== null && (
                    <span className="cell__aside">
                      <Code>{invocation.errorSummary}</Code>
                    </span>
                  )}
                </>
              )}
            </td>
            <td className="cell--actions">
              {invocation.id === current ? (
                <span className="cell__aside">{t('integrationAdmin.invocations.thisAttempt')}</span>
              ) : (
                <Link to={invocationPath(invocation.id)}>
                  {t('integrationAdmin.list.open')}{' '}
                  <span className="visually-hidden">
                    {t('integrationAdmin.invocations.attemptOf', {
                      number: invocation.attemptNo,
                      operation: invocation.operationCode,
                    })}
                  </span>
                </Link>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </TableContainer>
  );
}
