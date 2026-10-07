import { type ReactElement, useState } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';
import { EmptyState } from '@/shared/ui/States.tsx';

import { type ChangeRequestStatus } from '../api/types.ts';
import { CHANGE_REQUEST_STATUSES } from '../changeRequestRules.ts';
import { type ChangeRequestEntry } from '../useChangeRequestData.ts';

import { ChangeRequestTable } from './ChangeRequestTable.tsx';

/** SCR-105's status filter and table. The statuses offered are the ones the requests carry; all are shown by default. */
export function ChangeRequestRegisterView({
  entries,
  showProject,
  caption,
}: {
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
      <div className="filters">
        <SelectField
          label={t('changeRequests.filter.status')}
          name="status"
          value={status}
          placeholder={t('changeRequests.filter.allStatuses')}
          options={present.map((value) => ({
            value,
            label: t(`changeRequests.status.${value}`),
          }))}
          onChange={(value) => {
            setStatus(CHANGE_REQUEST_STATUSES.find((candidate) => candidate === value) ?? '');
          }}
        />
      </div>
      <p className="list-order" role="status">
        {t('changeRequests.filter.showing', { shown: shown.length, total: entries.length })}
      </p>
      {shown.length === 0 ? (
        <EmptyState title={t('changeRequests.filter.noMatch')} />
      ) : (
        <ChangeRequestTable entries={shown} showProject={showProject} caption={caption} />
      )}
    </>
  );
}
