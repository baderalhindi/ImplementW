import { usersApi } from '@/features/identity-access/api/identityAccessApi.ts';
import { type UserSummary } from '@/features/identity-access/api/types.ts';
import { UUID_PATTERN } from '@/features/identity-access/forms.ts';
import { useApiResource } from '@/shared/api/useApiResource.ts';

// The owner a task form names (MOD-010, MOD-013, MOD-014), and whether the person may search for one.

export const NONE = 'none';
export const OTHER = 'other';

/** The owner a form names: a person offered by name, no owner, or someone else found or typed. */
export interface AssigneeValue {
  /** A user id, NONE or OTHER. */
  choice: string;
  found: UserSummary | null;
  typedId: string;
}

export function assigneeValue(userId: string | null): AssigneeValue {
  return { choice: userId ?? NONE, found: null, typedId: '' };
}

/** The owner to send, and the code of what is wrong with it: REQUIRED (someone else, not chosen) or MALFORMED. */
export function assigneeOf(
  value: AssigneeValue,
  canSearch: boolean,
): {
  userId: string | null;
  code: string | null;
} {
  if (value.choice === NONE) {
    return { userId: null, code: null };
  }
  if (value.choice !== OTHER) {
    return { userId: value.choice, code: null };
  }
  if (canSearch) {
    return value.found === null
      ? { userId: null, code: 'REQUIRED' }
      : { userId: value.found.id, code: null };
  }
  const typed = value.typedId.trim();
  if (typed === '') {
    return { userId: null, code: 'REQUIRED' };
  }
  return UUID_PATTERN.test(typed)
    ? { userId: typed.toLowerCase(), code: null }
    : { userId: null, code: 'MALFORMED' };
}

async function probeUserSearch(signal: AbortSignal): Promise<boolean> {
  try {
    await usersApi.list({ status: 'ACTIVE', pageSize: 1 }, signal);
    return true;
  } catch {
    return false;
  }
}

/**
 * Whether the caller may search people (USER_VIEW, R01 only today: TASK-032). Without it, someone not offered by
 * name is given by their user id, and the API decides whether they may own the task (F-3).
 */
export function useUserSearch(): boolean | undefined {
  return useApiResource(probeUserSearch).data;
}
