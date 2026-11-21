import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar } from '@/shared/ui/FilterBar.tsx';

import { TASK_STATUSES } from '../presentation.ts';
import { type StatusFilter } from '../taskRules.ts';
import { DEFAULT_STATUS_FILTER, statusFilterOf } from '../useTaskStatusFilter.ts';

/** Open work by default; any one state, or every task. */
export function StatusFilterField({
  value,
  onChange,
}: {
  value: StatusFilter;
  onChange: (value: StatusFilter) => void;
}): ReactElement {
  const { t } = useI18n();
  return (
    <FilterBar
      fields={[
        {
          name: 'status',
          label: t('tasks.filter.label'),
          all: t('tasks.filter.open'),
          options: [
            ...TASK_STATUSES.map((status) => ({
              value: status,
              label: t(`tasks.status.${status}`),
            })),
            { value: 'all', label: t('tasks.filter.all') },
          ],
        },
      ]}
      values={{ status: value === DEFAULT_STATUS_FILTER ? undefined : value }}
      onChange={(_name, chosen) => {
        onChange(statusFilterOf(chosen));
      }}
      onClearAll={() => {
        onChange(DEFAULT_STATUS_FILTER);
      }}
    />
  );
}
