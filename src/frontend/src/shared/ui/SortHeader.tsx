import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';

export type SortDirection = 'ascending' | 'descending';

/**
 * A column header that sorts its table (review round 8, point 13): a button inside the header, which carries `aria-sort` while it orders the
 * rows. Pressing the sorted column turns the order round; another column starts in `firstDirection`.
 */
export function SortHeader<K extends string>({
  label,
  column,
  sort,
  onSort,
  firstDirection = 'ascending',
}: {
  label: string;
  column: K;
  sort: { key: K; direction: SortDirection };
  onSort: (next: { key: K; direction: SortDirection }) => void;
  firstDirection?: SortDirection;
}): ReactElement {
  const { t } = useI18n();
  const active = sort.key === column;
  const next: SortDirection = active
    ? sort.direction === 'ascending'
      ? 'descending'
      : 'ascending'
    : firstDirection;
  return (
    <th scope="col" aria-sort={active ? sort.direction : 'none'}>
      <button
        type="button"
        className="sort-header"
        title={t(`common.table.sort.${next}`)}
        onClick={() => {
          onSort({ key: column, direction: next });
        }}
      >
        {label}
        <span className="sort-header__arrow" aria-hidden="true">
          {active ? (sort.direction === 'ascending' ? '▲' : '▼') : '↕'}
        </span>
      </button>
    </th>
  );
}
