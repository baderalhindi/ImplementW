import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { SelectField } from '@/shared/ui/FormFields.tsx';

import { TASK_STATUSES } from '../presentation.ts';
import { type StatusFilter } from '../taskRules.ts';

function isStatusFilter(value: string): value is StatusFilter {
  return value === 'open' || value === 'all' || (TASK_STATUSES as string[]).includes(value);
}

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
    <div className="filters filters--inline">
      <SelectField
        label={t('tasks.filter.label')}
        name="status"
        value={value}
        options={[
          { value: 'open', label: t('tasks.filter.open') },
          ...TASK_STATUSES.map((status) => ({ value: status, label: t(`tasks.status.${status}`) })),
          { value: 'all', label: t('tasks.filter.all') },
        ]}
        onChange={(chosen) => {
          if (isStatusFilter(chosen)) {
            onChange(chosen);
          }
        }}
      />
    </div>
  );
}
