import { type PickedUser } from '@/features/identity-access/components/UserPicker.tsx';

// The owner a task form names (MOD-010, MOD-013, MOD-014), and whether the person may search for one.

export const NONE = 'none';
export const OTHER = 'other';

/** The owner a form names: a person offered by name, no owner, or someone else found or typed. */
export interface AssigneeValue {
  /** A user id, NONE or OTHER. */
  choice: string;
  found: PickedUser | null;
}

export function assigneeValue(userId: string | null): AssigneeValue {
  return { choice: userId ?? NONE, found: null };
}

/** The owner to send, and the code of what is wrong with it: REQUIRED (someone else, not chosen) or MALFORMED. */
export function assigneeOf(value: AssigneeValue): {
  userId: string | null;
  code: string | null;
} {
  if (value.choice === NONE) {
    return { userId: null, code: null };
  }
  if (value.choice !== OTHER) {
    return { userId: value.choice, code: null };
  }
  return value.found === null
    ? { userId: null, code: 'REQUIRED' }
    : { userId: value.found.id, code: null };
}
