import { type RefObject } from 'react';

import {
  type FieldCodes,
  type FieldErrors,
  useFieldErrors as useSharedFieldErrors,
} from '@/shared/forms/useFieldErrors.ts';

import { scheduleFieldMessage } from './problems.ts';

export { type FieldCodes } from '@/shared/forms/useFieldErrors.ts';

/** The schedule dialogs' field errors, worded by `scheduleFieldMessage` (days out of range, MOD-015's own codes). */
export function useFieldErrors(
  codes: FieldCodes,
  formRef: RefObject<HTMLFormElement | null>,
  messages: Partial<Record<string, string>> = {},
): FieldErrors {
  return useSharedFieldErrors(codes, formRef, scheduleFieldMessage, messages);
}
