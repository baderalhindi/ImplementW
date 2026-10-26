import { type ReactElement, useState } from 'react';
import { Link } from 'react-router';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { TableContainer } from '@/shared/ui/Layout.tsx';
import { EmptyState } from '@/shared/ui/States.tsx';

import { type SuspensionRequestType } from '../api/types.ts';
import { suspensionRequestPath } from '../paths.ts';
import {
  SUSPENSION_REQUEST_TYPES,
  SUSPENSION_VIEWS,
  type SuspensionView,
  suspensionViewOf,
} from '../suspensionRules.ts';
import { type SuspensionEntry } from '../useSuspensionClosureData.ts';

import { GovernedStatusBadge } from './GovernedState.tsx';

/**
 * SCR-108's views and table (WF-09 §13: Open requests, Approved pending activation, Active suspensions, Historical) with
 * a type filter: a resumption is a request of type Resume on the same register. All are shown by default.
 */
export function SuspensionRegisterView({
  entries,
  showProject,
  caption,
}: {
  entries: SuspensionEntry[];
  showProject: boolean;
  caption: string;
}): ReactElement {
  const { t, formatDateTime } = useI18n();
  const [view, setView] = useState<SuspensionView | ''>('');
  const [type, setType] = useState<SuspensionRequestType | ''>('');
  const shown = entries.filter(
    ({ request }) =>
      (view === '' || suspensionViewOf(request) === view) &&
      (type === '' || request.requestType === type),
  );

  return (
    <>
      <div className="filters">
        <SelectField
          label={t('suspensionClosure.suspension.filter.view')}
          name="view"
          value={view}
          placeholder={t('suspensionClosure.suspension.filter.allViews')}
          options={SUSPENSION_VIEWS.map((value) => ({
            value,
            label: t(`suspensionClosure.suspension.view.${value}`),
          }))}
          onChange={(value) => {
            setView(SUSPENSION_VIEWS.find((candidate) => candidate === value) ?? '');
          }}
        />
        <SelectField
          label={t('suspensionClosure.suspension.filter.type')}
          name="requestType"
          value={type}
          placeholder={t('suspensionClosure.suspension.filter.allTypes')}
          options={SUSPENSION_REQUEST_TYPES.map((value) => ({
            value,
            label: t(`suspensionClosure.suspension.type.${value}`),
          }))}
          onChange={(value) => {
            setType(SUSPENSION_REQUEST_TYPES.find((candidate) => candidate === value) ?? '');
          }}
        />
      </div>
      <p className="list-order" role="status">
        {t('suspensionClosure.suspension.filter.showing', {
          shown: shown.length,
          total: entries.length,
        })}
      </p>
      {shown.length === 0 ? (
        <EmptyState title={t('suspensionClosure.suspension.filter.noMatch')} />
      ) : (
        <TableContainer caption={caption}>
          <thead>
            <tr>
              <th scope="col">{t('suspensionClosure.suspension.table.request')}</th>
              {showProject && <th scope="col">{t('suspensionClosure.table.project')}</th>}
              <th scope="col">{t('suspensionClosure.suspension.table.status')}</th>
              <th scope="col">{t('suspensionClosure.suspension.table.effectiveDate')}</th>
              <th scope="col">{t('suspensionClosure.suspension.table.plannedResumption')}</th>
              <th scope="col">{t('suspensionClosure.table.updated')}</th>
            </tr>
          </thead>
          <tbody>
            {shown.map(({ request, project }) => (
              <tr key={request.id}>
                <td>
                  <Link className="cell__link" to={suspensionRequestPath(request.id)}>
                    {t(`suspensionClosure.suspension.type.${request.requestType}`)}
                  </Link>
                  <span className="cell__aside">
                    {t('suspensionClosure.table.revision', { revision: request.revisionNo })}
                  </span>
                </td>
                {showProject && (
                  <td>
                    <span dir="auto">{project.title.text}</span>
                  </td>
                )}
                <td>
                  <GovernedStatusBadge status={request.status} />
                  {suspensionViewOf(request) === 'IN_EFFECT' && (
                    <span className="cell__aside">
                      {t('suspensionClosure.suspension.view.IN_EFFECT')}
                    </span>
                  )}
                </td>
                <td>
                  {request.requestedEffectiveDate === null ? (
                    t('suspensionClosure.notStated')
                  ) : (
                    <span dir="ltr">{request.requestedEffectiveDate}</span>
                  )}
                </td>
                <td>
                  {request.plannedResumptionDate === null ? (
                    t('suspensionClosure.notStated')
                  ) : (
                    <span dir="ltr">{request.plannedResumptionDate}</span>
                  )}
                </td>
                <td>{formatDateTime(request.updatedAt)}</td>
              </tr>
            ))}
          </tbody>
        </TableContainer>
      )}
    </>
  );
}
