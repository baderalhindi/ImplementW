import { type ReactElement } from 'react';
import { type RowActionsColumn } from '@/features/identity-access/components/RowActions.tsx';
import { Link } from 'react-router';

import { formatSar, languageTag } from '@/features/projects/presentation.ts';
import { useI18n } from '@/shared/i18n/i18n.ts';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { SelectAllHeader, SelectRowCell } from '@/shared/ui/rowSelection.tsx';
import { useRowSelection } from '@/shared/ui/useRowSelection.ts';

import { type ChangeRequestDetail } from '../api/types.ts';
import { changeRequestPath } from '../paths.ts';
import { type ChangeRequestEntry } from '../useChangeRequestData.ts';

import { BandBadge, ChangeRequestStatusBadge } from './ChangeRequestBadges.tsx';

/** The impacts a request states, in a line: "+12 days · SAR 60,000 · scope". */
function ImpactSummary({ request }: { request: ChangeRequestDetail }): ReactElement {
  const { t } = useI18n();
  const parts = [
    ...(request.scheduleImpactDays === null
      ? []
      : [t('changeRequests.impacts.days', { days: request.scheduleImpactDays })]),
    ...(request.costImpactSar === null
      ? []
      : [t('changeRequests.impacts.sar', { amount: formatSar(request.costImpactSar) })]),
    ...(request.scopeImpact === null ? [] : [t('changeRequests.impacts.scope')]),
    ...(request.isContractualObligation ? [t('changeRequests.impacts.contractual')] : []),
  ];
  return <>{parts.length === 0 ? t('changeRequests.impacts.none') : parts.join(' · ')}</>;
}

interface ChangeRequestTableProps {
  entries: ChangeRequestEntry[];
  /** Across projects, each row names its project. */
  showProject: boolean;
  caption: string;
  /** Client review, item 8: the row's Edit and Delete, for a role that may manage it. */
  actions?: RowActionsColumn<ChangeRequestEntry> | undefined;
}

/**
 * SCR-105's table: each request's title (opening SCR-107), its type, its lifecycle status — Approved and Implemented in
 * their own words and styles — the materiality band recorded at review, the impacts it states and when it last changed.
 */
export function ChangeRequestTable({
  actions,
  entries,
  showProject,
  caption,
}: ChangeRequestTableProps): ReactElement {
  const { t, formatDateTime } = useI18n();
  const selection = useRowSelection();
  return (
    <>
      <TableContainer
        caption={caption}
        selection={{
          selection,
          exportName: 'change-requests',
          actions: actions?.bulk(entries, selection, ({ request }) => request.id),
        }}
      >
        <thead>
          <tr>
            <SelectAllHeader ids={entries.map(({ request }) => request.id)} selection={selection} />
            <th scope="col">{t('changeRequests.table.title')}</th>
            {showProject && <th scope="col">{t('changeRequests.table.project')}</th>}
            <th scope="col">{t('changeRequests.table.type')}</th>
            <th scope="col">{t('changeRequests.table.status')}</th>
            <th scope="col">{t('changeRequests.table.materiality')}</th>
            <th scope="col">{t('changeRequests.table.impacts')}</th>
            <th scope="col">{t('changeRequests.table.updated')}</th>
            {actions !== undefined && <th scope="col">{t('common.table.actions')}</th>}
          </tr>
        </thead>
        <tbody>
          {entries.map(({ request, project }) => (
            <tr key={request.id}>
              <SelectRowCell id={request.id} name={request.title.text} selection={selection} />
              <td>
                <Link
                  className="cell__link"
                  to={changeRequestPath(request.id)}
                  lang={languageTag(request.title.language)}
                  dir="auto"
                >
                  {request.title.text}
                </Link>
                <span className="cell__aside">
                  {t('changeRequests.table.revision', { revision: request.revisionNo })}
                </span>
              </td>
              {showProject && (
                <td>
                  <span lang={languageTag(project.title.language)} dir="auto">
                    {project.title.text}
                  </span>
                </td>
              )}
              <td>{t(`changeRequests.changeType.${request.changeType}`)}</td>
              <td>
                <ChangeRequestStatusBadge status={request.status} />
              </td>
              <td>
                {request.materiality === null ? (
                  <span className="cell__aside">{t('changeRequests.table.notRecorded')}</span>
                ) : (
                  <BandBadge bandNo={request.materiality.resultingBandNo} />
                )}
              </td>
              <td>
                <span dir="auto">
                  <ImpactSummary request={request} />
                </span>
              </td>
              <td>{formatDateTime(request.updatedAt)}</td>
              {actions !== undefined && (
                <td className="cell--actions">{actions.render({ request, project })}</td>
              )}
            </tr>
          ))}
        </tbody>
      </TableContainer>
      {actions?.dialog}
    </>
  );
}
