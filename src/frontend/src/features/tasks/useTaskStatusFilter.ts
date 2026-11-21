import { useListParams } from '@/shared/api/useListParams.ts';

import { TASK_STATUSES } from './presentation.ts';
import { type StatusFilter } from './taskRules.ts';

/** Open work unless another state is chosen. */
export const DEFAULT_STATUS_FILTER: StatusFilter = 'open';

export function statusFilterOf(value: string | null | undefined): StatusFilter {
  return value === 'all'
    ? 'all'
    : (TASK_STATUSES.find((status) => status === value) ?? DEFAULT_STATUS_FILTER);
}

/** The status filter in the page's URL (review round 8, point 01), so a link can open a task list on one state and Back keeps it. */
export function useTaskStatusFilter(): [StatusFilter, (value: StatusFilter) => void] {
  const { params, setFilter } = useListParams();
  return [
    statusFilterOf(params.get('status')),
    (value) => {
      setFilter('status', value === DEFAULT_STATUS_FILTER ? '' : value);
    },
  ];
}
