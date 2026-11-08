import { type RowActionsColumn } from '@/features/identity-access/components/RowActions.tsx';
import { type ReactElement, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar } from '@/shared/ui/FilterBar.tsx';
import { EmptyState } from '@/shared/ui/States.tsx';

import { type ChangeRequestStatus } from '../api/types.ts';
import { CHANGE_REQUEST_STATUSES } from '../changeRequestRules.ts';
import { type ChangeRequestEntry } from '../useChangeRequestData.ts';

import { ChangeRequestTable } from './ChangeRequestTable.tsx';

/** SCR-105's status filter and table. The statuses offered are the ones the requests carry; all are shown by default. */
export function ChangeRequestRegisterView({
  actions,
  entries,
  showProject,
  caption,
}: {
  actions?: RowActionsColumn<ChangeRequestEntry> | undefined;
  entries: ChangeRequestEntry[];
  showProject: boolean;
  caption: string;
}): ReactElement {
  const { t } = useI18n();
  const [status, setStatus] = useState<ChangeRequestStatus | ''>('');
  const present = CHANGE_REQUEST_STATUSES.filter((candidate) =>
    entries.some((entry) => entry.request.status === candidate),
  );
  const shown =
    status === '' ? entries : entries.filter((entry) => entry.request.status === status);

  return (
    <>
      <FilterBar
        fields={[
          {
            name: 'status',
            label: t('changeRequests.filter.status'),
            all: t('changeRequests.filter.allStatuses'),
            options: present.map((value) => ({
              value,
              label: t(`changeRequests.status.${value}`),
            })),
          },
        ]}
        values={{ status: status === '' ? undefined : status }}
        onChange={(_name, value) => {
          setStatus(CHANGE_REQUEST_STATUSES.find((candidate) => candidate === value) ?? '');
        }}
        onClearAll={() => {
          setStatus('');
        }}
      />
      <p className="list-order" role="status">
        {t('changeRequests.filter.showing', { shown: shown.length, total: entries.length })}
      </p>
      {shown.length === 0 ? (
        <EmptyState title={t('changeRequests.filter.noMatch')} />
      ) : (
        <ChangeRequestTable
          entries={shown}
          showProject={showProject}
          caption={caption}
          actions={actions}
        />
      )}
    </>
  );
}
