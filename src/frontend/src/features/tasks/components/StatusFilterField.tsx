import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { FilterBar } from '@/shared/ui/FilterBar.tsx';

import { TASK_STATUSES } from '../presentation.ts';
import { type StatusFilter } from '../taskRules.ts';

const DEFAULT_FILTER: StatusFilter = 'open';

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
      values={{ status: value === DEFAULT_FILTER ? undefined : value }}
      onChange={(_name, chosen) => {
        onChange(
          chosen === 'all'
            ? 'all'
            : (TASK_STATUSES.find((status) => status === chosen) ?? DEFAULT_FILTER),
        );
      }}
      onClearAll={() => {
        onChange(DEFAULT_FILTER);
      }}
    />
  );
}
